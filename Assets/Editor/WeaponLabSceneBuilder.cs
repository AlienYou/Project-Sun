using System.Collections.Generic;
using ProjectSun.FPS.Bootstrap;
using ProjectSun.FPS.Core;
using ProjectSun.FPS.UI;
using ProjectSun.FPS.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectSun.FPS.Editor
{
    /// <summary>Builds the owned standalone range used to validate the real player weapon pipeline.</summary>
    public static class WeaponLabSceneBuilder
    {
        private const string ScenePath = "Assets/_ProjectSun/Scenes/WeaponLab.unity";
        private const string PlayerPrefabPath = "Assets/_ProjectSun/Prefabs/Characters/Player.prefab";
        private const string TargetPrefabPath = "Assets/_ProjectSun/Prefabs/Characters/TrainingTarget.prefab";

        [MenuItem("Project Sun/Build WeaponLab Scene", priority = 20)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var catalog = CombatWeaponDataGenerator.CreateOrGetDataAssets();
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            GameObject targetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TargetPrefabPath);
            if (playerPrefab == null || targetPrefab == null)
            {
                EditorUtility.DisplayDialog("Project Sun", "Player or TrainingTarget prefab is missing.", "OK");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("WeaponLab");
            GameObject environment = new GameObject("Environment");
            environment.transform.SetParent(root.transform);
            GameObject targetsRoot = new GameObject("Targets");
            targetsRoot.transform.SetParent(root.transform);
            CreateLighting(root.transform);
            CreateEnvironment(environment.transform);

            GameObject player = PrefabUtility.InstantiatePrefab(playerPrefab, scene) as GameObject;
            player.name = "Player";
            player.transform.SetParent(root.transform);
            player.transform.SetPositionAndRotation(new Vector3(0f, 0.02f, 0f), Quaternion.identity);
            FpsPlayerInstaller playerInstaller = player.GetComponent<FpsPlayerInstaller>();

            TargetDummy[] targets = CreateTargets(targetPrefab, targetsRoot.transform, scene);
            GameObject uiRoot = new GameObject("WeaponLab UI");
            uiRoot.transform.SetParent(root.transform);
            FpsHud hud = uiRoot.AddComponent<FpsHud>();
            WeaponCustomizationUI customization = uiRoot.AddComponent<WeaponCustomizationUI>();
            WeaponLabTelemetryHud telemetry = uiRoot.AddComponent<WeaponLabTelemetryHud>();
            WeaponLabController labController = root.AddComponent<WeaponLabController>();
            WeaponLabSceneInstaller installer = root.AddComponent<WeaponLabSceneInstaller>();
            installer.SetReferences(playerInstaller, hud, customization, labController, telemetry, targets, catalog);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeObject = root;
            EditorUtility.DisplayDialog("Project Sun",
                "WeaponLab was built. Press Play: F6 resets player, ammunition and every target; TAB opens the loadout screen.",
                "OK");
        }

        private static void CreateLighting(Transform parent)
        {
            GameObject lightObject = new GameObject("WeaponLab Key Light", typeof(Light));
            lightObject.transform.SetParent(parent);
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(0.9f, 0.95f, 1f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.26f, 0.31f, 0.4f);
        }

        private static void CreateEnvironment(Transform environment)
        {
            CreateBlock("Floor", environment, new Vector3(0f, -0.3f, 42f), new Vector3(28f, 0.6f, 96f));
            CreateBlock("Backstop", environment, new Vector3(0f, 4f, 86f), new Vector3(28f, 8f, 0.8f));
            CreateBlock("Left Lane Wall", environment, new Vector3(-14f, 2f, 42f), new Vector3(0.5f, 4f, 96f));
            CreateBlock("Right Lane Wall", environment, new Vector3(14f, 2f, 42f), new Vector3(0.5f, 4f, 96f));
            CreateBlock("Ballistic Cover Wall", environment, new Vector3(6f, 1.5f, 18f), new Vector3(4f, 3f, 0.5f));

            CreateRangeMarker(environment, 10f);
            CreateRangeMarker(environment, 25f);
            CreateRangeMarker(environment, 50f);
        }

        private static TargetDummy[] CreateTargets(GameObject targetPrefab, Transform parent, Scene scene)
        {
            var results = new List<TargetDummy>();
            CreateTarget("Target 10m", new Vector3(0f, 1.2f, 10f));
            CreateTarget("Target 25m", new Vector3(0f, 1.2f, 25f));
            CreateTarget("Target 50m", new Vector3(0f, 1.2f, 50f));
            CreateTarget("Target Behind Cover", new Vector3(6f, 1.2f, 25f));
            return results.ToArray();

            void CreateTarget(string targetName, Vector3 position)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(targetPrefab, scene) as GameObject;
                instance.name = targetName;
                instance.transform.SetParent(parent);
                instance.transform.SetPositionAndRotation(position, Quaternion.identity);
                TargetDummy target = instance.GetComponent<TargetDummy>();
                if (target != null)
                {
                    target.SetIdleYawDegreesPerSecond(0f);
                    results.Add(target);
                }
            }
        }

        private static void CreateRangeMarker(Transform environment, float distance)
        {
            GameObject marker = CreateBlock($"Range Marker {distance:0}m", environment,
                new Vector3(-1.8f, 0.02f, distance), new Vector3(2.4f, 0.04f, 0.15f));
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.layer = CombatLayers.IgnoreRaycastLayer;
        }

        private static GameObject CreateBlock(string blockName, Transform parent, Vector3 position, Vector3 scale)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = blockName;
            block.transform.SetParent(parent);
            block.transform.SetPositionAndRotation(position, Quaternion.identity);
            block.transform.localScale = scale;
            CombatLayers.SetLayerRecursively(block, CombatLayers.WallLayer);
            block.isStatic = true;
            return block;
        }
    }
}
