using ProjectSun.FPS.Player;
using ProjectSun.FPS.Weapons;
using UnityEngine;

namespace ProjectSun.FPS.Presentation
{
    /// <summary>
    /// Project-owned bridge between the gameplay weapon and a cleaned Low Poly Shooter Pack first-person rig.
    /// The rig is visual-only: it never owns input, ammunition, damage, or camera control.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LowPolyShooterViewmodel : MonoBehaviour
    {
        [SerializeField] private LowPolyShooterViewmodelRig rig;
        private HitscanWeapon weapon;
        private FpsPlayerController player;
        private bool wasReloading;

        public bool HasViewmodelRig => rig != null;
        public LowPolyShooterViewmodelRig Rig => rig;
        public Transform Muzzle => rig != null ? rig.Muzzle : null;
        public Transform AimAnchor => rig != null ? rig.AimAnchor : null;
        public Transform VisualRoot => rig != null ? rig.transform : null;
        public WeaponPresentationProfile PresentationProfile => rig != null ? rig.PresentationProfile : null;
        public WeaponAdsProfile AdsProfile => rig != null ? rig.AdsProfile : null;

        public void SetViewmodelRig(LowPolyShooterViewmodelRig viewmodelRig) => rig = viewmodelRig;

        public void Configure(FpsPlayerController controller, HitscanWeapon hitscanWeapon, Transform fallbackVisual)
        {
            if (weapon != null) weapon.Fired -= PlayFire;
            player = controller;
            weapon = hitscanWeapon;
            if (rig == null || weapon == null) return;

            fallbackVisual?.gameObject.SetActive(false);
            weapon.Fired += PlayFire;
        }

        private void LateUpdate()
        {
            if (rig == null || weapon == null || player == null) return;

            rig.SetLocomotion(player.MoveInput.magnitude, weapon.IsAiming, player.IsSprinting);
            bool reloading = weapon.IsReloading;
            if (reloading && !wasReloading) rig.PlayReload();
            wasReloading = reloading;
        }

        private void OnDestroy()
        {
            if (weapon != null) weapon.Fired -= PlayFire;
        }

        private void PlayFire()
        {
            if (rig != null) rig.PlayFire();
        }
    }

}
