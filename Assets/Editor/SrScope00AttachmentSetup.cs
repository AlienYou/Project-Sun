using System.Collections.Generic;
using System.IO;
using ProjectSun.FPS.Core;
using ProjectSun.FPS.Presentation;
using ProjectSun.FPS.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectSun.FPS.Editor
{
    /// <summary>
    /// Adapts imported optic source meshes to the AR-4 first-person attachment contract.
    /// Source FBX files remain untouched; only generated Project Sun prefabs are used at runtime.
    /// </summary>
    public static class SrScope00AttachmentSetup
    {
        private const string CalibrationRootName = "AttachmentCalibrationRoot";
        private const string Ar4DefinitionPath = "Assets/_ProjectSun/Data/Weapons/Definitions/AR4Carbine.asset";
        private const string FallbackMaterialPath = "Assets/_ProjectSun/Art/Materials/Weapons/Attachments/M_ATT_Prototype_Dark.mat";

        // AR-4 scope socket convention: X lateral, +Y up and +Z towards the muzzle.
        // Imported C4D attachments use a different basis.
        private static readonly Quaternion SourceToAr4ScopeRotation =
            Quaternion.AngleAxis(180f, Vector3.up) * Quaternion.AngleAxis(-90f, Vector3.right);
        private const float RailEmbedDepth = 0.0035f;
        private const float RearLensInset = 0.004f;

        private readonly struct OpticSetup
        {
            public readonly string SourceFbxPath;
            public readonly string OutputPrefabPath;
            public readonly string AttachmentPath;
            public readonly string AdsProfilePath;
            public readonly string DisplayName;
            public readonly string AimAnchorName;
            public readonly float SightDistance;
            public readonly float TransitionSpeed;
            public readonly float FovReduction;

            public OpticSetup(string sourceFbxPath, string outputPrefabPath, string attachmentPath, string adsProfilePath,
                string displayName, string aimAnchorName, float sightDistance, float transitionSpeed, float fovReduction)
            {
                SourceFbxPath = sourceFbxPath;
                OutputPrefabPath = outputPrefabPath;
                AttachmentPath = attachmentPath;
                AdsProfilePath = adsProfilePath;
                DisplayName = displayName;
                AimAnchorName = aimAnchorName;
                SightDistance = sightDistance;
                TransitionSpeed = transitionSpeed;
                FovReduction = fovReduction;
            }
        }

        private static readonly OpticSetup SrScope00 = new OpticSetup(
            "Assets/_ProjectSun/Prefabs/Weapons/Attachments/SR_Scope_00 1.fbx",
            "Assets/_ProjectSun/Prefabs/Weapons/Attachments/PFB_ATT_AR4_SRScope00.prefab",
            "Assets/_ProjectSun/Data/Weapons/Attachments/SRScope00.asset",
            "Assets/_ProjectSun/Data/Weapons/ADS/ADS_SRScope00.asset",
            "SR Scope 00", "AimAnchor_SRScope00", 0.18f, 7f, 14f);

        private static readonly OpticSetup TanLrScope01 = new OpticSetup(
            "Assets/_ProjectSun/Prefabs/Weapons/Attachments/TAN_LR_Scope_01 1.fbx",
            "Assets/_ProjectSun/Prefabs/Weapons/Attachments/PFB_ATT_AR4_TanLrScope01.prefab",
            "Assets/_ProjectSun/Data/Weapons/Attachments/TanLrScope01.asset",
            "Assets/_ProjectSun/Data/Weapons/ADS/ADS_TanLrScope01.asset",
            "TAN LR SCOPE 01", "AimAnchor_TanLrScope01", 0.20f, 6f, 20f);

        [MenuItem("Project Sun/Prepare All Imported AR-4 Optics", priority = 18)]
        public static void PrepareAll()
        {
            CombatWeaponDataGenerator.CreateOrGetDataAssets();
            bool srPrepared = Prepare(SrScope00, false);
            bool tanPrepared = Prepare(TanLrScope01, false);
            if (srPrepared && tanPrepared)
                EditorUtility.DisplayDialog("Project Sun",
                    "All imported AR-4 optics were prepared. Test both optics in WeaponLab before marking their ADS profiles as reviewed.", "OK");
        }

        [MenuItem("Project Sun/Prepare SR Scope 00 As AR-4 Attachment", priority = 19)]
        public static void PrepareSrScope00()
        {
            CombatWeaponDataGenerator.CreateOrGetDataAssets();
            Prepare(SrScope00, true);
        }

        [MenuItem("Project Sun/Prepare TAN LR Scope 01 As AR-4 Attachment", priority = 20)]
        public static void PrepareTanLrScope01()
        {
            CombatWeaponDataGenerator.CreateOrGetDataAssets();
            Prepare(TanLrScope01, true);
        }

        [MenuItem("Project Sun/Migrate Prepared Optic Calibration Contracts", priority = 21)]
        public static void MigratePreparedOpticCalibrationContracts()
        {
            bool srMigrated = MigrateCalibrationContract(SrScope00);
            bool tanMigrated = MigrateCalibrationContract(TanLrScope01);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Project Sun",
                srMigrated && tanMigrated
                    ? "现有倍镜已迁移到统一校准根。ADS 光轴仍使用兼容模式，请在 Workbench 中点击“按当前结果播种光轴”完成方向迁移。"
                    : "部分倍镜无法迁移，请查看 Console。",
                "确定");
        }

        private static bool Prepare(OpticSetup setup, bool showSuccessDialog)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(setup.SourceFbxPath);
            if (source == null)
            {
                Debug.LogError($"{setup.DisplayName} source FBX is missing: {setup.SourceFbxPath}");
                return false;
            }

            GameObject outputRoot = new GameObject(Path.GetFileNameWithoutExtension(setup.OutputPrefabPath));
            try
            {
                Transform calibrationRoot = CreateCalibrationRoot(outputRoot.transform);
                GameObject modelInstance = PrefabUtility.InstantiatePrefab(source) as GameObject;
                if (modelInstance == null)
                {
                    Debug.LogError($"Could not instantiate {setup.DisplayName} source mesh.");
                    return false;
                }

                modelInstance.name = "Model";
                modelInstance.transform.SetParent(calibrationRoot, false);
                PrefabUtility.UnpackPrefabInstance(modelInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                RemoveExporterArtifacts(outputRoot.transform);
                ConfigureRenderers(outputRoot.transform);
                FitModelToScopeRail(modelInstance.transform);

                Bounds modelBounds = CalculateBoundsRelativeTo(calibrationRoot, modelInstance.transform);
                CreateAimAnchor(calibrationRoot, setup.AimAnchorName, SeedScopeAimAnchorLocalPose(modelBounds));
                CreateLensAnchor(calibrationRoot, setup.AimAnchorName, modelBounds);
                CreateClipProbe(calibrationRoot, setup.DisplayName, modelBounds);

                EnsureFolder("Assets/_ProjectSun/Prefabs/Weapons/Attachments");
                PrefabUtility.SaveAsPrefabAsset(outputRoot, setup.OutputPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(outputRoot);
            }

            GameObject outputPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(setup.OutputPrefabPath);
            WeaponDefinition ar4 = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(Ar4DefinitionPath);
            WeaponAttachment attachment = AssetDatabase.LoadAssetAtPath<WeaponAttachment>(setup.AttachmentPath);
            if (outputPrefab == null || ar4 == null || attachment == null)
            {
                Debug.LogError($"{setup.DisplayName} prefab, AR-4 definition or attachment data could not be resolved after generation.");
                return false;
            }

            WeaponAdsProfile profile = CreateOrGetAdsProfile(setup);
            attachment.SetCompatibleWeapons(ar4);
            attachment.SetViewmodelVisual(ar4, outputPrefab, "SOCKET_Scope", "SM_AR_01_Scope_Default", setup.AimAnchorName);
            attachment.SetAdsProfileOverride(profile);
            EditorUtility.SetDirty(attachment);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (showSuccessDialog)
                EditorUtility.DisplayDialog("Project Sun",
                    $"{setup.DisplayName} is now an AR-4 optic. Test hip fire and ADS in WeaponLab, then review sight alignment in the Weapon Presentation Workbench.", "OK");
            return true;
        }

        private static void RemoveExporterArtifacts(Transform root)
        {
            List<GameObject> remove = new List<GameObject>();
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child != root && child.name.Contains("CINEMA_4D_Editor"))
                    remove.Add(child.gameObject);
            foreach (GameObject artifact in remove)
                Object.DestroyImmediate(artifact);
        }

        private static void ConfigureRenderers(Transform root)
        {
            Material fallback = AssetDatabase.LoadAssetAtPath<Material>(FallbackMaterialPath);
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                Material material = renderer.sharedMaterial;
                if (fallback != null && (material == null || material.shader == null || material.shader.name == "Hidden/InternalErrorShader"))
                    renderer.sharedMaterial = fallback;
            }
            CombatLayers.SetLayerRecursively(root.gameObject, CombatLayers.ViewmodelLayer);
        }

        private static void FitModelToScopeRail(Transform modelRoot)
        {
            if (modelRoot == null || modelRoot.parent == null) return;
            modelRoot.localPosition = Vector3.zero;
            modelRoot.localRotation = SourceToAr4ScopeRotation;
            modelRoot.localScale = Vector3.one;

            Bounds fittedBounds = CalculateBoundsRelativeTo(modelRoot.parent, modelRoot);
            modelRoot.localPosition = new Vector3(
                -fittedBounds.center.x,
                -fittedBounds.min.y + RailEmbedDepth,
                -fittedBounds.center.z);
        }

        private static Bounds CalculateBoundsRelativeTo(Transform reference, Transform content)
        {
            Renderer[] renderers = content.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one * 0.04f);

            Bounds bounds = new Bounds(reference.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
            foreach (Renderer renderer in renderers)
            {
                Bounds worldBounds = renderer.bounds;
                Vector3 extents = worldBounds.extents;
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                            bounds.Encapsulate(reference.InverseTransformPoint(worldBounds.center +
                                Vector3.Scale(extents, new Vector3(x, y, z))));
            }
            return bounds;
        }

        private static Pose SeedScopeAimAnchorLocalPose(Bounds modelBounds)
        {
            Vector3 rearLensCentre = new Vector3(modelBounds.center.x, modelBounds.center.y,
                modelBounds.min.z + RearLensInset);
            return new Pose(rearLensCentre, Quaternion.identity);
        }

        private static void CreateAimAnchor(Transform parent, string anchorName, Pose localPose)
        {
            GameObject anchor = new GameObject(anchorName, typeof(AdsSightReference));
            anchor.layer = CombatLayers.ViewmodelLayer;
            anchor.transform.SetParent(parent, false);
            anchor.transform.SetLocalPositionAndRotation(localPose.position, localPose.rotation);
            // Imported axes must be verified against the equipped weapon before the authored rotation
            // replaces the legacy anchor-to-muzzle direction. Workbench performs that lossless bake.
            anchor.GetComponent<AdsSightReference>().SetOrientationAuthored(false);
        }

        private static Transform CreateCalibrationRoot(Transform attachmentRoot)
        {
            GameObject rootObject = new GameObject(CalibrationRootName,
                typeof(ViewmodelAttachmentCalibrationRoot));
            rootObject.layer = CombatLayers.ViewmodelLayer;
            rootObject.transform.SetParent(attachmentRoot, false);
            return rootObject.transform;
        }

        private static bool MigrateCalibrationContract(OpticSetup setup)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(setup.OutputPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Cannot migrate missing optic prefab: {setup.OutputPrefabPath}");
                return false;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(setup.OutputPrefabPath);
            try
            {
                Transform calibrationRoot = contents.GetComponentInChildren<ViewmodelAttachmentCalibrationRoot>(true)?.transform;
                if (calibrationRoot == null)
                {
                    calibrationRoot = CreateCalibrationRoot(contents.transform);
                    List<Transform> childrenToMove = new List<Transform>();
                    foreach (Transform child in contents.transform)
                        if (child != calibrationRoot && IsCalibrationContent(child)) childrenToMove.Add(child);
                    foreach (Transform child in childrenToMove) child.SetParent(calibrationRoot, false);
                }

                Transform aimAnchor = FindDescendant(contents.transform, setup.AimAnchorName);
                if (aimAnchor == null)
                {
                    Debug.LogError($"{setup.DisplayName} is missing {setup.AimAnchorName}.");
                    return false;
                }
                if (aimAnchor.GetComponent<AdsSightReference>() == null)
                    aimAnchor.gameObject.AddComponent<AdsSightReference>();

                PrefabUtility.SaveAsPrefabAsset(contents, setup.OutputPrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static bool IsCalibrationContent(Transform child)
        {
            return child.name == "Model" || child.name.StartsWith("AimAnchor_") ||
                child.GetComponent<ViewmodelScopeLens>() != null || child.GetComponent<ViewmodelClipProbe>() != null;
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName)) return null;
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == objectName) return candidate;
            return null;
        }

        private static void CreateLensAnchor(Transform parent, string aimAnchorName, Bounds modelBounds)
        {
            string lensAnchorName = aimAnchorName.Replace("AimAnchor_", "LensAnchor_");
            GameObject lensObject = new GameObject(lensAnchorName, typeof(ViewmodelScopeLens));
            lensObject.layer = CombatLayers.ViewmodelLayer;
            lensObject.transform.SetParent(parent, false);
            lensObject.transform.SetLocalPositionAndRotation(SeedScopeAimAnchorLocalPose(modelBounds).position,
                Quaternion.identity);
            float diameter = Mathf.Clamp(Mathf.Min(modelBounds.size.x, modelBounds.size.y) * 0.55f, 0.025f, 0.09f);
            lensObject.GetComponent<ViewmodelScopeLens>().Configure(diameter);
        }

        private static void CreateClipProbe(Transform parent, string opticName, Bounds localBounds)
        {
            GameObject probeObject = new GameObject("ClipProbe_" + opticName.Replace(" ", string.Empty) + "_Housing");
            probeObject.layer = CombatLayers.ViewmodelLayer;
            probeObject.transform.SetParent(parent, false);
            probeObject.transform.localPosition = localBounds.center;
            float radius = Mathf.Clamp(Mathf.Max(localBounds.extents.x, localBounds.extents.y) * 0.65f, 0.012f, 0.045f);
            ViewmodelClipProbe probe = probeObject.AddComponent<ViewmodelClipProbe>();
            probe.Configure(opticName + " Housing", radius);
        }

        private static WeaponAdsProfile CreateOrGetAdsProfile(OpticSetup setup)
        {
            WeaponAdsProfile profile = AssetDatabase.LoadAssetAtPath<WeaponAdsProfile>(setup.AdsProfilePath);
            if (profile != null) return profile;

            profile = ScriptableObject.CreateInstance<WeaponAdsProfile>();
            AssetDatabase.CreateAsset(profile, setup.AdsProfilePath);
            profile.ConfigureDefaults(setup.SightDistance, setup.TransitionSpeed, setup.FovReduction);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    /// <summary>Creates only missing optic sight-picture data and binds it to existing attachment assets.</summary>
    public static class OpticSightProfileSetup
    {
        private const string AttachmentRoot = "Assets/_ProjectSun/Data/Weapons/Attachments";
        private const string OpticRoot = "Assets/_ProjectSun/Data/Weapons/Optics";

        [MenuItem("Project Sun/Ensure Optic Sight Presentation Profiles", priority = 19)]
        public static void EnsureProfiles()
        {
            EnsureFolder(OpticRoot);
            int bound = 0;
            bound += Bind("M2Reflex", "OSP_M2Reflex", OpticSightType.Reflex, OpticReticleStyle.Dot,
                new Color(1f, 0.16f, 0.08f, 0.94f), 5f, 34f);
            bound += Bind("H7Holo", "OSP_H7Holo", OpticSightType.Holographic, OpticReticleStyle.RingDot,
                new Color(0.16f, 0.95f, 0.84f, 0.9f), 4f, 42f);
            bound += Bind("SRScope00", "OSP_SRScope00", OpticSightType.MagnifiedScope, OpticReticleStyle.Cross,
                new Color(0.12f, 0.96f, 0.2f, 0.9f), 3f, 44f);
            bound += Bind("TanLrScope01", "OSP_TanLrScope01", OpticSightType.MagnifiedScope, OpticReticleStyle.Cross,
                new Color(0.12f, 0.96f, 0.2f, 0.9f), 3f, 52f);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Project Sun", $"Bound {bound} optic sight presentation profile(s).\n" +
                "They appear in the Weapon Workbench ADS preview and in-game HUD only while the associated optic is aimed.", "OK");
        }

        private static int Bind(string attachmentName, string profileName, OpticSightType type, OpticReticleStyle style,
            Color color, float reticlePixels, float framePixels)
        {
            WeaponAttachment attachment = AssetDatabase.LoadAssetAtPath<WeaponAttachment>(
                AttachmentRoot + "/" + attachmentName + ".asset");
            if (attachment == null)
            {
                Debug.LogWarning($"Optic sight setup skipped missing attachment {attachmentName}.");
                return 0;
            }

            string profilePath = OpticRoot + "/" + profileName + ".asset";
            OpticSightProfile profile = AssetDatabase.LoadAssetAtPath<OpticSightProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<OpticSightProfile>();
                profile.ConfigureDefaults(type, style, color, reticlePixels, framePixels);
                AssetDatabase.CreateAsset(profile, profilePath);
                EditorUtility.SetDirty(profile);
            }

            if (attachment.OpticSightProfile == profile) return 0;
            attachment.SetOpticSightProfile(profile);
            EditorUtility.SetDirty(attachment);
            return 1;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            AssetDatabase.CreateFolder("Assets/_ProjectSun/Data/Weapons", "Optics");
        }
    }
}
