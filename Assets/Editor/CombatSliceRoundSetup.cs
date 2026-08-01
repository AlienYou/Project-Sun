using System.Collections.Generic;
using ProjectSun.FPS.Rounds;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectSun.FPS.Editor
{
    /// <summary>Creates the two-objective attack/defend loop in an existing CombatSlice without touching level geometry.</summary>
    public static class CombatSliceRoundSetup
    {
        private const string ScenePath = "Assets/_ProjectSun/Scenes/CombatSlice.unity";
        private const string ObjectiveMaterialPath = "Assets/_ProjectSun/Art/Materials/PrototypeObjective.mat";

        [MenuItem("Project Sun/Add Round Loop To Combat Slice", priority = 13)]
        public static void AddRoundLoop()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                EditorUtility.DisplayDialog("Project Sun", "Create CombatSlice before adding its round loop.", "OK");
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

            ObjectiveZone[] objectives = CreateObjectives(root.transform);
            Transform systemsRoot = root.transform.Find("Game Systems");
            if (systemsRoot == null)
            {
                GameObject systems = new GameObject("Game Systems");
                systems.transform.SetParent(root.transform);
                systemsRoot = systems.transform;
            }
            RoundManager roundManager = systemsRoot.GetComponent<RoundManager>();
            if (roundManager == null) roundManager = systemsRoot.gameObject.AddComponent<RoundManager>();
            roundManager.SetObjectives(objectives);

            EditorUtility.SetDirty(roundManager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Project Sun", "Round loop added. Play CombatSlice and hold F inside Objective A or B.", "OK");
        }

        public static ObjectiveZone[] CreateObjectives(Transform combatSliceRoot)
        {
            Transform objectiveRoot = combatSliceRoot.Find("Objectives");
            if (objectiveRoot == null)
            {
                GameObject root = new GameObject("Objectives");
                root.transform.SetParent(combatSliceRoot);
                objectiveRoot = root.transform;
            }

            ObjectiveZone[] existing = objectiveRoot.GetComponentsInChildren<ObjectiveZone>(true);
            if (existing.Length > 0) return existing;

            Material material = CreateOrGetObjectiveMaterial();
            return new[]
            {
                CreateObjective("OBJECTIVE A", new Vector3(-10f, 0f, 8f), material, objectiveRoot),
                CreateObjective("OBJECTIVE B", new Vector3(10f, 0f, 13f), material, objectiveRoot)
            };
        }

        private static ObjectiveZone CreateObjective(string label, Vector3 position, Material material, Transform parent)
        {
            GameObject root = new GameObject(label);
            root.transform.SetParent(parent);
            root.transform.position = position;
            Rigidbody rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Marker";
            marker.transform.SetParent(root.transform, false);
            marker.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            marker.transform.localScale = new Vector3(3f, 0.04f, 3f);
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            Renderer markerRenderer = marker.GetComponent<Renderer>();
            markerRenderer.sharedMaterial = material;

            GameObject volume = new GameObject("Activation Volume");
            volume.transform.SetParent(root.transform, false);
            BoxCollider trigger = volume.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1.2f, 0f);
            trigger.size = new Vector3(5f, 2.4f, 5f);

            ObjectiveZone objective = root.AddComponent<ObjectiveZone>();
            objective.SetPresentation(label, markerRenderer);
            return objective;
        }

        private static Material CreateOrGetObjectiveMaterial()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(ObjectiveMaterialPath);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader) { name = "PrototypeObjective" };
            Color color = new Color(0.2f, 0.65f, 1f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else material.color = color;
            AssetDatabase.CreateAsset(material, ObjectiveMaterialPath);
            return material;
        }
    }
}
