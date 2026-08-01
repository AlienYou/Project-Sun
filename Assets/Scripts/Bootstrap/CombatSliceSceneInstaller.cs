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
            RoundManager roundManager = FindObjectOfType<RoundManager>();
            if (roundManager != null)
                roundManager.ConfigureCombatants(playerInstaller, FindObjectsOfType<CombatBotController>());
            if (hud != null)
                hud.Configure(playerInstaller.Weapon, playerInstaller.Abilities, playerInstaller.Health, roundManager);
            if (customization != null)
                customization.Configure(playerInstaller.Weapon, playerInstaller.Player, playerInstaller.Abilities, loadoutCatalog);
            FpsSettingsMenu settings = GetComponent<FpsSettingsMenu>();
            if (settings == null) settings = gameObject.AddComponent<FpsSettingsMenu>();
            settings.Configure(playerInstaller.Player, playerInstaller.Weapon, playerInstaller.Abilities);
            CombatRayDebugOverlay debugOverlay = GetComponent<CombatRayDebugOverlay>();
            if (debugOverlay == null) debugOverlay = gameObject.AddComponent<CombatRayDebugOverlay>();
            debugOverlay.Configure(playerInstaller.Player);
        }
    }
}
