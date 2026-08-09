using ProjectSun.FPS.Core;
using ProjectSun.FPS.Weapons;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectSun.FPS.Presentation
{
    /// <summary>
    /// 把只包含世界层的倍率画面渲染到已装备瞄具的物理镜片口径中。
    /// 该组件只负责第一人称表现；命中和伤害始终使用主相机中心射线与枪口路径验证。
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class ScopeSightRenderer : MonoBehaviour
    {
        private const string ScopeCameraName = "Magnified Scope Camera";
        private const string LensSurfaceName = "Runtime Scope Lens Surface";
        private const string LensShaderResourceName = "ProjectSunScopeLens";
        private const string LensShaderName = "Project Sun/Scope Lens Composite";
        private const string LensMaskPassName = "ScopeApertureMask";
        private const int MinimumTextureSize = 192;
        private const int MaximumTextureSize = 1024;

        private static readonly int MainTextureId = Shader.PropertyToID("_MainTex");
        private static readonly int MaskTextureId = Shader.PropertyToID("_MaskTex");
        private static readonly int ReticleTextureId = Shader.PropertyToID("_ReticleTex");
        private static readonly int ReticleColorId = Shader.PropertyToID("_ReticleColor");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");
        private static readonly int UseMaskTextureId = Shader.PropertyToID("_UseMaskTex");
        private static readonly int UseReticleTextureId = Shader.PropertyToID("_UseReticleTex");
        private static readonly int ReticleStyleId = Shader.PropertyToID("_ReticleStyle");
        private static readonly int ReticleDotRadiusId = Shader.PropertyToID("_ReticleDotRadius");
        private static readonly int ReticleHalfFrameId = Shader.PropertyToID("_ReticleHalfFrame");
        private static readonly int ReticleHalfThicknessId = Shader.PropertyToID("_ReticleHalfThickness");
        private static readonly int ReticleGapId = Shader.PropertyToID("_ReticleGap");
        private static readonly int EyeboxOffsetId = Shader.PropertyToID("_EyeboxOffset");
        private static readonly int EyeboxSeverityId = Shader.PropertyToID("_EyeboxSeverity");
        private static readonly int EyeboxMaxOcclusionId = Shader.PropertyToID("_EyeboxMaxOcclusion");
        private static readonly int EyeboxContractionId = Shader.PropertyToID("_EyeboxContraction");

        private Camera worldCamera;
        private Camera scopeCamera;
        private UniversalAdditionalCameraData scopeCameraData;
        private RenderTexture scopeTexture;
        private GameObject lensSurface;
        private Mesh lensMesh;
        private Material lensMaterial;
        private MeshRenderer lensRenderer;
        private OpticSightProfile activeProfile;
        private Transform activeAimAnchor;
        private ViewmodelScopeLens activeScopeLens;
        private bool requestedActive;
        private float lensOpacity;
        private float eyeReliefMetres;
        private float eyeboxAngleDegrees;
        private float eyeboxSeverity;
        private Vector2 eyeboxOffset;
        private int lastRenderedFrame = -1;

        public bool IsActive => lensOpacity > 0.001f && scopeTexture != null && lensSurface != null &&
            lensSurface.activeSelf;
        public bool RendersIntegratedReticle => IsActive && activeProfile != null && activeProfile.HasReticle;
        public float LensOpacity => lensOpacity;
        public bool UsesIntegratedLensShader => lensMaterial != null && lensMaterial.shader != null &&
            lensMaterial.shader.name == LensShaderName;
        /// <summary>当前主相机到镜片的轴向眼距，单位为毫米；仅用于表现诊断。</summary>
        public float EyeReliefMillimetres => eyeReliefMetres * 1000f;
        /// <summary>当前主相机相对镜片光轴的夹角，单位为度。</summary>
        public float EyeboxAngleDegrees => eyeboxAngleDegrees;
        /// <summary>眼距和偏轴共同计算出的 Eyebox 黑边强度，有效范围 0～1。</summary>
        public float EyeboxSeverity => eyeboxSeverity;
        /// <summary>镜外 Renderer Feature 的运行状态或当前画质采样档位。</summary>
        public string PeripheralDiagnosticStatus
        {
            get
            {
                if (!ScopePeripheralEffectState.IsOwnedBy(this)) return "INACTIVE";
                if (ScopePeripheralEffectState.LastRenderedFrame < Time.frameCount - 2)
                    return "FEATURE NOT RENDERING";
                return ScopePeripheralEffectState.QualityLabel;
            }
        }
        public Texture ScopeTexture => scopeTexture;
        public float ScopeFieldOfView => scopeCamera != null ? scopeCamera.fieldOfView : 0f;
        public string ActiveAnchorName => activeScopeLens != null
            ? activeScopeLens.name
            : activeAimAnchor != null ? activeAimAnchor.name : "NONE";
        public string DiagnosticStatus
        {
            get
            {
                if (!requestedActive && lensOpacity <= 0.001f) return "INACTIVE";
                if (worldCamera == null) return "NO GAMEPLAY CAMERA";
                if (activeAimAnchor == null) return "NO AIM ANCHOR";
                if (scopeCamera == null) return "NO SCOPE CAMERA";
                if (scopeTexture == null || !scopeTexture.IsCreated()) return "NO RENDER TEXTURE";
                if (lensSurface == null || lensRenderer == null) return "NO LENS SURFACE";
                if (lensMaterial == null) return "NO LENS MATERIAL";
                if (!IsTextureBound()) return "TEXTURE NOT BOUND";
                if (lastRenderedFrame < Time.frameCount - 2) return "SCOPE CAMERA NOT RENDERING";
                if (lensOpacity < 0.999f) return requestedActive ? "FADING IN" : "FADING OUT";
                return "READY";
            }
        }

        /// <summary>绑定决定倍率视角和弹道方向的主玩法相机；相机改变时会释放并重建镜片资源。</summary>
        /// <param name="gameplayCamera">本地玩家主相机；允许为空，空值会停用倍率镜渲染。</param>
        public void Configure(Camera gameplayCamera)
        {
            if (worldCamera == gameplayCamera) return;
            ReleaseScopeCamera();
            worldCamera = gameplayCamera;
        }

        private void OnEnable()
        {
            // 镜外椭圆必须在 Viewmodel Camera 使用最终动画姿态进行剔除和渲染前更新。
            // 先取消再订阅可防止编辑器脚本重载或异常启停造成重复回调。
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            // 组件停用后不再有回调刷新帧状态，必须立即停用表面并释放倍率相机和 RT，
            // 避免换场景、切换玩家或禁用对象后继续占用显存。
            DeactivateLensSurface();
            ReleaseScopeCamera();
        }

        /// <summary>在目标 Viewmodel Camera 开始渲染前，按最终动画姿态刷新镜片表面和镜外投影。</summary>
        /// <param name="context">当前 SRP 渲染上下文；本回调不直接提交命令，因此仅用于生命周期签名。</param>
        /// <param name="renderingCamera">即将渲染的相机；只有活动 Viewmodel Overlay Camera 会触发更新。</param>
        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
        {
            if (lensOpacity <= 0.001f || renderingCamera == null ||
                renderingCamera != ResolvePresentationCamera()) return;
            UpdateLensSurface();
            UpdateOpticalEffects();
        }

        /// <summary>
        /// 更新当前瞄具和 ADS 状态。调用方必须传入运行时装配器生成的活动锚点，不能使用静态资源节点；
        /// 不兼容、未瞄准或引用缺失时会渐隐或立即释放对应资源。
        /// </summary>
        /// <param name="profile">当前瞄具视图配置；为空时立即停用镜片渲染。</param>
        /// <param name="isAiming">玩家是否处于 ADS 输入状态；false 时按 Profile 时长渐隐。</param>
        /// <param name="aimAnchor">运行时附件的 ADS 光轴锚点；没有独立 LensAnchor 时作为兼容后备。</param>
        /// <param name="scopeLens">运行时附件的物理镜片组件；倍率镜正式资源应提供该引用。</param>
        public void SetSight(OpticSightProfile profile, bool isAiming, Transform aimAnchor,
            ViewmodelScopeLens scopeLens)
        {
            if (activeProfile != profile)
            {
                activeProfile = profile;
                ReleaseScopeTexture();
                lensOpacity = 0f;
                eyeboxSeverity = 0f;
                eyeboxOffset = Vector2.zero;
                ScopePeripheralEffectState.Clear(this);
            }
            activeAimAnchor = aimAnchor;
            activeScopeLens = scopeLens;
            Transform lensAnchor = ResolveLensAnchor();
            bool configurationValid = profile != null && profile.UsesMagnifiedLensRendering &&
                worldCamera != null && lensAnchor != null;
            requestedActive = configurationValid && isAiming;

            if (!configurationValid)
            {
                lensOpacity = 0f;
                ScopePeripheralEffectState.Clear(this);
                DeactivateLensSurface();
                ReleaseScopeCamera();
                return;
            }

            if (!requestedActive && lensOpacity <= 0.001f) return;

            EnsureScopeCamera();
            EnsureScopeTexture();
            EnsureLensSurface();
            UpdateLensSurface();
        }

        private void LateUpdate()
        {
            if (activeProfile == null || ResolveLensAnchor() == null || worldCamera == null) return;
            float fadeDuration = Mathf.Max(0.02f, activeProfile.LensFadeDuration);
            lensOpacity = Mathf.MoveTowards(lensOpacity, requestedActive ? 1f : 0f,
                Time.deltaTime / fadeDuration);
            if (!requestedActive && lensOpacity <= 0.001f)
            {
                lensOpacity = 0f;
                DeactivateLensSurface();
                ReleaseScopeCamera();
                return;
            }

            EnsureScopeCamera();
            EnsureScopeTexture();
            EnsureLensSurface();
            if (scopeCamera == null || scopeTexture == null || lensSurface == null) return;

            UpdateLensSurface();
            UpdateScopeCamera();
            scopeCamera.Render();
            lastRenderedFrame = Time.frameCount;
        }

        private void OnDestroy()
        {
            ReleaseScopeCamera();
            DestroyLensSurface();
        }

        private void EnsureScopeCamera()
        {
            if (scopeCamera != null || worldCamera == null) return;

            GameObject cameraObject = new GameObject(ScopeCameraName, typeof(Camera), typeof(UniversalAdditionalCameraData));
            cameraObject.transform.SetParent(worldCamera.transform, false);
            scopeCamera = cameraObject.GetComponent<Camera>();
            scopeCameraData = cameraObject.GetComponent<UniversalAdditionalCameraData>();
            scopeCamera.CopyFrom(worldCamera);
            scopeCamera.enabled = false;
            scopeCamera.depth = worldCamera.depth - 10f;
            scopeCamera.rect = new Rect(0f, 0f, 1f, 1f);
            scopeCameraData.renderType = CameraRenderType.Base;
            scopeCameraData.renderPostProcessing = false;
        }

        private void UpdateScopeCamera()
        {
            // 从玩家眼睛朝当前物理镜片中心建立取景方向，使 ADS 过渡期间镜内中心射线
            // 始终穿过正在移动的镜片；镜片稳定在屏幕中心后会自然收敛到主相机方向。
            scopeCamera.transform.SetPositionAndRotation(worldCamera.transform.position, ResolveScopeViewRotation());
            // 倍率相机不得渲染第一人称层，否则镜片会再次采样自身 Render Texture，
            // 形成逐帧放大的递归反馈和放射状残影。
            scopeCamera.cullingMask = worldCamera.cullingMask & ~(1 << CombatLayers.ViewmodelLayer);
            scopeCamera.clearFlags = worldCamera.clearFlags;
            scopeCamera.backgroundColor = worldCamera.backgroundColor;
            scopeCamera.nearClipPlane = worldCamera.nearClipPlane;
            scopeCamera.farClipPlane = worldCamera.farClipPlane;
            scopeCamera.allowHDR = worldCamera.allowHDR;
            scopeCamera.allowMSAA = false;
            scopeCamera.aspect = 1f;
            scopeCamera.fieldOfView = CalculateMagnifiedFieldOfView(worldCamera.fieldOfView, activeProfile.Magnification);
            scopeCamera.targetTexture = scopeTexture;
            scopeCamera.enabled = false;
        }

        private void EnsureScopeTexture()
        {
            if (activeProfile == null) return;
            int shorterScreenSide = Mathf.Max(1, Mathf.Min(Screen.width, Screen.height));
            int requestedSize = Mathf.Clamp(Mathf.RoundToInt(shorterScreenSide * activeProfile.LensViewportScale *
                activeProfile.LensRenderResolutionScale), MinimumTextureSize, MaximumTextureSize);
            if (scopeTexture != null && scopeTexture.width == requestedSize && scopeTexture.height == requestedSize) return;

            ReleaseScopeTexture();
            scopeTexture = new RenderTexture(requestedSize, requestedSize, 16, RenderTextureFormat.ARGB32)
            {
                name = "Project Sun Scope Lens RT",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false
            };
            scopeTexture.Create();
            ApplyLensMaterialProperties();
        }

        private void ReleaseScopeTexture()
        {
            if (scopeCamera != null) scopeCamera.targetTexture = null;
            if (scopeTexture == null) return;
            scopeTexture.Release();
            Destroy(scopeTexture);
            scopeTexture = null;
        }

        private void ReleaseScopeCamera()
        {
            requestedActive = false;
            lensOpacity = 0f;
            ScopePeripheralEffectState.Clear(this);
            ReleaseScopeTexture();
            if (scopeCamera != null) Destroy(scopeCamera.gameObject);
            scopeCamera = null;
            scopeCameraData = null;
        }

        private void EnsureLensSurface()
        {
            Transform lensAnchor = ResolveLensAnchor();
            if (lensAnchor == null) return;
            if (lensSurface != null && lensSurface.transform.parent == lensAnchor) return;
            DestroyLensSurface();

            lensSurface = new GameObject(LensSurfaceName, typeof(MeshFilter), typeof(MeshRenderer));
            lensSurface.transform.SetParent(lensAnchor, false);
            lensSurface.layer = lensAnchor.gameObject.layer;
            lensMesh = CreateLensQuadMesh();
            MeshFilter meshFilter = lensSurface.GetComponent<MeshFilter>();
            meshFilter.sharedMesh = lensMesh;
            lensRenderer = lensSurface.GetComponent<MeshRenderer>();
            lensRenderer.shadowCastingMode = ShadowCastingMode.Off;
            lensRenderer.receiveShadows = false;
            lensRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            lensMaterial = CreateLensMaterial();
            lensRenderer.sharedMaterial = lensMaterial;
            ApplyLensMaterialProperties();
        }

        private void UpdateLensSurface()
        {
            Transform lensAnchor = ResolveLensAnchor();
            if (lensSurface == null || activeProfile == null || lensAnchor == null || worldCamera == null) return;
            // 已迁移镜片约定局部 +Z 指向目标、局部 +Y 指向瞄具上方，形成完整光学坐标系。
            // 旧资源在 Workbench 写入方向前继续朝向相机，避免升级组件后镜片突然侧转。
            Vector3 towardCamera = worldCamera.transform.position - lensAnchor.position;
            if (towardCamera.sqrMagnitude < 0.000001f) towardCamera = -worldCamera.transform.forward;
            towardCamera.Normalize();
            Quaternion lensRotation = activeScopeLens != null && activeScopeLens.OrientationAuthored
                ? lensAnchor.rotation
                : worldCamera.transform.rotation;
            lensSurface.transform.SetPositionAndRotation(
                lensAnchor.position + towardCamera * activeProfile.LensTowardCameraOffset,
                lensRotation);
            float apertureDiameter = activeScopeLens != null
                ? activeScopeLens.ClearApertureDiameter
                : activeProfile.LensPhysicalDiameter;
            lensSurface.transform.localScale = new Vector3(apertureDiameter, apertureDiameter, 1f);
            if (!lensSurface.activeSelf) lensSurface.SetActive(true);
            ApplyLensMaterialProperties();
        }

        private void ApplyLensMaterialProperties()
        {
            if (lensMaterial == null || activeProfile == null) return;
            if (lensMaterial.HasProperty(MainTextureId)) lensMaterial.SetTexture(MainTextureId, scopeTexture);
            if (lensMaterial.HasProperty(MaskTextureId))
                lensMaterial.SetTexture(MaskTextureId, activeProfile.LensMaskTexture != null
                    ? activeProfile.LensMaskTexture
                    : Texture2D.whiteTexture);
            if (lensMaterial.HasProperty(ReticleTextureId))
                lensMaterial.SetTexture(ReticleTextureId, activeProfile.ReticleTexture != null
                    ? activeProfile.ReticleTexture
                    : Texture2D.whiteTexture);
            if (lensMaterial.HasProperty(ReticleColorId))
                lensMaterial.SetColor(ReticleColorId, activeProfile.ReticleColor);
            if (lensMaterial.HasProperty(OpacityId)) lensMaterial.SetFloat(OpacityId, lensOpacity);
            if (lensMaterial.HasProperty(EdgeSoftnessId))
                lensMaterial.SetFloat(EdgeSoftnessId, activeProfile.LensEdgeSoftness);
            if (lensMaterial.HasProperty(UseMaskTextureId))
                lensMaterial.SetFloat(UseMaskTextureId, activeProfile.LensMaskTexture != null ? 1f : 0f);
            if (lensMaterial.HasProperty(UseReticleTextureId))
                lensMaterial.SetFloat(UseReticleTextureId, activeProfile.ReticleTexture != null ? 1f : 0f);
            if (lensMaterial.HasProperty(ReticleStyleId))
                lensMaterial.SetFloat(ReticleStyleId, activeProfile.HasReticle
                    ? (float)activeProfile.FallbackReticleStyle
                    : (float)OpticReticleStyle.None);

            float renderSize = Mathf.Max(1f, scopeTexture != null ? scopeTexture.width : MinimumTextureSize);
            float dotRadius = activeProfile.ReticleSizePixels * 0.5f / renderSize;
            float halfFrame = activeProfile.FrameSizePixels * 0.5f / renderSize;
            float halfThickness = Mathf.Max(1f, activeProfile.ReticleSizePixels * 0.32f) * 0.5f / renderSize;
            float gap = Mathf.Max(2f, activeProfile.ReticleSizePixels * 0.75f) / renderSize;
            if (lensMaterial.HasProperty(ReticleDotRadiusId)) lensMaterial.SetFloat(ReticleDotRadiusId, dotRadius);
            if (lensMaterial.HasProperty(ReticleHalfFrameId)) lensMaterial.SetFloat(ReticleHalfFrameId, halfFrame);
            if (lensMaterial.HasProperty(ReticleHalfThicknessId))
                lensMaterial.SetFloat(ReticleHalfThicknessId, halfThickness);
            if (lensMaterial.HasProperty(ReticleGapId)) lensMaterial.SetFloat(ReticleGapId, gap);
            if (lensMaterial.HasProperty(EyeboxOffsetId)) lensMaterial.SetVector(EyeboxOffsetId, eyeboxOffset);
            if (lensMaterial.HasProperty(EyeboxSeverityId))
                lensMaterial.SetFloat(EyeboxSeverityId, activeProfile.EyeboxEnabled ? eyeboxSeverity : 0f);
            if (lensMaterial.HasProperty(EyeboxMaxOcclusionId))
                lensMaterial.SetFloat(EyeboxMaxOcclusionId, activeProfile.EyeboxMaxOcclusion);
            if (lensMaterial.HasProperty(EyeboxContractionId))
                lensMaterial.SetFloat(EyeboxContractionId, activeProfile.EyeboxPupilContraction);
            lensMaterial.mainTexture = scopeTexture;
        }

        private void UpdateOpticalEffects()
        {
            if (activeProfile == null || lensSurface == null || lensRenderer == null ||
                lensMaterial == null || worldCamera == null)
            {
                ScopePeripheralEffectState.Clear(this);
                return;
            }

            Camera presentationCamera = ResolvePresentationCamera();
            if (presentationCamera == null)
            {
                ScopePeripheralEffectState.Clear(this);
                return;
            }

            int maskPassIndex = lensMaterial.FindPass(LensMaskPassName);
            if (maskPassIndex < 0)
            {
                ScopePeripheralEffectState.Clear(this);
                return;
            }

            UpdateEyebox(lensSurface.transform.position, lensSurface.transform.rotation);
            ApplyLensMaterialProperties();
            // 镜外清晰区不再由 CPU 估算屏幕椭圆，而是在 Renderer Pass 中直接重绘同一个运行时镜片网格。
            // 即使 ADS 动画在一帧内继续改变父节点，Pass 执行时读取的仍是与镜片表面一致的最终变换。
            ScopePeripheralEffectState.Publish(this, presentationCamera, lensRenderer, lensMaterial,
                maskPassIndex, activeProfile.OutsideLensDim, activeProfile.OutsideLensBlurPixels,
                activeProfile.PeripheralEdgeSoftness, lensOpacity);
        }

        /// <summary>按眼睛在镜片光学坐标系中的位置计算眼距、偏轴角和出瞳黑边强度。</summary>
        /// <param name="lensPosition">运行时镜片中心的世界坐标，单位为米。</param>
        /// <param name="lensRotation">镜片光学坐标系；局部 +Z 指向目标、局部 -Z 指向玩家眼睛。</param>
        private void UpdateEyebox(Vector3 lensPosition, Quaternion lensRotation)
        {
            Vector3 localEye = Quaternion.Inverse(lensRotation) * (worldCamera.transform.position - lensPosition);
            eyeReliefMetres = Mathf.Abs(localEye.z);
            float lateralDistance = new Vector2(localEye.x, localEye.y).magnitude;
            eyeboxAngleDegrees = Mathf.Atan2(lateralDistance, Mathf.Max(0.001f, eyeReliefMetres)) *
                Mathf.Rad2Deg;

            if (!activeProfile.EyeboxEnabled)
            {
                eyeboxSeverity = 0f;
                eyeboxOffset = Vector2.zero;
                return;
            }

            float angularSeverity = Mathf.InverseLerp(activeProfile.EyeboxAngularTolerance,
                activeProfile.EyeboxAngularTolerance + activeProfile.EyeboxAngularTransition,
                eyeboxAngleDegrees);
            // 眼距在“理想值 ± 安全范围”内不产生黑边，只有超出的距离才进入渐变区。
            float reliefError = Mathf.Max(0f,
                Mathf.Abs(eyeReliefMetres - activeProfile.IdealEyeRelief) - activeProfile.EyeReliefTolerance);
            float reliefSeverity = Mathf.Clamp01(reliefError / Mathf.Max(0.001f,
                activeProfile.EyeReliefTransition));
            eyeboxSeverity = Mathf.Max(angularSeverity, reliefSeverity);

            Vector2 lateralDirection = new Vector2(localEye.x, localEye.y);
            // 出瞳向眼睛偏移的反方向移动，模拟离轴观察时先从对应镜片边缘出现黑边。
            eyeboxOffset = lateralDirection.sqrMagnitude > 0.0000001f
                ? -lateralDirection.normalized * (activeProfile.EyeboxPupilShift * eyeboxSeverity)
                : Vector2.zero;
        }

        private Camera ResolvePresentationCamera()
        {
            ViewmodelCameraRenderer renderer = GetComponent<ViewmodelCameraRenderer>();
            return renderer != null && renderer.Camera != null ? renderer.Camera : worldCamera;
        }

        private Quaternion ResolveScopeViewRotation()
        {
            if (worldCamera == null || lensSurface == null) return worldCamera != null
                ? worldCamera.transform.rotation
                : Quaternion.identity;

            Vector3 viewDirection = lensSurface.transform.position - worldCamera.transform.position;
            if (viewDirection.sqrMagnitude < 0.000001f) return worldCamera.transform.rotation;
            viewDirection.Normalize();

            // 使用镜片上方向确定画面滚转，但先投影到视线垂直平面，避免上方向与视线接近平行时
            // Quaternion.LookRotation 产生无效旋转或瞬间翻转。
            Vector3 viewUp = Vector3.ProjectOnPlane(lensSurface.transform.up, viewDirection);
            if (viewUp.sqrMagnitude < 0.000001f)
                viewUp = Vector3.ProjectOnPlane(worldCamera.transform.up, viewDirection);
            return Quaternion.LookRotation(viewDirection, viewUp.normalized);
        }

        private bool IsTextureBound()
        {
            if (lensMaterial == null || scopeTexture == null) return false;
            if (lensMaterial.HasProperty(MainTextureId) && lensMaterial.GetTexture(MainTextureId) == scopeTexture)
                return true;
            return lensMaterial.mainTexture == scopeTexture;
        }

        private static Material CreateLensMaterial()
        {
            Shader shader = Resources.Load<Shader>(LensShaderResourceName);
            if (shader == null) shader = Shader.Find(LensShaderName);
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            if (shader == null)
            {
                Debug.LogError("Project Sun 倍率镜无法找到镜片 Shader 或紧急 Unlit 后备 Shader。");
                return null;
            }
            Material material = new Material(shader)
            {
                name = "Project Sun Runtime Scope Lens Material",
                hideFlags = HideFlags.DontSave
            };
            // 项目自有 Shader 在一次透明 Pass 中合成抗锯齿口径、可选遮罩、镜内准星和渐变。
            // 后续属性写入同时兼容紧急 Unlit 后备材质，使 Shader 被错误剥离时仍能显示诊断画面。
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_ZTest")) material.SetFloat("_ZTest", (float)CompareFunction.Always);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent + 50;
            return material;
        }

        private Transform ResolveLensAnchor()
        {
            return activeScopeLens != null ? activeScopeLens.transform : activeAimAnchor;
        }

        private void DeactivateLensSurface()
        {
            if (lensSurface != null && lensSurface.activeSelf) lensSurface.SetActive(false);
        }

        private void DestroyLensSurface()
        {
            if (lensSurface != null) Destroy(lensSurface);
            if (lensMaterial != null) Destroy(lensMaterial);
            if (lensMesh != null) Destroy(lensMesh);
            lensSurface = null;
            lensMaterial = null;
            lensMesh = null;
            lensRenderer = null;
        }

        private static Mesh CreateLensQuadMesh()
        {
            Mesh mesh = new Mesh { name = "Project Sun Scope Lens Quad", hideFlags = HideFlags.DontSave };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>按透视投影切线关系把主相机垂直 FOV 转换为真实倍率对应的镜内 FOV。</summary>
        /// <param name="sourceFieldOfView">主相机垂直视场角，单位为度；计算前限制在 5～160。</param>
        /// <param name="magnification">光学倍率，最小按 1 处理，避免除零或产生反向倍率。</param>
        private static float CalculateMagnifiedFieldOfView(float sourceFieldOfView, float magnification)
        {
            float sourceRadians = Mathf.Deg2Rad * Mathf.Clamp(sourceFieldOfView, 5f, 160f);
            float magnifiedRadians = 2f * Mathf.Atan(Mathf.Tan(sourceRadians * 0.5f) / Mathf.Max(1f, magnification));
            return Mathf.Clamp(magnifiedRadians * Mathf.Rad2Deg, 2f, 160f);
        }
    }
}
