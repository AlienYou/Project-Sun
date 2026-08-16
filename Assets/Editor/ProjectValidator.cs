using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectSun.FPS.Bootstrap;
using ProjectSun.FPS.Input;
using ProjectSun.FPS.Player;
using ProjectSun.FPS.Presentation;
using ProjectSun.FPS.Rounds;
using ProjectSun.FPS.UI;
using ProjectSun.FPS.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ProjectSun.FPS.Editor
{
    /// <summary>Project Sun 项目级只读预检。所有检查均返回稳定编号，供窗口、测试与构建门禁共同消费。</summary>
    public static class ProjectValidator
    {
        /// <summary>默认核心切片场景的固定项目路径。</summary>
        public const string CombatSlicePath = "Assets/_ProjectSun/Scenes/CombatSlice.unity";
        /// <summary>武器专项测试场景的固定项目路径。</summary>
        public const string WeaponLabPath = "Assets/_ProjectSun/Scenes/WeaponLab.unity";
        /// <summary>当前核心切片使用的权威配装目录路径。</summary>
        public const string CatalogPath = "Assets/_ProjectSun/Data/Weapons/Catalogs/AR4LoadoutCatalog.asset";
        /// <summary>本地玩家关键 Prefab 的固定项目路径。</summary>
        public const string PlayerPrefabPath = "Assets/_ProjectSun/Prefabs/Characters/Player.prefab";
        /// <summary>项目锁定的完整 Unity 编辑器版本。</summary>
        public const string RequiredUnityVersion = "2022.3.51f1c1";
        /// <summary>团队歼灭单方固定名册容量。</summary>
        public const int TeamCapacity = 6;

        private static readonly string[] ThirdPartyScenePrefixes =
        {
            "Assets/Infima Games/", "Assets/LowPolyFPSLite/", "Assets/LowPolyWeapons_LITE/"
        };

        /// <summary>执行完整只读校验。调用方可重复执行，方法不会保存、标脏或改写场景和资产。</summary>
        /// <returns>按检查阶段排列的结果；至少包含一个摘要所需的成功或失败项。</returns>
        public static IReadOnlyList<ProjectValidationResult> ValidateProject()
        {
            List<ProjectValidationResult> results = new List<ProjectValidationResult>();
            ValidateUnityVersion(results);
            ValidateLayers(results);
            ValidateInputSystem(results);
            results.AddRange(ValidateBuildScenePaths(EditorBuildSettings.scenes
                .Where(scene => scene.enabled).Select(scene => scene.path)));
            ValidateAssets(results);
            ValidateScene(CombatSlicePath, true, results);
            ValidateScene(WeaponLabPath, false, results);
            ValidateRendererFeature(results);
            return results;
        }

        /// <summary>纯函数校验启用场景顺序，可由 EditMode 测试传入虚拟路径而不接触工程设置。</summary>
        /// <param name="enabledScenePaths">按 Build Settings 顺序排列的启用场景路径；null 等同空列表。</param>
        /// <returns>入口顺序和第三方场景检查结果。</returns>
        public static IReadOnlyList<ProjectValidationResult> ValidateBuildScenePaths(
            IEnumerable<string> enabledScenePaths)
        {
            string[] paths = enabledScenePaths?.ToArray() ?? Array.Empty<string>();
            List<ProjectValidationResult> results = new List<ProjectValidationResult>();
            bool correctEntry = paths.Length > 0 && paths[0] == CombatSlicePath;
            results.Add(Result("PSV-BUILD-001", correctEntry, ProjectValidationSeverity.Error,
                correctEntry ? "启用场景首项为 CombatSlice。" : "启用场景首项必须是 CombatSlice。",
                AssetDatabase.LoadAssetAtPath<SceneAsset>(CombatSlicePath)));

            string thirdParty = paths.FirstOrDefault(path => ThirdPartyScenePrefixes.Any(
                prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));
            results.Add(Result("PSV-BUILD-002", thirdParty == null, ProjectValidationSeverity.Error,
                thirdParty == null ? "启用场景中没有第三方展示场景。" : $"第三方场景被错误启用：{thirdParty}",
                thirdParty == null ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(thirdParty)));

            bool weaponLabExists = Array.IndexOf(paths, WeaponLabPath) >= 0;
            results.Add(Result("PSV-BUILD-003", weaponLabExists, ProjectValidationSeverity.Warning,
                weaponLabExists ? "WeaponLab 已保留为专项测试场景。" : "WeaponLab 未加入启用场景，专项构建无法直接加载。",
                AssetDatabase.LoadAssetAtPath<SceneAsset>(WeaponLabPath)));
            return results;
        }

        /// <summary>纯函数校验 Layer 名称和固定索引，防止运行时回退索引掩盖配置错误。</summary>
        /// <param name="layers">Layer 名称到索引的映射；名称比较区分大小写，null 等同空映射。</param>
        /// <returns>每个必需 Layer 独立的检查结果。</returns>
        public static IReadOnlyList<ProjectValidationResult> ValidateRequiredLayers(
            IReadOnlyDictionary<string, int> layers)
        {
            Dictionary<string, int> required = new Dictionary<string, int>
            {
                { "Wall", 8 }, { "First Person View", 9 }, { "Character", 10 }
            };
            List<ProjectValidationResult> results = new List<ProjectValidationResult>();
            foreach (KeyValuePair<string, int> contract in required)
            {
                bool valid = layers != null && layers.TryGetValue(contract.Key, out int actual) && actual == contract.Value;
                string message = valid
                    ? $"Layer {contract.Key} 位于契约索引 {contract.Value}。"
                    : $"Layer {contract.Key} 缺失或索引错误，必须位于 {contract.Value}。";
                results.Add(Result($"PSV-LAYER-{contract.Value:000}", valid, ProjectValidationSeverity.Error, message));
            }
            return results;
        }

        /// <summary>纯函数校验双方六个槽位是否完整、唯一且在有效范围内。</summary>
        /// <param name="slots">待校验的阵营与槽位记录；null 等同空集合。</param>
        /// <returns>双方各一条完整性结果，并为重复、越界或未分配记录生成独立错误。</returns>
        public static IReadOnlyList<ProjectValidationResult> ValidateTeamSlots(IEnumerable<ValidationTeamSlot> slots)
        {
            ValidationTeamSlot[] records = slots?.ToArray() ?? Array.Empty<ValidationTeamSlot>();
            List<ProjectValidationResult> results = new List<ProjectValidationResult>();
            foreach (ValidationTeamSlot record in records)
            {
                bool valid = record.Team != CombatTeam.None && record.SlotIndex >= 0 && record.SlotIndex < TeamCapacity;
                if (!valid)
                    results.Add(Fail("PSV-TEAM-001", $"成员 {record.Name} 的阵营或槽位越界：{record.Team}/{record.SlotIndex}。"));
            }

            foreach (CombatTeam team in new[] { CombatTeam.Attackers, CombatTeam.Defenders })
            {
                ValidationTeamSlot[] teamRecords = records.Where(record => record.Team == team &&
                    record.SlotIndex >= 0 && record.SlotIndex < TeamCapacity).ToArray();
                bool unique = teamRecords.GroupBy(record => record.SlotIndex).All(group => group.Count() == 1);
                bool complete = unique && teamRecords.Select(record => record.SlotIndex).Distinct().Count() == TeamCapacity;
                results.Add(Result(team == CombatTeam.Attackers ? "PSV-TEAM-010" : "PSV-TEAM-011", complete,
                    ProjectValidationSeverity.Error,
                    complete ? $"{team} 的 0-5 槽位完整且唯一。" : $"{team} 必须恰好配置 0-5 六个唯一槽位。"));
            }
            return results;
        }

        /// <summary>构建门禁纯规则：Error 阻止构建，Warning 与 Pass 只进入报告。</summary>
        /// <param name="results">预检结果；null 视为无结果并拒绝构建，避免绕过门禁。</param>
        /// <returns>存在结果且没有 Error 时返回 true。</returns>
        public static bool CanBuild(IEnumerable<ProjectValidationResult> results)
        {
            if (results == null) return false;
            ProjectValidationResult[] snapshot = results.ToArray();
            return snapshot.Length > 0 && snapshot.All(result => result.Severity != ProjectValidationSeverity.Error);
        }

        private static void ValidateUnityVersion(List<ProjectValidationResult> results)
        {
            bool valid = Application.unityVersion == RequiredUnityVersion;
            results.Add(Result("PSV-ENV-001", valid, ProjectValidationSeverity.Error,
                valid ? $"Unity 版本为 {RequiredUnityVersion}。" :
                $"Unity 版本为 {Application.unityVersion}，项目固定要求 {RequiredUnityVersion}。"));
        }

        private static void ValidateLayers(List<ProjectValidationResult> results)
        {
            Dictionary<string, int> layers = new Dictionary<string, int>();
            for (int index = 0; index < 32; index++)
            {
                string layerName = LayerMask.LayerToName(index);
                if (!string.IsNullOrEmpty(layerName)) layers[layerName] = index;
            }
            results.AddRange(ValidateRequiredLayers(layers));
        }

        private static void ValidateInputSystem(List<ProjectValidationResult> results)
        {
            bool packageAvailable = InputSystem.settings != null;
            results.Add(Result("PSV-INPUT-001", packageAvailable, ProjectValidationSeverity.Error,
                packageAvailable ? "Input System 设置可用。" : "Input System 设置不可用。"));

            string projectSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "ProjectSettings", "ProjectSettings.asset");
            string settingsText = File.Exists(projectSettingsPath) ? File.ReadAllText(projectSettingsPath) : string.Empty;
            bool newInputEnabled = settingsText.Contains("activeInputHandler: 1") ||
                                   settingsText.Contains("activeInputHandler: 2");
            results.Add(Result("PSV-INPUT-002", newInputEnabled, ProjectValidationSeverity.Error,
                newInputEnabled ? "Player Settings 已启用 Input System。" : "Player Settings 未启用 Input System。"));

            MonoScript inputScript = FindScript(typeof(FpsInput));
            results.Add(Result("PSV-INPUT-003", inputScript != null, ProjectValidationSeverity.Error,
                inputScript != null ? "FpsInput 运行时 Action 定义可用。" : "缺少 FpsInput 运行时 Action 定义。", inputScript));
        }

        private static void ValidateAssets(List<ProjectValidationResult> results)
        {
            SceneAsset combatSlice = AssetDatabase.LoadAssetAtPath<SceneAsset>(CombatSlicePath);
            SceneAsset weaponLab = AssetDatabase.LoadAssetAtPath<SceneAsset>(WeaponLabPath);
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            WeaponLoadoutCatalog catalog = AssetDatabase.LoadAssetAtPath<WeaponLoadoutCatalog>(CatalogPath);
            results.Add(Result("PSV-ASSET-001", combatSlice != null, ProjectValidationSeverity.Error,
                combatSlice != null ? "CombatSlice 场景存在。" : "缺少 CombatSlice 场景。", combatSlice));
            results.Add(Result("PSV-ASSET-002", weaponLab != null, ProjectValidationSeverity.Error,
                weaponLab != null ? "WeaponLab 场景存在。" : "缺少 WeaponLab 场景。", weaponLab));
            results.Add(Result("PSV-ASSET-003", playerPrefab != null, ProjectValidationSeverity.Error,
                playerPrefab != null ? "关键 Player Prefab 存在。" : "缺少关键 Player Prefab。", playerPrefab));

            bool catalogValid = catalog != null && catalog.DefaultPrimaryWeapon != null &&
                                catalog.DefaultSecondaryWeapon != null && catalog.TacticalEquipment.Any(item => item != null);
            results.Add(Result("PSV-ASSET-004", catalogValid, ProjectValidationSeverity.Error,
                catalogValid ? "武器目录包含主武器、副武器和战术装备。" :
                "武器目录缺失，或未完整配置主武器、副武器与战术装备。", catalog));
        }

        private static void ValidateScene(string scenePath, bool validateCombatContract,
            List<ProjectValidationResult> results)
        {
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (sceneAsset == null) return;
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedForValidation = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (openedForValidation) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                GameObject[] roots = scene.GetRootGameObjects();
                int missingScripts = roots.Sum(root => CountMissingScripts(root));
                results.Add(Result(scenePath == CombatSlicePath ? "PSV-SCENE-001" : "PSV-SCENE-002",
                    missingScripts == 0, ProjectValidationSeverity.Error,
                    missingScripts == 0 ? $"{scene.name} 没有 Missing Script。" :
                    $"{scene.name} 包含 {missingScripts} 个 Missing Script。", sceneAsset));
                if (validateCombatContract) ValidateCombatSceneObjects(roots, sceneAsset, results);
            }
            catch (Exception exception)
            {
                results.Add(new ProjectValidationResult("PSV-SCENE-000", ProjectValidationSeverity.Error,
                    $"无法只读检查场景 {scenePath}：{exception.Message}", sceneAsset));
            }
            finally
            {
                // 只关闭本次临时打开的场景，保留用户原有场景布局及其未保存修改。
                if (openedForValidation && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void ValidateCombatSceneObjects(GameObject[] roots, SceneAsset sceneAsset,
            List<ProjectValidationResult> results)
        {
            FpsPlayerInstaller[] players = ComponentsInScene<FpsPlayerInstaller>(roots);
            RoundManager[] rounds = ComponentsInScene<RoundManager>(roots);
            FpsHud[] huds = ComponentsInScene<FpsHud>(roots);
            CombatSliceSceneInstaller[] installers = ComponentsInScene<CombatSliceSceneInstaller>(roots);
            AddUniqueResult(results, "PSV-SCENE-010", "本地玩家", players.Length, sceneAsset);
            AddUniqueResult(results, "PSV-SCENE-011", "RoundManager", rounds.Length, sceneAsset);
            AddUniqueResult(results, "PSV-SCENE-012", "HUD", huds.Length, sceneAsset);
            AddUniqueResult(results, "PSV-SCENE-013", "CombatSliceSceneInstaller", installers.Length, sceneAsset);

            Camera[] cameras = ComponentsInScene<Camera>(roots)
                .Where(camera => camera.enabled && camera.targetTexture == null).ToArray();
            AudioListener[] listeners = ComponentsInScene<AudioListener>(roots)
                .Where(listener => listener.enabled && listener.gameObject.activeInHierarchy).ToArray();
            AddUniqueResult(results, "PSV-SCENE-014", "启用的 Base Camera", cameras.Length, sceneAsset);
            AddUniqueResult(results, "PSV-SCENE-015", "启用的 AudioListener", listeners.Length, sceneAsset);

            TeamSpawnGroup[] groups = ComponentsInScene<TeamSpawnGroup>(roots);
            foreach (CombatTeam team in new[] { CombatTeam.Attackers, CombatTeam.Defenders })
            {
                TeamSpawnGroup[] teamGroups = groups.Where(group => group.Team == team).ToArray();
                bool valid = teamGroups.Length == 1 && teamGroups[0].SlotCount == TeamCapacity;
                if (valid)
                {
                    for (int slot = 0; slot < TeamCapacity; slot++)
                        valid &= teamGroups[0].TryGetSpawnPose(slot, out _);
                }
                results.Add(Result(team == CombatTeam.Attackers ? "PSV-SPAWN-010" : "PSV-SPAWN-011", valid,
                    ProjectValidationSeverity.Error,
                    valid ? $"{team} 出生点组包含六个有效槽位。" :
                    $"{team} 必须且只能有一个出生点组，并包含六个非空槽位。", sceneAsset));
            }

            TeamCombatant[] combatants = ComponentsInScene<TeamCombatant>(roots);
            results.AddRange(ValidateTeamSlots(combatants.Select(combatant =>
                new ValidationTeamSlot(combatant.name, combatant.Team, combatant.TeamSlot))));
        }

        private static void ValidateRendererFeature(List<ProjectValidationResult> results)
        {
            HashSet<ScriptableRendererData> rendererData = new HashSet<ScriptableRendererData>();
            CollectRendererData(GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset, rendererData);
            for (int quality = 0; quality < QualitySettings.names.Length; quality++)
                CollectRendererData(QualitySettings.GetRenderPipelineAssetAt(quality) as UniversalRenderPipelineAsset, rendererData);
            bool installed = rendererData.Any(data => data != null && data.rendererFeatures.Any(
                feature => feature is ScopePeripheralRenderFeature));
            UnityEngine.Object context = rendererData.FirstOrDefault();
            results.Add(Result("PSV-URP-001", installed, ProjectValidationSeverity.Error,
                installed ? "当前 URP Renderer Data 已安装 ScopePeripheralRenderFeature。" :
                "当前 URP Renderer Data 缺少 ScopePeripheralRenderFeature；Validator 不会自动修改。", context));
        }

        private static void CollectRendererData(UniversalRenderPipelineAsset pipeline,
            ISet<ScriptableRendererData> destination)
        {
            if (pipeline == null) return;
            SerializedObject serializedPipeline = new SerializedObject(pipeline);
            SerializedProperty list = serializedPipeline.FindProperty("m_RendererDataList");
            if (list == null) return;
            for (int index = 0; index < list.arraySize; index++)
            {
                ScriptableRendererData data = list.GetArrayElementAtIndex(index).objectReferenceValue as ScriptableRendererData;
                if (data != null) destination.Add(data);
            }
        }

        private static int CountMissingScripts(GameObject root)
        {
            int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
            foreach (Transform child in root.transform) count += CountMissingScripts(child.gameObject);
            return count;
        }

        private static T[] ComponentsInScene<T>(IEnumerable<GameObject> roots) where T : Component
        {
            return roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        }

        private static void AddUniqueResult(List<ProjectValidationResult> results, string id, string label,
            int count, UnityEngine.Object context)
        {
            results.Add(Result(id, count == 1, ProjectValidationSeverity.Error,
                count == 1 ? $"CombatSlice 具有唯一的{label}。" :
                $"CombatSlice 的{label}数量必须为 1，当前为 {count}。", context));
        }

        private static MonoScript FindScript(Type type)
        {
            return AssetDatabase.FindAssets($"{type.Name} t:MonoScript")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MonoScript>)
                .FirstOrDefault(script => script != null && script.GetClass() == type);
        }

        private static ProjectValidationResult Result(string id, bool passed,
            ProjectValidationSeverity failureSeverity, string message, UnityEngine.Object context = null)
        {
            return new ProjectValidationResult(id,
                passed ? ProjectValidationSeverity.Pass : failureSeverity, message, context);
        }

        private static ProjectValidationResult Fail(string id, string message)
        {
            return new ProjectValidationResult(id, ProjectValidationSeverity.Error, message);
        }
    }

    /// <summary>单条预检结果的严重程度；只有 Error 会阻断开发构建。</summary>
    public enum ProjectValidationSeverity { Pass, Warning, Error }

    /// <summary>稳定、可定位且不可变的预检结果。</summary>
    public sealed class ProjectValidationResult
    {
        /// <summary>创建一条预检结果。</summary>
        /// <param name="id">跨版本稳定的检查编号，用于测试和报告定位。</param>
        /// <param name="severity">结果严重程度；Error 阻断开发构建。</param>
        /// <param name="message">面向项目成员的中文结果描述。</param>
        /// <param name="context">可选的场景、资产或对象定位引用；无法定位时为 null。</param>
        public ProjectValidationResult(string id, ProjectValidationSeverity severity, string message,
            UnityEngine.Object context = null)
        {
            Id = id;
            Severity = severity;
            Message = message;
            Context = context;
        }

        /// <summary>跨版本稳定的检查编号。</summary>
        public string Id { get; }
        /// <summary>决定日志展示及是否阻断构建的严重程度。</summary>
        public ProjectValidationSeverity Severity { get; }
        /// <summary>面向项目成员的中文结果描述。</summary>
        public string Message { get; }
        /// <summary>可由编辑器定位的对象或资产；无法定位时为 null。</summary>
        public UnityEngine.Object Context { get; }
    }

    /// <summary>可序列化为纯数据的阵营槽位快照，避免 EditMode 测试依赖场景对象。</summary>
    public readonly struct ValidationTeamSlot
    {
        /// <summary>创建成员槽位快照。</summary>
        /// <param name="name">成员可读名称，仅用于失败信息。</param>
        /// <param name="team">成员阵营；None 会被报告为错误。</param>
        /// <param name="slotIndex">稳定槽位索引，有效范围为 0-5。</param>
        public ValidationTeamSlot(string name, CombatTeam team, int slotIndex)
        {
            Name = name ?? "<未命名>";
            Team = team;
            SlotIndex = slotIndex;
        }

        /// <summary>用于错误报告的成员名称。</summary>
        public string Name { get; }
        /// <summary>成员所属阵营；None 表示无效配置。</summary>
        public CombatTeam Team { get; }
        /// <summary>成员稳定槽位，有效范围为 0-5。</summary>
        public int SlotIndex { get; }
    }
}
