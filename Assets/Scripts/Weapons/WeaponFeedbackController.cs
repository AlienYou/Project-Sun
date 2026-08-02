using ProjectSun.FPS.Input;
using ProjectSun.FPS.Player;
using ProjectSun.FPS.Core;
using UnityEngine;

namespace ProjectSun.FPS.Weapons
{
    /// <summary>
    /// Client-side weapon presentation: camera kick, first-person weapon recoil, ADS transition and a reusable muzzle light.
    /// It intentionally consumes weapon events only; authoritative weapon simulation remains in HitscanWeapon.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponFeedbackController : MonoBehaviour
    {
        [Header("View Kick")]
        [SerializeField, Min(0f)] private float verticalKick = 0.55f;
        [SerializeField, Min(0f)] private float horizontalKick = 0.18f;
        [Header("Viewmodel")]
        [SerializeField, Min(1f)] private float returnSpeed = 18f;
        [SerializeField, Min(1f)] private float aimTransitionSpeed = 14f;
        [SerializeField, Range(1f, 30f)] private float adsFovReduction = 12f;
        [SerializeField, Min(0.01f)] private float aimAnchorDistance = 0.18f;
        [Header("Viewmodel Safety")]
        [Tooltip("Maximum camera-space translation added on top of the weapon's authored fire animation.")]
        [SerializeField, Range(0.001f, 0.025f)] private float maxRecoilTravel = 0.006f;
        [SerializeField, Range(0.001f, 0.025f)] private float recoilTravelPerKick = 0.006f;
        [SerializeField, Range(0.1f, 5f)] private float maxRecoilPitch = 1.2f;
        [SerializeField, Range(0.1f, 5f)] private float recoilPitchPerKick = 0.9f;
        [SerializeField, Range(0.1f, 1.5f)] private float obstructionProbeDistance = 0.75f;
        [SerializeField, Range(0.02f, 0.25f)] private float obstructionProbeRadius = 0.1f;
        [SerializeField, Range(0f, 0.45f)] private float obstructionLowering = 0.2f;
        [SerializeField, Range(0f, 30f)] private float obstructionRoll = 14f;
        [SerializeField, Min(1f)] private float obstructionTransitionSpeed = 16f;
        [Header("Prototype Fallback")]
        [SerializeField] private Vector3 adsPositionOffset = new Vector3(-0.11f, 0.055f, -0.17f);
        [SerializeField] private Vector3 adsRotationOffset = new Vector3(-2f, 0f, 0f);

        private HitscanWeapon weapon;
        private FpsPlayerController player;
        private FpsInput input;
        private Camera viewCamera;
        private Transform weaponVisual;
        private Transform muzzle;
        private Transform aimAnchor;
        private WeaponAdsProfile adsProfile;
        private WeaponPresentationProfile presentationProfile;
        private Vector3 hipPosition;
        private Quaternion hipRotation;
        private float visualKick;
        private float aimAmount;
        private float obstructionAmount;
        private Light muzzleLight;
        private float muzzleLightUntil;

        public void Configure(HitscanWeapon hitscanWeapon, FpsPlayerController controller, Camera camera,
            Transform visual, Transform muzzleTransform, Transform weaponAimAnchor = null, WeaponAdsProfile weaponAdsProfile = null,
            WeaponPresentationProfile weaponPresentationProfile = null)
        {
            if (weapon != null) weapon.Fired -= OnWeaponFired;
            weapon = hitscanWeapon;
            player = controller;
            input = player != null ? player.Input : null;
            viewCamera = camera;
            weaponVisual = visual;
            muzzle = muzzleTransform;
            aimAnchor = weaponAimAnchor;
            adsProfile = weaponAdsProfile;
            presentationProfile = weaponPresentationProfile;
            if (weapon != null) weapon.Fired += OnWeaponFired;

            if (weaponVisual != null)
            {
                hipPosition = weaponVisual.localPosition;
                hipRotation = weaponVisual.localRotation;
            }
            // Configure can be called again after a respawn. A new life must never inherit an ADS
            // interpolation or an ADS FOV from the previous configuration.
            aimAmount = 0f;
            visualKick = 0f;
            obstructionAmount = 0f;
            if (viewCamera != null)
                viewCamera.fieldOfView = input != null ? input.FieldOfView : 78f;
            EnsureMuzzleLight();
        }

        private void OnDestroy()
        {
            if (weapon != null) weapon.Fired -= OnWeaponFired;
        }

        private void LateUpdate()
        {
            if (weapon == null || weaponVisual == null || viewCamera == null) return;
            float delta = Time.deltaTime;
            WeaponAdsProfile activeAdsProfile = ResolveAdsProfile();
            float targetAim = weapon.IsAiming ? 1f : 0f;
            float activeAimTransitionSpeed = activeAdsProfile != null ? activeAdsProfile.TransitionSpeed : aimTransitionSpeed;
            aimAmount = Mathf.MoveTowards(aimAmount, targetAim, activeAimTransitionSpeed * delta);
            visualKick = Mathf.MoveTowards(visualKick, 0f, returnSpeed * delta);
            float targetObstruction = GetObstructionAmount();
            obstructionAmount = Mathf.MoveTowards(obstructionAmount, targetObstruction, obstructionTransitionSpeed * delta);

            Vector3 presentedHipPosition = hipPosition + (presentationProfile != null
                ? presentationProfile.ResolveHipPositionOffset(weapon.Loadout)
                : adsProfile != null ? adsProfile.HipCameraSpacePositionOffset
                : Vector3.zero);
            Quaternion presentedHipRotation = hipRotation * Quaternion.Euler(presentationProfile != null
                ? presentationProfile.ResolveHipRotationOffset(weapon.Loadout)
                : adsProfile != null ? adsProfile.HipCameraSpaceRotationOffset
                : Vector3.zero);
            Quaternion aimRotation = hipRotation * Quaternion.Euler(adsRotationOffset);
            Vector3 aimPosition = hipPosition + adsPositionOffset;
            if (TryGetCalibratedSightPose(activeAdsProfile, out Vector3 calibratedPosition, out Quaternion calibratedRotation))
            {
                aimPosition = calibratedPosition;
                aimRotation = calibratedRotation;
            }
            else if (TryGetAimAnchorPosition(hipRotation, out Vector3 anchoredPosition))
            {
                aimPosition = anchoredPosition;
                aimRotation = hipRotation;
            }
            // Imported first-person animation already contains the primary mechanical recoil.
            // Keep this root-level layer subtle and bounded so it enhances rather than doubles that motion.
            float presentationKickMultiplier = ResolveViewKickMultiplier();
            float recoilTravel = Mathf.Min(maxRecoilTravel, visualKick * recoilTravelPerKick * presentationKickMultiplier);
            float recoilPitch = Mathf.Min(maxRecoilPitch, visualKick * recoilPitchPerKick * presentationKickMultiplier);
            Vector3 obstructionOffset = Vector3.down * obstructionLowering * obstructionAmount;
            Vector3 targetPosition = Vector3.Lerp(presentedHipPosition, aimPosition, aimAmount) + obstructionOffset + Vector3.back * recoilTravel;
            Quaternion targetRotation = Quaternion.Slerp(presentedHipRotation, aimRotation, aimAmount) *
                Quaternion.Euler(Vector3.left * recoilPitch + Vector3.forward * obstructionRoll * obstructionAmount);
            float smoothing = 1f - Mathf.Exp(-returnSpeed * delta);
            weaponVisual.localPosition = Vector3.Lerp(weaponVisual.localPosition, targetPosition, smoothing);
            weaponVisual.localRotation = Quaternion.Slerp(weaponVisual.localRotation, targetRotation, smoothing);

            float baseFov = input != null ? input.FieldOfView : 78f;
            float activeFovReduction = activeAdsProfile != null ? activeAdsProfile.FovReduction : adsFovReduction;
            float targetFov = baseFov - activeFovReduction * aimAmount;
            viewCamera.fieldOfView = Mathf.Lerp(viewCamera.fieldOfView, targetFov,
                1f - Mathf.Exp(-activeAimTransitionSpeed * delta));
            if (muzzleLight != null && muzzleLight.enabled && Time.time >= muzzleLightUntil)
                muzzleLight.enabled = false;
        }

        private void OnWeaponFired()
        {
            float presentationKickMultiplier = ResolveViewKickMultiplier();
            if (player != null)
                player.AddViewKick(verticalKick * presentationKickMultiplier,
                    Random.Range(-horizontalKick, horizontalKick) * presentationKickMultiplier);
            visualKick = Mathf.Min(1.5f, visualKick + 1f);
            if (muzzleLight != null)
            {
                muzzleLight.enabled = true;
                muzzleLightUntil = Time.time + 0.045f;
            }
        }

        private bool TryGetAimAnchorPosition(Quaternion desiredLocalRotation, out Vector3 localPosition)
        {
            localPosition = default;
            if (aimAnchor == null || weaponVisual == null || weaponVisual.parent == null || viewCamera == null) return false;

            Vector3 anchorRelativePosition = weaponVisual.InverseTransformPoint(aimAnchor.position);
            Quaternion desiredWorldRotation = weaponVisual.parent.rotation * desiredLocalRotation;
            Vector3 desiredAnchorPosition = viewCamera.transform.position + viewCamera.transform.forward * aimAnchorDistance;
            Vector3 desiredWorldPosition = desiredAnchorPosition - desiredWorldRotation * anchorRelativePosition;
            localPosition = weaponVisual.parent.InverseTransformPoint(desiredWorldPosition);
            return true;
        }

        private WeaponAdsProfile ResolveAdsProfile()
        {
            return presentationProfile != null ? presentationProfile.ResolveAdsProfile(weapon != null ? weapon.Loadout : null) : adsProfile;
        }

        private float ResolveViewKickMultiplier()
        {
            return presentationProfile != null ? presentationProfile.ResolveViewKickMultiplier(weapon != null ? weapon.Loadout : null) : 1f;
        }

        private bool TryGetCalibratedSightPose(WeaponAdsProfile activeAdsProfile, out Vector3 localPosition, out Quaternion localRotation)
        {
            return WeaponAdsAlignment.TryGetCalibratedPose(weaponVisual, aimAnchor, muzzle, viewCamera, activeAdsProfile,
                out localPosition, out localRotation);
        }

        private float GetObstructionAmount()
        {
            if (viewCamera == null || obstructionProbeDistance <= 0f || obstructionProbeRadius <= 0f) return 0f;
            if (!Physics.SphereCast(viewCamera.transform.position, obstructionProbeRadius, viewCamera.transform.forward,
                    out RaycastHit hit, obstructionProbeDistance, CombatLayers.WallMask, QueryTriggerInteraction.Ignore))
                return 0f;
            return 1f - Mathf.Clamp01(hit.distance / obstructionProbeDistance);
        }

        private void EnsureMuzzleLight()
        {
            if (muzzle == null || muzzleLight != null) return;
            GameObject lightObject = new GameObject("Muzzle Flash Light", typeof(Light));
            lightObject.transform.SetParent(muzzle, false);
            Light created = lightObject.GetComponent<Light>();
            created.type = LightType.Point;
            created.color = new Color(1f, 0.58f, 0.16f);
            created.intensity = 3.5f;
            created.range = 3f;
            created.enabled = false;
            muzzleLight = created;
        }
    }
}
