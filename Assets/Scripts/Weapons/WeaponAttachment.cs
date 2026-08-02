using UnityEngine;

namespace ProjectSun.FPS.Weapons
{
    [CreateAssetMenu(menuName = "Project Sun/FPS/Weapon Attachment", fileName = "WeaponAttachment")]
    public sealed class WeaponAttachment : ScriptableObject
    {
        public AttachmentSlot slot;
        public string displayName;
        [Tooltip("Multipliers are applied after all flat values.")]
        public float damageMultiplier = 1f;
        public float fireRateMultiplier = 1f;
        public float magazineMultiplier = 1f;
        public float reloadMultiplier = 1f;
        public float hipSpreadMultiplier = 1f;
        public float aimSpreadMultiplier = 1f;
        public float rangeMultiplier = 1f;

        [Header("First-Person Presentation")]
        [Tooltip("Optional ADS calibration supplied by an optic. Leave empty for attachments that do not provide a sight picture.")]
        [SerializeField] private WeaponAdsProfile adsProfileOverride;
        [Tooltip("Additive camera-space offset applied to the weapon family's hip-fire pose.")]
        [SerializeField] private Vector3 hipCameraSpacePositionOffset;
        [SerializeField] private Vector3 hipCameraSpaceRotationOffset;
        [SerializeField, Range(0.1f, 2f)] private float viewKickMultiplier = 1f;

        public WeaponAdsProfile AdsProfileOverride => adsProfileOverride;
        public Vector3 HipCameraSpacePositionOffset => hipCameraSpacePositionOffset;
        public Vector3 HipCameraSpaceRotationOffset => hipCameraSpaceRotationOffset;
        public float ViewKickMultiplier => viewKickMultiplier;
    }
}
