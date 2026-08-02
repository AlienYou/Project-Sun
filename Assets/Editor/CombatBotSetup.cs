using ProjectSun.FPS.AI;
using ProjectSun.FPS.Core;
using ProjectSun.FPS.Rounds;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ProjectSun.FPS.Editor
{
    /// <summary>Builds the offline 6v6 team-elimination test roster without baking navigation data.</summary>
    public static class CombatBotSetup
    {
        private const string ScenePath = "Assets/_ProjectSun/Scenes/CombatSlice.unity";
        private const string BotPrefabPath = "Assets/_ProjectSun/Prefabs/Characters/TrainingDefender.prefab";
        private const string AttackerMaterialPath = "Assets/_ProjectSun/Art/Materials/PrototypeAttacker.mat";
        private const string DefenderMaterialPath = "Assets/_ProjectSun/Art/Materials/PrototypeDefender.mat";
        private const int TeamSize = 6;

        [MenuItem("Project Sun/Setup 6v6 Team Elimination Bots", priority = 14)]
        public static void SetupTeamElimination()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                EditorUtility.DisplayDialog("Project Sun", "Create CombatSlice before adding team elimination bots.", "OK");
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

            CreateCombatBots(root.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Project Sun",
                "6v6 team elimination roster is ready: Player + 5 attackers versus 6 defenders. Press Play to validate the round loop.",
                "OK");
        }

        // Preserves the previous menu entry for existing production notes and scene builder calls.
        [MenuItem("Project Sun/Add Defender Bots To Combat Slice", priority = 15)]
        public static void AddDefenders() => SetupTeamElimination();

        public static void CreateCombatBots(Transform combatSliceRoot)
        {
            Transform environment = combatSliceRoot.Find("Environment");
            Transform player = combatSliceRoot.Find("Player");
            if (environment == null || player == null) return;

            Transform systems = GetOrCreateChild(combatSliceRoot, "Game Systems");
            RuntimeNavMeshBuilder navMeshBuilder = systems.GetComponent<RuntimeNavMeshBuilder>();
            if (navMeshBuilder == null) navMeshBuilder = systems.gameObject.AddComponent<RuntimeNavMeshBuilder>();
            navMeshBuilder.SetNavigationRoot(environment);

            CombatCoverPoint[] coverPoints = combatSliceRoot.GetComponentsInChildren<CombatCoverPoint>(true);
            Transform attackerRoot = GetOrCreateChild(combatSliceRoot, "Attackers");
            Transform defenderRoot = GetOrCreateChild(combatSliceRoot, "Defenders");
            GameObject botPrefab = CreateOrGetBotPrefab();

            // The human player is the sixth attacker in the local validation roster.
            EnsureTeamBots(attackerRoot, TeamSize - 1, CombatTeam.Attackers, AttackerPositions(), botPrefab, player, coverPoints);
            EnsureTeamBots(defenderRoot, TeamSize, CombatTeam.Defenders, DefenderPositions(), botPrefab, player, coverPoints);
        }

        private static void EnsureTeamBots(Transform teamRoot, int desiredCount, CombatTeam team, Vector3[] spawnPositions,
            GameObject botPrefab, Transform player, CombatCoverPoint[] coverPoints)
        {
            CombatBotController[] existing = teamRoot.GetComponentsInChildren<CombatBotController>(true);
            for (int index = existing.Length; index < desiredCount; index++)
            {
                GameObject botObject = (GameObject)PrefabUtility.InstantiatePrefab(botPrefab);
                botObject.name = team == CombatTeam.Attackers ? $"Attacker {index + 1:00}" : $"Defender {index + 1:00}";
                botObject.transform.SetParent(teamRoot);
                botObject.transform.position = spawnPositions[Mathf.Min(index, spawnPositions.Length - 1)];
            }

            CombatBotController[] configuredBots = teamRoot.GetComponentsInChildren<CombatBotController>(true);
            for (int index = 0; index < configuredBots.Length; index++)
            {
                CombatBotController bot = configuredBots[index];
                Vector3 position = index < spawnPositions.Length ? spawnPositions[index] : bot.transform.position;
                if (index >= existing.Length) bot.transform.position = position;
                bot.Configure(player, System.Array.Empty<ObjectiveZone>(), position);
                bot.SetCoverPoints(coverPoints);

                TeamCombatant combatant = bot.GetComponent<TeamCombatant>();
                if (combatant == null) combatant = bot.gameObject.AddComponent<TeamCombatant>();
                combatant.SetTeam(team);
                ApplyTeamMaterial(bot, team);
                EditorUtility.SetDirty(bot);
                EditorUtility.SetDirty(combatant);
            }
        }

        private static Vector3[] AttackerPositions() => new[]
        {
            new Vector3(-4f, 0f, -8f), new Vector3(-2f, 0f, -9f), new Vector3(0f, 0f, -8f),
            new Vector3(2f, 0f, -9f), new Vector3(4f, 0f, -8f)
        };

        private static Vector3[] DefenderPositions() => new[]
        {
            new Vector3(-7f, 0f, 6f), new Vector3(12f, 0f, 11f), new Vector3(2f, 0f, 17f),
            new Vector3(-12f, 0f, 14f), new Vector3(8f, 0f, 18f), new Vector3(0f, 0f, 22f)
        };

        private static Transform GetOrCreateChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null) return child;
            GameObject childObject = new GameObject(childName);
            childObject.transform.SetParent(parent);
            return childObject.transform;
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
            root.AddComponent<TeamCombatant>();
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
            visual.GetComponent<Renderer>().sharedMaterial = CreateOrGetTeamMaterial(DefenderMaterialPath, "PrototypeDefender",
                new Color(0.92f, 0.22f, 0.2f));

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BotPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void ApplyTeamMaterial(CombatBotController bot, CombatTeam team)
        {
            Color color = team == CombatTeam.Attackers ? new Color(0.18f, 0.5f, 0.95f) : new Color(0.92f, 0.22f, 0.2f);
            string path = team == CombatTeam.Attackers ? AttackerMaterialPath : DefenderMaterialPath;
            string materialName = team == CombatTeam.Attackers ? "PrototypeAttacker" : "PrototypeDefender";
            Material material = CreateOrGetTeamMaterial(path, materialName, color);
            foreach (Renderer renderer in bot.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterial = material;
        }

        private static Material CreateOrGetTeamMaterial(string path, string materialName, Color color)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader) { name = materialName };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else material.color = color;
            AssetDatabase.CreateAsset(material, path);
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
