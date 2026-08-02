using ProjectSun.FPS.Presentation;
using ProjectSun.FPS.Weapons;
using ProjectSun.FPS.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ProjectSun.FPS.Editor
{
    /// <summary>
    /// Edit-mode workbench for one-time ADS calibration. It uses the same pose calculation as runtime,
    /// but never saves the temporary viewmodel pose; only the selected ADS profile is persisted.
    /// </summary>
    public sealed class WeaponAdsCalibrationWorkbench : EditorWindow
    {
        private const string PlayerPrefabPath = "Assets/_ProjectSun/Prefabs/Characters/Player.prefab";
        private LowPolyShooterViewmodelRig previewRig;
        private Camera previewCamera;
        private WeaponAdsProfile profile;
        private bool previewActive;
        private bool hasHipPose;
        private Vector3 hipPosition;
        private Quaternion hipRotation;
        private double previousTickTime;
        private GameObject cameraPreviewPlayer;
        private LowPolyShooterViewmodelRig cameraPreviewRig;
        private Camera cameraPreviewCamera;
        private GameObject cameraPreviewCameraObject;
        private RenderTexture cameraPreviewTexture;
        private readonly Light[] cameraPreviewLights = new Light[2];
        [SerializeField, Min(0.0005f)] private float visualNudgeStep = 0.002f;
        [SerializeField] private bool showAdvancedSettings;
        [SerializeField] private bool showSightReferenceMarker = true;
        [SerializeField, Range(0.002f, 0.03f)] private float sightReferenceMarkerRadius = 0.008f;

        [MenuItem("Project Sun/Tools/ADS Calibration Workbench", priority = 40)]
        public static void Open()
        {
            WeaponAdsCalibrationWorkbench window = GetWindow<WeaponAdsCalibrationWorkbench>("ADS Calibration");
            window.minSize = new Vector2(390f, 440f);
            window.TryBindSelection();
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += TickPreview;
            SceneView.duringSceneGui += DrawSceneGuides;
            previousTickTime = EditorApplication.timeSinceStartup;
        }

        private void OnDisable()
        {
            EditorApplication.update -= TickPreview;
            SceneView.duringSceneGui -= DrawSceneGuides;
            StopPreview();
        }

        private void OnSelectionChange()
        {
            if (!previewActive) TryBindSelection();
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "选择 Player Prefab 中的 “FP Viewmodel - LPSP AR-01”，启动预览后无需 Play 或按住右键。" +
                "临时姿势在关闭预览时自动恢复；只有 ADS Profile 的数据会保存。", MessageType.Info);

            EditorGUI.BeginChangeCheck();
            LowPolyShooterViewmodelRig selectedRig = (LowPolyShooterViewmodelRig)EditorGUILayout.ObjectField(
                "Viewmodel Rig", previewRig, typeof(LowPolyShooterViewmodelRig), true);
            if (EditorGUI.EndChangeCheck()) BindRig(selectedRig);

            EditorGUI.BeginChangeCheck();
            Camera selectedCamera = (Camera)EditorGUILayout.ObjectField(
                "Preview Camera", previewCamera, typeof(Camera), true);
            if (EditorGUI.EndChangeCheck())
            {
                StopPreview();
                previewCamera = selectedCamera;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Selected Viewmodel")) TryBindSelection();
                if (GUILayout.Button("Open Player Prefab"))
                {
                    GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
                    if (playerPrefab != null) AssetDatabase.OpenAsset(playerPrefab);
                }
            }

            EditorGUILayout.Space(6f);
            DrawProfileEditor();
            EditorGUILayout.Space(8f);
            DrawPreviewControls();
            if (previewActive) DrawRuntimeCameraPreview();
            EditorGUILayout.Space(8f);
            DrawStatus();
        }

        private void DrawProfileEditor()
        {
            EditorGUILayout.LabelField("ADS Profile", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField(profile, typeof(WeaponAdsProfile), false);
            if (profile == null)
            {
                EditorGUILayout.HelpBox("当前 Viewmodel 没有 ADS Profile。先执行 Project Sun > Integrate Low Poly Shooter Arms (AR-01)。",
                    MessageType.Warning);
                return;
            }

            SerializedObject serializedProfile = new SerializedObject(profile);
            serializedProfile.Update();
            SerializedProperty sightDistance = serializedProfile.FindProperty("sightDistance");
            SerializedProperty zeroDistance = serializedProfile.FindProperty("zeroDistance");
            SerializedProperty referenceOffset = serializedProfile.FindProperty("sightReferenceLocalOffset");
            SerializedProperty positionOffset = serializedProfile.FindProperty("cameraSpacePositionOffset");
            SerializedProperty rotationOffset = serializedProfile.FindProperty("cameraSpaceRotationOffset");
            SerializedProperty hipPositionOffset = serializedProfile.FindProperty("hipCameraSpacePositionOffset");
            SerializedProperty hipRotationOffset = serializedProfile.FindProperty("hipCameraSpaceRotationOffset");
            SerializedProperty visualReview = serializedProfile.FindProperty("visualSightPlacementReviewed");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(sightDistance, new GUIContent("Sight Distance"));
            EditorGUILayout.PropertyField(zeroDistance, new GUIContent("Calibration Zero Distance"));
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(referenceOffset, new GUIContent("Visual Reference Offset"));
            showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "Advanced Presentation Settings");
            if (showAdvancedSettings)
            {
                EditorGUILayout.LabelField("Hip Presentation (not used by ADS calibration)", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(hipPositionOffset, new GUIContent("Hip Position Offset"));
                EditorGUILayout.PropertyField(hipRotationOffset, new GUIContent("Hip Rotation Offset"));
                EditorGUILayout.Space(3f);
                EditorGUILayout.LabelField("ADS Presentation", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(positionOffset, new GUIContent("Camera Position Offset"));
                EditorGUILayout.PropertyField(rotationOffset, new GUIContent("Camera Rotation Offset"));
                EditorGUILayout.PropertyField(serializedProfile.FindProperty("transitionSpeed"), new GUIContent("ADS Transition Speed"));
                EditorGUILayout.PropertyField(serializedProfile.FindProperty("fovReduction"), new GUIContent("ADS FOV Reduction"));
            }
            bool alignmentValuesChanged = EditorGUI.EndChangeCheck();

            EditorGUILayout.Space(3f);
            if (alignmentValuesChanged)
            {
                Undo.RecordObject(profile, "Calibrate ADS Profile");
                visualReview.boolValue = false;
                serializedProfile.ApplyModifiedProperties();
                EditorUtility.SetDirty(profile);
                SceneView.RepaintAll();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select Profile")) Selection.activeObject = profile;
                if (GUILayout.Button("Save Profile")) AssetDatabase.SaveAssets();
            }
        }

        private void DrawPreviewControls()
        {
            using (new EditorGUI.DisabledScope(!CanPreview()))
            {
                string label = previewActive ? "Stop & Restore Hip Pose" : "Start Persistent ADS Preview";
                if (GUILayout.Button(label, GUILayout.Height(32f)))
                {
                    if (previewActive) StopPreview();
                    else StartPreview();
                }
            }

            if (previewActive)
                EditorGUILayout.HelpBox("预览正使用运行时相同的 ADS 对齐公式。请先停止预览，再保存 Player Prefab。", MessageType.Warning);

            if (previewActive) DrawVisualNudgeControls();
        }

        private void DrawVisualNudgeControls()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Visual Sight Assist", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("用屏幕方向移动武器，直到真实红点/机械瞄具中心压住黄色空心标记。", EditorStyles.wordWrappedMiniLabel);
            visualNudgeStep = EditorGUILayout.Slider("Nudge Step", visualNudgeStep, 0.0005f, 0.01f);
            showSightReferenceMarker = EditorGUILayout.Toggle("Show Sight Marker", showSightReferenceMarker);
            if (showSightReferenceMarker)
                sightReferenceMarkerRadius = EditorGUILayout.Slider("Marker Radius", sightReferenceMarkerRadius, 0.002f, 0.03f);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Weapon Up", GUILayout.Width(100f))) NudgeViewmodel(previewCamera.transform.up);
                GUILayout.FlexibleSpace();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Weapon Left")) NudgeViewmodel(-previewCamera.transform.right);
                if (GUILayout.Button("Weapon Right")) NudgeViewmodel(previewCamera.transform.right);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Weapon Down", GUILayout.Width(100f))) NudgeViewmodel(-previewCamera.transform.up);
                GUILayout.FlexibleSpace();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Weapon Back")) NudgeViewmodel(-previewCamera.transform.forward);
                if (GUILayout.Button("Weapon Forward")) NudgeViewmodel(previewCamera.transform.forward);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset Visual Reference Offset")) SetVisualReferenceOffset(Vector3.zero, "Reset Visual Sight Reference");
                if (GUILayout.Button("Auto Centre Sight Reference")) AutoCentreSightReference();
            }
            if (GUILayout.Button("Mark Visual Sight Placement Reviewed")) SetVisualReview(true);
        }

        private void DrawStatus()
        {
            EditorGUILayout.LabelField("Calibration Guide", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("1. Scene 视图中选择 Player Camera，并使用 Ctrl+Shift+F 对齐视图。", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("2. 蓝线是相机中心瞄准线；黄球是 Aim Anchor，必须放在真实瞄具中心。", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("3. 黄线是瞄具到枪口的轴线；紫色圆靶位于 Calibration Zero Distance。", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("4. 先修正 Aim Anchor 的实际观察位置；仅用 Profile 做极小微调。", EditorStyles.wordWrappedMiniLabel);

            if (!previewActive || !CanPreview()) return;
            if (!TryMeasureAlignment(out float screenErrorPixels))
            {
                EditorGUILayout.HelpBox("无法测量瞄具参考点：请确认 Aim Anchor 位于相机前方。", MessageType.Error);
                return;
            }

            const float screenTolerancePixels = 2f;
            bool cameraCentrePassed = screenErrorPixels <= screenTolerancePixels;
            bool ready = cameraCentrePassed && profile.VisualSightPlacementReviewed;
            string result = ready ? "CALIBRATION READY — 可保存为该瞄具的校准基线。" : "NOT READY — 完成所有校准项后再保存。";
            MessageType type = ready ? MessageType.Info : MessageType.Warning;
            string screenState = cameraCentrePassed ? "PASS" : "OFF";
            string visualState = profile.VisualSightPlacementReviewed ? "REVIEWED" : "REVIEW REQUIRED";
            EditorGUILayout.HelpBox(result + $"\n1. Sight reference → camera centre: {screenState}  " +
                $"{screenErrorPixels:0.00}px @1080p/16:9 (limit {screenTolerancePixels:0.00}px)" +
                (cameraCentrePassed ? " — No action needed." : " — Click Auto Centre Sight Reference.") +
                $"\n2. Visual sight placement: {visualState}  " +
                (profile.VisualSightPlacementReviewed
                    ? "— Confirmed."
                    : "— Centre the weapon sight on the yellow marker, then click Mark Visual Sight Placement Reviewed.") +
                $"\n3. Gameplay muzzle path: LOCKED TO CAMERA TARGET @ {profile.ZeroDistance:0.0}m — Read only.", type);
        }

        private void StartPreview()
        {
            if (!CanPreview()) return;
            hipPosition = previewRig.transform.localPosition;
            hipRotation = previewRig.transform.localRotation;
            hasHipPose = true;
            previewActive = true;
            previousTickTime = EditorApplication.timeSinceStartup;
            CreateRuntimeCameraPreview();
            TickPreview();
        }

        private void StopPreview()
        {
            if (previewRig != null && hasHipPose)
            {
                previewRig.transform.localPosition = hipPosition;
                previewRig.transform.localRotation = hipRotation;
                previewRig.ResetPreviewPose();
            }
            previewActive = false;
            hasHipPose = false;
            DisposeRuntimeCameraPreview();
            SceneView.RepaintAll();
        }

        private void TickPreview()
        {
            if (!previewActive) return;
            if (!CanPreview())
            {
                StopPreview();
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            float deltaTime = Mathf.Clamp((float)(now - previousTickTime), 0.001f, 0.05f);
            previousTickTime = now;
            ApplyAdsPreviewPose(previewRig, previewCamera, deltaTime);
            UpdateRuntimeCameraPreview(deltaTime);
            SceneView.RepaintAll();
            Repaint();
        }

        private void ApplyAdsPreviewPose(LowPolyShooterViewmodelRig rig, Camera camera, float deltaTime)
        {
            if (rig == null || camera == null) return;
            rig.PreviewAimingPose(deltaTime);
            if (!WeaponAdsAlignment.TryGetCalibratedPose(rig.transform, rig.AimAnchor, rig.Muzzle, camera, profile,
                    out Vector3 localPosition, out Quaternion localRotation)) return;
            rig.transform.localPosition = localPosition;
            rig.transform.localRotation = localRotation;
        }

        private void DrawRuntimeCameraPreview()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Runtime ADS Camera Preview", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("与玩家右键时相同的相机视角；中心准线是黄色空心标记的屏幕位置。", EditorStyles.wordWrappedMiniLabel);
            Rect previewRect = GUILayoutUtility.GetRect(360f, 220f, GUILayout.ExpandWidth(true));
            if (cameraPreviewCamera == null || cameraPreviewTexture == null || cameraPreviewRig == null)
            {
                EditorGUI.HelpBox(previewRect, "正在创建隔离的相机预览…", MessageType.Info);
                return;
            }

            if (Event.current.type == EventType.Repaint)
            {
                EnsurePreviewRenderTexture(previewRect);
                RenderRuntimeCameraPreview();
                GUI.DrawTexture(previewRect, cameraPreviewTexture, ScaleMode.StretchToFill, false);
                DrawCameraCentreOverlay(previewRect);
            }
        }

        private static void DrawCameraCentreOverlay(Rect previewRect)
        {
            const float radius = 8f;
            const float thickness = 1.5f;
            Vector2 centre = previewRect.center;
            Color old = GUI.color;
            GUI.color = new Color(1f, 0.78f, 0.2f, 0.85f);
            GUI.DrawTexture(new Rect(centre.x - radius, centre.y - thickness * 0.5f, radius * 2f, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(centre.x - thickness * 0.5f, centre.y - radius, thickness, radius * 2f), Texture2D.whiteTexture);
            GUI.color = old;
        }

        private bool CanPreview()
        {
            return !Application.isPlaying && previewRig != null && previewRig.transform.parent != null &&
                previewRig.AimAnchor != null && previewRig.Muzzle != null && previewCamera != null && profile != null;
        }

        private void TryBindSelection()
        {
            GameObject selected = Selection.activeGameObject;
            LowPolyShooterViewmodelRig selectedRig = selected != null
                ? selected.GetComponentInParent<LowPolyShooterViewmodelRig>()
                : null;
            if (selectedRig != null) BindRig(selectedRig);
        }

        private void BindRig(LowPolyShooterViewmodelRig newRig)
        {
            if (previewRig == newRig) return;
            StopPreview();
            previewRig = newRig;
            profile = previewRig != null ? previewRig.AdsProfile : null;
            previewCamera = previewRig != null ? previewRig.GetComponentInParent<Camera>() : null;
        }

        private void CreateRuntimeCameraPreview()
        {
            DisposeRuntimeCameraPreview();
            if (!CanPreview()) return;

            GameObject playerRoot = previewRig.transform.root.gameObject;
            cameraPreviewPlayer = Object.Instantiate(playerRoot);
            cameraPreviewPlayer.hideFlags = HideFlags.HideAndDontSave;
            cameraPreviewRig = cameraPreviewPlayer.GetComponentInChildren<LowPolyShooterViewmodelRig>(true);
            Camera sourceCamera = cameraPreviewRig != null ? cameraPreviewRig.GetComponentInParent<Camera>() : null;
            if (cameraPreviewRig == null || sourceCamera == null)
            {
                DisposeRuntimeCameraPreview();
                return;
            }

            // The edit-mode source prefab is not initialized by ViewmodelCameraRenderer, so assign
            // its visual hierarchy to the same dedicated layer the runtime renderer uses.
            SetLayerRecursively(cameraPreviewRig.transform, CombatLayers.ViewmodelLayer);

            foreach (Camera camera in cameraPreviewPlayer.GetComponentsInChildren<Camera>(true))
                camera.enabled = false;
            foreach (AudioListener listener in cameraPreviewPlayer.GetComponentsInChildren<AudioListener>(true))
                listener.enabled = false;

            cameraPreviewCameraObject = new GameObject("ADS Workbench Preview Camera", typeof(Camera),
                typeof(UniversalAdditionalCameraData));
            cameraPreviewCameraObject.hideFlags = HideFlags.HideAndDontSave;
            cameraPreviewCamera = cameraPreviewCameraObject.GetComponent<Camera>();
            cameraPreviewCamera.CopyFrom(sourceCamera);
            cameraPreviewCamera.transform.position = sourceCamera.transform.position;
            cameraPreviewCamera.transform.rotation = sourceCamera.transform.rotation;
            cameraPreviewCamera.nearClipPlane = 0.01f;
            cameraPreviewCamera.farClipPlane = 10f;
            cameraPreviewCamera.clearFlags = CameraClearFlags.SolidColor;
            cameraPreviewCamera.backgroundColor = new Color(0.025f, 0.035f, 0.055f, 1f);
            cameraPreviewCamera.cullingMask = 1 << CombatLayers.ViewmodelLayer;
            cameraPreviewCamera.enabled = false;
            UniversalAdditionalCameraData data = cameraPreviewCameraObject.GetComponent<UniversalAdditionalCameraData>();
            data.renderType = CameraRenderType.Base;
            data.renderPostProcessing = false;
            UniversalAdditionalCameraData sourceData = sourceCamera.GetComponent<UniversalAdditionalCameraData>();
            if (sourceData != null)
            {
                data.volumeLayerMask = sourceData.volumeLayerMask;
                data.volumeTrigger = sourceData.volumeTrigger;
            }
            CreatePreviewRenderTexture(1024, 640);
            CreatePreviewLights();
        }

        private void EnsurePreviewRenderTexture(Rect previewRect)
        {
            int width = Mathf.Max(1, Mathf.CeilToInt(previewRect.width * 2f));
            int height = Mathf.Max(1, Mathf.CeilToInt(previewRect.height * 2f));
            if (cameraPreviewTexture != null && cameraPreviewTexture.width == width && cameraPreviewTexture.height == height)
                return;

            CreatePreviewRenderTexture(width, height);
        }

        private void CreatePreviewRenderTexture(int width, int height)
        {
            if (cameraPreviewTexture != null) Object.DestroyImmediate(cameraPreviewTexture);
            cameraPreviewTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "ADS Workbench Preview",
                hideFlags = HideFlags.HideAndDontSave
            };
            cameraPreviewTexture.Create();
            if (cameraPreviewCamera != null)
            {
                cameraPreviewCamera.aspect = (float)width / height;
                cameraPreviewCamera.ResetProjectionMatrix();
            }
        }

        private void UpdateRuntimeCameraPreview(float deltaTime)
        {
            if (cameraPreviewCamera == null || cameraPreviewRig == null || previewCamera == null || profile == null) return;
            Camera previewCameraComponent = cameraPreviewCamera;
            Transform playerCamera = cameraPreviewRig.GetComponentInParent<Camera>()?.transform;
            if (playerCamera == null) return;

            previewCameraComponent.transform.position = playerCamera.position;
            previewCameraComponent.transform.rotation = playerCamera.rotation;
            float gameplayAdsFov = Mathf.Max(1f, previewCamera.fieldOfView - profile.FovReduction);
            previewCameraComponent.fieldOfView = ViewmodelCameraRenderer.CalculatePresentationFieldOfView(gameplayAdsFov);
            ApplyAdsPreviewPose(cameraPreviewRig, previewCameraComponent, deltaTime);
        }

        private void RenderRuntimeCameraPreview()
        {
            if (cameraPreviewCamera == null || cameraPreviewTexture == null) return;
            // A direct URP render request is deterministic in 2022 LTS and avoids PreviewRenderUtility's
            // preview-scene path, which can omit isolated viewmodel renderers in SRP.
            cameraPreviewCamera.SubmitRenderRequest(new UniversalRenderPipeline.SingleCameraRequest
            {
                destination = cameraPreviewTexture
            });
        }

        private void DisposeRuntimeCameraPreview()
        {
            if (cameraPreviewPlayer != null) Object.DestroyImmediate(cameraPreviewPlayer);
            cameraPreviewPlayer = null;
            cameraPreviewRig = null;
            foreach (Light light in cameraPreviewLights)
                if (light != null) Object.DestroyImmediate(light.gameObject);
            for (int i = 0; i < cameraPreviewLights.Length; i++) cameraPreviewLights[i] = null;
            if (cameraPreviewCameraObject != null) Object.DestroyImmediate(cameraPreviewCameraObject);
            cameraPreviewCameraObject = null;
            cameraPreviewCamera = null;
            if (cameraPreviewTexture != null) Object.DestroyImmediate(cameraPreviewTexture);
            cameraPreviewTexture = null;
        }

        private void CreatePreviewLights()
        {
            CreatePreviewLight(0, 1.25f, Quaternion.Euler(35f, -30f, 0f));
            CreatePreviewLight(1, 0.85f, Quaternion.Euler(340f, 145f, 0f));
        }

        private void CreatePreviewLight(int index, float intensity, Quaternion rotation)
        {
            GameObject lightObject = new GameObject("ADS Workbench Preview Light", typeof(Light));
            lightObject.hideFlags = HideFlags.HideAndDontSave;
            lightObject.layer = CombatLayers.ViewmodelLayer;
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.color = new Color(0.82f, 0.88f, 1f);
            light.cullingMask = 1 << CombatLayers.ViewmodelLayer;
            light.transform.rotation = rotation;
            cameraPreviewLights[index] = light;
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            if (root == null) return;
            root.gameObject.layer = layer;
            foreach (Transform child in root)
                SetLayerRecursively(child, layer);
        }

        private void DrawSceneGuides(SceneView sceneView)
        {
            if (!previewActive || !CanPreview()) return;

            Vector3 cameraPosition = previewCamera.transform.position;
            Vector3 aimEnd = cameraPosition + previewCamera.transform.forward * 3f;
            Handles.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            Handles.DrawAAPolyLine(3f, cameraPosition, aimEnd);
            Handles.DrawWireDisc(aimEnd, previewCamera.transform.forward, 0.04f);
            Handles.Label(aimEnd, " CAMERA AIM");

            Vector3 sightReference = WeaponAdsAlignment.GetSightReferenceWorldPosition(previewRig.AimAnchor, profile);
            if (showSightReferenceMarker)
            {
                Handles.color = new Color(1f, 0.78f, 0.2f, 0.78f);
                Vector3 markerRight = previewCamera.transform.right * sightReferenceMarkerRadius;
                Vector3 markerUp = previewCamera.transform.up * sightReferenceMarkerRadius;
                Handles.DrawWireDisc(sightReference, previewCamera.transform.forward, sightReferenceMarkerRadius);
                Handles.DrawLine(sightReference - markerRight, sightReference + markerRight);
                Handles.DrawLine(sightReference - markerUp, sightReference + markerUp);
                Handles.Label(sightReference + markerUp * 1.4f, "SIGHT REFERENCE");
            }

            Vector3 target = cameraPosition + previewCamera.transform.forward * profile.ZeroDistance;
            Vector3 right = previewCamera.transform.right * 0.2f;
            Vector3 up = previewCamera.transform.up * 0.2f;
            Handles.color = new Color(0.9f, 0.35f, 1f, 0.9f);
            Handles.DrawWireDisc(target, previewCamera.transform.forward, 0.22f);
            Handles.DrawLine(target - right, target + right);
            Handles.DrawLine(target - up, target + up);
            Handles.Label(target, $" ZERO TARGET {profile.ZeroDistance:0.0}m");

            Handles.color = new Color(1f, 0.78f, 0.2f, 0.9f);
            Handles.DrawDottedLine(previewRig.Muzzle.position, target, 4f);
            Handles.Label(previewRig.Muzzle.position, " MUZZLE ZERO PATH");
        }

        private bool TryMeasureAlignment(out float screenErrorPixels)
        {
            screenErrorPixels = float.PositiveInfinity;
            if (!CanPreview()) return false;

            Vector3 sightReference = WeaponAdsAlignment.GetSightReferenceWorldPosition(previewRig.AimAnchor, profile);
            Vector3 viewport = previewCamera.WorldToViewportPoint(sightReference);
            if (viewport.z <= 0f) return false;
            screenErrorPixels = Mathf.Max(Mathf.Abs(viewport.x - 0.5f) * 1920f,
                Mathf.Abs(viewport.y - 0.5f) * 1080f);
            return true;
        }

        private void NudgeViewmodel(Vector3 desiredWorldMovement)
        {
            if (!CanPreview() || desiredWorldMovement.sqrMagnitude < 0.000001f) return;
            Vector3 localDelta = previewRig.AimAnchor.InverseTransformVector(-desiredWorldMovement.normalized * visualNudgeStep);
            SetVisualReferenceOffset(profile.SightReferenceLocalOffset + localDelta, "Nudge Visual Sight Reference");
        }

        private void SetVisualReferenceOffset(Vector3 newOffset, string undoLabel)
        {
            if (profile == null) return;
            Undo.RecordObject(profile, undoLabel);
            SerializedObject serializedProfile = new SerializedObject(profile);
            serializedProfile.FindProperty("sightReferenceLocalOffset").vector3Value = newOffset;
            serializedProfile.FindProperty("visualSightPlacementReviewed").boolValue = false;
            serializedProfile.ApplyModifiedProperties();
            EditorUtility.SetDirty(profile);
            SceneView.RepaintAll();
        }

        private void AutoCentreSightReference()
        {
            if (profile == null) return;
            Undo.RecordObject(profile, "Auto Centre Sight Reference");
            SerializedObject serializedProfile = new SerializedObject(profile);
            SerializedProperty positionOffset = serializedProfile.FindProperty("cameraSpacePositionOffset");
            Vector3 offset = positionOffset.vector3Value;
            offset.x = 0f;
            offset.y = 0f;
            positionOffset.vector3Value = offset;
            serializedProfile.FindProperty("visualSightPlacementReviewed").boolValue = false;
            serializedProfile.ApplyModifiedProperties();
            EditorUtility.SetDirty(profile);
            SceneView.RepaintAll();
        }

        private void SetVisualReview(bool reviewed)
        {
            if (profile == null) return;
            Undo.RecordObject(profile, reviewed ? "Review Visual Sight Placement" : "Clear Visual Sight Review");
            SerializedObject serializedProfile = new SerializedObject(profile);
            serializedProfile.FindProperty("visualSightPlacementReviewed").boolValue = reviewed;
            serializedProfile.ApplyModifiedProperties();
            EditorUtility.SetDirty(profile);
            Repaint();
        }
    }
}
