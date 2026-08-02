using UnityEngine;

namespace ProjectSun.FPS.Weapons
{
    /// <summary>
    /// Owns the reusable first-person presentation baseline for one weapon.
    /// Gameplay simulation remains in <see cref="WeaponDefinition"/> and <see cref="WeaponAttachment"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "WPP_Weapon", menuName = "Project Sun/FPS/Weapon Presentation Profile")]
    public sealed class WeaponPresentationProfile : ScriptableObject
    {
        [Header("Default ADS")]
        [SerializeField] private WeaponAdsProfile defaultAdsProfile;

        [Header("Hip Presentation")]
        [Tooltip("Camera-space baseline for the full first-person rig. Positive Z moves it farther forward from the camera.")]
        [SerializeField] private Vector3 hipCameraSpacePositionOffset = new Vector3(0f, 0f, 0.18f);
        [SerializeField] private Vector3 hipCameraSpaceRotationOffset;

        [Header("Recoil")]
        [SerializeField, Range(0.1f, 2f)] private float viewKickMultiplier = 1f;

        public WeaponAdsProfile DefaultAdsProfile => defaultAdsProfile;

        public void ConfigureDefaults(WeaponAdsProfile adsProfile)
        {
            defaultAdsProfile = adsProfile;
            hipCameraSpacePositionOffset = new Vector3(0f, 0f, 0.18f);
            hipCameraSpaceRotationOffset = Vector3.zero;
            viewKickMultiplier = 1f;
        }

        /// <summary>An optic can replace the base calibration without changing weapon gameplay data.</summary>
        public WeaponAdsProfile ResolveAdsProfile(WeaponLoadout loadout)
        {
            WeaponAttachment optic = loadout != null ? loadout.GetEquipped(AttachmentSlot.Optic) : null;
            return optic != null && optic.AdsProfileOverride != null ? optic.AdsProfileOverride : defaultAdsProfile;
        }

        public Vector3 ResolveHipPositionOffset(WeaponLoadout loadout)
        {
            Vector3 result = hipCameraSpacePositionOffset;
            Vector3 unusedRotation = Vector3.zero;
            ApplyAttachmentPresentation(loadout, ref result, ref unusedRotation, out _);
            return result;
        }

        public Vector3 ResolveHipRotationOffset(WeaponLoadout loadout)
        {
            Vector3 unusedPosition = Vector3.zero;
            Vector3 result = hipCameraSpaceRotationOffset;
            ApplyAttachmentPresentation(loadout, ref unusedPosition, ref result, out _);
            return result;
        }

        public float ResolveViewKickMultiplier(WeaponLoadout loadout)
        {
            Vector3 unusedPosition = Vector3.zero;
            Vector3 unusedRotation = Vector3.zero;
            ApplyAttachmentPresentation(loadout, ref unusedPosition, ref unusedRotation, out float multiplier);
            return Mathf.Max(0.1f, viewKickMultiplier * multiplier);
        }

        private static void ApplyAttachmentPresentation(WeaponLoadout loadout, ref Vector3 positionOffset,
            ref Vector3 rotationOffset, out float recoilMultiplier)
        {
            recoilMultiplier = 1f;
            if (loadout == null) return;
            foreach (WeaponAttachment attachment in loadout.Attachments)
            {
                if (attachment == null) continue;
                positionOffset += attachment.HipCameraSpacePositionOffset;
                rotationOffset += attachment.HipCameraSpaceRotationOffset;
                recoilMultiplier *= attachment.ViewKickMultiplier;
            }
        }
    }
}
