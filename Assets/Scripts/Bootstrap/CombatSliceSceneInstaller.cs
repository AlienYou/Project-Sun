using ProjectSun.FPS.UI;
using ProjectSun.FPS.Weapons;
using ProjectSun.FPS.Rounds;
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
            playerInstaller.Initialize();
            if (loadoutCatalog != null)
                playerInstaller.Weapon.SetWeaponDefinition(loadoutCatalog.DefaultWeapon);
            RoundManager roundManager = FindObjectOfType<RoundManager>();
            if (hud != null)
                hud.Configure(playerInstaller.Weapon, playerInstaller.Abilities, playerInstaller.Health, roundManager);
            if (customization != null)
                customization.Configure(playerInstaller.Weapon, playerInstaller.Player, playerInstaller.Abilities, loadoutCatalog);
        }
    }
}
