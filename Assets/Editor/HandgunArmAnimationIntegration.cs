using System;
using System.Collections.Generic;
using ProjectSun.FPS.Presentation;
using ProjectSun.FPS.Weapons;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ProjectSun.FPS.Editor
{
    /// <summary>
    /// Creates a Project Sun-owned handgun arm override from the migrated source-pack clips, then
    /// assigns it to the secondary presentation slot on Player. The runtime never references the
    /// ignored source package after this command completes.
    /// </summary>
    public static class HandgunArmAnimationIntegration
    {
        private const string OwnedSourceOverridePath =
            "Assets/_ProjectSun/Art/ThirdParty/Infima/LowPolyShooterSample/Animators/Character/OC_LPSP_PCH_Handgun_03.overrideController";
        private const string CleanControllerPath = "Assets/_ProjectSun/Art/Characters/Animations/AC_FP_LPSP_HG3_Clean.controller";
        private const string CleanOverridePath = "Assets/_ProjectSun/Art/Characters/Animations/OC_FP_LPSP_HG3_Clean.overrideController";
        private const string PlayerPrefabPath = "Assets/_ProjectSun/Prefabs/Characters/Player.prefab";
        private const string HandgunAdsProfilePath = "Assets/_ProjectSun/Data/Weapons/ADS/ADS_HG3.asset";
        private const string HandgunPresentationProfilePath = "Assets/_ProjectSun/Data/Weapons/Presentation/WPP_HG3.asset";

        [MenuItem("Project Sun/Integrate HG-3 Arm Animations & Switch Transition", priority = 14)]
        public static void Integrate()
        {
            if (!LowPolyShooterOwnershipMigration.MigrateHandgunRuntimeAssets()) return;

            AnimatorOverrideController sourceOverride = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(OwnedSourceOverridePath);
            AnimatorController sourceController = sourceOverride != null
                ? sourceOverride.runtimeAnimatorController as AnimatorController
                : null;
            if (sourceController == null)
            {
                EditorUtility.DisplayDialog("Project Sun", "The migrated HG-3 arm override or its base controller is unavailable.", "OK");
                return;
            }

            AnimatorController cleanController = CloneControllerWithoutBehaviours(sourceController, CleanControllerPath);
            AnimatorOverrideController cleanOverride = CreateCleanOverride(sourceOverride, cleanController);
            AssignToPlayerPrefab(cleanOverride);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = cleanOverride;
            EditorUtility.DisplayDialog("Project Sun",
                "HG-3 now uses handgun-specific arm idle, fire, reload, holster and unholster animations. Test 1/2 switching in CombatSlice.", "OK");
        }

        private static AnimatorController CloneControllerWithoutBehaviours(AnimatorController source, string targetPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(targetPath) != null) AssetDatabase.DeleteAsset(targetPath);
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), targetPath))
                throw new InvalidOperationException($"Could not copy handgun arm controller to {targetPath}.");
            AnimatorController clean = AssetDatabase.LoadAssetAtPath<AnimatorController>(targetPath);
            foreach (AnimatorControllerLayer layer in clean.layers)
                RemoveBehaviours(layer.stateMachine);
            EditorUtility.SetDirty(clean);
            return clean;
        }

        private static AnimatorOverrideController CreateCleanOverride(AnimatorOverrideController source,
            AnimatorController cleanController)
        {
            if (AssetDatabase.LoadMainAssetAtPath(CleanOverridePath) != null) AssetDatabase.DeleteAsset(CleanOverridePath);
            AnimatorOverrideController clean = new AnimatorOverrideController(cleanController) { name = "OC_FP_LPSP_HG3_Clean" };
            List<KeyValuePair<AnimationClip, AnimationClip>> sourceOverrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            List<KeyValuePair<AnimationClip, AnimationClip>> cleanOverrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            source.GetOverrides(sourceOverrides);
            clean.GetOverrides(cleanOverrides);
            if (sourceOverrides.Count != cleanOverrides.Count)
                throw new InvalidOperationException("The HG-3 arm controller no longer matches its override table.");

            for (int index = 0; index < cleanOverrides.Count; index++)
                cleanOverrides[index] = new KeyValuePair<AnimationClip, AnimationClip>(cleanOverrides[index].Key,
                    sourceOverrides[index].Value);
            clean.ApplyOverrides(cleanOverrides);
            AssetDatabase.CreateAsset(clean, CleanOverridePath);
            return AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(CleanOverridePath);
        }

        private static void AssignToPlayerPrefab(AnimatorOverrideController handgunArms)
        {
            GameObject player = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                if (player.GetComponent<PlayerMatchLoadout>() == null) player.AddComponent<PlayerMatchLoadout>();
                WeaponInventoryController inventory = player.GetComponent<WeaponInventoryController>();
                if (inventory == null) inventory = player.AddComponent<WeaponInventoryController>();
                inventory.ConfigureViewmodelReferences(player.GetComponent<LowPolyShooterViewmodel>());
                inventory.SetArmsController(WeaponInventorySlot.Secondary, handgunArms);
                inventory.SetPresentationProfiles(WeaponInventorySlot.Secondary,
                    AssetDatabase.LoadAssetAtPath<WeaponAdsProfile>(HandgunAdsProfilePath),
                    AssetDatabase.LoadAssetAtPath<WeaponPresentationProfile>(HandgunPresentationProfilePath));
                EditorUtility.SetDirty(player);
                PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(player);
            }
        }

        private static void RemoveBehaviours(AnimatorStateMachine machine)
        {
            foreach (ChildAnimatorState state in machine.states)
                state.state.behaviours = Array.Empty<StateMachineBehaviour>();
            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
                RemoveBehaviours(child.stateMachine);
        }
    }
}
