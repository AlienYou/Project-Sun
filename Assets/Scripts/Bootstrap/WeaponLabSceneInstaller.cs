using ProjectSun.FPS.Core;
using ProjectSun.FPS.UI;
using ProjectSun.FPS.Weapons;
using ProjectSun.FPS.World;
using UnityEngine;

namespace ProjectSun.FPS.Bootstrap
{
    /// <summary>Composition root for the standalone WeaponLab. It intentionally has no round manager or bots.</summary>
    [DisallowMultipleComponent]
    public sealed class WeaponLabSceneInstaller : MonoBehaviour
    {
        [SerializeField] private FpsPlayerInstaller playerInstaller;
        [SerializeField] private FpsHud hud;
        [SerializeField] private WeaponCustomizationUI customization;
        [SerializeField] private WeaponLabController labController;
        [SerializeField] private WeaponLabTelemetryHud telemetry;
        [SerializeField] private TargetDummy[] targets = System.Array.Empty<TargetDummy>();
        [SerializeField] private WeaponLoadoutCatalog loadoutCatalog;

        public void SetReferences(FpsPlayerInstaller player, FpsHud playerHud, WeaponCustomizationUI loadoutUi,
            WeaponLabController controller, WeaponLabTelemetryHud telemetryHud, TargetDummy[] trainingTargets,
            WeaponLoadoutCatalog catalog)
        {
            playerInstaller = player;
            hud = playerHud;
            customization = loadoutUi;
            labController = controller;
            telemetry = telemetryHud;
            targets = trainingTargets ?? System.Array.Empty<TargetDummy>();
            loadoutCatalog = catalog;
        }

        private void Awake()
        {
            if (playerInstaller == null) return;
            CombatLayers.ApplyCombatSliceLayers(transform);
            playerInstaller.Initialize();
            if (loadoutCatalog != null)
                playerInstaller.Weapon.SetWeaponDefinition(loadoutCatalog.DefaultPrimaryWeapon);
            if (playerInstaller.MatchLoadout != null)
                playerInstaller.MatchLoadout.Configure(playerInstaller.Weapon, loadoutCatalog);
            if (playerInstaller.WeaponInventory != null)
                playerInstaller.WeaponInventory.Configure(playerInstaller);

            if (hud != null)
                hud.Configure(playerInstaller.Weapon, playerInstaller.Abilities, playerInstaller.Health);
            if (customization != null)
                customization.Configure(playerInstaller.Weapon, playerInstaller.Player, playerInstaller.Abilities, loadoutCatalog,
                    null, playerInstaller.MatchLoadout);
            if (labController != null)
                labController.Configure(playerInstaller, targets);
            if (telemetry != null)
                telemetry.Configure(playerInstaller, labController);

            FpsSettingsMenu settings = GetComponent<FpsSettingsMenu>();
            if (settings == null) settings = gameObject.AddComponent<FpsSettingsMenu>();
            settings.Configure(playerInstaller.Player, playerInstaller.Weapon, playerInstaller.Abilities);
        }
    }
}
