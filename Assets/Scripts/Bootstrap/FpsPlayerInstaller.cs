using ProjectSun.FPS.Abilities;
using ProjectSun.FPS.Core;
using ProjectSun.FPS.Player;
using ProjectSun.FPS.Weapons;
using UnityEngine;

namespace ProjectSun.FPS.Bootstrap
{
    /// <summary>Scene/prefab composition root for the player. Keeps scene references out of gameplay components.</summary>
    [DisallowMultipleComponent]
    public sealed class FpsPlayerInstaller : MonoBehaviour
    {
        [SerializeField] private FpsPlayerController player;
        [SerializeField] private Health health;
        [SerializeField] private HitscanWeapon weapon;
        [SerializeField] private FpsAbilityController abilities;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Transform muzzle;

        private bool initialized;

        public FpsPlayerController Player => player;
        public Health Health => health;
        public HitscanWeapon Weapon => weapon;
        public FpsAbilityController Abilities => abilities;

        public void SetReferences(FpsPlayerController controller, Health playerHealth, HitscanWeapon hitscanWeapon,
            FpsAbilityController abilityController, Camera camera, Transform muzzleTransform)
        {
            player = controller;
            health = playerHealth;
            weapon = hitscanWeapon;
            abilities = abilityController;
            playerCamera = camera;
            muzzle = muzzleTransform;
        }

        public void Initialize()
        {
            if (initialized || player == null || weapon == null || abilities == null || playerCamera == null || muzzle == null)
                return;

            player.Configure(playerCamera.transform);
            weapon.Configure(playerCamera, muzzle);
            abilities.Configure(player, weapon);
            initialized = true;
        }

        private void Awake() => Initialize();
    }
}
