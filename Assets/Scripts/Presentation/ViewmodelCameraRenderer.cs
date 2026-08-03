using ProjectSun.FPS.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ProjectSun.FPS.Presentation
{
    /// <summary>
    /// Renders first-person arms and weapons in a dedicated URP overlay camera. This keeps the visual
    /// rig out of the world camera's near clip plane while leaving gameplay, physics and hit detection unchanged.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ViewmodelCameraRenderer : MonoBehaviour
    {
        private const string ViewmodelCameraName = "Viewmodel Camera";
        private const float ViewmodelFieldOfViewOffset = 16f;
        private const float MinimumViewmodelFieldOfView = 94f;
        public const float NearClipPlane = 0.01f;
        private Camera worldCamera;
        private Camera viewmodelCamera;
        private UniversalAdditionalCameraData worldCameraData;
        private UniversalAdditionalCameraData viewmodelCameraData;
        private int originalWorldCullingMask;
        private bool hasStoredWorldMask;

        public Camera Camera => viewmodelCamera;

        /// <summary>Keeps the whole weapon readable while the gameplay camera narrows during ADS.</summary>
        public static float CalculatePresentationFieldOfView(float gameplayFieldOfView)
        {
            return Mathf.Max(MinimumViewmodelFieldOfView, gameplayFieldOfView + ViewmodelFieldOfViewOffset);
        }

        public void Configure(Camera gameplayCamera, Transform viewmodelRoot)
        {
            if (gameplayCamera == null || viewmodelRoot == null) return;
            if (worldCamera != gameplayCamera)
            {
                RestoreWorldCamera();
                worldCamera = gameplayCamera;
                originalWorldCullingMask = worldCamera.cullingMask;
                hasStoredWorldMask = true;
            }

            SetLayerRecursively(viewmodelRoot, CombatLayers.ViewmodelLayer);
            worldCamera.cullingMask = originalWorldCullingMask & ~(1 << CombatLayers.ViewmodelLayer);
            EnsureViewmodelCamera();
            ConfigureCameraStack();
            viewmodelCamera.fieldOfView = CalculatePresentationFieldOfView(worldCamera.fieldOfView);
        }

        private void LateUpdate()
        {
            if (worldCamera == null || viewmodelCamera == null) return;
            viewmodelCamera.fieldOfView = CalculatePresentationFieldOfView(worldCamera.fieldOfView);
            viewmodelCamera.nearClipPlane = NearClipPlane;
            viewmodelCamera.farClipPlane = 10f;
            viewmodelCamera.rect = worldCamera.rect;
            viewmodelCamera.aspect = worldCamera.aspect;
        }

        private void OnDestroy()
        {
            RestoreWorldCamera();
            if (viewmodelCamera != null) Destroy(viewmodelCamera.gameObject);
        }

        private void EnsureViewmodelCamera()
        {
            if (viewmodelCamera != null) return;

            GameObject cameraObject = new GameObject(ViewmodelCameraName, typeof(Camera), typeof(UniversalAdditionalCameraData));
            cameraObject.transform.SetParent(worldCamera.transform, false);
            viewmodelCamera = cameraObject.GetComponent<Camera>();
            viewmodelCameraData = cameraObject.GetComponent<UniversalAdditionalCameraData>();
            viewmodelCamera.clearFlags = CameraClearFlags.Depth;
            viewmodelCamera.cullingMask = 1 << CombatLayers.ViewmodelLayer;
            viewmodelCamera.nearClipPlane = NearClipPlane;
            viewmodelCamera.farClipPlane = 10f;
            viewmodelCamera.depth = worldCamera.depth + 1f;
            viewmodelCamera.useOcclusionCulling = false;
            viewmodelCamera.allowHDR = worldCamera.allowHDR;
            viewmodelCamera.allowMSAA = worldCamera.allowMSAA;
            viewmodelCameraData.renderType = CameraRenderType.Overlay;
            viewmodelCameraData.renderPostProcessing = false;
        }

        private void ConfigureCameraStack()
        {
            if (worldCamera == null || viewmodelCamera == null) return;
            worldCameraData = worldCamera.GetComponent<UniversalAdditionalCameraData>();
            if (worldCameraData == null) worldCameraData = worldCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            if (viewmodelCameraData == null) viewmodelCameraData = viewmodelCamera.GetComponent<UniversalAdditionalCameraData>();
            worldCameraData.renderType = CameraRenderType.Base;
            viewmodelCameraData.renderType = CameraRenderType.Overlay;
            if (!worldCameraData.cameraStack.Contains(viewmodelCamera))
                worldCameraData.cameraStack.Add(viewmodelCamera);
        }

        private void RestoreWorldCamera()
        {
            if (worldCameraData != null && viewmodelCamera != null)
                worldCameraData.cameraStack.Remove(viewmodelCamera);
            if (worldCamera != null && hasStoredWorldMask)
                worldCamera.cullingMask = originalWorldCullingMask;
            hasStoredWorldMask = false;
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root)
                SetLayerRecursively(child, layer);
        }
    }
}
