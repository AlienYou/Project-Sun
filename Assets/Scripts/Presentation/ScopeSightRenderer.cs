using ProjectSun.FPS.Core;
using ProjectSun.FPS.Weapons;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectSun.FPS.Presentation
{
    /// <summary>
    /// Renders a magnified, world-only view into a physical lens surface parented to the equipped
    /// optic's Aim Anchor. Gameplay rays continue to use the main player camera.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class ScopeSightRenderer : MonoBehaviour
    {
        private const string ScopeCameraName = "Magnified Scope Camera";
        private const string LensSurfaceName = "Runtime Scope Lens Surface";
        private const int MinimumTextureSize = 192;
        private const int MaximumTextureSize = 1024;
        private const int DiscSegments = 64;

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
        private int lastRenderedFrame = -1;

        public bool IsActive => requestedActive && scopeTexture != null && lensSurface != null && lensSurface.activeSelf;
        public Rect ReticleViewport => new Rect(0f, 0f, Screen.width, Screen.height);
        public Texture ScopeTexture => scopeTexture;
        public float ScopeFieldOfView => scopeCamera != null ? scopeCamera.fieldOfView : 0f;
        public string ActiveAnchorName => activeScopeLens != null
            ? activeScopeLens.name
            : activeAimAnchor != null ? activeAimAnchor.name : "NONE";
        public string DiagnosticStatus
        {
            get
            {
                if (!requestedActive) return "INACTIVE";
                if (worldCamera == null) return "NO GAMEPLAY CAMERA";
                if (activeAimAnchor == null) return "NO AIM ANCHOR";
                if (scopeCamera == null) return "NO SCOPE CAMERA";
                if (scopeTexture == null || !scopeTexture.IsCreated()) return "NO RENDER TEXTURE";
                if (lensSurface == null || lensRenderer == null) return "NO LENS SURFACE";
                if (lensMaterial == null) return "NO LENS MATERIAL";
                if (!IsTextureBound()) return "TEXTURE NOT BOUND";
                if (lastRenderedFrame < Time.frameCount - 2) return "SCOPE CAMERA NOT RENDERING";
                return "READY";
            }
        }

        public void Configure(Camera gameplayCamera)
        {
            if (worldCamera == gameplayCamera) return;
            ReleaseScopeCamera();
            worldCamera = gameplayCamera;
        }

        /// <summary>
        /// Activates the physical lens only while a compatible scope is actively aimed. The caller
        /// supplies the live Aim Anchor returned by the attachment presenter rather than a static prefab node.
        /// </summary>
        public void SetSight(OpticSightProfile profile, bool isAiming, Transform aimAnchor,
            ViewmodelScopeLens scopeLens)
        {
            if (activeProfile != profile)
            {
                activeProfile = profile;
                ReleaseScopeTexture();
            }
            activeAimAnchor = aimAnchor;
            activeScopeLens = scopeLens;
            Transform lensAnchor = ResolveLensAnchor();
            requestedActive = profile != null && profile.UsesMagnifiedLensRendering && isAiming &&
                worldCamera != null && lensAnchor != null;

            if (!requestedActive)
            {
                DeactivateLensSurface();
                ReleaseScopeCamera();
                return;
            }

            EnsureScopeCamera();
            EnsureScopeTexture();
            EnsureLensSurface();
            UpdateLensSurface();
        }

        private void LateUpdate()
        {
            if (!requestedActive || activeProfile == null || ResolveLensAnchor() == null || worldCamera == null) return;
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
            scopeCamera.transform.SetPositionAndRotation(worldCamera.transform.position, worldCamera.transform.rotation);
            // Never render the first-person layer into its own Render Texture. Doing so makes the
            // lens sample itself recursively and produces the radial feedback visible in a scope.
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
            ApplyLensTexture();
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
            lensMesh = CreateDiscMesh();
            MeshFilter meshFilter = lensSurface.GetComponent<MeshFilter>();
            meshFilter.sharedMesh = lensMesh;
            lensRenderer = lensSurface.GetComponent<MeshRenderer>();
            lensRenderer.shadowCastingMode = ShadowCastingMode.Off;
            lensRenderer.receiveShadows = false;
            lensRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            lensMaterial = CreateLensMaterial();
            lensRenderer.sharedMaterial = lensMaterial;
            ApplyLensTexture();
        }

        private void UpdateLensSurface()
        {
            Transform lensAnchor = ResolveLensAnchor();
            if (lensSurface == null || activeProfile == null || lensAnchor == null || worldCamera == null) return;
            // Aim Anchor rotation is not a lens-plane contract: ADS derives its sight frame from the
            // anchor-to-muzzle vector. Face the generated disc toward the live view camera so imported
            // weapon axes cannot turn the Render Texture into a narrow edge-on strip.
            Vector3 towardCamera = worldCamera.transform.position - lensAnchor.position;
            if (towardCamera.sqrMagnitude < 0.000001f) towardCamera = -worldCamera.transform.forward;
            towardCamera.Normalize();
            lensSurface.transform.SetPositionAndRotation(
                lensAnchor.position + towardCamera * activeProfile.LensTowardCameraOffset,
                // Keep local X/Y aligned with camera right/up so the Render Texture is not mirrored.
                // The generated disc is double-sided, therefore its normal may point away from the eye.
                worldCamera.transform.rotation);
            float apertureDiameter = activeScopeLens != null
                ? activeScopeLens.ClearApertureDiameter
                : activeProfile.LensPhysicalDiameter;
            lensSurface.transform.localScale = new Vector3(apertureDiameter, apertureDiameter, 1f);
            if (!lensSurface.activeSelf) lensSurface.SetActive(true);
            ApplyLensTexture();
        }

        private void ApplyLensTexture()
        {
            if (lensMaterial == null) return;
            if (lensMaterial.HasProperty("_BaseMap")) lensMaterial.SetTexture("_BaseMap", scopeTexture);
            if (lensMaterial.HasProperty("_BaseColor")) lensMaterial.SetColor("_BaseColor", Color.white);
            lensMaterial.mainTexture = scopeTexture;
        }

        private bool IsTextureBound()
        {
            if (lensMaterial == null || scopeTexture == null) return false;
            if (lensMaterial.HasProperty("_BaseMap") && lensMaterial.GetTexture("_BaseMap") == scopeTexture) return true;
            return lensMaterial.mainTexture == scopeTexture;
        }

        private static Material CreateLensMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            if (shader == null)
            {
                Debug.LogError("Project Sun scope lens could not find an unlit shader.");
                return null;
            }
            Material material = new Material(shader)
            {
                name = "Project Sun Runtime Scope Lens Material",
                hideFlags = HideFlags.DontSave
            };
            // The lens is a deliberate post-viewmodel composite. Its authored circular aperture masks
            // the texture, while Always depth testing prevents imported opaque glass or tube geometry
            // from hiding half of the sight picture.
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

        private static Mesh CreateDiscMesh()
        {
            Mesh mesh = new Mesh { name = "Project Sun Scope Lens Disc", hideFlags = HideFlags.DontSave };
            Vector3[] vertices = new Vector3[DiscSegments + 1];
            Vector2[] uvs = new Vector2[DiscSegments + 1];
            int[] triangles = new int[DiscSegments * 6];
            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);
            for (int index = 0; index < DiscSegments; index++)
            {
                float angle = index / (float)DiscSegments * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * 0.5f;
                float y = Mathf.Sin(angle) * 0.5f;
                vertices[index + 1] = new Vector3(x, y, 0f);
                uvs[index + 1] = new Vector2(x + 0.5f, y + 0.5f);
            }
            for (int index = 0; index < DiscSegments; index++)
            {
                int next = (index + 1) % DiscSegments;
                int triangleOffset = index * 6;
                triangles[triangleOffset] = 0;
                triangles[triangleOffset + 1] = next + 1;
                triangles[triangleOffset + 2] = index + 1;
                triangles[triangleOffset + 3] = 0;
                triangles[triangleOffset + 4] = index + 1;
                triangles[triangleOffset + 5] = next + 1;
            }
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float CalculateMagnifiedFieldOfView(float sourceFieldOfView, float magnification)
        {
            float sourceRadians = Mathf.Deg2Rad * Mathf.Clamp(sourceFieldOfView, 5f, 160f);
            float magnifiedRadians = 2f * Mathf.Atan(Mathf.Tan(sourceRadians * 0.5f) / Mathf.Max(1f, magnification));
            return Mathf.Clamp(magnifiedRadians * Mathf.Rad2Deg, 2f, 160f);
        }
    }
}
