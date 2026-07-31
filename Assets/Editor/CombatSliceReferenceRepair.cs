using ProjectSun.FPS.Abilities;
using ProjectSun.FPS.Bootstrap;
using ProjectSun.FPS.Core;
using ProjectSun.FPS.Player;
using ProjectSun.FPS.Weapons;
using ProjectSun.FPS.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectSun.FPS.Editor
{
    /// <summary>Migrates prototype assets created before Health was placed in its own Unity script file.</summary>
    public static class CombatSliceReferenceRepair
    {
        private const string PlayerPrefabPath = "Assets/_ProjectSun/Prefabs/Characters/Player.prefab";
        private const string TargetPrefabPath = "Assets/_ProjectSun/Prefabs/Characters/TrainingTarget.prefab";
        private const string ScenePath = "Assets/_ProjectSun/Scenes/CombatSlice.unity";

        [MenuItem("Project Sun/Repair Combat Slice References", priority = 11)]
        public static void Repair()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            int removedScripts = RepairPrefab(PlayerPrefabPath);
            removedScripts += RepairPrefab(TargetPrefabPath);
            removedScripts += RepairScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Project Sun", $"Combat Slice repaired. Removed {removedScripts} missing component reference(s) and restored required Health components.", "OK");
        }

        private static int RepairPrefab(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return 0;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            int removed = RepairHierarchy(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            return removed;
        }

        private static int RepairScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return 0;
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            int removed = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                removed += RepairHierarchy(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return removed;
        }

        private static int RepairHierarchy(GameObject root)
        {
            int removed = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
            if (removed > 0) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);

            bool requiresHealth = root.GetComponent<TargetDummy>() != null || root.GetComponent<FpsPlayerInstaller>() != null;
            if (requiresHealth && root.GetComponent<Health>() == null)
                root.AddComponent<Health>();

            FpsPlayerInstaller playerInstaller = root.GetComponent<FpsPlayerInstaller>();
            if (playerInstaller != null)
            {
                playerInstaller.SetReferences(
                    root.GetComponent<FpsPlayerController>(),
                    root.GetComponent<Health>(),
                    root.GetComponent<HitscanWeapon>(),
                    root.GetComponent<FpsAbilityController>(),
                    root.GetComponentInChildren<Camera>(true),
                    FindDescendant(root.transform, "Muzzle"));
            }

            foreach (Transform child in root.transform)
                removed += RepairHierarchy(child.gameObject);
            return removed;
        }

        private static Transform FindDescendant(Transform root, string childName)
        {
            if (root.name == childName) return root;
            foreach (Transform child in root)
            {
                Transform found = FindDescendant(child, childName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
