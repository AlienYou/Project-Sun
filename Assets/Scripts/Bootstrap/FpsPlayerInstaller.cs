using ProjectSun.FPS.Abilities;
using ProjectSun.FPS.Core;
using ProjectSun.FPS.Player;
using ProjectSun.FPS.Input;
using ProjectSun.FPS.Presentation;
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
        private PlayerMatchLoadout matchLoadout;

        public FpsPlayerController Player => player;
        public Health Health => health;
        public HitscanWeapon Weapon => weapon;
        public FpsAbilityController Abilities => abilities;
        public PlayerMatchLoadout MatchLoadout => matchLoadout;

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
            if (initialized || player == null || weapon == null || abilities == null || playerCamera == null)
                return;

            if (GetComponent<FpsInput>() == null)
                gameObject.AddComponent<FpsInput>();
            matchLoadout = GetComponent<PlayerMatchLoadout>();
            if (matchLoadout == null)
                matchLoadout = gameObject.AddComponent<PlayerMatchLoadout>();
            player.Configure(playerCamera.transform, playerCamera);
            Transform configuredMuzzle = muzzle;
            Transform weaponVisual = muzzle != null ? muzzle.parent : null;
            LowPolyShooterViewmodel viewmodel = GetComponent<LowPolyShooterViewmodel>();
            if (viewmodel != null && viewmodel.HasViewmodelRig)
            {
                viewmodel.Configure(player, weapon, weaponVisual);
                if (viewmodel.Muzzle != null) configuredMuzzle = viewmodel.Muzzle;
                if (viewmodel.VisualRoot != null) weaponVisual = viewmodel.VisualRoot;
            }
            if (configuredMuzzle == null || weaponVisual == null)
            {
                Debug.LogError("Player has no configured first-person viewmodel or fallback weapon visual.", this);
                return;
            }
            weapon.Configure(playerCamera, configuredMuzzle);
            abilities.Configure(player, weapon);
            ViewmodelCameraRenderer viewmodelRenderer = GetComponent<ViewmodelCameraRenderer>();
            if (viewmodelRenderer == null) viewmodelRenderer = gameObject.AddComponent<ViewmodelCameraRenderer>();
            viewmodelRenderer.Configure(playerCamera, weaponVisual);
            WeaponFeedbackController feedback = GetComponent<WeaponFeedbackController>();
            if (feedback == null) feedback = gameObject.AddComponent<WeaponFeedbackController>();
            feedback.Configure(weapon, player, playerCamera, weaponVisual, configuredMuzzle,
                viewmodel != null ? viewmodel.AimAnchor : null, viewmodel != null ? viewmodel.AdsProfile : null,
                viewmodel != null ? viewmodel.PresentationProfile : null);
            if (GetComponent<PlayerRespawnController>() == null)
                gameObject.AddComponent<PlayerRespawnController>();
            initialized = true;
        }

        private void Awake() => Initialize();
    }
}
