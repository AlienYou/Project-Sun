using ProjectSun.FPS.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectSun.FPS.Editor
{
    /// <summary>Creates editable cover/peek anchors around the three greybox cover blocks.</summary>
    public static class CombatCoverSetup
    {
        private const string ScenePath = "Assets/_ProjectSun/Scenes/CombatSlice.unity";

        [MenuItem("Project Sun/Add Tactical Cover Points To Combat Slice", priority = 16)]
        public static void AddTacticalCoverPoints()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                EditorUtility.DisplayDialog("Project Sun", "Create CombatSlice before adding tactical cover points.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject root = GameObject.Find("Combat Slice");
            if (root == null)
            {
                EditorUtility.DisplayDialog("Project Sun", "Combat Slice root was not found. No changes were made.", "OK");
                return;
            }
            CreateCoverPoints(root.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Project Sun", "Tactical cover points are ready. Toggle Gizmos to inspect blue anchors and validate Defender cover movement.", "OK");
        }

        public static CombatCoverPoint[] CreateCoverPoints(Transform combatSliceRoot)
        {
            if (combatSliceRoot == null) return System.Array.Empty<CombatCoverPoint>();
            Transform environment = combatSliceRoot.Find("Environment");
            if (environment == null) return System.Array.Empty<CombatCoverPoint>();

            Transform root = combatSliceRoot.Find("Tactical Cover Points");
            if (root == null)
            {
                GameObject rootObject = new GameObject("Tactical Cover Points");
                rootObject.transform.SetParent(combatSliceRoot);
                root = rootObject.transform;
            }
            CombatCoverPoint[] existing = root.GetComponentsInChildren<CombatCoverPoint>(true);
            if (existing.Length > 0)
            {
                AssignCoverPoints(combatSliceRoot, existing);
                return existing;
            }

            string[] coverNames = { "Cover A", "Cover B", "Cover C" };
            foreach (string coverName in coverNames)
            {
                Transform cover = environment.Find(coverName);
                if (cover == null) continue;
                Collider collider = cover.GetComponent<Collider>();
                if (collider == null) continue;
                AddPair(root, coverName, collider.bounds, -1f);
                AddPair(root, coverName, collider.bounds, 1f);
            }
            CombatCoverPoint[] created = root.GetComponentsInChildren<CombatCoverPoint>(true);
            AssignCoverPoints(combatSliceRoot, created);
            return created;
        }

        private static void AddPair(Transform parent, string coverName, Bounds bounds, float side)
        {
            float edgeX = bounds.extents.x + 0.55f;
            float behindZ = bounds.extents.z + 0.6f;
            Vector3 coverPosition = new Vector3(bounds.center.x + side * edgeX * 0.65f, 0f, bounds.center.z + behindZ);
            Vector3 peekPosition = new Vector3(bounds.center.x + side * edgeX, 0f, bounds.center.z + behindZ * 0.25f);
            GameObject anchor = new GameObject($"{coverName} {(side < 0f ? "Left" : "Right")}");
            anchor.transform.SetParent(parent);
            CombatCoverPoint point = anchor.AddComponent<CombatCoverPoint>();
            point.SetPositions(coverPosition, peekPosition);
        }

        private static void AssignCoverPoints(Transform combatSliceRoot, CombatCoverPoint[] points)
        {
            foreach (CombatBotController defender in combatSliceRoot.GetComponentsInChildren<CombatBotController>(true))
            {
                defender.SetCoverPoints(points);
                EditorUtility.SetDirty(defender);
            }
        }
    }
}
