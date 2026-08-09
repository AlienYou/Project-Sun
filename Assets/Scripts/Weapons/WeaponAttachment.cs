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

    /// <summary>定义瞄具画面的渲染类别，不参与武器模型装配、射线命中或伤害判定。</summary>
    public enum OpticSightType { Reflex, Holographic, MagnifiedScope }

    /// <summary>未提供正式准星贴图时使用的程序化后备准星形状。</summary>
    public enum OpticReticleStyle { None, Dot, RingDot, Cross }

    /// <summary>
    /// 保存瞄具在 ADS 期间可复用的纯表现配置。该资产不决定弹道；同一瞄具安装到不同武器族时，
    /// 武器姿态仍由独立的 WeaponAdsProfile 校准。
    /// </summary>
    [CreateAssetMenu(fileName = "OSP_Optic", menuName = "Project Sun/FPS/Optic Sight Profile")]
    public sealed class OpticSightProfile : ScriptableObject
    {
        [Tooltip("瞄具画面的渲染类别；只有 MagnifiedScope 会启用独立倍率相机、镜片合成和 Eyebox。")]
        [SerializeField] private OpticSightType sightType = OpticSightType.Reflex;
        [Tooltip("未提供准星贴图时使用的程序化准星；None 表示不绘制后备准星。")]
        [SerializeField] private OpticReticleStyle fallbackReticleStyle = OpticReticleStyle.Dot;
        [Tooltip("可选的项目自有准星遮罩贴图；为空时使用程序化后备准星，贴图颜色由准星颜色统一着色。")]
        [SerializeField] private Texture2D reticleTexture;
        [Tooltip("准星颜色和透明度；HDR 亮度可用于后续发光表现，Alpha 控制准星合成强度。")]
        [SerializeField] private Color reticleColor = new Color(1f, 0.18f, 0.12f, 0.92f);
        [Tooltip("程序化准星点或线的基准尺寸，单位为像素，有效范围 1～24。")]
        [SerializeField, Range(1f, 24f)] private float reticleSizePixels = 5f;
        [Tooltip("准星外框或贴图占用的基准尺寸，单位为像素，有效范围 8～120。")]
        [SerializeField, Range(8f, 120f)] private float frameSizePixels = 40f;
        [Header("Magnified Lens Rendering")]
        [Tooltip("倍率镜在 ADS 时使用的光学倍率，有效范围 1～12；红点和全息瞄具忽略该值。")]
        [SerializeField, Range(1f, 12f)] private float magnification = 4f;
        [Tooltip("镜片视野直径相对于屏幕短边的比例，有效范围 0.3～0.95；用于计算倍率镜 Render Texture 尺寸。")]
        [SerializeField, Range(0.3f, 0.95f)] private float lensViewportScale = 0.68f;
        [Tooltip("倍率镜启用时镜片外区域的暗化强度，0 表示不暗化，0.9 表示最多保留 10% 亮度。")]
        [SerializeField, Range(0f, 0.9f)] private float outsideLensDim = 0.35f;
        [Tooltip("最高画质下镜片外区域的模糊半径，单位为屏幕像素，有效范围 0～6；低画质会减少采样或禁用模糊。")]
        [SerializeField, Range(0f, 6f)] private float outsideLensBlurPixels = 2f;
        [Tooltip("镜外效果与物理镜片投影边缘的抗锯齿过渡倍数，有效范围 0.5～4；数值越大边缘越柔和。")]
        [SerializeField, Range(0.5f, 4f)] private float peripheralEdgeSoftness = 1.5f;
        [Tooltip("倍率镜离屏渲染分辨率倍率，有效范围 0.25～1；仅在 ADS 期间创建并更新镜片相机。")]
        [SerializeField, Range(0.25f, 1f)] private float lensRenderResolutionScale = 0.7f;
        [Tooltip("物理镜片圆形遮罩的屏幕空间抗锯齿倍数，有效范围 0.5～4；数值越大边缘越柔和。")]
        [SerializeField, Range(0.5f, 4f)] private float lensEdgeSoftness = 1.25f;
        [Tooltip("进入或退出 ADS 时镜片画面渐变时长，单位为秒，有效范围 0.02～0.35。")]
        [SerializeField, Range(0.02f, 0.35f)] private float lensFadeDuration = 0.1f;
        [Tooltip("可选的镜片口径遮罩贴图；为空时使用解析度无关的圆形遮罩，非空时与圆形遮罩相乘。")]
        [SerializeField] private Texture2D lensMaskTexture;
        [Header("Physical Lens Surface")]
        [Tooltip("未配置 LensAnchor 组件时生成镜片表面的后备直径，单位为米，有效范围 0.005～0.15。")]
        [SerializeField, Range(0.005f, 0.15f)] private float lensPhysicalDiameter = 0.045f;
        [Tooltip("运行时镜片从锚点向玩家眼睛移动的距离，单位为米，有效范围 0～0.01；用于避免被导入模型的后镜片遮挡。")]
        [SerializeField, Range(0f, 0.01f)] private float lensTowardCameraOffset = 0.0008f;
        [Header("Eyebox")]
        [Tooltip("是否模拟眼睛离开可用眼盒后产生的出瞳黑边；关闭时仍保留镜外弱化和倍率渲染。")]
        [SerializeField] private bool eyeboxEnabled = true;
        [Tooltip("完整看见出瞳时相机到镜片的理想轴向距离，单位为米，有效范围 0.05～0.6。")]
        [SerializeField, Range(0.05f, 0.6f)] private float idealEyeRelief = 0.09f;
        [Tooltip("理想眼距两侧保持完整出瞳的安全范围，单位为米，有效范围 0.01～0.3。")]
        [SerializeField, Range(0.01f, 0.3f)] private float eyeReliefTolerance = 0.035f;
        [Tooltip("超出安全眼距后达到最大黑边所需的额外距离，单位为米，有效范围 0.01～0.3。")]
        [SerializeField, Range(0.01f, 0.3f)] private float eyeReliefTransition = 0.04f;
        [Tooltip("相机相对光轴不产生黑边的最大夹角，单位为度，有效范围 0.1～15。")]
        [SerializeField, Range(0.1f, 15f)] private float eyeboxAngularTolerance = 2.5f;
        [Tooltip("超出安全夹角后达到最大黑边所需的角度范围，单位为度，有效范围 0.1～20。")]
        [SerializeField, Range(0.1f, 20f)] private float eyeboxAngularTransition = 4f;
        [Tooltip("严重偏轴时可见出瞳在镜片 UV 空间内的最大位移，有效范围 0～0.35。")]
        [SerializeField, Range(0f, 0.35f)] private float eyeboxPupilShift = 0.12f;
        [Tooltip("严重偏轴时可见出瞳半径的最大收缩比例，有效范围 0～0.75。")]
        [SerializeField, Range(0f, 0.75f)] private float eyeboxPupilContraction = 0.28f;
        [Tooltip("出瞳外黑边的最大不透明度，有效范围 0～1；1 表示完全黑色。")]
        [SerializeField, Range(0f, 1f)] private float eyeboxMaxOcclusion = 0.92f;

        public OpticSightType SightType => sightType;
        public OpticReticleStyle FallbackReticleStyle => fallbackReticleStyle;
        public Texture2D ReticleTexture => reticleTexture;
        public Color ReticleColor => reticleColor;
        public float ReticleSizePixels => reticleSizePixels;
        public float FrameSizePixels => frameSizePixels;
        public bool HasReticle => reticleTexture != null || fallbackReticleStyle != OpticReticleStyle.None;
        public bool UsesMagnifiedLensRendering => sightType == OpticSightType.MagnifiedScope && magnification > 1.01f;
        public float Magnification => magnification;
        public float LensViewportScale => lensViewportScale;
        public float OutsideLensDim => outsideLensDim;
        public float OutsideLensBlurPixels => outsideLensBlurPixels;
        public float PeripheralEdgeSoftness => peripheralEdgeSoftness;
        public float LensRenderResolutionScale => lensRenderResolutionScale;
        public float LensEdgeSoftness => lensEdgeSoftness;
        public float LensFadeDuration => lensFadeDuration;
        public Texture2D LensMaskTexture => lensMaskTexture;
        public float LensPhysicalDiameter => lensPhysicalDiameter;
        public float LensTowardCameraOffset => lensTowardCameraOffset;
        public bool EyeboxEnabled => eyeboxEnabled;
        public float IdealEyeRelief => idealEyeRelief;
        public float EyeReliefTolerance => eyeReliefTolerance;
        public float EyeReliefTransition => eyeReliefTransition;
        public float EyeboxAngularTolerance => eyeboxAngularTolerance;
        public float EyeboxAngularTransition => eyeboxAngularTransition;
        public float EyeboxPupilShift => eyeboxPupilShift;
        public float EyeboxPupilContraction => eyeboxPupilContraction;
        public float EyeboxMaxOcclusion => eyeboxMaxOcclusion;

        /// <summary>由项目资源生成工具写入新建 Profile 的默认表现；工具不得用它覆盖已由美术调整的资产。</summary>
        /// <param name="type">瞄具渲染类别；倍率镜会获得倍率、镜外弱化和 Eyebox 默认值。</param>
        /// <param name="style">没有准星贴图时使用的程序化准星形状；None 表示不显示后备准星。</param>
        /// <param name="color">准星颜色及透明度；允许 HDR 颜色。</param>
        /// <param name="sizePixels">准星点或线的基准像素尺寸，写入时限制在 1～24。</param>
        /// <param name="framePixels">准星外框或贴图基准像素尺寸，写入时限制在 8～120。</param>
        public void ConfigureDefaults(OpticSightType type, OpticReticleStyle style, Color color, float sizePixels,
            float framePixels)
        {
            sightType = type;
            fallbackReticleStyle = style;
            reticleColor = color;
            reticleSizePixels = Mathf.Clamp(sizePixels, 1f, 24f);
            frameSizePixels = Mathf.Clamp(framePixels, 8f, 120f);
            magnification = type == OpticSightType.MagnifiedScope ? 4f : 1f;
            lensViewportScale = type == OpticSightType.MagnifiedScope ? 0.68f : 1f;
            outsideLensDim = type == OpticSightType.MagnifiedScope ? 0.35f : 0f;
            outsideLensBlurPixels = type == OpticSightType.MagnifiedScope ? 2f : 0f;
            peripheralEdgeSoftness = 1.5f;
            lensRenderResolutionScale = 0.7f;
            lensEdgeSoftness = 1.25f;
            lensFadeDuration = 0.1f;
            lensPhysicalDiameter = type == OpticSightType.MagnifiedScope ? 0.045f : 0.02f;
            lensTowardCameraOffset = 0.0008f;
            eyeboxEnabled = type == OpticSightType.MagnifiedScope;
            idealEyeRelief = 0.09f;
            eyeReliefTolerance = 0.035f;
            eyeReliefTransition = 0.04f;
            eyeboxAngularTolerance = 2.5f;
            eyeboxAngularTransition = 4f;
            eyeboxPupilShift = 0.12f;
            eyeboxPupilContraction = 0.28f;
            eyeboxMaxOcclusion = 0.92f;
        }
    }
}
