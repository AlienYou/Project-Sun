using System.Collections.Generic;
using UnityEngine;

namespace ProjectSun.FPS.Weapons
{
    /// <summary>One weapon-family-specific first-person visual for an attachment.</summary>
    [System.Serializable]
    public sealed class WeaponAttachmentViewmodelVisual
    {
        [SerializeField] private WeaponDefinition weapon;
        [SerializeField] private GameObject prefab;
        [SerializeField] private string mountName = "SOCKET_Scope";
        [SerializeField] private string replacedBuiltInVisualName;
        [SerializeField] private string aimAnchorName;

        public WeaponDefinition Weapon => weapon;
        public GameObject Prefab => prefab;
        public string MountName => mountName;
        public string ReplacedBuiltInVisualName => replacedBuiltInVisualName;
        public string AimAnchorName => aimAnchorName;

        public bool Matches(WeaponDefinition definition) => weapon == definition && prefab != null;

        public void Configure(WeaponDefinition definition, GameObject visualPrefab, string visualMountName,
            string builtInVisualName, string visualAimAnchorName)
        {
            weapon = definition;
            prefab = visualPrefab;
            mountName = visualMountName ?? string.Empty;
            replacedBuiltInVisualName = builtInVisualName ?? string.Empty;
            aimAnchorName = visualAimAnchorName ?? string.Empty;
        }
    }

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
        [Tooltip("Optional reticle and sight-picture data shown while this optic is aimed.")]
        [SerializeField] private OpticSightProfile opticSightProfile;
        [Tooltip("Additive camera-space offset applied to the weapon family's hip-fire pose.")]
        [SerializeField] private Vector3 hipCameraSpacePositionOffset;
        [SerializeField] private Vector3 hipCameraSpaceRotationOffset;
        [SerializeField, Range(0.1f, 2f)] private float viewKickMultiplier = 1f;

        [Header("First-Person Visuals")]
        [Tooltip("Per-weapon viewmodel bindings. A binding owns the visual prefab, mount and optional ADS aim anchor for one weapon family.")]
        [SerializeField] private List<WeaponAttachmentViewmodelVisual> viewmodelVisuals = new List<WeaponAttachmentViewmodelVisual>();

        [Header("Compatibility")]
        [Tooltip("Leave empty to allow every weapon exposed by the loadout catalog. Populate this list for weapon-family-specific attachments.")]
        [SerializeField] private List<WeaponDefinition> compatibleWeapons = new List<WeaponDefinition>();

        public WeaponAdsProfile AdsProfileOverride => adsProfileOverride;
        public OpticSightProfile OpticSightProfile => opticSightProfile;
        public Vector3 HipCameraSpacePositionOffset => hipCameraSpacePositionOffset;
        public Vector3 HipCameraSpaceRotationOffset => hipCameraSpaceRotationOffset;
        public float ViewKickMultiplier => viewKickMultiplier;

        public bool TryGetViewmodelVisual(WeaponDefinition weapon, out WeaponAttachmentViewmodelVisual visual)
        {
            if (viewmodelVisuals != null)
                foreach (WeaponAttachmentViewmodelVisual candidate in viewmodelVisuals)
                    if (candidate != null && candidate.Matches(weapon))
                    {
                        visual = candidate;
                        return true;
                    }
            visual = null;
            return false;
        }

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

        /// <summary>Content-authoring helper used by the owned attachment presentation setup command.</summary>
        public void SetViewmodelVisual(WeaponDefinition weapon, GameObject visualPrefab, string mountName,
            string replacedBuiltInVisualName, string aimAnchorName)
        {
            if (weapon == null || visualPrefab == null) return;
            if (viewmodelVisuals == null) viewmodelVisuals = new List<WeaponAttachmentViewmodelVisual>();
            WeaponAttachmentViewmodelVisual existing = null;
            foreach (WeaponAttachmentViewmodelVisual candidate in viewmodelVisuals)
                if (candidate != null && candidate.Weapon == weapon)
                {
                    existing = candidate;
                    break;
                }
            if (existing == null)
            {
                existing = new WeaponAttachmentViewmodelVisual();
                viewmodelVisuals.Add(existing);
            }
            existing.Configure(weapon, visualPrefab, mountName, replacedBuiltInVisualName, aimAnchorName);
        }

        /// <summary>Assigns the optic-specific ADS calibration without coupling it to weapon gameplay stats.</summary>
        public void SetAdsProfileOverride(WeaponAdsProfile profile) => adsProfileOverride = profile;

        /// <summary>Assigns the optic's reticle presentation without affecting weapon aim or ballistics.</summary>
        public void SetOpticSightProfile(OpticSightProfile profile) => opticSightProfile = profile;
    }

    /// <summary>Classifies the gameplay-facing sight picture without coupling it to a weapon model or hit simulation.</summary>
    public enum OpticSightType { Reflex, Holographic, MagnifiedScope }

    /// <summary>Fallback reticle shapes used until a final authored reticle texture is assigned.</summary>
    public enum OpticReticleStyle { None, Dot, RingDot, Cross }

    /// <summary>
    /// Reusable visual data for an optic while ADS is active. Magnification remains owned by the optic's ADS profile
    /// so the same sight picture can be calibrated independently on each weapon family.
    /// </summary>
    [CreateAssetMenu(fileName = "OSP_Optic", menuName = "Project Sun/FPS/Optic Sight Profile")]
    public sealed class OpticSightProfile : ScriptableObject
    {
        [SerializeField] private OpticSightType sightType = OpticSightType.Reflex;
        [SerializeField] private OpticReticleStyle fallbackReticleStyle = OpticReticleStyle.Dot;
        [SerializeField] private Texture2D reticleTexture;
        [SerializeField] private Color reticleColor = new Color(1f, 0.18f, 0.12f, 0.92f);
        [SerializeField, Range(1f, 24f)] private float reticleSizePixels = 5f;
        [SerializeField, Range(8f, 120f)] private float frameSizePixels = 40f;

        public OpticSightType SightType => sightType;
        public OpticReticleStyle FallbackReticleStyle => fallbackReticleStyle;
        public Texture2D ReticleTexture => reticleTexture;
        public Color ReticleColor => reticleColor;
        public float ReticleSizePixels => reticleSizePixels;
        public float FrameSizePixels => frameSizePixels;
        public bool HasReticle => reticleTexture != null || fallbackReticleStyle != OpticReticleStyle.None;

        /// <summary>Used by owned setup tools; existing author-tuned assets are never overwritten by those tools.</summary>
        public void ConfigureDefaults(OpticSightType type, OpticReticleStyle style, Color color, float sizePixels,
            float framePixels)
        {
            sightType = type;
            fallbackReticleStyle = style;
            reticleColor = color;
            reticleSizePixels = Mathf.Clamp(sizePixels, 1f, 24f);
            frameSizePixels = Mathf.Clamp(framePixels, 8f, 120f);
        }
    }
}
