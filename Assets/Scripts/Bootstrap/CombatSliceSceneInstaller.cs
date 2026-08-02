using ProjectSun.FPS.UI;
using ProjectSun.FPS.Core;
using ProjectSun.FPS.Weapons;
using ProjectSun.FPS.Rounds;
using ProjectSun.FPS.AI;
using UnityEngine;

namespace ProjectSun.FPS.Bootstrap
{
    /// <summary>Marks a hand-authored combat scene and connects its presentation systems at runtime.</summary>
    [DisallowMultipleComponent]
    public sealed class CombatSliceSceneInstaller : MonoBehaviour
    {
        [SerializeField] private FpsPlayerInstaller playerInstaller;
        [SerializeField] private FpsHud hud;
        [SerializeField] private WeaponCustomizationUI customization;
        [SerializeField] private WeaponLoadoutCatalog loadoutCatalog;

        public void SetReferences(FpsPlayerInstaller player, FpsHud playerHud, WeaponCustomizationUI loadoutUi,
            WeaponLoadoutCatalog catalog = null)
        {
            playerInstaller = player;
            hud = playerHud;
            customization = loadoutUi;
            loadoutCatalog = catalog;
        }

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
            if (roundManager != null)
                roundManager.ConfigureCombatants(playerInstaller, attackers.ToArray(), defenders.ToArray());
            if (hud != null)
                hud.Configure(playerInstaller.Weapon, playerInstaller.Abilities, playerInstaller.Health, roundManager);
            if (customization != null)
                customization.Configure(playerInstaller.Weapon, playerInstaller.Player, playerInstaller.Abilities, loadoutCatalog,
                    roundManager, playerInstaller.MatchLoadout);
            FpsSettingsMenu settings = GetComponent<FpsSettingsMenu>();
            if (settings == null) settings = gameObject.AddComponent<FpsSettingsMenu>();
            settings.Configure(playerInstaller.Player, playerInstaller.Weapon, playerInstaller.Abilities);
            CombatRayDebugOverlay debugOverlay = GetComponent<CombatRayDebugOverlay>();
            if (debugOverlay == null) debugOverlay = gameObject.AddComponent<CombatRayDebugOverlay>();
            debugOverlay.Configure(playerInstaller.Player, playerInstaller.Health, roundManager, allBots);
        }
    }
}
