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
                "6v6 team elimination roster and deterministic spawn groups are ready: Player + 5 attackers versus 6 defenders. Press Play to validate the round loop.",
                "OK");
        }

        // Preserves the previous menu entry for existing production notes and scene builder calls.
        [MenuItem("Project Sun/Add Defender Bots To Combat Slice", priority = 15)]
        public static void AddDefenders() => SetupTeamElimination();

        /// <summary>创建或修复 6v6 Bot 阵容、稳定名册槽位与双方出生点组。</summary>
        /// <param name="combatSliceRoot">Combat Slice 场景根节点；缺少 Environment 或 Player 时不做修改。</param>
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

            TeamCombatant playerCombatant = player.GetComponent<TeamCombatant>();
            if (playerCombatant == null) playerCombatant = Undo.AddComponent<TeamCombatant>(player.gameObject);
            Undo.RecordObject(playerCombatant, "Assign Player Team Slot");
            playerCombatant.AssignTeamSlot(CombatTeam.Attackers, 0);
            EditorUtility.SetDirty(playerCombatant);

            CreateTeamSpawnGroups(combatSliceRoot, player, attackerRoot, defenderRoot);
        }

        private static void EnsureTeamBots(Transform teamRoot, int desiredCount, CombatTeam team, Vector3[] spawnPositions,
            GameObject botPrefab, Transform player, CombatCoverPoint[] coverPoints)
        {
            CombatBotController[] existing = teamRoot.GetComponentsInChildren<CombatBotController>(true);
            for (int index = existing.Length; index < desiredCount; index++)
            {
                GameObject botObject = (GameObject)PrefabUtility.InstantiatePrefab(botPrefab);
                Undo.RegisterCreatedObjectUndo(botObject, "Create Team Elimination Bot");
                botObject.name = team == CombatTeam.Attackers ? $"Attacker {index + 1:00}" : $"Defender {index + 1:00}";
                botObject.transform.SetParent(teamRoot);
                botObject.transform.position = spawnPositions[Mathf.Min(index, spawnPositions.Length - 1)];
            }

            CombatBotController[] configuredBots = teamRoot.GetComponentsInChildren<CombatBotController>(true);
            System.Array.Sort(configuredBots, (left, right) => string.CompareOrdinal(left.name, right.name));
            for (int index = 0; index < configuredBots.Length; index++)
            {
                CombatBotController bot = configuredBots[index];
                Vector3 position = index < spawnPositions.Length ? spawnPositions[index] : bot.transform.position;
                if (index >= existing.Length) bot.transform.position = position;
                bot.Configure(player, System.Array.Empty<ObjectiveZone>(), position);
                bot.SetCoverPoints(coverPoints);

                TeamCombatant combatant = bot.GetComponent<TeamCombatant>();
                if (combatant == null) combatant = Undo.AddComponent<TeamCombatant>(bot.gameObject);
                Undo.RecordObject(combatant, "Assign Bot Team Slot");
                int rosterSlot = team == CombatTeam.Attackers ? index + 1 : index;
                combatant.AssignTeamSlot(team, rosterSlot);
                ApplyTeamMaterial(bot, team);
                EditorUtility.SetDirty(bot);
                EditorUtility.SetDirty(combatant);
            }
        }

        private static void CreateTeamSpawnGroups(Transform combatSliceRoot, Transform player, Transform attackerRoot,
            Transform defenderRoot)
        {
            Transform spawnRoot = GetOrCreateChild(combatSliceRoot, "Team Spawn Groups");
            CombatBotController[] attackerBots = attackerRoot.GetComponentsInChildren<CombatBotController>(true);
            CombatBotController[] defenderBots = defenderRoot.GetComponentsInChildren<CombatBotController>(true);
            System.Array.Sort(attackerBots, (left, right) => string.CompareOrdinal(left.name, right.name));
            System.Array.Sort(defenderBots, (left, right) => string.CompareOrdinal(left.name, right.name));
            Vector3[] defaultAttackerPositions = AttackerPositions();
            Vector3[] defaultDefenderPositions = DefenderPositions();

            Pose[] attackerPoses = new Pose[TeamSize];
            attackerPoses[0] = new Pose(player.position, player.rotation);
            for (int slotIndex = 1; slotIndex < TeamSize; slotIndex++)
            {
                CombatBotController bot = slotIndex - 1 < attackerBots.Length ? attackerBots[slotIndex - 1] : null;
                Vector3 fallbackPosition = defaultAttackerPositions[Mathf.Min(slotIndex - 1, defaultAttackerPositions.Length - 1)];
                attackerPoses[slotIndex] = bot != null
                    ? new Pose(bot.transform.position, bot.transform.rotation)
                    : new Pose(fallbackPosition, Quaternion.identity);
            }

            Pose[] defenderPoses = new Pose[TeamSize];
            for (int slotIndex = 0; slotIndex < TeamSize; slotIndex++)
            {
                CombatBotController bot = slotIndex < defenderBots.Length ? defenderBots[slotIndex] : null;
                Vector3 fallbackPosition = defaultDefenderPositions[Mathf.Min(slotIndex, defaultDefenderPositions.Length - 1)];
                defenderPoses[slotIndex] = bot != null
                    ? new Pose(bot.transform.position, bot.transform.rotation)
                    : new Pose(fallbackPosition, Quaternion.Euler(0f, 180f, 0f));
            }

            EnsureSpawnGroup(spawnRoot, "Attacker Spawn Group", CombatTeam.Attackers, attackerPoses);
            EnsureSpawnGroup(spawnRoot, "Defender Spawn Group", CombatTeam.Defenders, defenderPoses);
        }

        private static void EnsureSpawnGroup(Transform parent, string groupName, CombatTeam team, Pose[] initialPoses)
        {
            Transform groupRoot = GetOrCreateChild(parent, groupName);
            TeamSpawnGroup group = groupRoot.GetComponent<TeamSpawnGroup>();
            if (group == null) group = Undo.AddComponent<TeamSpawnGroup>(groupRoot.gameObject);

            Transform[] anchors = new Transform[initialPoses.Length];
            for (int slotIndex = 0; slotIndex < initialPoses.Length; slotIndex++)
            {
                string anchorName = $"Slot {slotIndex:00}";
                Transform existing = groupRoot.Find(anchorName);
                bool created = existing == null;
                Transform anchor = created ? GetOrCreateChild(groupRoot, anchorName) : existing;
                if (created)
                {
                    // 只在首次创建时播种位置；再次执行工具必须保留关卡设计师手工调整后的出生姿态。
                    anchor.SetPositionAndRotation(initialPoses[slotIndex].position, initialPoses[slotIndex].rotation);
                }
                anchors[slotIndex] = anchor;
            }

            Undo.RecordObject(group, "Configure Team Spawn Group");
            group.Configure(team, anchors);
            EditorUtility.SetDirty(group);
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
            Undo.RegisterCreatedObjectUndo(childObject, $"Create {childName}");
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
