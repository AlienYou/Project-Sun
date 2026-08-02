using ProjectSun.FPS.Core;
using ProjectSun.FPS.Presentation;
using ProjectSun.FPS.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ProjectSun.FPS.Editor
{
    /// <summary>
    /// Weapon-centric edit-mode workbench. It previews the exact hip and ADS pose calculations used at runtime,
    /// while only persisting the selected weapon's presentation and ADS profile assets.
    /// </summary>
    public sealed class WeaponAdsCalibrationWorkbench : EditorWindow
    {
        private const string PlayerPrefabPath = "Assets/_ProjectSun/Prefabs/Characters/Player.prefab";
        private const float ScreenTolerancePixels = 2f;

        private enum PreviewMode { Hip, Ads, Compare }

        [SerializeField] private WeaponInventorySlot selectedSlot = WeaponInventorySlot.Primary;
        [SerializeField] private PreviewMode previewMode = PreviewMode.Compare;
        [SerializeField] private Vector2 scrollPosition;
        [SerializeField] private bool freezeAnimationForCalibration;
        [SerializeField, Min(0.0005f)] private float visualNudgeStep = 0.002f;
        [SerializeField] private bool showAdvancedSettings;
        [SerializeField] private bool showSightReferenceMarker = true;
        [SerializeField, Range(0.002f, 0.03f)] private float sightReferenceMarkerRadius = 0.008f;

        private LowPolyShooterViewmodelRig previewRig;
        private WeaponInventoryController inventory;
        private WeaponViewmodelSlot selectedViewmodel;
        private WeaponPresentationProfile presentationProfile;
        private WeaponAdsProfile profile;
        private Camera previewCamera;
        private bool previewActive;
        private bool previewPosesPrimed;
        private bool hasScenePreviewMode;
        private PreviewMode lastScenePreviewMode;
        private bool hasHipPose;
        private Vector3 hipPosition;
        private Quaternion hipRotation;
        private double previousTickTime;

        // Temporary changes applied to the source prefab while the Scene preview is active.
        private bool sourcePresentationCaptured;
        private RuntimeAnimatorController originalArmsController;
        private Animator originalWeaponAnimator;
        private Transform originalMuzzle;
        private Transform originalAimAnchor;
        private Transform originalMagazine;
        private WeaponAdsProfile originalAdsProfile;
        private WeaponPresentationProfile originalPresentationProfile;
        private Transform sourcePrimaryRoot;
        private Transform sourceSecondaryRoot;
        private bool sourcePrimaryWasActive;
        private bool sourceSecondaryWasActive;

        // Isolated runtime-camera preview. It is never saved into the Player prefab.
        private GameObject cameraPreviewPlayer;
        private LowPolyShooterViewmodelRig cameraPreviewRig;
        private Camera cameraPreviewCamera;
        private GameObject cameraPreviewCameraObject;
        private GameObject adsPreviewPlayer;
        private LowPolyShooterViewmodelRig adsPreviewRig;
        private Camera adsPreviewCamera;
        private GameObject adsPreviewCameraObject;
        private RenderTexture hipPreviewTexture;
        private RenderTexture adsPreviewTexture;
        private Vector3 cameraPreviewHipPosition;
        private Quaternion cameraPreviewHipRotation;
        private Vector3 adsPreviewHipPosition;
        private Quaternion adsPreviewHipRotation;
        private readonly Light[] cameraPreviewLights = new Light[2];

        [MenuItem("Project Sun/Tools/Weapon Presentation Workbench", priority = 40)]
        [MenuItem("Project Sun/Tools/ADS Calibration Workbench", priority = 41)]
        public static void Open()
        {
            WeaponAdsCalibrationWorkbench window = GetWindow<WeaponAdsCalibrationWorkbench>("Weapon Workbench");
            window.minSize = new Vector2(430f, 560f);
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
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "以武器为单位校准。选择 Player 或其中的 FP Viewmodel 后，可在同一窗口直接查看腰射、右键 ADS，或并排对照。所有预览都复用游戏运行时的姿态与视角计算；只会保存当前武器的 Profile。",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            LowPolyShooterViewmodelRig selectedRig = (LowPolyShooterViewmodelRig)EditorGUILayout.ObjectField(
                "Viewmodel Rig", previewRig, typeof(LowPolyShooterViewmodelRig), true);
            if (EditorGUI.EndChangeCheck()) BindRig(selectedRig);

            EditorGUI.BeginChangeCheck();
            Camera selectedCamera = (Camera)EditorGUILayout.ObjectField(
                "Player Camera", previewCamera, typeof(Camera), true);
            if (EditorGUI.EndChangeCheck())
            {
                StopPreview();
                previewCamera = selectedCamera;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("使用当前选择")) TryBindSelection();
                if (GUILayout.Button("打开 Player 预制体"))
                {
                    GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
                    if (playerPrefab != null) AssetDatabase.OpenAsset(playerPrefab);
                }
            }

            EditorGUILayout.Space(8f);
            DrawWeaponSelector();
            EditorGUILayout.Space(8f);
            DrawWeaponProfileEditor();
            EditorGUILayout.Space(8f);
            DrawPreviewControls();
            if (previewActive) DrawRuntimeCameraPreview();
            EditorGUILayout.Space(8f);
            DrawStatus();
            EditorGUILayout.EndScrollView();
        }

        private void DrawWeaponSelector()
        {
            EditorGUILayout.LabelField("当前武器", EditorStyles.boldLabel);
            if (inventory == null)
            {
                EditorGUILayout.HelpBox("未找到 Player 的 Weapon Inventory。仍可校准当前 Rig；要使用主/副武器切换，请打开 Project Sun 的 Player 预制体后重新选择。",
                    MessageType.Warning);
                return;
            }

            EditorGUI.BeginChangeCheck();
            WeaponInventorySlot newSlot = (WeaponInventorySlot)EditorGUILayout.EnumPopup("武器槽位", selectedSlot);
            if (EditorGUI.EndChangeCheck())
            {
                StopPreview();
                selectedSlot = newSlot;
                ResolveSelectedWeapon();
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("编辑目标", GetWeaponDisplayName());
                EditorGUILayout.ObjectField("武器模型", selectedViewmodel != null ? selectedViewmodel.VisualRoot : null,
                    typeof(Transform), true);
                EditorGUILayout.ObjectField("表现 Profile", presentationProfile, typeof(WeaponPresentationProfile), false);
                EditorGUILayout.ObjectField("ADS Profile", profile, typeof(WeaponAdsProfile), false);
            }

            if (selectedViewmodel == null || !selectedViewmodel.IsPresentationReady)
                EditorGUILayout.HelpBox("此武器槽缺少模型、枪口、瞄准锚点或手臂动画控制器，无法启动预览。",
                    MessageType.Error);
        }

        private void DrawWeaponProfileEditor()
        {
            EditorGUILayout.LabelField("该武器的表现配置", EditorStyles.boldLabel);
            if (presentationProfile != null)
            {
                SerializedObject serializedPresentation = new SerializedObject(presentationProfile);
                serializedPresentation.Update();
                SerializedProperty hipPosition = serializedPresentation.FindProperty("hipCameraSpacePositionOffset");
                SerializedProperty hipRotation = serializedPresentation.FindProperty("hipCameraSpaceRotationOffset");
                SerializedProperty viewKick = serializedPresentation.FindProperty("viewKickMultiplier");
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.LabelField("腰射（未按右键）", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(hipPosition, new GUIContent("相机空间位置"));
                EditorGUILayout.PropertyField(hipRotation, new GUIContent("相机空间旋转"));
                EditorGUILayout.PropertyField(viewKick, new GUIContent("后坐力倍率"));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(presentationProfile, "Adjust Weapon Hip Presentation");
                    serializedPresentation.ApplyModifiedProperties();
                    EditorUtility.SetDirty(presentationProfile);
                    TickPreview();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("此武器尚未创建 Presentation Profile；腰射将使用其模型的默认姿态。",
                    MessageType.Warning);
            }

            if (profile == null)
            {
                EditorGUILayout.HelpBox("此武器配置为仅腰射：无需 ADS Profile 或 Aim Anchor。工作台仍可用于调整腰射表现。",
                    MessageType.Info);
                return;
            }

            SerializedObject serializedProfile = new SerializedObject(profile);
            serializedProfile.Update();
            SerializedProperty sightDistance = serializedProfile.FindProperty("sightDistance");
            SerializedProperty zeroDistance = serializedProfile.FindProperty("zeroDistance");
            SerializedProperty referenceOffset = serializedProfile.FindProperty("sightReferenceLocalOffset");
            SerializedProperty positionOffset = serializedProfile.FindProperty("cameraSpacePositionOffset");
            SerializedProperty rotationOffset = serializedProfile.FindProperty("cameraSpaceRotationOffset");
            SerializedProperty transitionSpeed = serializedProfile.FindProperty("transitionSpeed");
            SerializedProperty fovReduction = serializedProfile.FindProperty("fovReduction");
            SerializedProperty visualReview = serializedProfile.FindProperty("visualSightPlacementReviewed");

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("右键 ADS", EditorStyles.miniBoldLabel);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(sightDistance, new GUIContent("瞄具距离"));
            EditorGUILayout.PropertyField(zeroDistance, new GUIContent("归零距离"));
            showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "高级 ADS 微调");
            if (showAdvancedSettings)
            {
                EditorGUILayout.PropertyField(referenceOffset, new GUIContent("机瞄参考修正（模型空间）"));
                EditorGUILayout.PropertyField(positionOffset, new GUIContent("相机空间位置微调"));
                EditorGUILayout.PropertyField(rotationOffset, new GUIContent("相机空间旋转微调"));
                EditorGUILayout.PropertyField(transitionSpeed, new GUIContent("ADS 过渡速度"));
                EditorGUILayout.PropertyField(fovReduction, new GUIContent("ADS FOV 缩减"));
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(profile, "Calibrate Weapon ADS Profile");
                visualReview.boolValue = false;
                serializedProfile.ApplyModifiedProperties();
                EditorUtility.SetDirty(profile);
                TickPreview();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("选择表现 Profile") && presentationProfile != null)
                    Selection.activeObject = presentationProfile;
                if (GUILayout.Button("选择 ADS Profile")) Selection.activeObject = profile;
                if (GUILayout.Button("保存 Profile")) AssetDatabase.SaveAssets();
            }
        }

        private void DrawPreviewControls()
        {
            EditorGUILayout.LabelField("实时武器预览", EditorStyles.boldLabel);
            if (HasAdsPreview)
            {
                EditorGUI.BeginChangeCheck();
                PreviewMode newMode = (PreviewMode)GUILayout.Toolbar((int)previewMode,
                    new[] { "腰射", "右键 ADS", "并排对照" });
                if (EditorGUI.EndChangeCheck())
                {
                    previewMode = newMode;
                    TickPreview();
                }
            }
            else
            {
                previewMode = PreviewMode.Hip;
                EditorGUILayout.LabelField("仅腰射武器：没有右键 ADS 视图。", EditorStyles.miniLabel);
            }

            EditorGUI.BeginChangeCheck();
            freezeAnimationForCalibration = EditorGUILayout.Toggle(
                new GUIContent("冻结动画用于校准", "关闭时与游戏内一样连续播放动画；开启后固定当前姿态，便于检查机瞄中心。"),
                freezeAnimationForCalibration);
            if (EditorGUI.EndChangeCheck())
            {
                if (!freezeAnimationForCalibration) previewPosesPrimed = false;
                TickPreview();
            }

            using (new EditorGUI.DisabledScope(!CanPreview()))
            {
                string label = previewActive ? "停止预览并还原 Player" : "启动实时武器预览";
                if (GUILayout.Button(label, GUILayout.Height(32f)))
                {
                    if (previewActive) StopPreview();
                    else StartPreview();
                }
            }

            if (!previewActive) return;
            EditorGUILayout.HelpBox("无需进入 Play 模式。窗口预览、Scene 视图辅助线和运行时视角共用相同的腰射 / ADS 计算。关闭预览会还原 Player 预制体中的临时姿态和武器显示状态。",
                MessageType.None);
            if (previewMode != PreviewMode.Hip) DrawVisualNudgeControls();
        }

        private void DrawVisualNudgeControls()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("机瞄对齐辅助", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("在 ADS 或并排视图中，让真实机瞄中心对准黄色空心十字。方向按钮修改当前武器的“机瞄参考修正”；不会影响其他武器。",
                EditorStyles.wordWrappedMiniLabel);
            visualNudgeStep = EditorGUILayout.Slider("每次微调距离", visualNudgeStep, 0.0005f, 0.01f);
            showSightReferenceMarker = EditorGUILayout.Toggle("显示黄色机瞄参考", showSightReferenceMarker);
            if (showSightReferenceMarker)
                sightReferenceMarkerRadius = EditorGUILayout.Slider("参考标记大小", sightReferenceMarkerRadius, 0.002f, 0.03f);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("武器上移", GUILayout.Width(100f))) NudgeViewmodel(previewCamera.transform.up);
                GUILayout.FlexibleSpace();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("武器左移")) NudgeViewmodel(-previewCamera.transform.right);
                if (GUILayout.Button("武器右移")) NudgeViewmodel(previewCamera.transform.right);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("武器下移", GUILayout.Width(100f))) NudgeViewmodel(-previewCamera.transform.up);
                GUILayout.FlexibleSpace();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("武器后移")) NudgeViewmodel(-previewCamera.transform.forward);
                if (GUILayout.Button("武器前移")) NudgeViewmodel(previewCamera.transform.forward);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("重置机瞄参考修正")) SetVisualReferenceOffset(Vector3.zero, "Reset Visual Sight Reference");
                if (GUILayout.Button("居中 ADS 微调")) CentreAdsMicroOffset();
            }
            if (GUILayout.Button("确认当前武器机瞄位置正确")) SetVisualReview(true);
        }

        private void DrawStatus()
        {
            EditorGUILayout.LabelField("对齐状态", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("腰射：调整该武器的 Presentation Profile；ADS：调整该武器的 ADS Profile。投射命中始终从相机准心出发，机瞄只负责让视觉与准心重合。",
                EditorStyles.wordWrappedMiniLabel);
            if (!HasAdsPreview)
            {
                EditorGUILayout.HelpBox("当前武器为仅腰射配置：无需机瞄对齐、ADS Profile 或 Aim Anchor。", MessageType.Info);
                return;
            }
            if (!previewActive || !CanPreview() || previewMode == PreviewMode.Hip) return;
            if (!TryMeasureAlignment(out float screenErrorPixels))
            {
                EditorGUILayout.HelpBox("无法测量机瞄参考点：请确认 Aim Anchor 位于相机前方。", MessageType.Error);
                return;
            }

            bool cameraCentrePassed = screenErrorPixels <= ScreenTolerancePixels;
            bool ready = cameraCentrePassed && profile.VisualSightPlacementReviewed;
            string result = ready ? "校准完成" : "尚未完成";
            MessageType type = ready ? MessageType.Info : MessageType.Warning;
            string screenState = cameraCentrePassed ? "通过" : "偏离";
            string visualState = profile.VisualSightPlacementReviewed ? "已确认" : "待确认";
            EditorGUILayout.HelpBox(result + $"\n1. 机瞄参考 → 相机中心：{screenState}，误差 {screenErrorPixels:0.00}px（阈值 {ScreenTolerancePixels:0.00}px）" +
                $"\n2. 视觉机瞄位置：{visualState}" +
                $"\n3. 游戏命中路径：锁定相机准心，归零距离 {profile.ZeroDistance:0.0}m。", type);
        }

        private void StartPreview()
        {
            if (!CanPreview() || !PrepareSourceWeapon()) return;
            hipPosition = previewRig.transform.localPosition;
            hipRotation = previewRig.transform.localRotation;
            hasHipPose = true;
            previewActive = true;
            previewPosesPrimed = false;
            hasScenePreviewMode = false;
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
            previewPosesPrimed = false;
            hasScenePreviewMode = false;
            hasHipPose = false;
            RestoreSourcePresentation();
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
            // Live mode intentionally follows the same continuously advancing animation presentation as gameplay.
            // Freezing is an opt-in aid for inspecting one exact sight-alignment sample.
            bool advanceAnimation = !freezeAnimationForCalibration || !previewPosesPrimed;
            float animationDeltaTime = freezeAnimationForCalibration && advanceAnimation
                ? Mathf.Max(0.2f, deltaTime)
                : advanceAnimation ? deltaTime : 0f;
            PreviewMode sceneMode = previewMode == PreviewMode.Hip ? PreviewMode.Hip : PreviewMode.Ads;
            bool advanceSceneAnimation = advanceAnimation || !hasScenePreviewMode || lastScenePreviewMode != sceneMode;
            float sceneAnimationDeltaTime = advanceSceneAnimation
                ? freezeAnimationForCalibration ? Mathf.Max(0.2f, deltaTime) : deltaTime
                : 0f;
            ApplyPreviewPose(previewRig, previewCamera, sceneMode, hipPosition, hipRotation, sceneAnimationDeltaTime,
                advanceSceneAnimation);
            ApplyPreviewPose(cameraPreviewRig, cameraPreviewCamera, PreviewMode.Hip, cameraPreviewHipPosition,
                cameraPreviewHipRotation, animationDeltaTime, advanceAnimation);
            ApplyPreviewPose(adsPreviewRig, adsPreviewCamera, PreviewMode.Ads, adsPreviewHipPosition,
                adsPreviewHipRotation, animationDeltaTime, advanceAnimation);
            previewPosesPrimed = true;
            hasScenePreviewMode = true;
            lastScenePreviewMode = sceneMode;
            UpdateRuntimeCameraPreview();
            SceneView.RepaintAll();
            Repaint();
        }

        private void ApplyPreviewPose(LowPolyShooterViewmodelRig rig, Camera camera, PreviewMode mode,
            Vector3 authoredHipPosition, Quaternion authoredHipRotation, float deltaTime, bool advanceAnimation)
        {
            if (rig == null || camera == null) return;
            rig.transform.localPosition = authoredHipPosition + ResolveHipPositionOffset();
            rig.transform.localRotation = authoredHipRotation * Quaternion.Euler(ResolveHipRotationOffset());
            if (mode == PreviewMode.Hip)
            {
                if (advanceAnimation) rig.PreviewHipPose(deltaTime);
                return;
            }

            if (profile == null) return;
            if (advanceAnimation) rig.PreviewAimingPose(deltaTime);
            if (!WeaponAdsAlignment.TryGetCalibratedPose(rig.transform, rig.AimAnchor, rig.Muzzle, camera, profile,
                    out Vector3 localPosition, out Quaternion localRotation)) return;
            rig.transform.localPosition = localPosition;
            rig.transform.localRotation = localRotation;
        }

        private Vector3 ResolveHipPositionOffset()
        {
            return presentationProfile != null ? presentationProfile.ResolveHipPositionOffset(null) :
                profile != null ? profile.HipCameraSpacePositionOffset : Vector3.zero;
        }

        private Vector3 ResolveHipRotationOffset()
        {
            return presentationProfile != null ? presentationProfile.ResolveHipRotationOffset(null) :
                profile != null ? profile.HipCameraSpaceRotationOffset : Vector3.zero;
        }

        private void DrawRuntimeCameraPreview()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("运行时第一人称预览", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("黄色十字是屏幕中心准星。并排模式左侧为腰射，右侧为按住右键后的 ADS。",
                EditorStyles.wordWrappedMiniLabel);
            Rect previewRect = GUILayoutUtility.GetRect(360f, 220f, GUILayout.ExpandWidth(true));
            bool hipPreviewAvailable = cameraPreviewCamera != null && cameraPreviewRig != null;
            bool adsPreviewAvailable = adsPreviewCamera != null && adsPreviewRig != null;
            bool activePreviewAvailable = previewMode == PreviewMode.Hip ? hipPreviewAvailable
                : previewMode == PreviewMode.Ads ? adsPreviewAvailable
                : hipPreviewAvailable && adsPreviewAvailable;
            if (!activePreviewAvailable)
            {
                EditorGUI.HelpBox(previewRect, "正在创建隔离的第一人称预览…", MessageType.Info);
                return;
            }

            if (Event.current.type != EventType.Repaint) return;
            bool compare = previewMode == PreviewMode.Compare;
            EnsurePreviewTextures(previewRect, compare);
            if (compare)
            {
                float gap = 4f;
                Rect hipRect = new Rect(previewRect.x, previewRect.y, (previewRect.width - gap) * 0.5f, previewRect.height);
                Rect adsRect = new Rect(hipRect.xMax + gap, previewRect.y, hipRect.width, previewRect.height);
                DrawPreviewTile(hipRect, PreviewMode.Hip, hipPreviewTexture, "腰射");
                DrawPreviewTile(adsRect, PreviewMode.Ads, adsPreviewTexture, "右键 ADS");
            }
            else
            {
                PreviewMode mode = previewMode == PreviewMode.Hip ? PreviewMode.Hip : PreviewMode.Ads;
                RenderTexture texture = mode == PreviewMode.Hip ? hipPreviewTexture : adsPreviewTexture;
                DrawPreviewTile(previewRect, mode, texture, mode == PreviewMode.Hip ? "腰射" : "右键 ADS");
            }
        }

        private void DrawPreviewTile(Rect rect, PreviewMode mode, RenderTexture texture, string title)
        {
            if (texture == null) return;
            RenderRuntimeCameraPreview(mode, texture);
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
            DrawCameraCentreOverlay(rect);
            GUI.Label(new Rect(rect.x + 6f, rect.y + 5f, rect.width - 12f, 18f), title, EditorStyles.whiteMiniLabel);
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
            if (Application.isPlaying || previewRig == null || previewRig.transform.parent == null || previewCamera == null)
                return false;
            if (previewMode == PreviewMode.Hip) return true;
            if (!HasAdsPreview) return false;
            Transform muzzle = selectedViewmodel != null ? selectedViewmodel.Muzzle : previewRig.Muzzle;
            Transform aimAnchor = selectedViewmodel != null ? selectedViewmodel.AimAnchor : previewRig.AimAnchor;
            return muzzle != null && aimAnchor != null;
        }

        private bool HasAdsPreview => profile != null &&
            (selectedViewmodel == null || (selectedViewmodel.Muzzle != null && selectedViewmodel.AimAnchor != null));

        private void TryBindSelection()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null) return;
            LowPolyShooterViewmodelRig selectedRig = selected.GetComponentInParent<LowPolyShooterViewmodelRig>();
            if (selectedRig == null) selectedRig = selected.GetComponentInChildren<LowPolyShooterViewmodelRig>(true);
            if (selectedRig != null) BindRig(selectedRig);
        }

        private void BindRig(LowPolyShooterViewmodelRig newRig)
        {
            if (previewRig != newRig) StopPreview();
            previewRig = newRig;
            inventory = previewRig != null ? previewRig.GetComponentInParent<WeaponInventoryController>() : null;
            previewCamera = previewRig != null ? previewRig.GetComponentInParent<Camera>() : null;
            ResolveSelectedWeapon();
        }

        private void ResolveSelectedWeapon()
        {
            selectedViewmodel = null;
            presentationProfile = null;
            profile = null;
            if (inventory != null && inventory.TryGetViewmodelSlot(selectedSlot, out WeaponViewmodelSlot slot))
            {
                selectedViewmodel = slot;
                presentationProfile = slot.PresentationProfile;
                profile = presentationProfile != null && presentationProfile.DefaultAdsProfile != null
                    ? presentationProfile.DefaultAdsProfile
                    : slot.AdsProfile;
                return;
            }

            if (previewRig == null) return;
            presentationProfile = previewRig.PresentationProfile;
            profile = presentationProfile != null && presentationProfile.DefaultAdsProfile != null
                ? presentationProfile.DefaultAdsProfile
                : previewRig.AdsProfile;
        }

        private string GetWeaponDisplayName()
        {
            string slotName = selectedSlot == WeaponInventorySlot.Primary ? "主武器" : "副武器";
            return selectedViewmodel != null && selectedViewmodel.VisualRoot != null
                ? slotName + "  ·  " + selectedViewmodel.VisualRoot.name
                : slotName;
        }

        private bool PrepareSourceWeapon()
        {
            if (previewRig == null || inventory == null || selectedViewmodel == null) return true;
            CaptureSourcePresentation();
            return ConfigureRigForSlot(previewRig, inventory, selectedSlot);
        }

        private void CaptureSourcePresentation()
        {
            if (sourcePresentationCaptured || previewRig == null) return;
            sourcePresentationCaptured = true;
            originalArmsController = previewRig.ArmsController;
            originalWeaponAnimator = previewRig.WeaponAnimator;
            originalMuzzle = previewRig.Muzzle;
            originalAimAnchor = previewRig.AimAnchor;
            originalMagazine = previewRig.Magazine;
            originalAdsProfile = previewRig.AdsProfile;
            originalPresentationProfile = previewRig.PresentationProfile;
            if (inventory != null)
            {
                inventory.TryGetViewmodelSlot(WeaponInventorySlot.Primary, out WeaponViewmodelSlot primary);
                inventory.TryGetViewmodelSlot(WeaponInventorySlot.Secondary, out WeaponViewmodelSlot secondary);
                sourcePrimaryRoot = primary != null ? primary.VisualRoot : null;
                sourceSecondaryRoot = secondary != null ? secondary.VisualRoot : null;
                sourcePrimaryWasActive = sourcePrimaryRoot != null && sourcePrimaryRoot.gameObject.activeSelf;
                sourceSecondaryWasActive = sourceSecondaryRoot != null && sourceSecondaryRoot.gameObject.activeSelf;
            }
        }

        private void RestoreSourcePresentation()
        {
            if (!sourcePresentationCaptured) return;
            if (sourcePrimaryRoot != null) sourcePrimaryRoot.gameObject.SetActive(sourcePrimaryWasActive);
            if (sourceSecondaryRoot != null) sourceSecondaryRoot.gameObject.SetActive(sourceSecondaryWasActive);
            if (previewRig != null)
            {
                previewRig.ConfigureWeaponPresentation(originalArmsController, originalWeaponAnimator, originalMuzzle,
                    originalAimAnchor, originalMagazine, originalAdsProfile, originalPresentationProfile);
                previewRig.ResetPreviewPose();
            }
            sourcePresentationCaptured = false;
            originalArmsController = null;
            originalWeaponAnimator = null;
            originalMuzzle = null;
            originalAimAnchor = null;
            originalMagazine = null;
            originalAdsProfile = null;
            originalPresentationProfile = null;
            sourcePrimaryRoot = null;
            sourceSecondaryRoot = null;
        }

        private static bool ConfigureRigForSlot(LowPolyShooterViewmodelRig rig, WeaponInventoryController sourceInventory,
            WeaponInventorySlot slot)
        {
            if (rig == null || sourceInventory == null || !sourceInventory.TryGetViewmodelSlot(slot, out WeaponViewmodelSlot selected))
                return false;
            sourceInventory.TryGetViewmodelSlot(WeaponInventorySlot.Primary, out WeaponViewmodelSlot primary);
            sourceInventory.TryGetViewmodelSlot(WeaponInventorySlot.Secondary, out WeaponViewmodelSlot secondary);
            if (primary != null && primary.VisualRoot != null)
                primary.VisualRoot.gameObject.SetActive(slot == WeaponInventorySlot.Primary);
            if (secondary != null && secondary.VisualRoot != null)
                secondary.VisualRoot.gameObject.SetActive(slot == WeaponInventorySlot.Secondary);
            rig.ConfigureWeaponPresentation(selected.ArmsController, selected.WeaponAnimator, selected.Muzzle,
                selected.AimAnchor, selected.Magazine, selected.AdsProfile, selected.PresentationProfile);
            return true;
        }

        private void CreateRuntimeCameraPreview()
        {
            DisposeRuntimeCameraPreview();
            if (!CanPreview()) return;

            GameObject playerRoot = previewRig.transform.root.gameObject;
            if (!CreatePreviewInstance(playerRoot, "Hip", Vector3.right * 1000f, out cameraPreviewPlayer,
                    out cameraPreviewRig, out cameraPreviewCameraObject, out cameraPreviewCamera,
                    out cameraPreviewHipPosition, out cameraPreviewHipRotation) ||
                !CreatePreviewInstance(playerRoot, "ADS", Vector3.right * 2000f, out adsPreviewPlayer,
                    out adsPreviewRig, out adsPreviewCameraObject, out adsPreviewCamera,
                    out adsPreviewHipPosition, out adsPreviewHipRotation))
            {
                DisposeRuntimeCameraPreview();
                return;
            }

            CreatePreviewLights();
        }

        private bool CreatePreviewInstance(GameObject playerRoot, string poseName, Vector3 worldOffset,
            out GameObject previewPlayer, out LowPolyShooterViewmodelRig previewModelRig,
            out GameObject previewCameraObject, out Camera previewCameraComponent, out Vector3 authoredHipPosition,
            out Quaternion authoredHipRotation)
        {
            previewPlayer = Object.Instantiate(playerRoot);
            previewPlayer.name = "Weapon Workbench " + poseName + " Preview Player";
            previewPlayer.hideFlags = HideFlags.HideAndDontSave;
            previewPlayer.transform.position += worldOffset;
            previewModelRig = previewPlayer.GetComponentInChildren<LowPolyShooterViewmodelRig>(true);
            WeaponInventoryController previewInventory = previewPlayer.GetComponentInChildren<WeaponInventoryController>(true);
            Camera sourceCamera = previewModelRig != null ? previewModelRig.GetComponentInParent<Camera>() : null;
            if (previewModelRig == null || sourceCamera == null ||
                (previewInventory != null && !ConfigureRigForSlot(previewModelRig, previewInventory, selectedSlot)))
            {
                if (previewPlayer != null) Object.DestroyImmediate(previewPlayer);
                previewPlayer = null;
                previewModelRig = null;
                previewCameraObject = null;
                previewCameraComponent = null;
                authoredHipPosition = Vector3.zero;
                authoredHipRotation = Quaternion.identity;
                return false;
            }

            previewModelRig.ResetPreviewPose();
            authoredHipPosition = previewModelRig.transform.localPosition;
            authoredHipRotation = previewModelRig.transform.localRotation;
            SetLayerRecursively(previewModelRig.transform, CombatLayers.ViewmodelLayer);
            foreach (Camera camera in previewPlayer.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            foreach (AudioListener listener in previewPlayer.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;

            previewCameraObject = new GameObject("Weapon Workbench " + poseName + " Preview Camera", typeof(Camera),
                typeof(UniversalAdditionalCameraData));
            previewCameraObject.hideFlags = HideFlags.HideAndDontSave;
            previewCameraComponent = previewCameraObject.GetComponent<Camera>();
            previewCameraComponent.CopyFrom(sourceCamera);
            previewCameraComponent.transform.SetPositionAndRotation(sourceCamera.transform.position, sourceCamera.transform.rotation);
            previewCameraComponent.nearClipPlane = 0.01f;
            previewCameraComponent.farClipPlane = 10f;
            previewCameraComponent.clearFlags = CameraClearFlags.SolidColor;
            previewCameraComponent.backgroundColor = new Color(0.025f, 0.035f, 0.055f, 1f);
            previewCameraComponent.cullingMask = 1 << CombatLayers.ViewmodelLayer;
            previewCameraComponent.enabled = false;
            UniversalAdditionalCameraData data = previewCameraObject.GetComponent<UniversalAdditionalCameraData>();
            data.renderType = CameraRenderType.Base;
            data.renderPostProcessing = false;
            UniversalAdditionalCameraData sourceData = sourceCamera.GetComponent<UniversalAdditionalCameraData>();
            if (sourceData != null)
            {
                data.volumeLayerMask = sourceData.volumeLayerMask;
                data.volumeTrigger = sourceData.volumeTrigger;
            }
            return true;
        }

        private void UpdateRuntimeCameraPreview()
        {
            UpdatePreviewCamera(cameraPreviewRig, cameraPreviewCamera);
            UpdatePreviewCamera(adsPreviewRig, adsPreviewCamera);
        }

        private static void UpdatePreviewCamera(LowPolyShooterViewmodelRig rig, Camera previewCameraComponent)
        {
            if (rig == null || previewCameraComponent == null) return;
            Transform playerCamera = rig.GetComponentInParent<Camera>()?.transform;
            if (playerCamera != null)
                previewCameraComponent.transform.SetPositionAndRotation(playerCamera.position, playerCamera.rotation);
        }

        private void EnsurePreviewTextures(Rect previewRect, bool compare)
        {
            int tileWidth = Mathf.Max(1, Mathf.CeilToInt((compare ? previewRect.width * 0.5f : previewRect.width) * 2f));
            int height = Mathf.Max(1, Mathf.CeilToInt(previewRect.height * 2f));
            EnsurePreviewTexture(ref hipPreviewTexture, tileWidth, height, "Weapon Workbench Hip Preview");
            EnsurePreviewTexture(ref adsPreviewTexture, tileWidth, height, "Weapon Workbench ADS Preview");
            if (cameraPreviewCamera != null)
            {
                cameraPreviewCamera.aspect = (float)tileWidth / height;
                cameraPreviewCamera.ResetProjectionMatrix();
            }
            if (adsPreviewCamera != null)
            {
                adsPreviewCamera.aspect = (float)tileWidth / height;
                adsPreviewCamera.ResetProjectionMatrix();
            }
        }

        private static void EnsurePreviewTexture(ref RenderTexture texture, int width, int height, string textureName)
        {
            if (texture != null && texture.width == width && texture.height == height) return;
            if (texture != null) Object.DestroyImmediate(texture);
            texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = textureName,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.Create();
        }

        private void RenderRuntimeCameraPreview(PreviewMode mode, RenderTexture destination)
        {
            Camera previewCameraComponent = mode == PreviewMode.Ads ? adsPreviewCamera : cameraPreviewCamera;
            LowPolyShooterViewmodelRig previewModelRig = mode == PreviewMode.Ads ? adsPreviewRig : cameraPreviewRig;
            if (previewCameraComponent == null || previewModelRig == null || destination == null ||
                (mode == PreviewMode.Ads && profile == null)) return;
            float gameplayFov = previewCamera != null ? previewCamera.fieldOfView : 78f;
            if (mode == PreviewMode.Ads && profile != null) gameplayFov = Mathf.Max(1f, gameplayFov - profile.FovReduction);
            previewCameraComponent.fieldOfView = ViewmodelCameraRenderer.CalculatePresentationFieldOfView(gameplayFov);
            previewCameraComponent.SubmitRenderRequest(new UniversalRenderPipeline.SingleCameraRequest { destination = destination });
        }

        private void DisposeRuntimeCameraPreview()
        {
            if (cameraPreviewPlayer != null) Object.DestroyImmediate(cameraPreviewPlayer);
            cameraPreviewPlayer = null;
            cameraPreviewRig = null;
            if (adsPreviewPlayer != null) Object.DestroyImmediate(adsPreviewPlayer);
            adsPreviewPlayer = null;
            adsPreviewRig = null;
            foreach (Light light in cameraPreviewLights)
                if (light != null) Object.DestroyImmediate(light.gameObject);
            for (int i = 0; i < cameraPreviewLights.Length; i++) cameraPreviewLights[i] = null;
            if (cameraPreviewCameraObject != null) Object.DestroyImmediate(cameraPreviewCameraObject);
            cameraPreviewCameraObject = null;
            cameraPreviewCamera = null;
            if (adsPreviewCameraObject != null) Object.DestroyImmediate(adsPreviewCameraObject);
            adsPreviewCameraObject = null;
            adsPreviewCamera = null;
            if (hipPreviewTexture != null) Object.DestroyImmediate(hipPreviewTexture);
            hipPreviewTexture = null;
            if (adsPreviewTexture != null) Object.DestroyImmediate(adsPreviewTexture);
            adsPreviewTexture = null;
        }

        private void CreatePreviewLights()
        {
            CreatePreviewLight(0, 1.25f, Quaternion.Euler(35f, -30f, 0f));
            CreatePreviewLight(1, 0.85f, Quaternion.Euler(340f, 145f, 0f));
        }

        private void CreatePreviewLight(int index, float intensity, Quaternion rotation)
        {
            GameObject lightObject = new GameObject("Weapon Workbench Preview Light", typeof(Light));
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
            foreach (Transform child in root) SetLayerRecursively(child, layer);
        }

        private void DrawSceneGuides(SceneView sceneView)
        {
            if (!previewActive || !CanPreview() || previewMode == PreviewMode.Hip) return;
            Vector3 cameraPosition = previewCamera.transform.position;
            Vector3 aimEnd = cameraPosition + previewCamera.transform.forward * 3f;
            Handles.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            Handles.DrawAAPolyLine(3f, cameraPosition, aimEnd);
            Handles.DrawWireDisc(aimEnd, previewCamera.transform.forward, 0.04f);
            Handles.Label(aimEnd, " CAMERA AIM");

            Vector3 sightReference = WeaponAdsAlignment.GetSightReferenceWorldPosition(previewRig.AimAnchor, profile);
            if (showSightReferenceMarker)
            {
                Handles.color = new Color(1f, 0.78f, 0.2f, 0.62f);
                Vector3 markerRight = previewCamera.transform.right * sightReferenceMarkerRadius;
                Vector3 markerUp = previewCamera.transform.up * sightReferenceMarkerRadius;
                Handles.DrawWireDisc(sightReference, previewCamera.transform.forward, sightReferenceMarkerRadius);
                Handles.DrawLine(sightReference - markerRight, sightReference + markerRight);
                Handles.DrawLine(sightReference - markerUp, sightReference + markerUp);
                Handles.Label(sightReference + markerUp * 1.4f, " SIGHT REFERENCE");
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
            if (!CanPreview() || desiredWorldMovement.sqrMagnitude < 0.000001f || previewRig.AimAnchor == null) return;
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
            TickPreview();
        }

        private void CentreAdsMicroOffset()
        {
            if (profile == null) return;
            Undo.RecordObject(profile, "Centre ADS Micro Offset");
            SerializedObject serializedProfile = new SerializedObject(profile);
            SerializedProperty positionOffset = serializedProfile.FindProperty("cameraSpacePositionOffset");
            Vector3 offset = positionOffset.vector3Value;
            offset.x = 0f;
            offset.y = 0f;
            positionOffset.vector3Value = offset;
            serializedProfile.FindProperty("visualSightPlacementReviewed").boolValue = false;
            serializedProfile.ApplyModifiedProperties();
            EditorUtility.SetDirty(profile);
            TickPreview();
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
