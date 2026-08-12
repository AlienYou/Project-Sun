using ProjectSun.FPS.UI;
using ProjectSun.FPS.Core;
using ProjectSun.FPS.Weapons;
using ProjectSun.FPS.Rounds;
using ProjectSun.FPS.AI;
using ProjectSun.FPS.Player;
using UnityEngine;

namespace ProjectSun.FPS.Bootstrap
{
    /// <summary>Marks a hand-authored combat scene and connects its presentation systems at runtime.</summary>
    [DisallowMultipleComponent]
    public sealed class CombatSliceSceneInstaller : MonoBehaviour
    {
        [SerializeField, Tooltip("CombatSlice 中的本地玩家安装器；负责暴露玩家玩法组件引用。")]
        private FpsPlayerInstaller playerInstaller;
        [SerializeField, Tooltip("显示玩家生命、弹药、比分和阵营存活人数的 HUD。")]
        private FpsHud hud;
        [SerializeField, Tooltip("准备阶段配装界面；RoundManager 会决定其是否允许编辑。")]
        private WeaponCustomizationUI customization;
        [SerializeField, Tooltip("该场景允许选择的武器与附件目录；为空时沿用玩家预制体默认配置。")]
        private WeaponLoadoutCatalog loadoutCatalog;

        /// <summary>写入 CombatSlice 的主要场景引用，供建场与修复工具使用。</summary>
        /// <param name="player">本地玩家安装器，不能为空。</param>
        /// <param name="playerHud">玩家 HUD；允许为空以运行无界面规则测试。</param>
        /// <param name="loadoutUi">准备阶段配装界面；允许为空。</param>
        /// <param name="catalog">武器配装目录；为空时使用玩家当前配置。</param>
        public void SetReferences(FpsPlayerInstaller player, FpsHud playerHud, WeaponCustomizationUI loadoutUi,
            WeaponLoadoutCatalog catalog = null)
        {
            playerInstaller = player;
            hud = playerHud;
            customization = loadoutUi;
            loadoutCatalog = catalog;
        }

        /// <summary>替换场景使用的配装目录。</summary>
        /// <param name="catalog">新的武器配装目录；null 表示不覆盖玩家当前武器定义。</param>
        public void SetLoadoutCatalog(WeaponLoadoutCatalog catalog) => loadoutCatalog = catalog;

        private void Awake()
        {
            if (playerInstaller == null) return;
            CombatLayers.ApplyCombatSliceLayers(transform.parent);
            playerInstaller.Initialize();
            if (loadoutCatalog != null)
                playerInstaller.Weapon.SetWeaponDefinition(loadoutCatalog.DefaultWeapon);
            if (playerInstaller.MatchLoadout != null)
                playerInstaller.MatchLoadout.Configure(playerInstaller.Weapon, loadoutCatalog);
            if (playerInstaller.WeaponInventory != null)
                playerInstaller.WeaponInventory.Configure(playerInstaller);
            if (playerInstaller.TacticalEquipment != null)
                playerInstaller.TacticalEquipment.ResetForRound();
            RoundManager roundManager = FindObjectOfType<RoundManager>();
            CombatCoverPoint[] coverPoints = FindObjectsOfType<CombatCoverPoint>();
            CombatBotController[] allBots = FindObjectsOfType<CombatBotController>();
            System.Collections.Generic.List<CombatBotController> attackers = new System.Collections.Generic.List<CombatBotController>();
            System.Collections.Generic.List<CombatBotController> defenders = new System.Collections.Generic.List<CombatBotController>();
            foreach (CombatBotController bot in allBots)
            {
                bot.SetCoverPoints(coverPoints);
                TeamCombatant combatant = bot.GetComponent<TeamCombatant>();
                if (combatant != null && combatant.Team == CombatTeam.Attackers)
                    attackers.Add(bot);
                else
                    defenders.Add(bot);
            }
            // FindObjectsOfType 不保证返回顺序；名称排序让数组顺序稳定映射到队伍槽位与出生点。
            attackers.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            defenders.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            PlayerSpectatorController spectatorController = null;
            if (roundManager != null)
            {
                TeamSpawnGroup[] spawnGroups = FindObjectsOfType<TeamSpawnGroup>();
                System.Array.Sort(spawnGroups, (left, right) => string.CompareOrdinal(left.name, right.name));
                roundManager.ConfigureSpawnGroups(spawnGroups);
                roundManager.ConfigureCombatants(playerInstaller, attackers.ToArray(), defenders.ToArray());
                spectatorController = GetComponent<PlayerSpectatorController>();
                if (spectatorController == null) spectatorController = gameObject.AddComponent<PlayerSpectatorController>();
                spectatorController.Configure(roundManager, playerInstaller.PlayerCamera, playerInstaller.Player.Input);
            }
            if (hud != null)
                hud.Configure(playerInstaller.Weapon, playerInstaller.Abilities, playerInstaller.Health, roundManager,
                    playerInstaller.TacticalEquipment, spectatorController);
            if (customization != null)
                customization.Configure(playerInstaller.Weapon, playerInstaller.Player, playerInstaller.Abilities, loadoutCatalog,
                    roundManager, playerInstaller.MatchLoadout);
            FpsSettingsMenu settings = GetComponent<FpsSettingsMenu>();
            if (settings == null) settings = gameObject.AddComponent<FpsSettingsMenu>();
            settings.Configure(playerInstaller.Player, playerInstaller.Weapon, playerInstaller.Abilities, roundManager);
            CombatRayDebugOverlay debugOverlay = GetComponent<CombatRayDebugOverlay>();
            if (debugOverlay == null) debugOverlay = gameObject.AddComponent<CombatRayDebugOverlay>();
            debugOverlay.Configure(playerInstaller.Player, playerInstaller.Health, roundManager, allBots);
        }
    }
}
