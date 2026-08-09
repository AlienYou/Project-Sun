using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectSun.FPS.Presentation
{
    /// <summary>镜外合成的全局画质档位；只控制采样成本，不改变瞄具资产或玩法数据。</summary>
    public enum ScopePeripheralQualityTier
    {
        DimOnly,
        SoftBlurFourTap,
        SoftBlurEightTap
    }

    /// <summary>
    /// 在当前帧的倍率镜组件和 Viewmodel Overlay Camera 渲染功能之间传递只读参数。
    /// 状态不写回瞄具资产或弹道数据，因此切换全局画质不会改变玩法权威状态。
    /// </summary>
    internal static class ScopePeripheralEffectState
    {
        internal readonly struct Snapshot
        {
            /// <summary>运行时物理镜片的 MeshRenderer；Renderer Pass 会用其最终变换生成口径 Mask。</summary>
            public readonly Renderer LensRenderer;
            /// <summary>包含专用口径 Mask Pass 的运行时镜片材质。</summary>
            public readonly Material LensMaterial;
            /// <summary>镜片材质中 `ScopeApertureMask` Pass 的索引；负值表示配置无效。</summary>
            public readonly int MaskPassIndex;
            /// <summary>镜外暗化比例，有效范围 0～1。</summary>
            public readonly float OutsideDim;
            /// <summary>当前画质档位采用的模糊半径，单位为屏幕像素。</summary>
            public readonly float BlurRadiusPixels;
            /// <summary>镜片投影边缘的导数抗锯齿倍数。</summary>
            public readonly float EdgeSoftness;
            /// <summary>跟随 ADS 渐变的总效果权重，有效范围 0～1。</summary>
            public readonly float Opacity;
            /// <summary>当前全局画质对应的镜外采样档位。</summary>
            public readonly ScopePeripheralQualityTier QualityTier;

            /// <summary>创建同一帧内只读的镜外渲染快照。</summary>
            /// <param name="lensRenderer">活动镜片表面的 MeshRenderer；不得引用静态 Prefab 资源。</param>
            /// <param name="lensMaterial">活动镜片的运行时材质；必须包含专用口径 Mask Pass。</param>
            /// <param name="maskPassIndex">口径 Mask Pass 索引；有效值必须大于等于 0。</param>
            /// <param name="outsideDim">镜外暗化比例，有效范围 0～1。</param>
            /// <param name="blurRadiusPixels">镜外模糊半径，单位为屏幕像素；0 表示不模糊。</param>
            /// <param name="edgeSoftness">镜片投影边缘的导数抗锯齿倍数，有效范围 0.5～4。</param>
            /// <param name="opacity">本帧效果权重，有效范围 0～1，通常跟随 ADS 渐变。</param>
            /// <param name="qualityTier">由全局画质解析出的采样档位。</param>
            public Snapshot(Renderer lensRenderer, Material lensMaterial, int maskPassIndex, float outsideDim,
                float blurRadiusPixels, float edgeSoftness, float opacity, ScopePeripheralQualityTier qualityTier)
            {
                LensRenderer = lensRenderer;
                LensMaterial = lensMaterial;
                MaskPassIndex = maskPassIndex;
                OutsideDim = outsideDim;
                BlurRadiusPixels = blurRadiusPixels;
                EdgeSoftness = edgeSoftness;
                Opacity = opacity;
                QualityTier = qualityTier;
            }
        }

        private static Object owner;
        private static Camera targetCamera;
        private static Snapshot snapshot;
        private static int lastPublishedFrame = -1;
        private static int lastRenderedFrame = -1;

        /// <summary>镜外 Pass 最近一次成功提交的 Unity 帧编号，-1 表示尚未执行。</summary>
        public static int LastRenderedFrame => lastRenderedFrame;
        /// <summary>用于运行时诊断面板显示的当前采样档位名称。</summary>
        public static string QualityLabel
        {
            get
            {
                switch (snapshot.QualityTier)
                {
                    case ScopePeripheralQualityTier.SoftBlurEightTap: return "BLUR 8 TAP";
                    case ScopePeripheralQualityTier.SoftBlurFourTap: return "BLUR 4 TAP";
                    default: return "DIM ONLY";
                }
            }
        }

        /// <summary>发布当前帧唯一的本地玩家倍率镜参数；下一帧未再次发布时状态自动失效。</summary>
        /// <param name="effectOwner">状态所有者，用于避免其他实例错误清理活动效果。</param>
        /// <param name="camera">最终执行合成的 Viewmodel Overlay Camera。</param>
        /// <param name="lensRenderer">活动镜片表面的 MeshRenderer；Pass 会在执行时读取其最终变换。</param>
        /// <param name="lensMaterial">活动镜片的运行时材质；必须包含专用口径 Mask Pass。</param>
        /// <param name="maskPassIndex">口径 Mask Pass 索引；有效值必须大于等于 0。</param>
        /// <param name="outsideDim">镜外暗化比例，有效范围 0～1，方法内部会限制范围。</param>
        /// <param name="blurRadiusPixels">最高档模糊半径，单位为屏幕像素；低画质可能强制为 0。</param>
        /// <param name="edgeSoftness">镜片投影边缘过渡倍数，有效范围 0.5～4。</param>
        /// <param name="opacity">ADS 渐变权重，有效范围 0～1。</param>
        public static void Publish(Object effectOwner, Camera camera, Renderer lensRenderer,
            Material lensMaterial, int maskPassIndex, float outsideDim, float blurRadiusPixels,
            float edgeSoftness, float opacity)
        {
            if (owner != effectOwner || targetCamera != camera) lastRenderedFrame = -1;
            owner = effectOwner;
            targetCamera = camera;
            ScopePeripheralQualityTier quality = ResolveQualityTier();
            snapshot = new Snapshot(lensRenderer, lensMaterial, maskPassIndex, Mathf.Clamp01(outsideDim),
                quality == ScopePeripheralQualityTier.DimOnly ? 0f : Mathf.Max(0f, blurRadiusPixels),
                Mathf.Clamp(edgeSoftness, 0.5f, 4f), Mathf.Clamp01(opacity), quality);
            lastPublishedFrame = Time.frameCount;
        }

        /// <summary>仅允许状态所有者清除效果，避免换枪或销毁顺序误伤新的活动实例。</summary>
        /// <param name="effectOwner">请求清理状态的倍率镜组件；不是当前所有者时忽略。</param>
        public static void Clear(Object effectOwner)
        {
            if (owner != effectOwner) return;
            owner = null;
            targetCamera = null;
            lastPublishedFrame = -1;
            lastRenderedFrame = -1;
        }

        /// <summary>判断诊断信息是否属于指定倍率镜实例。</summary>
        /// <param name="effectOwner">需要检查的倍率镜组件。</param>
        public static bool IsOwnedBy(Object effectOwner) => owner == effectOwner;

        /// <summary>判断指定相机在当前帧是否需要执行镜外合成。</summary>
        /// <param name="camera">URP 当前准备渲染的相机。</param>
        public static bool IsActiveFor(Camera camera)
        {
            return owner != null && targetCamera == camera && snapshot.Opacity > 0.001f &&
                snapshot.LensRenderer != null && snapshot.LensMaterial != null && snapshot.MaskPassIndex >= 0 &&
                (snapshot.OutsideDim > 0.001f || snapshot.BlurRadiusPixels > 0.01f) &&
                lastPublishedFrame >= Time.frameCount - 1;
        }

        /// <summary>读取指定相机在当前帧有效的不可变参数快照。</summary>
        /// <param name="camera">正在执行 Renderer Feature 的相机。</param>
        /// <param name="current">成功时返回当前帧快照；失败时内容不可使用。</param>
        public static bool TryGet(Camera camera, out Snapshot current)
        {
            current = snapshot;
            return IsActiveFor(camera);
        }

        /// <summary>记录镜外 Pass 已在当前帧完成，供 WeaponLab 判断功能是否真正进入渲染链。</summary>
        /// <param name="camera">刚完成镜外合成的相机。</param>
        public static void MarkRendered(Camera camera)
        {
            if (targetCamera == camera) lastRenderedFrame = Time.frameCount;
        }

        private static ScopePeripheralQualityTier ResolveQualityTier()
        {
            int qualityLevel = QualitySettings.GetQualityLevel();
            if (qualityLevel <= 1) return ScopePeripheralQualityTier.DimOnly;
            if (qualityLevel <= 3) return ScopePeripheralQualityTier.SoftBlurFourTap;
            return ScopePeripheralQualityTier.SoftBlurEightTap;
        }
    }

    /// <summary>
    /// 只在最终 Viewmodel Overlay Camera 上执行倍率镜镜外暗化和模糊。
    /// 清晰区域来自 Renderer Pass 用活动镜片网格生成的单通道口径 Mask，不假设镜片固定在屏幕中心。
    /// Shader 缺失或不受当前图形 API 支持时不会注入 Pass，以保留原始相机画面。
    /// </summary>
    public sealed class ScopePeripheralRenderFeature : ScriptableRendererFeature
    {
        private const string ShaderResourceName = "ProjectSunScopePeripheral";
        private const string ShaderName = "Project Sun/Scope Peripheral Composite";
        private ScopePeripheralPass pass;
        private Material material;

        public override void Create()
        {
            DisposeResources();
            Shader shader = Resources.Load<Shader>(ShaderResourceName);
            if (shader == null) shader = Shader.Find(ShaderName);
            // 全屏 Pass 的 Shader 缺失或当前图形 API 不支持时必须停止注入渲染。
            // 继续执行无效材质会覆盖相机颜色目标，表现为整个 ADS 画面变黑。
            if (shader == null || !shader.isSupported) return;
            material = CoreUtils.CreateEngineMaterial(shader);
            pass = new ScopePeripheralPass(material);
        }

        /// <summary>仅在当前相机与活动倍率镜目标一致时，把镜外 Pass 加入 URP 队列。</summary>
        /// <param name="renderer">当前相机使用的 URP Renderer。</param>
        /// <param name="renderingData">当前相机和帧的渲染上下文。</param>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (pass == null || material == null ||
                !ScopePeripheralEffectState.IsActiveFor(renderingData.cameraData.camera)) return;
            renderer.EnqueuePass(pass);
        }

        /// <summary>在 URP 初始化颜色目标后，把最终相机颜色 RTHandle 交给镜外 Pass。</summary>
        /// <param name="renderer">提供当前相机颜色目标的 URP Renderer。</param>
        /// <param name="renderingData">用于确认当前相机仍是活动倍率镜目标的渲染上下文。</param>
        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            if (pass == null || material == null ||
                !ScopePeripheralEffectState.IsActiveFor(renderingData.cameraData.camera)) return;
            pass.SetInput(renderer.cameraColorTargetHandle, renderingData.cameraData.camera);
        }

        /// <summary>释放运行时材质和临时 RTHandle，防止脚本重载或 Renderer 销毁后泄漏显存。</summary>
        /// <param name="disposing">由 URP 传入的释放阶段标记；两种路径都必须释放原生渲染资源。</param>
        protected override void Dispose(bool disposing)
        {
            DisposeResources();
        }

        private void DisposeResources()
        {
            pass?.Dispose();
            pass = null;
            CoreUtils.Destroy(material);
            material = null;
        }

        private sealed class ScopePeripheralPass : ScriptableRenderPass
        {
            private static readonly int LensMaskTextureId = Shader.PropertyToID("_LensMaskTex");
            private static readonly int OutsideDimId = Shader.PropertyToID("_OutsideDim");
            private static readonly int BlurRadiusId = Shader.PropertyToID("_BlurRadiusPixels");
            private static readonly int BlurQualityId = Shader.PropertyToID("_BlurQuality");
            private static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");
            private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
            private static readonly int SourceTexelSizeId = Shader.PropertyToID("_SourceTexelSize");
            private readonly ProfilingSampler scopeProfilingSampler =
                new ProfilingSampler("Project Sun Scope Peripheral");
            private readonly Material material;
            private RTHandle source;
            private RTHandle scratch;
            private RTHandle apertureMask;
            private Camera camera;

            /// <summary>创建在透明物体完成后执行的镜外合成 Pass。</summary>
            /// <param name="material">使用项目镜外 Shader 创建的运行时材质，不得为空。</param>
            public ScopePeripheralPass(Material material)
            {
                this.material = material;
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Color);
            }

            /// <summary>设置本帧需要读取并回写的相机颜色目标。</summary>
            /// <param name="colorTarget">URP 为当前相机分配的颜色 RTHandle。</param>
            /// <param name="targetCamera">与活动倍率镜状态匹配的 Viewmodel Overlay Camera。</param>
            public void SetInput(RTHandle colorTarget, Camera targetCamera)
            {
                source = colorTarget;
                camera = targetCamera;
            }

            /// <summary>按相机格式分配可复用的无深度、单采样临时纹理，尺寸变化时才会重建。</summary>
            /// <param name="cmd">URP 提供的配置命令缓冲；本方法不立即执行命令。</param>
            /// <param name="cameraTextureDescriptor">当前相机颜色目标描述，用于保持格式和尺寸一致。</param>
            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                RenderTextureDescriptor descriptor = cameraTextureDescriptor;
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;
                RenderingUtils.ReAllocateIfNeeded(ref scratch, descriptor, FilterMode.Bilinear,
                    TextureWrapMode.Clamp, name: "_ProjectSunScopePeripheralScratch");

                // 口径 Mask 只保存 0～1 覆盖率，使用单通道纹理可把额外显存控制在同分辨率 RGBA 纹理的约四分之一。
                RenderTextureDescriptor maskDescriptor = descriptor;
                maskDescriptor.colorFormat = RenderTextureFormat.R8;
                RenderingUtils.ReAllocateIfNeeded(ref apertureMask, maskDescriptor, FilterMode.Bilinear,
                    TextureWrapMode.Clamp, name: "_ProjectSunScopeApertureMask");
            }

            /// <summary>先合成到临时纹理，再回写相机目标，避免同一纹理被同时读取和写入。</summary>
            /// <param name="context">用于提交命令缓冲的当前 SRP 渲染上下文。</param>
            /// <param name="renderingData">当前帧渲染数据；实际效果参数来自同帧快照。</param>
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (material == null || source == null || scratch == null || apertureMask == null || camera == null ||
                    !ScopePeripheralEffectState.TryGet(camera, out ScopePeripheralEffectState.Snapshot settings))
                    return;

                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, scopeProfilingSampler))
                {
                    // 与镜片表面使用同一个 Renderer 和当前相机矩阵重新栅格化口径，避免 CPU 屏幕投影
                    // 与 Viewmodel 最终动画姿态之间出现一帧偏差。Mask 不受 Eyebox 黑边或镜片渐变影响。
                    CoreUtils.SetRenderTarget(cmd, apertureMask, ClearFlag.Color, Color.black);
                    cmd.DrawRenderer(settings.LensRenderer, settings.LensMaterial, 0, settings.MaskPassIndex);
                    material.SetTexture(LensMaskTextureId, apertureMask.rt);
                    material.SetFloat(OutsideDimId, settings.OutsideDim);
                    material.SetFloat(BlurRadiusId, settings.BlurRadiusPixels);
                    material.SetFloat(BlurQualityId, (float)settings.QualityTier);
                    material.SetFloat(EdgeSoftnessId, settings.EdgeSoftness);
                    material.SetFloat(OpacityId, settings.Opacity);
                    // URP 14 的常规 Blitter 重载不会设置 _BlitTextureSize，因此必须根据本帧相机目标
                    // 显式传入像素尺寸。使用描述符而不是 Screen 尺寸可兼容 Game 窗口和动态分辨率。
                    int sourceWidth = Mathf.Max(1, renderingData.cameraData.cameraTargetDescriptor.width);
                    int sourceHeight = Mathf.Max(1, renderingData.cameraData.cameraTargetDescriptor.height);
                    material.SetVector(SourceTexelSizeId, new Vector4(1f / sourceWidth, 1f / sourceHeight,
                        sourceWidth, sourceHeight));
                    Blitter.BlitCameraTexture(cmd, source, scratch, material, 0);
                    Blitter.BlitCameraTexture(cmd, scratch, source);
                }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
                ScopePeripheralEffectState.MarkRendered(camera);
            }

            public void Dispose()
            {
                scratch?.Release();
                scratch = null;
                apertureMask?.Release();
                apertureMask = null;
                source = null;
                camera = null;
            }
        }
    }
}
