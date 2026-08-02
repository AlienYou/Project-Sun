using System.Collections.Generic;
using ProjectSun.FPS.Bootstrap;
using ProjectSun.FPS.Core;
using ProjectSun.FPS.Presentation;
using ProjectSun.FPS.Weapons;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ProjectSun.FPS.Editor
{
    /// <summary>Builds a Project Sun-owned visual prefab from the selected Low Poly Shooter Pack sample assets.</summary>
    public static class LowPolyShooterPackViewmodelSetup
    {
        private const string SourceCharacterPath =
            "Assets/Infima Games/Low Poly Shooter Pack - Free Sample/Prefabs/P_LPSP_FP_CH.prefab";
        private const string SourceWeaponPath =
            "Assets/Infima Games/Low Poly Shooter Pack - Free Sample/Prefabs/Weapons/P_LPSP_WEP_AR_01.prefab";
        private const string ViewmodelPrefabPath =
            "Assets/_ProjectSun/Prefabs/Characters/PFB_FP_Operator_LPSP_AR01.prefab";
        private const string PlayerPrefabPath = "Assets/_ProjectSun/Prefabs/Characters/Player.prefab";
        private const string ArmsControllerPath =
            "Assets/_ProjectSun/Art/Characters/Animations/AC_FP_LPSP_AR01_Clean.controller";
        private const string ArmsOverridePath =
            "Assets/_ProjectSun/Art/Characters/Animations/OC_FP_LPSP_AR01_Clean.overrideController";
        private const string WeaponControllerPath =
            "Assets/_ProjectSun/Art/Weapons/Animations/AC_WPN_AR01_Clean.controller";
        private const string AdsProfilePath = "Assets/_ProjectSun/Data/Weapons/ADS/ADS_AR01.asset";
        private const string PresentationProfilePath = "Assets/_ProjectSun/Data/Weapons/Presentation/WPP_AR01.asset";
        private const string EmbeddedViewmodelName = "FP Viewmodel - LPSP AR-01";

        [MenuItem("Project Sun/Integrate Low Poly Shooter Arms (AR-01)", priority = 18)]
        public static void Integrate()
        {
            GameObject sourceCharacter = AssetDatabase.LoadAssetAtPath<GameObject>(SourceCharacterPath);
            GameObject sourceWeapon = AssetDatabase.LoadAssetAtPath<GameObject>(SourceWeaponPath);
            if (sourceCharacter == null || sourceWeapon == null)
            {
                EditorUtility.DisplayDialog("Project Sun", "The Low Poly Shooter Pack sample prefabs were not found. No changes were made.", "OK");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null)
            {
                EditorUtility.DisplayDialog("Project Sun", "The Project Sun Player prefab was not found. No changes were made.", "OK");
                return;
            }

            EnsureFolder("Assets/_ProjectSun/Prefabs/Characters");
            LowPolyShooterViewmodelRig viewmodelPrefab = CreateViewmodelPrefab(sourceCharacter, sourceWeapon);
            ConfigurePlayerPrefab(viewmodelPrefab, false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = viewmodelPrefab;
            EditorUtility.DisplayDialog("Project Sun",
                "Low Poly Shooter Pack AR-01 arms were integrated as a visual-only viewmodel. Press Play in CombatSlice to validate fire, reload and ADS.",
                "OK");
        }

        [MenuItem("Project Sun/Finalize Player Viewmodel (Remove Prototype)", priority = 19)]
        public static void FinalizePlayerViewmodel()
        {
            GameObject viewmodelObject = AssetDatabase.LoadAssetAtPath<GameObject>(ViewmodelPrefabPath);
            LowPolyShooterViewmodelRig viewmodelPrefab = viewmodelObject != null
                ? viewmodelObject.GetComponent<LowPolyShooterViewmodelRig>()
                : null;
            if (viewmodelPrefab == null)
            {
                EditorUtility.DisplayDialog("Project Sun", "Generate and validate the AR-01 viewmodel before removing the prototype weapon.", "OK");
                return;
            }

            ConfigurePlayerPrefab(viewmodelPrefab, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            EditorUtility.DisplayDialog("Project Sun",
                "The Player now uses the AR-01 viewmodel directly. The legacy Prototype Carbine fallback was removed.", "OK");
        }

        private static LowPolyShooterViewmodelRig CreateViewmodelPrefab(GameObject sourceCharacter, GameObject sourceWeapon)
        {
            GameObject sourceContents = PrefabUtility.LoadPrefabContents(SourceCharacterPath);
            GameObject root = Object.Instantiate(sourceContents);
            PrefabUtility.UnloadPrefabContents(sourceContents);
            root.name = "PFB_FP_Operator_LPSP_AR01";

            RemoveThirdPartyRuntimeComponents(root);
            Transform weaponRoot = FindDescendant(root.transform, sourceWeapon.name);
            if (weaponRoot == null)
                weaponRoot = AddWeaponFallback(root.transform, sourceWeapon);
            weaponRoot.gameObject.SetActive(true);

            Animator armsAnimator = FindComponentOnNamedObject<Animator>(root.transform, "SK_FP_CH_Default_Root");
            Animator weaponAnimator = weaponRoot.GetComponentInChildren<Animator>(true);
            Transform muzzle = FindDescendant(weaponRoot, "SOCKET_Muzzle");
            Transform scopeSocket = FindDescendant(weaponRoot, "SOCKET_Scope");
            Transform magazine = FindDescendant(weaponRoot, "magazine");
            if (armsAnimator == null || weaponAnimator == null || muzzle == null || scopeSocket == null)
            {
                Object.DestroyImmediate(root);
                throw new System.InvalidOperationException("The sample rig no longer matches the expected AR-01 hierarchy.");
            }
            CreateCleanAnimatorControllers(armsAnimator, weaponAnimator);
            Transform aimAnchor = CreateAimAnchor(scopeSocket);
            WeaponAdsProfile adsProfile = GetOrCreateAdsProfile();
            WeaponPresentationProfile presentationProfile = GetOrCreatePresentationProfile(adsProfile);

            LowPolyShooterViewmodelRig rig = root.AddComponent<LowPolyShooterViewmodelRig>();
            rig.ConfigureReferences(armsAnimator, weaponAnimator, muzzle, aimAnchor, magazine, adsProfile, presentationProfile);
            LowPolyShooterAnimationEvents events = armsAnimator.gameObject.AddComponent<LowPolyShooterAnimationEvents>();
            events.Configure(rig);
            SetLayerRecursively(root.transform, CombatLayers.ViewmodelLayer);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, ViewmodelPrefabPath);
            Object.DestroyImmediate(root);
            return saved.GetComponent<LowPolyShooterViewmodelRig>();
        }

        private static Transform CreateAimAnchor(Transform scopeSocket)
        {
            Transform existing = FindDescendant(scopeSocket, "Aim Anchor");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            GameObject anchor = new GameObject("Aim Anchor");
            anchor.transform.SetParent(scopeSocket, false);
            anchor.transform.localPosition = new Vector3(0f, 0.015f, 0.045f);
            anchor.transform.localRotation = Quaternion.identity;
            return anchor.transform;
        }

        private static WeaponAdsProfile GetOrCreateAdsProfile()
        {
            WeaponAdsProfile profile = AssetDatabase.LoadAssetAtPath<WeaponAdsProfile>(AdsProfilePath);
            if (profile != null) return profile;

            EnsureFolder("Assets/_ProjectSun/Data/Weapons/ADS");
            profile = ScriptableObject.CreateInstance<WeaponAdsProfile>();
            profile.name = "ADS_AR01";
            profile.ConfigureDefaults(0.18f, 14f, 12f);
            AssetDatabase.CreateAsset(profile, AdsProfilePath);
            return profile;
        }

        private static WeaponPresentationProfile GetOrCreatePresentationProfile(WeaponAdsProfile adsProfile)
        {
            WeaponPresentationProfile profile = AssetDatabase.LoadAssetAtPath<WeaponPresentationProfile>(PresentationProfilePath);
            if (profile != null) return profile;

            EnsureFolder("Assets/_ProjectSun/Data/Weapons/Presentation");
            profile = ScriptableObject.CreateInstance<WeaponPresentationProfile>();
            profile.name = "WPP_AR01";
            profile.ConfigureDefaults(adsProfile);
            AssetDatabase.CreateAsset(profile, PresentationProfilePath);
            return profile;
        }

        private static void CreateCleanAnimatorControllers(Animator armsAnimator, Animator weaponAnimator)
        {
            EnsureFolder("Assets/_ProjectSun/Art/Characters/Animations");
            EnsureFolder("Assets/_ProjectSun/Art/Weapons/Animations");

            if (armsAnimator.runtimeAnimatorController is AnimatorOverrideController sourceOverride &&
                sourceOverride.runtimeAnimatorController is AnimatorController sourceArmsController)
            {
                AnimatorController cleanArmsController = CloneControllerWithoutBehaviours(sourceArmsController, ArmsControllerPath);
                AnimatorOverrideController cleanOverride = new AnimatorOverrideController(cleanArmsController);
                List<KeyValuePair<AnimationClip, AnimationClip>> overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                sourceOverride.GetOverrides(overrides);
                cleanOverride.ApplyOverrides(overrides);
                ReplaceAsset(cleanOverride, ArmsOverridePath);
                armsAnimator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(ArmsOverridePath);
            }
            else if (armsAnimator.runtimeAnimatorController is AnimatorController sourceBaseArmsController)
            {
                armsAnimator.runtimeAnimatorController = CloneControllerWithoutBehaviours(sourceBaseArmsController, ArmsControllerPath);
            }

            if (weaponAnimator.runtimeAnimatorController is AnimatorController sourceWeaponController)
                weaponAnimator.runtimeAnimatorController = CloneControllerWithoutBehaviours(sourceWeaponController, WeaponControllerPath);
        }

        private static AnimatorController CloneControllerWithoutBehaviours(AnimatorController source, string targetPath)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(sourcePath))
                throw new System.InvalidOperationException("The source AnimatorController must be a saved asset.");

            if (AssetDatabase.LoadMainAssetAtPath(targetPath) != null && !AssetDatabase.DeleteAsset(targetPath))
                throw new System.InvalidOperationException($"Could not replace generated AnimatorController at {targetPath}.");
            if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
                throw new System.InvalidOperationException($"Could not copy AnimatorController from {sourcePath} to {targetPath}.");

            AnimatorController clone = AssetDatabase.LoadAssetAtPath<AnimatorController>(targetPath);
            if (clone == null)
                throw new System.InvalidOperationException($"Could not load copied AnimatorController at {targetPath}.");

            clone.name = System.IO.Path.GetFileNameWithoutExtension(targetPath);
            foreach (AnimatorControllerLayer layer in clone.layers)
                RemoveStateMachineBehaviours(layer.stateMachine);
            EditorUtility.SetDirty(clone);
            AssetDatabase.SaveAssets();
            return clone;
        }

        private static void RemoveStateMachineBehaviours(AnimatorStateMachine machine)
        {
            foreach (ChildAnimatorState child in machine.states)
                child.state.behaviours = System.Array.Empty<StateMachineBehaviour>();
            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
                RemoveStateMachineBehaviours(child.stateMachine);
        }

        private static void ReplaceAsset(Object asset, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
        }

        private static Transform AddWeaponFallback(Transform parent, GameObject sourceWeapon)
        {
            GameObject weapon = (GameObject)PrefabUtility.InstantiatePrefab(sourceWeapon);
            weapon.transform.SetParent(parent, false);
            RemoveThirdPartyRuntimeComponents(weapon);
            return weapon.transform;
        }

        private static void ConfigurePlayerPrefab(LowPolyShooterViewmodelRig viewmodelPrefab, bool removePrototypeCarbine)
        {
            GameObject playerContents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                FpsPlayerInstaller installer = playerContents.GetComponent<FpsPlayerInstaller>();
                if (installer == null) throw new System.InvalidOperationException("Player prefab has no FpsPlayerInstaller.");

                LowPolyShooterViewmodel viewmodel = playerContents.GetComponent<LowPolyShooterViewmodel>();
                if (viewmodel == null) viewmodel = playerContents.AddComponent<LowPolyShooterViewmodel>();
                LowPolyShooterViewmodelRig embeddedRig = EmbedViewmodel(playerContents.transform, viewmodelPrefab);
                viewmodel.SetViewmodelRig(embeddedRig);
                if (removePrototypeCarbine) RemovePrototypeCarbine(playerContents.transform, installer);
                EditorUtility.SetDirty(playerContents);
                PrefabUtility.SaveAsPrefabAsset(playerContents, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerContents);
            }
        }

        private static LowPolyShooterViewmodelRig EmbedViewmodel(Transform playerRoot, LowPolyShooterViewmodelRig sourceRig)
        {
            Transform playerCamera = FindDescendant(playerRoot, "Player Camera");
            if (playerCamera == null) throw new System.InvalidOperationException("Player prefab has no Player Camera transform.");

            Transform existing = FindDescendant(playerCamera, EmbeddedViewmodelName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            GameObject embedded = (GameObject)PrefabUtility.InstantiatePrefab(sourceRig.gameObject);
            embedded.name = EmbeddedViewmodelName;
            embedded.transform.SetParent(playerCamera, false);
            embedded.transform.localPosition = new Vector3(0f, -1.8f, 0f);
            embedded.transform.localRotation = Quaternion.identity;
            return embedded.GetComponent<LowPolyShooterViewmodelRig>();
        }

        private static void RemovePrototypeCarbine(Transform playerRoot, FpsPlayerInstaller installer)
        {
            Transform prototypeCarbine = FindDescendant(playerRoot, "Prototype Carbine");
            if (prototypeCarbine != null) Object.DestroyImmediate(prototypeCarbine.gameObject);

            SerializedObject serializedInstaller = new SerializedObject(installer);
            SerializedProperty muzzleProperty = serializedInstaller.FindProperty("muzzle");
            if (muzzleProperty != null) muzzleProperty.objectReferenceValue = null;
            serializedInstaller.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RemoveThirdPartyRuntimeComponents(GameObject root)
        {
            foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                Object.DestroyImmediate(component);
            foreach (Camera component in root.GetComponentsInChildren<Camera>(true))
                Object.DestroyImmediate(component);
            foreach (AudioListener component in root.GetComponentsInChildren<AudioListener>(true))
                Object.DestroyImmediate(component);
            foreach (AudioReverbZone component in root.GetComponentsInChildren<AudioReverbZone>(true))
                Object.DestroyImmediate(component);
            foreach (AudioSource component in root.GetComponentsInChildren<AudioSource>(true))
                Object.DestroyImmediate(component);
            foreach (CharacterController component in root.GetComponentsInChildren<CharacterController>(true))
                Object.DestroyImmediate(component);
            foreach (Collider component in root.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(component);
            foreach (Rigidbody component in root.GetComponentsInChildren<Rigidbody>(true))
                Object.DestroyImmediate(component);
        }

        private static T FindComponentOnNamedObject<T>(Transform root, string objectName) where T : Component
        {
            Transform transform = FindDescendant(root, objectName);
            return transform != null ? transform.GetComponent<T>() : null;
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root.name == objectName) return root;
            foreach (Transform child in root)
            {
                Transform found = FindDescendant(child, objectName);
                if (found != null) return found;
            }
            return null;
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root) SetLayerRecursively(child, layer);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
