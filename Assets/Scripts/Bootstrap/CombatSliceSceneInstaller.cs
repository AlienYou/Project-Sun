using ProjectSun.FPS.UI;
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

        public void SetReferences(FpsPlayerInstaller player, FpsHud playerHud, WeaponCustomizationUI loadoutUi)
        {
            playerInstaller = player;
            hud = playerHud;
            customization = loadoutUi;
        }

        private void Awake()
        {
            if (playerInstaller == null) return;
            playerInstaller.Initialize();
            if (hud != null)
                hud.Configure(playerInstaller.Weapon, playerInstaller.Abilities, playerInstaller.Health);
            if (customization != null)
                customization.Configure(playerInstaller.Weapon, playerInstaller.Player, playerInstaller.Abilities);
        }
    }
}
