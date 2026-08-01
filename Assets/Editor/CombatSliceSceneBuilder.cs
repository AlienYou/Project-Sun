using ProjectSun.FPS.Abilities;
using ProjectSun.FPS.Bootstrap;
using ProjectSun.FPS.Core;
using ProjectSun.FPS.Player;
using ProjectSun.FPS.Input;
using ProjectSun.FPS.Rounds;
using ProjectSun.FPS.UI;
using ProjectSun.FPS.Weapons;
using ProjectSun.FPS.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectSun.FPS.Editor
{
    /// <summary>
    /// Creates the first editable combat scene and its prototype prefabs without replacing user-authored assets.
    /// Run from Project Sun > Build Combat Slice Scene after scripts have compiled.
    /// </summary>
    public static class CombatSliceSceneBuilder
    {
        private const string Root = "Assets/_ProjectSun";
        private const string ScenePath = Root + "/Scenes/CombatSlice.unity";
        private const string PlayerPrefabPath = Root + "/Prefabs/Characters/Player.prefab";
        private const string TargetPrefabPath = Root + "/Prefabs/Characters/TrainingTarget.prefab";
        private const string FloorMaterialPath = Root + "/Art/Materials/PrototypeFloor.mat";
        private const string WallMaterialPath = Root + "/Art/Materials/PrototypeWall.mat";
        private const string TargetMaterialPath = Root + "/Art/Materials/PrototypeTarget.mat";
        private const string WeaponMaterialPath = Root + "/Art/Materials/PrototypeWeapon.mat";

        [MenuItem("Project Sun/Build Combat Slice Scene", priority = 10)]
        public static void BuildCombatSlice()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolders();

            SceneAsset existingScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (existingScene != null)
            {
                EditorSceneManager.OpenScene(ScenePath);
                EditorUtility.DisplayDialog("Project Sun", "CombatSlice already exists and has been opened. Existing scenes and prefabs were not overwritten.", "OK");
                return;
            }

            Material floorMaterial = CreateOrGetMaterial(FloorMaterialPath, new Color(0.06f, 0.08f, 0.11f));
            Material wallMaterial = CreateOrGetMaterial(WallMaterialPath, new Color(0.11f, 0.16f, 0.21f));
            Material targetMaterial = CreateOrGetMaterial(TargetMaterialPath, new Color(0.11f, 0.60f, 0.82f));
            Material weaponMaterial = CreateOrGetMaterial(WeaponMaterialPath, new Color(0.09f, 0.12f, 0.15f));
            WeaponLoadoutCatalog loadoutCatalog = CombatWeaponDataGenerator.CreateOrGetDataAssets();
            GameObject playerPrefab = CreateOrGetPlayerPrefab(weaponMaterial);
            GameObject targetPrefab = CreateOrGetTargetPrefab(targetMaterial);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject sceneRoot = new GameObject("Combat Slice");
            CreateLighting(sceneRoot.transform);
            CreateEnvironment(sceneRoot.transform, floorMaterial, wallMaterial);
            ObjectiveZone[] objectives = CombatSliceRoundSetup.CreateObjectives(sceneRoot.transform);
            FpsPlayerInstaller player = CreatePlayer(sceneRoot.transform, playerPrefab);
            CreateTargets(sceneRoot.transform, targetPrefab);
            CreateSystems(sceneRoot.transform, player, loadoutCatalog, objectives);
            CombatBotSetup.CreateCombatBots(sceneRoot.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = sceneRoot;
            EditorUtility.DisplayDialog("Project Sun", "CombatSlice has been created. Press Play to validate the editable scene.", "OK");
        }

        private static void EnsureFolders()
        {
            EnsureFolder(Root);
            EnsureFolder(Root + "/Scenes");
            EnsureFolder(Root + "/Prefabs");
            EnsureFolder(Root + "/Prefabs/Characters");
            EnsureFolder(Root + "/Art");
            EnsureFolder(Root + "/Art/Materials");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static Material CreateOrGetMaterial(string path, Color color)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else material.color = color;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject CreateOrGetPlayerPrefab(Material weaponMaterial)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (existing != null) return existing;

            GameObject root = new GameObject("Player");
            CharacterController characterController = root.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = 0.32f;
            characterController.center = new Vector3(0f, 0.9f, 0f);
            Health health = root.AddComponent<Health>();
            root.AddComponent<FpsInput>();
            FpsPlayerController player = root.AddComponent<FpsPlayerController>();
            HitscanWeapon weapon = root.AddComponent<HitscanWeapon>();
            FpsAbilityController abilities = root.AddComponent<FpsAbilityController>();

            GameObject cameraObject = new GameObject("Player Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            cameraObject.tag = "MainCamera";
            Camera playerCamera = cameraObject.GetComponent<Camera>();
            playerCamera.nearClipPlane = 0.03f;
            playerCamera.fieldOfView = 78f;
            playerCamera.clearFlags = CameraClearFlags.Skybox;

            GameObject weaponVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            weaponVisual.name = "Prototype Carbine";
            weaponVisual.transform.SetParent(cameraObject.transform, false);
            weaponVisual.transform.localPosition = new Vector3(0.28f, -0.25f, 0.65f);
            weaponVisual.transform.localRotation = Quaternion.Euler(0f, 0f, -4f);
            weaponVisual.transform.localScale = new Vector3(0.16f, 0.15f, 0.72f);
            Object.DestroyImmediate(weaponVisual.GetComponent<Collider>());
            weaponVisual.GetComponent<Renderer>().sharedMaterial = weaponMaterial;

            GameObject muzzleObject = new GameObject("Muzzle");
            muzzleObject.transform.SetParent(weaponVisual.transform, false);
            muzzleObject.transform.localPosition = new Vector3(0f, 0f, 0.55f);

            FpsPlayerInstaller installer = root.AddComponent<FpsPlayerInstaller>();
            installer.SetReferences(player, health, weapon, abilities, playerCamera, muzzleObject.transform);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateOrGetTargetPrefab(Material targetMaterial)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(TargetPrefabPath);
            if (existing != null) return existing;

            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            root.name = "Training Target";
            root.transform.localScale = new Vector3(0.7f, 1.2f, 0.7f);
            root.GetComponent<Renderer>().sharedMaterial = targetMaterial;
            root.AddComponent<Health>();
            root.AddComponent<TargetDummy>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, TargetPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static FpsPlayerInstaller CreatePlayer(Transform parent, GameObject prefab)
        {
            GameObject playerObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            playerObject.name = "Player";
            playerObject.transform.SetParent(parent);
            playerObject.transform.position = new Vector3(0f, 0.03f, -14f);
            return playerObject.GetComponent<FpsPlayerInstaller>();
        }

        private static void CreateSystems(Transform parent, FpsPlayerInstaller player, WeaponLoadoutCatalog loadoutCatalog,
            ObjectiveZone[] objectives)
        {
            GameObject systems = new GameObject("Game Systems");
            systems.transform.SetParent(parent);
            FpsHud hud = systems.AddComponent<FpsHud>();
            WeaponCustomizationUI customization = systems.AddComponent<WeaponCustomizationUI>();
            systems.AddComponent<FpsSettingsMenu>();
            RoundManager roundManager = systems.AddComponent<RoundManager>();
            roundManager.SetObjectives(objectives);
            CombatSliceSceneInstaller installer = systems.AddComponent<CombatSliceSceneInstaller>();
            installer.SetReferences(player, hud, customization, loadoutCatalog);
        }

        private static void CreateLighting(Transform parent)
        {
            GameObject directionalObject = new GameObject("Range Directional Light", typeof(Light));
            directionalObject.transform.SetParent(parent);
            directionalObject.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            Light directional = directionalObject.GetComponent<Light>();
            directional.type = LightType.Directional;
            directional.intensity = 1.15f;
            RenderSettings.ambientIntensity = 0.8f;
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.045f, 0.065f, 0.09f);
            RenderSettings.fogDensity = 0.009f;
        }

        private static void CreateEnvironment(Transform parent, Material floorMaterial, Material wallMaterial)
        {
            GameObject environment = new GameObject("Environment");
            environment.transform.SetParent(parent);
            CreateBlock("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(38f, 1f, 48f), floorMaterial, environment.transform);
            CreateBlock("North Wall", new Vector3(0f, 3f, 20f), new Vector3(38f, 6f, 1f), wallMaterial, environment.transform);
            CreateBlock("South Wall", new Vector3(0f, 3f, -20f), new Vector3(38f, 6f, 1f), wallMaterial, environment.transform);
            CreateBlock("East Wall", new Vector3(19f, 3f, 0f), new Vector3(1f, 6f, 40f), wallMaterial, environment.transform);
            CreateBlock("West Wall", new Vector3(-19f, 3f, 0f), new Vector3(1f, 6f, 40f), wallMaterial, environment.transform);
            CreateBlock("Cover A", new Vector3(-5f, 1.1f, -2f), new Vector3(4f, 2.2f, 1.2f), wallMaterial, environment.transform);
            CreateBlock("Cover B", new Vector3(6f, 1.1f, 7f), new Vector3(3f, 2.2f, 1.2f), wallMaterial, environment.transform);
            CreateBlock("Cover C", new Vector3(-8f, 1.1f, 12f), new Vector3(2.5f, 2.2f, 1.2f), wallMaterial, environment.transform);
        }

        private static void CreateTargets(Transform parent, GameObject prefab)
        {
            GameObject targets = new GameObject("Training Targets");
            targets.transform.SetParent(parent);
            Vector3[] positions =
            {
                new Vector3(-8f, 1.2f, -1f), new Vector3(5f, 1.2f, 2f), new Vector3(-2f, 1.2f, 9f),
                new Vector3(10f, 1.2f, 13f), new Vector3(-12f, 1.2f, 15f)
            };
            for (int i = 0; i < positions.Length; i++)
            {
                GameObject target = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                target.name = $"Training Target {i + 1:00}";
                target.transform.SetParent(targets.transform);
                target.transform.position = positions[i];
            }
        }

        private static void CreateBlock(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent);
            block.transform.position = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void AddSceneToBuildSettings()
        {
            foreach (EditorBuildSettingsScene configuredScene in EditorBuildSettings.scenes)
                if (configuredScene.path == ScenePath)
                    return;

            EditorBuildSettingsScene[] oldScenes = EditorBuildSettings.scenes;
            EditorBuildSettingsScene[] newScenes = new EditorBuildSettingsScene[oldScenes.Length + 1];
            System.Array.Copy(oldScenes, newScenes, oldScenes.Length);
            newScenes[newScenes.Length - 1] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = newScenes;
        }
    }
}
