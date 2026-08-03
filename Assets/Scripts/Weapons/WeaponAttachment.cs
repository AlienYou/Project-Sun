using System.Collections.Generic;
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

        [Header("Compatibility")]
        [Tooltip("Leave empty to allow every weapon exposed by the loadout catalog. Populate this list for weapon-family-specific attachments.")]
        [SerializeField] private List<WeaponDefinition> compatibleWeapons = new List<WeaponDefinition>();

        public WeaponAdsProfile AdsProfileOverride => adsProfileOverride;
        public Vector3 HipCameraSpacePositionOffset => hipCameraSpacePositionOffset;
        public Vector3 HipCameraSpaceRotationOffset => hipCameraSpaceRotationOffset;
        public float ViewKickMultiplier => viewKickMultiplier;

        public bool IsCompatibleWith(WeaponDefinition weapon)
        {
            if (weapon == null) return false;
            return compatibleWeapons == null || compatibleWeapons.Count == 0 || compatibleWeapons.Contains(weapon);
        }

        /// <summary>Content-authoring helper used by the owned catalog generator.</summary>
        public void SetCompatibleWeapons(params WeaponDefinition[] weapons)
        {
            if (compatibleWeapons == null) compatibleWeapons = new List<WeaponDefinition>();
            compatibleWeapons.Clear();
            if (weapons == null) return;
            foreach (WeaponDefinition weapon in weapons)
                if (weapon != null && !compatibleWeapons.Contains(weapon)) compatibleWeapons.Add(weapon);
        }
    }
}
