using UnityEngine;
using ProjectSun.FPS.Weapons;

namespace ProjectSun.FPS.Presentation
{
    /// <summary>References and animation operations for the generated visual-only first-person rig.</summary>
    [DisallowMultipleComponent]
    public sealed class LowPolyShooterViewmodelRig : MonoBehaviour
    {
        [SerializeField] private Animator armsAnimator;
        [SerializeField] private Animator weaponAnimator;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Transform aimAnchor;
        [SerializeField] private Transform magazine;
        [SerializeField] private WeaponPresentationProfile presentationProfile;
        // Kept for existing generated viewmodels and the ADS workbench. New rigs receive both references.
        [SerializeField] private WeaponAdsProfile adsProfile;

        private int actionsLayer = -1;
        private int overlayLayer = -1;

        public Transform Muzzle => muzzle;
        public Transform AimAnchor => aimAnchor;
        public WeaponPresentationProfile PresentationProfile => presentationProfile;
        public WeaponAdsProfile AdsProfile => adsProfile;

        public void ConfigureReferences(Animator arms, Animator weapon, Transform muzzleTransform, Transform aimAnchorTransform,
            Transform magazineTransform, WeaponAdsProfile weaponAdsProfile, WeaponPresentationProfile weaponPresentationProfile = null)
        {
            armsAnimator = arms;
            weaponAnimator = weapon;
            muzzle = muzzleTransform;
            aimAnchor = aimAnchorTransform;
            magazine = magazineTransform;
            adsProfile = weaponAdsProfile;
            presentationProfile = weaponPresentationProfile;
            CacheAnimatorLayers();
        }

#if UNITY_EDITOR
        /// <summary>Editor workbench hook. Never called by the runtime weapon pipeline.</summary>
        public void PreviewAimingPose(float deltaTime)
        {
            if (armsAnimator != null && armsAnimator.runtimeAnimatorController != null)
            {
                armsAnimator.SetFloat("Movement", 0f);
                armsAnimator.SetFloat("Aiming", 1f);
                armsAnimator.SetBool("Aim", true);
                armsAnimator.SetBool("Running", false);
                armsAnimator.Update(Mathf.Max(0.001f, deltaTime));
            }
            if (weaponAnimator != null && weaponAnimator.runtimeAnimatorController != null)
                weaponAnimator.Update(Mathf.Max(0.001f, deltaTime));
        }

        /// <summary>Returns the rig to its authored edit-mode pose after an ADS preview.</summary>
        public void ResetPreviewPose()
        {
            if (armsAnimator != null && armsAnimator.runtimeAnimatorController != null)
            {
                armsAnimator.Rebind();
                armsAnimator.Update(0f);
            }
            if (weaponAnimator != null && weaponAnimator.runtimeAnimatorController != null)
            {
                weaponAnimator.Rebind();
                weaponAnimator.Update(0f);
            }
        }
#endif

        public void SetLocomotion(float movement, bool aiming, bool running)
        {
            if (armsAnimator == null) return;
            armsAnimator.SetFloat("Movement", Mathf.Clamp01(movement), 0.1f, Time.deltaTime);
            armsAnimator.SetFloat("Aiming", aiming ? 1f : 0f, 0.1f, Time.deltaTime);
            armsAnimator.SetBool("Aim", aiming);
            armsAnimator.SetBool("Running", running && !aiming);
        }

        public void PlayFire()
        {
            if (armsAnimator != null)
            {
                CacheAnimatorLayers();
                armsAnimator.CrossFade("Fire", 0.04f, overlayLayer, 0f);
            }
            if (weaponAnimator != null) weaponAnimator.Play("Fire", 0, 0f);
        }

        public void PlayReload()
        {
            if (armsAnimator != null)
            {
                CacheAnimatorLayers();
                armsAnimator.Play("Reload", actionsLayer, 0f);
            }
            if (weaponAnimator != null) weaponAnimator.Play("Reload", 0, 0f);
        }

        public void SetMagazineActive(int active)
        {
            if (magazine != null) magazine.gameObject.SetActive(active != 0);
        }

        private void Awake() => CacheAnimatorLayers();

        private void CacheAnimatorLayers()
        {
            if (armsAnimator == null) return;
            actionsLayer = Mathf.Max(0, armsAnimator.GetLayerIndex("Layer Actions"));
            overlayLayer = Mathf.Max(0, armsAnimator.GetLayerIndex("Layer Overlay"));
        }
    }
}
