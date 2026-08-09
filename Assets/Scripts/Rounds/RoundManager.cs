using ProjectSun.FPS.AI;
using ProjectSun.FPS.Bootstrap;
using ProjectSun.FPS.Core;
using ProjectSun.FPS.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectSun.FPS.Rounds
{
    public enum RoundState { Preparation, Active, AttackersWin, DefendersWin, Draw, MatchComplete }

    /// <summary>
    /// Project Sun 首个单命团队歼灭模式的离线权威。阵营、名册、比分与回合生命周期不依赖具体目标物，
    /// 以便后续迁移到服务器权威并让爆破模式复用同一套基础设施。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoundManager : MonoBehaviour
    {
        [Header("Match Rules")]
        [SerializeField, Range(1, 6), Tooltip("单方最大名册容量，团队歼灭正式规则固定为 6；较小值仅用于开发测试。")]
        private int maxTeamSize = 6;
        [SerializeField, Min(1), Tooltip("赢得整场比赛所需的回合数，正式规则为先赢 7 回合。")]
        private int roundsToWin = 7;
        [SerializeField, Min(1f), Tooltip("每回合开始前允许调整配装的准备时间，单位为秒。")]
        private float preparationSeconds = 6f;
        [SerializeField, Min(10f), Tooltip("单个战斗回合的最长时间，单位为秒，必须大于等于 10。")]
        private float roundSeconds = 180f;
        [SerializeField, Min(1f), Tooltip("回合结果展示时间，单位为秒；结束后进入下一次准备阶段。")]
        private float resultSeconds = 7f;
        [SerializeField, Tooltip("时间耗尽时是否按双方存活人数判胜；关闭后直接判定平局。")]
        private bool resolveTimeoutByAliveCount = true;

        [Header("Test Controls")]
        [SerializeField, Tooltip("是否允许使用测试快捷键重新加载当前场景；正式构建应关闭。")]
        private bool allowFastRestart = true;
        [SerializeField, Tooltip("开发测试快速重开整场比赛的按键，默认 F8。")]
        private KeyCode fastRestartKey = KeyCode.F8;

        [Header("Combatants")]
        [SerializeField, Tooltip("本地玩家的安装器。当前离线验证将玩家固定分配到进攻方槽位 0。")]
        private FpsPlayerInstaller playerInstaller;
        [SerializeField, Tooltip("进攻方 Bot，数组顺序映射槽位 1-5；空引用会保留对应空槽。")]
        private CombatBotController[] attackers = System.Array.Empty<CombatBotController>();
        [SerializeField, Tooltip("防守方 Bot，数组顺序映射槽位 0-5；空引用会保留对应空槽。")]
        private CombatBotController[] defenders = System.Array.Empty<CombatBotController>();

        [Header("Team Spawns")]
        [SerializeField, Tooltip("进攻方槽位出生点组；未配置时保留角色原有出生位置以兼容旧场景。")]
        private TeamSpawnGroup attackerSpawnGroup;
        [SerializeField, Tooltip("防守方槽位出生点组；未配置时保留角色原有出生位置以兼容旧场景。")]
        private TeamSpawnGroup defenderSpawnGroup;

        private RoundState state;
        private float stateEndsAt;
        private string resultReason = string.Empty;
        private Health playerHealth;
        private PlayerRespawnController playerRespawn;
        private TeamCombatant playerCombatant;
        private TeamRoster attackerRoster;
        private TeamRoster defenderRoster;
        private int attackerRounds;
        private int defenderRounds;

        public RoundState State => state;
        public float TimeRemaining => Mathf.Max(0f, stateEndsAt - Time.time);
        public string ResultReason => resultReason;
        public int MaxTeamSize => maxTeamSize;
        public int RoundsToWin => roundsToWin;
        public int AttackerRounds => attackerRounds;
        public int DefenderRounds => defenderRounds;
        /// <summary>进攻方稳定槽位名册；初始化前可能为 null。</summary>
        public TeamRoster AttackerRoster => attackerRoster;
        /// <summary>防守方稳定槽位名册；初始化前可能为 null。</summary>
        public TeamRoster DefenderRoster => defenderRoster;
        /// <summary>
        /// Loadout changes are a pre-round decision. Keeping this rule on the match authority makes
        /// the later networked version a matter of validating the same state on the server.
        /// </summary>
        public bool CanEditLoadout => state == RoundState.Preparation;
        public int AliveAttackerCount
        {
            get => attackerRoster?.AliveCount ?? 0;
        }
        public int AliveDefenderCount
        {
            get => defenderRoster?.AliveCount ?? 0;
        }

        public string StateLabel => state switch
        {
            RoundState.Preparation => "PREPARE",
            RoundState.Active => "ROUND LIVE",
            RoundState.AttackersWin => "ATTACKERS WIN",
            RoundState.DefendersWin => "DEFENDERS WIN",
            RoundState.Draw => "ROUND DRAW",
            RoundState.MatchComplete => attackerRounds > defenderRounds ? "ATTACKERS WIN THE MATCH" : "DEFENDERS WIN THE MATCH",
            _ => string.Empty
        };

        public string ScoreLabel => $"ATT {attackerRounds}/{roundsToWin}  -  {defenderRounds}/{roundsToWin} DEF";

        public string ObjectiveText
        {
            get
            {
                if (state == RoundState.Preparation)
                    return "ELIMINATE THE ENEMY TEAM // ONE LIFE ONLY";
                if (state == RoundState.Active)
                    return $"ELIMINATE THE ENEMY TEAM  //  ALIVE {AliveAttackerCount} : {AliveDefenderCount}";
                if (state == RoundState.MatchComplete)
                    return $"{StateLabel}  //  PRESS [{fastRestartKey}] TO RESTART TEST MATCH";
                return resultReason;
            }
        }

        /// <summary>旧场景建场器兼容入口；团队歼灭不消费目标区，未来爆破规则集会持有这些数据。</summary>
        /// <param name="objectives">旧攻防目标区数组；当前模式有意忽略，允许为 null。</param>
        public void SetObjectives(ObjectiveZone[] objectives) { }

        /// <summary>配置本地玩家与全部防守方 Bot，保留旧版“玩家对 Bot”调用方式。</summary>
        /// <param name="player">本地玩家安装器，允许为 null 以便诊断不完整场景。</param>
        /// <param name="defenderBots">防守方 Bot；null 按空数组处理。</param>
        public void ConfigureCombatants(FpsPlayerInstaller player, CombatBotController[] defenderBots)
            => ConfigureCombatants(player, System.Array.Empty<CombatBotController>(), defenderBots);

        /// <summary>配置完整离线阵容并按数组顺序建立稳定阵营槽位。</summary>
        /// <param name="player">本地玩家安装器；当前产品规则将其分配到进攻方槽位 0。</param>
        /// <param name="attackerBots">进攻方 Bot，数组索引 0 映射阵营槽位 1。</param>
        /// <param name="defenderBots">防守方 Bot，数组索引直接映射阵营槽位。</param>
        public void ConfigureCombatants(FpsPlayerInstaller player, CombatBotController[] attackerBots,
            CombatBotController[] defenderBots)
        {
            if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
            playerInstaller = player;
            attackers = attackerBots ?? System.Array.Empty<CombatBotController>();
            defenders = defenderBots ?? System.Array.Empty<CombatBotController>();
            playerHealth = playerInstaller != null ? playerInstaller.Health : null;
            playerRespawn = playerInstaller != null ? playerInstaller.GetComponent<PlayerRespawnController>() : null;
            playerCombatant = EnsureCombatant(playerInstaller != null ? playerInstaller.gameObject : null, CombatTeam.Attackers);
            RebuildRosters();
            ValidateTeamCapacity();
            ValidateSpawnCoverage();
            if (playerHealth != null) playerHealth.Died += OnPlayerDied;
        }

        /// <summary>
        /// 从场景安装器提供的出生点组中绑定双方数据。重复阵营只接受第一个有效组，避免运行结果依赖查找顺序。
        /// </summary>
        /// <param name="spawnGroups">场景内一次性收集的出生点组；null 按空数组处理且保留 Inspector 已有引用。</param>
        public void ConfigureSpawnGroups(TeamSpawnGroup[] spawnGroups)
        {
            TeamSpawnGroup[] groups = spawnGroups ?? System.Array.Empty<TeamSpawnGroup>();
            bool foundAttackers = false;
            bool foundDefenders = false;

            foreach (TeamSpawnGroup group in groups)
            {
                if (group == null || group.Team == CombatTeam.None) continue;
                if (group.Team == CombatTeam.Attackers && !foundAttackers)
                {
                    attackerSpawnGroup = group;
                    foundAttackers = true;
                }
                else if (group.Team == CombatTeam.Defenders && !foundDefenders)
                {
                    defenderSpawnGroup = group;
                    foundDefenders = true;
                }
                else
                {
                    Debug.LogWarning($"发现重复的 {group.Team} 出生点组 {group.name}，已保留先发现的配置。", group);
                }
            }

            ValidateSpawnCoverage();
        }

        /// <summary>返回离线 Bot 所属阵营的最近存活敌人。</summary>
        /// <param name="requester">发起查询的 Bot；为空、未分配阵营或没有敌人时返回 null。</param>
        public Transform GetTargetFor(CombatBotController requester)
        {
            if (requester == null) return null;
            TeamCombatant requesterCombatant = requester.GetComponent<TeamCombatant>();
            if (requesterCombatant == null || requesterCombatant.Team == CombatTeam.None) return null;

            CombatTeam opposingTeam = requesterCombatant.Team == CombatTeam.Attackers
                ? CombatTeam.Defenders
                : CombatTeam.Attackers;
            Transform closest = null;
            float closestDistance = float.MaxValue;

            if (opposingTeam == CombatTeam.Attackers)
                ConsiderTargets(attackerRoster, requester.transform.position, ref closest, ref closestDistance);
            else
                ConsiderTargets(defenderRoster, requester.transform.position, ref closest, ref closestDistance);

            return closest;
        }

        private void Start()
        {
            if (playerInstaller == null) playerInstaller = FindObjectOfType<FpsPlayerInstaller>();
            if (attackerRoster == null || defenderRoster == null)
            {
                // 仅在缺少安装器显式配置时执行一次兼容发现；正式场景应由 Bootstrap 提供稳定排序后的数组。
                if (attackers.Length == 0 && defenders.Length == 0)
                    DiscoverSceneBots(out attackers, out defenders);
                ConfigureCombatants(playerInstaller, attackers, defenders);
            }
            RestartMatch();
        }

        private void OnDestroy()
        {
            if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
        }

        private void Update()
        {
            if (allowFastRestart && UnityEngine.Input.GetKeyDown(fastRestartKey))
            {
                ReloadCurrentScene();
                return;
            }

            if (state == RoundState.Active)
            {
                if (AliveAttackerCount == 0)
                {
                    FinishRound(RoundState.DefendersWin, "ATTACKERS ELIMINATED");
                    return;
                }
                if (AliveDefenderCount == 0)
                {
                    FinishRound(RoundState.AttackersWin, "DEFENDERS ELIMINATED");
                    return;
                }
            }

            if (state == RoundState.MatchComplete || Time.time < stateEndsAt) return;
            switch (state)
            {
                case RoundState.Preparation:
                    BeginRound();
                    break;
                case RoundState.Active:
                    ResolveRoundTimeout();
                    break;
                case RoundState.AttackersWin:
                case RoundState.DefendersWin:
                case RoundState.Draw:
                    if (attackerRounds >= roundsToWin || defenderRounds >= roundsToWin)
                        BeginMatchComplete();
                    else
                        BeginPreparation();
                    break;
            }
        }

        /// <summary>在不更换阵容和出生配置的情况下，清空比分并开始一场新的先赢七回合测试比赛。</summary>
        public void RestartMatch()
        {
            attackerRounds = 0;
            defenderRounds = 0;
            BeginPreparation();
        }

        private void BeginPreparation()
        {
            state = RoundState.Preparation;
            stateEndsAt = Time.time + preparationSeconds;
            resultReason = "ELIMINATE THE ENEMY TEAM";
            ResetCombatants();
            SetPlayerGameplayEnabled(false);
            SetPlayerLoadoutEditingEnabled(true);
        }

        private void BeginRound()
        {
            state = RoundState.Active;
            stateEndsAt = Time.time + roundSeconds;
            resultReason = string.Empty;
            if (playerRespawn != null) playerRespawn.SetRoundRespawnsEnabled(false);
            SetPlayerLoadoutEditingEnabled(false);
            SetPlayerGameplayEnabled(true);
            EnableBotsForRound(attackers);
            EnableBotsForRound(defenders);
        }

        private void ResolveRoundTimeout()
        {
            if (!resolveTimeoutByAliveCount)
            {
                FinishRound(RoundState.Draw, "TIME EXPIRED");
                return;
            }

            if (AliveAttackerCount > AliveDefenderCount)
                FinishRound(RoundState.AttackersWin, "TIME EXPIRED // ATTACKERS HAVE MORE SURVIVORS");
            else if (AliveDefenderCount > AliveAttackerCount)
                FinishRound(RoundState.DefendersWin, "TIME EXPIRED // DEFENDERS HAVE MORE SURVIVORS");
            else
                FinishRound(RoundState.Draw, "TIME EXPIRED // EQUAL SURVIVORS");
        }

        private void FinishRound(RoundState result, string reason)
        {
            if (state != RoundState.Active) return;

            state = result;
            stateEndsAt = Time.time + resultSeconds;
            resultReason = reason;
            if (result == RoundState.AttackersWin) attackerRounds++;
            else if (result == RoundState.DefendersWin) defenderRounds++;

            SetPlayerLoadoutEditingEnabled(false);
            SetPlayerGameplayEnabled(false);
            DisableBots(attackers);
            DisableBots(defenders);
        }

        private void BeginMatchComplete()
        {
            state = RoundState.MatchComplete;
            stateEndsAt = 0f;
            resultReason = attackerRounds > defenderRounds ? "ATTACKERS REACHED MATCH POINT" : "DEFENDERS REACHED MATCH POINT";
            SetPlayerLoadoutEditingEnabled(false);
            SetPlayerGameplayEnabled(false);
            DisableBots(attackers);
            DisableBots(defenders);
        }

        private void OnPlayerDied()
        {
            if (state == RoundState.Active && AliveAttackerCount == 0)
                FinishRound(RoundState.DefendersWin, "ATTACKERS ELIMINATED");
        }

        private void ResetCombatants()
        {
            // 出生姿态必须先写入各控制器，再由控制器自己的安全重置流程关闭 CharacterController/NavMeshAgent 后传送。
            ApplySpawnAssignments();

            if (playerInstaller != null && playerInstaller.WeaponInventory != null)
                playerInstaller.WeaponInventory.ResetForRound();
            if (playerInstaller != null && playerInstaller.TacticalEquipment != null)
                playerInstaller.TacticalEquipment.ResetForRound();
            if (playerRespawn != null)
            {
                playerRespawn.SetRoundRespawnsEnabled(true);
                playerRespawn.ResetForRound();
            }
            else
            {
                playerHealth?.ResetHealth();
            }

            ResetBotsForRound(attackers);
            ResetBotsForRound(defenders);
        }

        private void SetPlayerGameplayEnabled(bool enabled)
        {
            if (playerInstaller == null) return;
            if (playerInstaller.Player != null) playerInstaller.Player.SetGameplayInputEnabled(enabled);
            if (playerInstaller.Weapon != null) playerInstaller.Weapon.SetGameplayInputEnabled(enabled);
            if (playerInstaller.Abilities != null) playerInstaller.Abilities.SetGameplayInputEnabled(enabled);
            if (playerInstaller.TacticalEquipment != null) playerInstaller.TacticalEquipment.SetGameplayInputEnabled(enabled);
        }

        private void SetPlayerLoadoutEditingEnabled(bool enabled)
        {
            if (playerInstaller == null) return;
            if (playerInstaller.Weapon != null) playerInstaller.Weapon.SetLoadoutEditingEnabled(enabled);
            if (playerInstaller.MatchLoadout != null) playerInstaller.MatchLoadout.SetEditingEnabled(enabled);
        }

        private static void ReloadCurrentScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(activeScene.name))
                SceneManager.LoadScene(activeScene.name);
        }

        private static TeamCombatant EnsureCombatant(GameObject combatantObject, CombatTeam team)
        {
            if (combatantObject == null) return null;
            TeamCombatant combatant = combatantObject.GetComponent<TeamCombatant>();
            if (combatant == null) combatant = combatantObject.AddComponent<TeamCombatant>();
            combatant.SetTeam(team);
            return combatant;
        }

        private void RebuildRosters()
        {
            attackerRoster = new TeamRoster(CombatTeam.Attackers, maxTeamSize);
            defenderRoster = new TeamRoster(CombatTeam.Defenders, maxTeamSize);

            AssignRosterSlot(attackerRoster, playerCombatant, 0);
            for (int index = 0; index < attackers.Length; index++)
            {
                CombatBotController bot = attackers[index];
                TeamCombatant combatant = EnsureCombatant(bot != null ? bot.gameObject : null, CombatTeam.Attackers);
                AssignRosterSlot(attackerRoster, combatant, index + 1);
            }

            for (int index = 0; index < defenders.Length; index++)
            {
                CombatBotController bot = defenders[index];
                TeamCombatant combatant = EnsureCombatant(bot != null ? bot.gameObject : null, CombatTeam.Defenders);
                AssignRosterSlot(defenderRoster, combatant, index);
            }
        }

        private void AssignRosterSlot(TeamRoster roster, TeamCombatant combatant, int slotIndex)
        {
            if (roster == null || combatant == null) return;
            combatant.AssignTeamSlot(roster.Team, slotIndex);
            if (!roster.TryAssign(combatant, slotIndex, out string failureReason))
            {
                combatant.AssignTeamSlot(roster.Team, -1);
                Debug.LogWarning($"无法注册阵营成员 {combatant.name}：{failureReason}", combatant);
            }
        }

        private void ValidateTeamCapacity()
        {
            int attackerCount = CountAssignedBots(attackers) + (playerInstaller != null ? 1 : 0);
            int defenderCount = CountAssignedBots(defenders);
            if (attackerCount > maxTeamSize)
                Debug.LogWarning($"进攻方配置了 {attackerCount} 名成员，超过模式上限 {maxTeamSize}。超出的成员不会进入权威名册。", this);
            if (defenderCount > maxTeamSize)
                Debug.LogWarning($"防守方配置了 {defenderCount} 名成员，超过模式上限 {maxTeamSize}。超出的成员不会进入权威名册。", this);
        }

        private void ValidateSpawnCoverage()
        {
            ValidateSpawnCoverage(attackerRoster, attackerSpawnGroup);
            ValidateSpawnCoverage(defenderRoster, defenderSpawnGroup);
        }

        private void ValidateSpawnCoverage(TeamRoster roster, TeamSpawnGroup spawnGroup)
        {
            // 未配置出生点组是旧场景的显式兼容路径，不输出噪声；一旦配置，就要求覆盖名册中的每个实际成员。
            if (roster == null || spawnGroup == null) return;
            if (spawnGroup.Team != roster.Team)
            {
                Debug.LogError($"出生点组 {spawnGroup.name} 属于 {spawnGroup.Team}，不能绑定到 {roster.Team} 名册。", spawnGroup);
                return;
            }

            for (int slotIndex = 0; slotIndex < roster.Capacity; slotIndex++)
            {
                if (!roster.TryGetMember(slotIndex, out TeamCombatant member)) continue;
                if (spawnGroup.TryGetSpawnPose(slotIndex, out _)) continue;
                Debug.LogWarning($"{roster.Team} 槽位 {slotIndex}（{member.name}）缺少出生锚点，将沿用角色原有出生位置。", spawnGroup);
            }
        }

        private void ApplySpawnAssignments()
        {
            if (playerCombatant != null && playerRespawn != null && TryGetSpawnPose(playerCombatant, out Pose playerPose))
                playerRespawn.SetRoundSpawn(playerPose);

            ApplyBotSpawnAssignments(attackers);
            ApplyBotSpawnAssignments(defenders);
        }

        private void ApplyBotSpawnAssignments(CombatBotController[] bots)
        {
            foreach (CombatBotController bot in bots)
            {
                if (bot == null) continue;
                TeamCombatant combatant = bot.GetComponent<TeamCombatant>();
                if (combatant != null && TryGetSpawnPose(combatant, out Pose spawnPose))
                    bot.SetRoundSpawn(spawnPose);
            }
        }

        private bool TryGetSpawnPose(TeamCombatant combatant, out Pose spawnPose)
        {
            spawnPose = default;
            TeamSpawnGroup spawnGroup = combatant.Team == CombatTeam.Attackers
                ? attackerSpawnGroup
                : combatant.Team == CombatTeam.Defenders
                    ? defenderSpawnGroup
                    : null;
            return spawnGroup != null && spawnGroup.TryGetSpawnPose(combatant.TeamSlot, out spawnPose);
        }

        private static void DiscoverSceneBots(out CombatBotController[] attackerBots, out CombatBotController[] defenderBots)
        {
            CombatBotController[] allBots = FindObjectsOfType<CombatBotController>();
            System.Collections.Generic.List<CombatBotController> discoveredAttackers =
                new System.Collections.Generic.List<CombatBotController>(allBots.Length);
            System.Collections.Generic.List<CombatBotController> discoveredDefenders =
                new System.Collections.Generic.List<CombatBotController>(allBots.Length);

            foreach (CombatBotController bot in allBots)
            {
                if (bot == null) continue;
                TeamCombatant combatant = bot.GetComponent<TeamCombatant>();
                if (combatant != null && combatant.Team == CombatTeam.Attackers)
                    discoveredAttackers.Add(bot);
                else
                    discoveredDefenders.Add(bot);
            }

            // 场景查找顺序没有契约，兼容发现必须按名称稳定排序，避免每次启动映射到不同出生槽位。
            discoveredAttackers.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            discoveredDefenders.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            attackerBots = discoveredAttackers.ToArray();
            defenderBots = discoveredDefenders.ToArray();
        }

        private static int CountAssignedBots(CombatBotController[] bots)
        {
            int count = 0;
            foreach (CombatBotController bot in bots)
                if (bot != null) count++;
            return count;
        }

        private static void ConsiderTarget(TeamCombatant candidate, Vector3 origin, ref Transform closest, ref float closestDistance)
        {
            if (candidate == null || !candidate.IsAlive) return;
            float distance = (candidate.transform.position - origin).sqrMagnitude;
            if (distance >= closestDistance) return;
            closest = candidate.transform;
            closestDistance = distance;
        }

        private static void ConsiderTargets(TeamRoster roster, Vector3 origin, ref Transform closest,
            ref float closestDistance)
        {
            if (roster == null) return;
            for (int slotIndex = 0; slotIndex < roster.Capacity; slotIndex++)
            {
                if (!roster.TryGetMember(slotIndex, out TeamCombatant candidate)) continue;
                ConsiderTarget(candidate, origin, ref closest, ref closestDistance);
            }
        }

        private static void EnableBotsForRound(CombatBotController[] bots)
        {
            foreach (CombatBotController bot in bots)
            {
                if (bot == null) continue;
                TeamCombatant combatant = bot.GetComponent<TeamCombatant>();
                if (combatant == null || combatant.TeamSlot < 0)
                {
                    bot.SetCombatEnabled(false);
                    continue;
                }
                bot.SetRoundRespawnsEnabled(false);
                bot.SetCombatEnabled(true);
            }
        }

        private static void DisableBots(CombatBotController[] bots)
        {
            foreach (CombatBotController bot in bots)
                if (bot != null) bot.SetCombatEnabled(false);
        }

        private static void ResetBotsForRound(CombatBotController[] bots)
        {
            foreach (CombatBotController bot in bots)
            {
                if (bot == null) continue;
                bot.SetCombatEnabled(false);
                bot.SetRoundRespawnsEnabled(true);
                bot.ResetForRound();
            }
        }
    }
}
