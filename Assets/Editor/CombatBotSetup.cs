using ProjectSun.FPS.AI;
using ProjectSun.FPS.Core;
using ProjectSun.FPS.Player;
using ProjectSun.FPS.Rounds;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ProjectSun.FPS.Editor
{
    /// <summary>Adds reusable defender bots and a runtime NavMesh builder to the editable combat slice.</summary>
    public static class CombatBotSetup
    {
        private const string ScenePath = "Assets/_ProjectSun/Scenes/CombatSlice.unity";
        private const string BotPrefabPath = "Assets/_ProjectSun/Prefabs/Characters/TrainingDefender.prefab";
        private const string BotMaterialPath = "Assets/_ProjectSun/Art/Materials/PrototypeDefender.mat";

        [MenuItem("Project Sun/Add Defender Bots To Combat Slice", priority = 14)]
        public static void AddDefenders()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                EditorUtility.DisplayDialog("Project Sun", "Create CombatSlice before adding defender bots.", "OK");
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
            if (root.GetComponentsInChildren<ObjectiveZone>(true).Length == 0)
            {
                EditorUtility.DisplayDialog("Project Sun", "Add the round loop before adding defenders.", "OK");
                return;
            }

            CreateCombatBots(root.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Project Sun", "Three defenders and runtime navigation are ready. Press Play to validate their routes.", "OK");
        }

        public static void CreateCombatBots(Transform combatSliceRoot)
        {
            Transform environment = combatSliceRoot.Find("Environment");
            Transform player = combatSliceRoot.Find("Player");
            ObjectiveZone[] objectives = combatSliceRoot.GetComponentsInChildren<ObjectiveZone>(true);
            if (environment == null || player == null || objectives.Length == 0) return;

            Transform systems = combatSliceRoot.Find("Game Systems");
            if (systems == null)
            {
                GameObject systemsObject = new GameObject("Game Systems");
                systemsObject.transform.SetParent(combatSliceRoot);
                systems = systemsObject.transform;
            }
            RuntimeNavMeshBuilder navMeshBuilder = systems.GetComponent<RuntimeNavMeshBuilder>();
            if (navMeshBuilder == null) navMeshBuilder = systems.gameObject.AddComponent<RuntimeNavMeshBuilder>();
            navMeshBuilder.SetNavigationRoot(environment);

            Transform defenders = combatSliceRoot.Find("Defenders");
            if (defenders == null)
            {
                GameObject defendersObject = new GameObject("Defenders");
                defendersObject.transform.SetParent(combatSliceRoot);
                defenders = defendersObject.transform;
            }
            CombatCoverPoint[] coverPoints = combatSliceRoot.GetComponentsInChildren<CombatCoverPoint>(true);

            CombatBotController[] existingBots = defenders.GetComponentsInChildren<CombatBotController>(true);
            if (existingBots.Length > 0)
            {
                foreach (CombatBotController bot in existingBots)
                {
                    bot.Configure(player, objectives, bot.transform.position);
                    bot.SetCoverPoints(coverPoints);
                    EditorUtility.SetDirty(bot);
                }
                return;
            }

            GameObject botPrefab = CreateOrGetBotPrefab();
            Vector3[] positions =
            {
                new Vector3(-7f, 0f, 6f), new Vector3(12f, 0f, 11f), new Vector3(2f, 0f, 17f)
            };
            for (int i = 0; i < positions.Length; i++)
            {
                GameObject botObject = (GameObject)PrefabUtility.InstantiatePrefab(botPrefab);
                botObject.name = $"Defender {i + 1:00}";
                botObject.transform.SetParent(defenders);
                botObject.transform.position = positions[i];
                CombatBotController bot = botObject.GetComponent<CombatBotController>();
                bot.Configure(player, objectives, positions[i]);
                bot.SetCoverPoints(coverPoints);
                EditorUtility.SetDirty(bot);
            }
        }

        private static GameObject CreateOrGetBotPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(BotPrefabPath);
            if (existing != null) return existing;
            EnsureFolder("Assets/_ProjectSun/Prefabs/Characters");
            EnsureFolder("Assets/_ProjectSun/Art/Materials");

            GameObject root = new GameObject("Training Defender");
            root.layer = CombatLayers.CharacterLayer;
            root.AddComponent<Health>();
            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.32f;
            agent.height = 1.8f;
            agent.baseOffset = 0f;
            agent.speed = 3.6f;
            agent.angularSpeed = 540f;
            agent.acceleration = 18f;
            root.AddComponent<CombatBotController>();

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.layer = CombatLayers.CharacterLayer;
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.up;
            visual.transform.localScale = new Vector3(0.7f, 1f, 0.7f);
            visual.GetComponent<Renderer>().sharedMaterial = CreateOrGetBotMaterial();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BotPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Material CreateOrGetBotMaterial()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(BotMaterialPath);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader) { name = "PrototypeDefender" };
            Color color = new Color(0.92f, 0.22f, 0.2f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else material.color = color;
            AssetDatabase.CreateAsset(material, BotMaterialPath);
            return material;
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
