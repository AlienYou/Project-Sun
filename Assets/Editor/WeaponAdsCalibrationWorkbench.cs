using System.Collections.Generic;
using ProjectSun.FPS.Core;
using ProjectSun.FPS.Presentation;
using ProjectSun.FPS.UI;
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
        private const string LoadoutCatalogPath = "Assets/_ProjectSun/Data/Weapons/Catalogs/AR4LoadoutCatalog.asset";
        private const float ScreenTolerancePixels = 2f;
        private const float LensPreciseTolerancePixels = 2f;
        private const float LensReferenceWidthPixels = 1920f;
        private const float LensReferenceHeightPixels = 1080f;
        private const float LensCentreToleranceViewport = 0.015f;
        private const float LensAxisToleranceDegrees = 3f;
        private const float LensMinimumViewportDiameter = 0.02f;
        private const float LensMaximumViewportDiameter = 0.95f;
        private const double AttachmentPrefabAutosaveDelaySeconds = 0.45d;

        private enum PreviewMode { Hip, Ads, Compare }
        private enum ScopeLensCentreGrade { Precise, Acceptable, Failed }
        private enum AttachmentSceneEditMode
        {
            None,
            ModelPosition,
            ModelRotation,
            AimAnchorPosition,
            AimAnchorRotation,
            LensPosition,
            LensRotation,
            LensAperture
        }

        private static readonly string[] AttachmentSceneEditLabels =
        {
            "关闭",
            "移动附件整体",
            "旋转附件整体",
            "移动 ADS 瞄准基准",
            "旋转 ADS 光轴",
            "移动 LensAnchor",
            "旋转镜片平面",
            "调整镜片有效口径"
        };

        [SerializeField] private WeaponInventorySlot selectedSlot = WeaponInventorySlot.Primary;
        [SerializeField] private WeaponAttachment selectedOptic;
        [SerializeField] private bool lockPlayerSession = true;
        [SerializeField] private bool showAttachmentCalibration = true;
        [SerializeField] private bool showHipPresentation = true;
        [SerializeField] private bool showAdsConfiguration = true;
        [SerializeField] private bool showAttachmentGeometryRepair;
        [SerializeField] private bool showScopeLensCalibration = true;
        [SerializeField] private bool showScopeLensRenderingConfiguration = true;
        [SerializeField] private bool showScopePeripheralConfiguration;
        [SerializeField] private bool showScopeLensValidationDetails = true;
        [SerializeField] private bool showScopeLensGuides = true;
        [SerializeField] private PreviewMode previewMode = PreviewMode.Compare;
        [SerializeField] private Vector2 scrollPosition;
        [SerializeField] private bool freezeAnimationForCalibration;
        [SerializeField, Min(0.00001f)] private float visualNudgeStep = 0.002f;
        [SerializeField, Range(0.005f, 0.08f)] private float viewmodelSafetyMargin = 0.015f;
        [SerializeField] private bool showAdvancedSettings;
        [SerializeField] private bool showSightReferenceMarker = true;
        [SerializeField, Range(0.002f, 0.03f)] private float sightReferenceMarkerRadius = 0.008f;
        [SerializeField] private bool showClipProbeGuides = true;
        [SerializeField] private bool showClipProbeDetails;
        [SerializeField] private AttachmentSceneEditMode attachmentSceneEditMode = AttachmentSceneEditMode.AimAnchorPosition;

        private LowPolyShooterViewmodelRig previewRig;
        private WeaponInventoryController inventory;
        private WeaponViewmodelSlot selectedViewmodel;
        private WeaponPresentationProfile presentationProfile;
        private WeaponAdsProfile profile;
        private WeaponLoadout previewLoadout;
        private readonly List<WeaponAttachment> availableOptics = new List<WeaponAttachment>();
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
        private WeaponAttachmentViewmodelPresenter sourceAttachmentPresenter;

        // Kept alive for the active attachment editing session so Unity Undo retains a stable prefab-content target.
        private GameObject loadedAttachmentPrefabContents;
        private string loadedAttachmentPrefabPath;
        private WeaponAttachmentViewmodelVisual loadedAttachmentVisual;
        private bool attachmentPrefabSavePending;
        private double attachmentPrefabAutosaveAt;
        private int attachmentSceneUndoGroup = -1;
        private bool attachmentSceneDragActive;
        private AttachmentSceneEditMode attachmentSceneDragMode = AttachmentSceneEditMode.None;

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
        private readonly List<ClipProbeMeasurement> hipClipProbeMeasurements = new List<ClipProbeMeasurement>();
        private readonly List<ClipProbeMeasurement> adsClipProbeMeasurements = new List<ClipProbeMeasurement>();

        private readonly struct ClipProbeMeasurement
        {
            public readonly ViewmodelClipProbe Probe;
            public readonly float Clearance;
            public readonly bool IsInFrontOfCamera;

            public ClipProbeMeasurement(ViewmodelClipProbe probe, float clearance, bool isInFrontOfCamera)
            {
                Probe = probe;
                Clearance = clearance;
                IsInFrontOfCamera = isInFrontOfCamera;
            }
        }

        private readonly struct CalibrationDependentPose
        {
            public readonly Transform Transform;
            public readonly Vector3 LocalPosition;
            public readonly Quaternion LocalRotation;

            public CalibrationDependentPose(Transform transform, Vector3 localPosition, Quaternion localRotation)
            {
                Transform = transform;
                LocalPosition = localPosition;
                LocalRotation = localRotation;
            }
        }

        private readonly struct ScopeLensMeasurement
        {
            public readonly ViewmodelScopeLens Lens;
            public readonly Vector3 ViewportCentre;
            public readonly float ViewportDiameter;
            public readonly float CentreError;
            public readonly Vector2 CentreOffsetPixels;
            public readonly float CentrePixelError;
            public readonly float CameraAxisError;
            public readonly float SightAxisError;
            public readonly bool HasSightAxisMeasurement;
            public readonly bool IsInFrontOfCamera;

            public ScopeLensMeasurement(ViewmodelScopeLens lens, Vector3 viewportCentre, float viewportDiameter,
                float centreError, Vector2 centreOffsetPixels, float centrePixelError, float cameraAxisError,
                float sightAxisError, bool hasSightAxisMeasurement, bool isInFrontOfCamera)
            {
                Lens = lens;
                ViewportCentre = viewportCentre;
                ViewportDiameter = viewportDiameter;
                CentreError = centreError;
                CentreOffsetPixels = centreOffsetPixels;
                CentrePixelError = centrePixelError;
                CameraAxisError = cameraAxisError;
                SightAxisError = sightAxisError;
                HasSightAxisMeasurement = hasSightAxisMeasurement;
                IsInFrontOfCamera = isInFrontOfCamera;
            }
        }

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
            Undo.undoRedoPerformed += HandleAttachmentPrefabUndoRedo;
            previousTickTime = EditorApplication.timeSinceStartup;
        }

        private void OnDisable()
        {
            EditorApplication.update -= TickPreview;
            SceneView.duringSceneGui -= DrawSceneGuides;
            Undo.undoRedoPerformed -= HandleAttachmentPrefabUndoRedo;
            if (attachmentSceneUndoGroup >= 0)
            {
                Undo.CollapseUndoOperations(attachmentSceneUndoGroup);
                attachmentSceneUndoGroup = -1;
            }
            attachmentSceneDragActive = false;
            attachmentSceneDragMode = AttachmentSceneEditMode.None;
            StopPreview();
            ReleaseAttachmentPrefabContents();
        }

        private void OnSelectionChange()
        {
            // Keep the Player binding stable while a designer opens an attachment prefab or selects its Model node.
            // The workbench only follows Selection again when explicitly unlocked.
            if (!previewActive && (!lockPlayerSession || previewRig == null)) TryBindSelection();
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

            lockPlayerSession = EditorGUILayout.ToggleLeft("锁定当前 Player 工作会话", lockPlayerSession);
            EditorGUILayout.LabelField(lockPlayerSession
                ? "已锁定：打开附件预制体或选择其 Model 节点，当前 Player 预览仍会保持绑定。"
                : "跟随选择：选择新的 Player 或 Viewmodel Rig 时，工作台会自动重新绑定。",
                EditorStyles.miniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("使用当前选择"))
                {
                    TryBindSelection();
                    lockPlayerSession = previewRig != null;
                }
                if (GUILayout.Button("打开 Player 预制体"))
                {
                    GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
                    if (playerPrefab != null) AssetDatabase.OpenAsset(playerPrefab);
                }
            }

            EditorGUILayout.Space(8f);
            DrawWeaponSelector();
            EditorGUILayout.Space(8f);
            DrawAttachmentSelector();
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
                ReleaseAttachmentPrefabContents();
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

        private void DrawAttachmentSelector()
        {
            showAttachmentCalibration = EditorGUILayout.Foldout(showAttachmentCalibration, "动态配件校准", true,
                EditorStyles.foldoutHeader);
            if (!showAttachmentCalibration) return;
            if (selectedViewmodel == null || previewLoadout == null || previewLoadout.Weapon == null)
            {
                EditorGUILayout.HelpBox("当前武器没有可用于配件预览的 WeaponDefinition。请先选择 Player 或其第一人称 Viewmodel。",
                    MessageType.Warning);
                return;
            }

            string[] labels = new string[availableOptics.Count + 1];
            labels[0] = "武器默认瞄具（不装配）";
            int selectedIndex = 0;
            for (int index = 0; index < availableOptics.Count; index++)
            {
                WeaponAttachment optic = availableOptics[index];
                labels[index + 1] = optic != null ? optic.displayName : "Missing Optic";
                if (optic == selectedOptic) selectedIndex = index + 1;
            }

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup("校准瞄具", selectedIndex, labels);
            if (EditorGUI.EndChangeCheck())
            {
                bool restartPreview = previewActive;
                if (restartPreview) StopPreview();
                ReleaseAttachmentPrefabContents();
                selectedOptic = newIndex > 0 ? availableOptics[newIndex - 1] : null;
                ResolveSelectedWeapon();
                if (restartPreview) StartPreview();
            }

            if (selectedOptic == null)
            {
                EditorGUILayout.HelpBox("当前预览使用武器原生 ADS 瞄准基准（AimAnchor）。选择一个已绑定第一人称外观的瞄具后，工作台会复用运行时装配链路。",
                    MessageType.Info);
                return;
            }

            if (!TryGetSelectedOpticVisual(out WeaponAttachmentViewmodelVisual visual))
            {
                EditorGUILayout.HelpBox("该瞄具没有当前武器可用的第一人称视觉绑定，不能进行视觉校准。", MessageType.Error);
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("运行时预制体", visual.Prefab, typeof(GameObject), false);
                EditorGUILayout.ObjectField("生效 ADS Profile", profile, typeof(WeaponAdsProfile), false);
            }
            DrawAttachmentPrefabEditors(visual);
            DrawLiveAttachmentAlignmentStatus();
        }

        private void DrawAttachmentPrefabEditors(WeaponAttachmentViewmodelVisual visual)
        {
            if (visual == null || visual.Prefab == null || string.IsNullOrWhiteSpace(visual.AimAnchorName)) return;
            string prefabPath = AssetDatabase.GetAssetPath(visual.Prefab);
            if (string.IsNullOrWhiteSpace(prefabPath)) return;
            if (!TryGetAttachmentPrefabContents(visual, prefabPath, out GameObject prefabContents)) return;
            DrawAttachmentPrefabEditorsInPlace(visual, prefabContents, prefabPath);
            DrawAttachmentSceneEditControls();
            DrawAttachmentPrefabSaveControls();
        }

        private bool TryGetAttachmentPrefabContents(WeaponAttachmentViewmodelVisual visual, string prefabPath,
            out GameObject prefabContents)
        {
            prefabContents = null;
            if (loadedAttachmentPrefabContents != null && loadedAttachmentPrefabPath == prefabPath)
            {
                loadedAttachmentVisual = visual;
                prefabContents = loadedAttachmentPrefabContents;
                return true;
            }

            ReleaseAttachmentPrefabContents();
            loadedAttachmentPrefabContents = PrefabUtility.LoadPrefabContents(prefabPath);
            loadedAttachmentPrefabPath = loadedAttachmentPrefabContents != null ? prefabPath : null;
            loadedAttachmentVisual = loadedAttachmentPrefabContents != null ? visual : null;
            prefabContents = loadedAttachmentPrefabContents;
            return prefabContents != null;
        }

        private void ReleaseAttachmentPrefabContents()
        {
            SaveAttachmentPrefabContentsNow();
            if (loadedAttachmentPrefabContents != null)
                PrefabUtility.UnloadPrefabContents(loadedAttachmentPrefabContents);
            loadedAttachmentPrefabContents = null;
            loadedAttachmentPrefabPath = null;
            loadedAttachmentVisual = null;
            attachmentPrefabSavePending = false;
        }

        private void HandleAttachmentPrefabUndoRedo()
        {
            if (loadedAttachmentPrefabContents == null || loadedAttachmentVisual == null ||
                string.IsNullOrWhiteSpace(loadedAttachmentPrefabPath)) return;

            SaveAttachmentPrefabContentsNow(true);
            SynchronizeLoadedAttachmentPrefabToLivePreviews();
            Repaint();
        }

        private void SynchronizeLoadedAttachmentPrefabToLivePreviews()
        {
            if (!previewActive || loadedAttachmentPrefabContents == null || loadedAttachmentVisual == null) return;
            Transform aimAnchor = FindDescendant(loadedAttachmentPrefabContents.transform,
                loadedAttachmentVisual.AimAnchorName);
            Transform calibrationTarget = FindAttachmentCalibrationTransform(loadedAttachmentPrefabContents.transform);
            ViewmodelScopeLens scopeLens = FindScopeLens(loadedAttachmentPrefabContents.transform);
            AdsSightReference sightReference = aimAnchor != null ? aimAnchor.GetComponent<AdsSightReference>() : null;
            if (aimAnchor != null)
                ApplyAttachmentPrefabEditsToLivePreviews(loadedAttachmentVisual, true, aimAnchor.localPosition,
                    aimAnchor.localEulerAngles, sightReference != null && sightReference.OrientationAuthored,
                    calibrationTarget != null, calibrationTarget != null ? calibrationTarget.localPosition : Vector3.zero,
                    calibrationTarget != null ? calibrationTarget.localEulerAngles : Vector3.zero,
                    calibrationTarget != null ? calibrationTarget.localScale : Vector3.one);
            if (scopeLens != null)
                ApplyScopeLensPrefabEditsToLivePreviews(loadedAttachmentVisual, scopeLens.transform.localPosition,
                    scopeLens.transform.localEulerAngles, scopeLens.ClearApertureDiameter,
                    scopeLens.OrientationAuthored, scopeLens.VisualPlacementReviewed);
        }

        private void DrawAttachmentSceneEditControls()
        {
            EditorGUILayout.LabelField("Scene 直接编辑", EditorStyles.miniBoldLabel);
            attachmentSceneEditMode = (AttachmentSceneEditMode)EditorGUILayout.Popup("编辑对象",
                (int)attachmentSceneEditMode, AttachmentSceneEditLabels);
            if (attachmentSceneDragActive)
                EditorGUILayout.LabelField($"正在拖拽：{AttachmentSceneEditLabels[(int)attachmentSceneDragMode]}；松开鼠标后统一保存。",
                    EditorStyles.miniLabel);
            if (attachmentSceneEditMode == AttachmentSceneEditMode.None) return;
            EditorGUILayout.HelpBox(previewActive
                    ? "切到 Scene 视图，拖拽当前显示的彩色手柄即可直接校准。整体挂载用蓝/绿色，ADS 基准用黄色，LensAnchor 与口径用青色；松开鼠标后可用 Ctrl+Z 撤销整次拖拽。"
                    : "启动实时武器预览后，Scene 视图才会显示所选附件的直接编辑手柄。",
                MessageType.None);
        }

        private void DrawAttachmentPrefabSaveControls()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(attachmentPrefabSavePending
                    ? "预览已更新；停止编辑 0.45 秒后自动保存。"
                    : "附件模板已保存。", EditorStyles.miniLabel);
                if (GUILayout.Button("立即保存", GUILayout.Width(84f))) SaveAttachmentPrefabContentsNow(true);
            }
        }

        private void ScheduleAttachmentPrefabSave()
        {
            attachmentPrefabSavePending = true;
            attachmentPrefabAutosaveAt = EditorApplication.timeSinceStartup + AttachmentPrefabAutosaveDelaySeconds;
        }

        private void FlushDeferredAttachmentPrefabSave()
        {
            if (attachmentSceneDragActive) return;
            if (attachmentPrefabSavePending && EditorApplication.timeSinceStartup >= attachmentPrefabAutosaveAt)
                SaveAttachmentPrefabContentsNow();
        }

        private void SaveAttachmentPrefabContentsNow(bool force = false)
        {
            if ((!force && !attachmentPrefabSavePending) || loadedAttachmentPrefabContents == null ||
                string.IsNullOrWhiteSpace(loadedAttachmentPrefabPath)) return;
            PrefabUtility.SaveAsPrefabAsset(loadedAttachmentPrefabContents, loadedAttachmentPrefabPath);
            AssetDatabase.SaveAssets();
            attachmentPrefabSavePending = false;
        }

        private void DrawAttachmentPrefabEditorsInPlace(WeaponAttachmentViewmodelVisual visual,
            GameObject prefabContents, string prefabPath)
        {
            Transform aimAnchor = FindDescendant(prefabContents.transform, visual.AimAnchorName);
            if (aimAnchor == null)
            {
                EditorGUILayout.HelpBox($"预制体缺少 ADS 瞄准基准（AimAnchor）：{visual.AimAnchorName}", MessageType.Error);
                return;
            }

            Transform calibrationTarget = FindAttachmentCalibrationTransform(prefabContents.transform);
            bool hasCalibrationRoot = calibrationTarget != null &&
                calibrationTarget.GetComponent<ViewmodelAttachmentCalibrationRoot>() != null;
            EditorGUILayout.HelpBox(
                "这里直接编辑运行时动态装配所使用的附件模板。保存后当前 Player 预览会即时更新，不会重新切枪；打开预制体检查 Model 也不会失去本次工作会话。",
                MessageType.Info);

            EditorGUILayout.LabelField("ADS 瞄准基准（附件本地）", EditorStyles.miniBoldLabel);
            AdsSightReference sightReference = aimAnchor.GetComponent<AdsSightReference>();
            EditorGUI.BeginChangeCheck();
            Vector3 newAnchorPosition = EditorGUILayout.Vector3Field("ADS 瞄准基准位置", aimAnchor.localPosition);
            Vector3 newAnchorRotation = EditorGUILayout.Vector3Field("ADS 光轴旋转", aimAnchor.localEulerAngles);
            bool orientationAuthored = EditorGUILayout.Toggle("使用已制作光轴", sightReference != null &&
                sightReference.OrientationAuthored);
            bool anchorChanged = EditorGUI.EndChangeCheck();
            if (sightReference == null)
                EditorGUILayout.HelpBox("当前仍使用兼容模式：方向由 ADS 瞄准基准到枪口的连线推导。请先按当前视觉结果播种光轴。",
                    MessageType.Warning);
            else if (sightReference.OrientationAuthored)
                EditorGUILayout.HelpBox("ADS 光轴姿态已制作：位置为理想观察参考，本地 +Z 指向目标，本地 +Y 为瞄具上方。",
                    MessageType.Info);
            using (new EditorGUI.DisabledScope(!previewActive))
                if (GUILayout.Button("按当前视觉结果播种 ADS 光轴"))
                {
                    SeedAuthoredSightOrientation(visual, aimAnchor, calibrationTarget);
                    return;
                }

            Vector3 newModelPosition = Vector3.zero;
            Vector3 newModelRotation = Vector3.zero;
            Vector3 newModelScale = Vector3.one;
            bool modelChanged = false;
            if (calibrationTarget != null)
            {
                EditorGUILayout.Space(3f);
                EditorGUILayout.LabelField("附件整体挂载校准", EditorStyles.miniBoldLabel);
                if (!hasCalibrationRoot)
                    EditorGUILayout.HelpBox("旧附件尚无 AttachmentCalibrationRoot；整体调整会用兼容路径同步锚点与探针。建议执行资源迁移工具。",
                        MessageType.Warning);
                else
                    EditorGUILayout.HelpBox("统一校准根已生效：整体移动、旋转或缩放会同时带动模型、ADS 光轴、镜片与 Clip Probe。",
                        MessageType.Info);
                EditorGUI.BeginChangeCheck();
                newModelPosition = EditorGUILayout.Vector3Field("整体位置", calibrationTarget.localPosition);
                newModelRotation = EditorGUILayout.Vector3Field("整体旋转", calibrationTarget.localEulerAngles);
                newModelScale = EditorGUILayout.Vector3Field("整体缩放", calibrationTarget.localScale);
                modelChanged = EditorGUI.EndChangeCheck();
            }
            else
            {
                EditorGUILayout.HelpBox("该附件模板没有根级 Model 节点；请用资源准备工具生成标准附件模板后再校准。",
                    MessageType.Warning);
            }

            if (anchorChanged || modelChanged)
                ApplyAttachmentPrefabAssetEdits(visual, aimAnchor, calibrationTarget, anchorChanged, newAnchorPosition,
                    newAnchorRotation, orientationAuthored, modelChanged, newModelPosition, newModelRotation,
                    newModelScale);

            DrawScopeLensPrefabEditor(visual, prefabContents.transform, aimAnchor);
        }

        private void DrawScopeLensPrefabEditor(WeaponAttachmentViewmodelVisual visual, Transform prefabRoot,
            Transform aimAnchor)
        {
            OpticSightProfile opticProfile = selectedOptic != null ? selectedOptic.OpticSightProfile : null;
            bool requiresMagnifiedLens = opticProfile != null && opticProfile.UsesMagnifiedLensRendering;
            ViewmodelScopeLens[] scopeLenses = prefabRoot != null
                ? prefabRoot.GetComponentsInChildren<ViewmodelScopeLens>(true)
                : new ViewmodelScopeLens[0];
            if (!requiresMagnifiedLens && scopeLenses.Length == 0) return;

            EditorGUILayout.Space(5f);
            showScopeLensCalibration = EditorGUILayout.Foldout(showScopeLensCalibration,
                "倍率镜 LensAnchor 校准与验收", true, EditorStyles.foldoutHeader);
            if (!showScopeLensCalibration) return;

            if (!requiresMagnifiedLens)
            {
                EditorGUILayout.HelpBox(
                    "当前瞄具不是倍率镜，却包含 ViewmodelScopeLens。该组件不会在运行时启用；若这是红点或全息瞄具，应移除镜片组件。",
                    MessageType.Warning);
                return;
            }
            if (scopeLenses.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "倍率镜预制体缺少 ViewmodelScopeLens。请通过资源准备工具创建 LensAnchor；不要用 AimAnchor 代替物理镜片。",
                    MessageType.Error);
                return;
            }
            if (scopeLenses.Length > 1)
            {
                EditorGUILayout.HelpBox($"检测到 {scopeLenses.Length} 个 ViewmodelScopeLens；每个倍率镜只允许一个物理后镜片契约。",
                    MessageType.Error);
                return;
            }

            DrawMagnifiedLensRenderingProfile(opticProfile);
            ViewmodelScopeLens scopeLens = scopeLenses[0];
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("LensAnchor", scopeLens, typeof(ViewmodelScopeLens), true);

            EditorGUI.BeginChangeCheck();
            Vector3 newPosition = EditorGUILayout.Vector3Field("镜片中心位置", scopeLens.transform.localPosition);
            Vector3 newRotation = EditorGUILayout.Vector3Field("镜片平面旋转", scopeLens.transform.localEulerAngles);
            float apertureMillimetres = EditorGUILayout.Slider("有效口径（mm）",
                scopeLens.ClearApertureDiameter * 1000f, 5f, 150f);
            bool orientationAuthored = EditorGUILayout.Toggle("使用已制作镜片平面",
                scopeLens.OrientationAuthored);
            bool changed = EditorGUI.EndChangeCheck();
            if (changed)
                ApplyScopeLensPrefabAssetEdits(visual, scopeLens, newPosition, newRotation,
                    apertureMillimetres / 1000f, orientationAuthored, false);

            EditorGUILayout.LabelField(
                "LensAnchor 只定义后镜片中心、平面方向与有效口径；它不会改变 ADS 姿态或弹道。本地 +Z 指向目标，本地 +Y 为瞄具上方。",
                EditorStyles.wordWrappedMiniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!previewActive))
                    if (GUILayout.Button("按当前 ADS 视角播种镜片平面"))
                    {
                        SeedScopeLensOrientation(visual, scopeLens);
                        return;
                    }
                if (GUILayout.Button("选择 LensAnchor")) Selection.activeObject = scopeLens.gameObject;
            }

            showScopeLensGuides = EditorGUILayout.Toggle("在预览与 Scene 显示镜片边界", showScopeLensGuides);
            DrawNudgeStepControls("每次 LensAnchor 微调（mm）");
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!previewActive))
                    if (GUILayout.Button("镜片上移", GUILayout.Width(100f)))
                        NudgeScopeLens(visual, previewCamera.transform.up);
                GUILayout.FlexibleSpace();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!previewActive))
                {
                    if (GUILayout.Button("镜片左移")) NudgeScopeLens(visual, -previewCamera.transform.right);
                    if (GUILayout.Button("镜片右移")) NudgeScopeLens(visual, previewCamera.transform.right);
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!previewActive))
                    if (GUILayout.Button("镜片下移", GUILayout.Width(100f)))
                        NudgeScopeLens(visual, -previewCamera.transform.up);
                GUILayout.FlexibleSpace();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!previewActive))
                {
                    if (GUILayout.Button("镜片后移")) NudgeScopeLens(visual, -previewCamera.transform.forward);
                    if (GUILayout.Button("镜片前移")) NudgeScopeLens(visual, previewCamera.transform.forward);
                }
            }

            bool canReview = DrawScopeLensContractValidation(prefabRoot, aimAnchor, scopeLens);
            using (new EditorGUI.DisabledScope(!canReview))
                if (GUILayout.Button(scopeLens.VisualPlacementReviewed
                        ? "镜片视觉覆盖已确认（再次点击可重新确认）"
                        : "确认镜片边界覆盖真实后镜片"))
                    ApplyScopeLensPrefabAssetEdits(visual, scopeLens, scopeLens.transform.localPosition,
                        scopeLens.transform.localEulerAngles, scopeLens.ClearApertureDiameter,
                        scopeLens.OrientationAuthored, true, -1, "确认倍率镜镜片视觉覆盖");
        }

        private void DrawMagnifiedLensRenderingProfile(OpticSightProfile opticProfile)
        {
            showScopeLensRenderingConfiguration = EditorGUILayout.Foldout(showScopeLensRenderingConfiguration,
                "镜片渲染与镜内准星", true);
            if (!showScopeLensRenderingConfiguration || opticProfile == null) return;

            SerializedObject serializedOptic = new SerializedObject(opticProfile);
            serializedOptic.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedOptic.FindProperty("magnification"), new GUIContent("镜内倍率"));
            EditorGUILayout.PropertyField(serializedOptic.FindProperty("lensRenderResolutionScale"),
                new GUIContent("RT 分辨率倍率"));
            EditorGUILayout.PropertyField(serializedOptic.FindProperty("lensEdgeSoftness"),
                new GUIContent("镜片边缘抗锯齿"));
            EditorGUILayout.PropertyField(serializedOptic.FindProperty("lensFadeDuration"),
                new GUIContent("开镜渐变时间"));
            EditorGUILayout.PropertyField(serializedOptic.FindProperty("lensMaskTexture"),
                new GUIContent("可选镜片遮罩"));
            showScopePeripheralConfiguration = EditorGUILayout.Foldout(showScopePeripheralConfiguration,
                "镜外弱化与 Eyebox", true);
            if (showScopePeripheralConfiguration)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedOptic.FindProperty("outsideLensDim"),
                    new GUIContent("镜外暗化"));
                EditorGUILayout.PropertyField(serializedOptic.FindProperty("outsideLensBlurPixels"),
                    new GUIContent("镜外模糊半径"));
                EditorGUILayout.PropertyField(serializedOptic.FindProperty("peripheralEdgeSoftness"),
                    new GUIContent("镜外边缘过渡"));
                EditorGUILayout.PropertyField(serializedOptic.FindProperty("eyeboxEnabled"),
                    new GUIContent("启用 Eyebox"));
                if (serializedOptic.FindProperty("eyeboxEnabled").boolValue)
                {
                    EditorGUILayout.PropertyField(serializedOptic.FindProperty("idealEyeRelief"),
                        new GUIContent("理想眼距（米）"));
                    EditorGUILayout.PropertyField(serializedOptic.FindProperty("eyeReliefTolerance"),
                        new GUIContent("眼距安全范围"));
                    EditorGUILayout.PropertyField(serializedOptic.FindProperty("eyeReliefTransition"),
                        new GUIContent("眼距黑边过渡"));
                    EditorGUILayout.PropertyField(serializedOptic.FindProperty("eyeboxAngularTolerance"),
                        new GUIContent("光轴安全角（度）"));
                    EditorGUILayout.PropertyField(serializedOptic.FindProperty("eyeboxAngularTransition"),
                        new GUIContent("光轴黑边过渡（度）"));
                    EditorGUILayout.PropertyField(serializedOptic.FindProperty("eyeboxPupilShift"),
                        new GUIContent("出瞳偏移上限"));
                    EditorGUILayout.PropertyField(serializedOptic.FindProperty("eyeboxPupilContraction"),
                        new GUIContent("出瞳收缩上限"));
                    EditorGUILayout.PropertyField(serializedOptic.FindProperty("eyeboxMaxOcclusion"),
                        new GUIContent("黑边最大强度"));
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.HelpBox(
                    "镜外模糊由全局画质自动分档：Very Low/Low 仅暗化，Medium/High 为 4 次采样，Very High/Ultra 为 8 次采样。Profile 只定义该瞄具的视觉强度。",
                    MessageType.None);
            }
            EditorGUILayout.Space(2f);
            EditorGUILayout.PropertyField(serializedOptic.FindProperty("reticleTexture"),
                new GUIContent("准星贴图"));
            EditorGUILayout.PropertyField(serializedOptic.FindProperty("fallbackReticleStyle"),
                new GUIContent("无贴图后备准星"));
            EditorGUILayout.PropertyField(serializedOptic.FindProperty("reticleColor"),
                new GUIContent("准星颜色与亮度"));
            EditorGUILayout.PropertyField(serializedOptic.FindProperty("reticleSizePixels"),
                new GUIContent("准星点/线尺寸"));
            EditorGUILayout.PropertyField(serializedOptic.FindProperty("frameSizePixels"),
                new GUIContent("准星外框尺寸"));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(opticProfile, "调整倍率镜渲染与镜内准星");
                serializedOptic.ApplyModifiedProperties();
                EditorUtility.SetDirty(opticProfile);
                TickPreview();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("选择瞄具视图 Profile")) Selection.activeObject = opticProfile;
                if (GUILayout.Button("保存瞄具视图 Profile")) AssetDatabase.SaveAssets();
            }
            EditorGUILayout.HelpBox(
                "倍率镜准星现在由镜片 Shader 合成并受镜片遮罩裁剪；红点与全息仍使用非倍率 HUD/反射式路径。未指定准星贴图时使用程序化后备准星。",
                MessageType.None);
        }

        private void SeedScopeLensOrientation(WeaponAttachmentViewmodelVisual visual, ViewmodelScopeLens assetLens)
        {
            if (!previewActive || adsPreviewCamera == null || assetLens == null ||
                !TryGetLiveAttachmentRoot(adsPreviewRig, out Transform attachmentRoot)) return;
            ViewmodelScopeLens liveLens = FindScopeLens(attachmentRoot);
            if (liveLens == null || liveLens.transform.parent == null) return;

            Quaternion localRotation = Quaternion.Inverse(liveLens.transform.parent.rotation) *
                adsPreviewCamera.transform.rotation;
            ApplyScopeLensPrefabAssetEdits(visual, assetLens, assetLens.transform.localPosition,
                localRotation.eulerAngles, assetLens.ClearApertureDiameter, true, false, -1,
                "播种倍率镜镜片平面");
        }

        private void ApplyScopeLensPrefabAssetEdits(WeaponAttachmentViewmodelVisual visual,
            ViewmodelScopeLens scopeLens, Vector3 newPosition, Vector3 newRotation, float newDiameter,
            bool orientationAuthored, bool visualReviewed, int undoGroup = -1,
            string undoName = "校准倍率镜 LensAnchor")
        {
            if (visual == null || scopeLens == null) return;
            bool ownsUndoGroup = undoGroup < 0;
            if (ownsUndoGroup)
            {
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(undoName);
            }

            Undo.RecordObject(scopeLens.transform, undoName);
            Undo.RecordObject(scopeLens, undoName);
            scopeLens.transform.localPosition = newPosition;
            scopeLens.transform.localEulerAngles = newRotation;
            scopeLens.Configure(newDiameter);
            scopeLens.SetOrientationAuthored(orientationAuthored);
            scopeLens.SetVisualPlacementReviewed(visualReviewed);
            EditorUtility.SetDirty(scopeLens);
            ScheduleAttachmentPrefabSave();
            if (ownsUndoGroup) Undo.CollapseUndoOperations(undoGroup);
            if (previewActive)
                ApplyScopeLensPrefabEditsToLivePreviews(visual, newPosition, newRotation, newDiameter,
                    orientationAuthored, visualReviewed);
        }

        private void SeedAuthoredSightOrientation(WeaponAttachmentViewmodelVisual visual, Transform assetAimAnchor,
            Transform calibrationTarget)
        {
            if (previewRig == null || previewRig.AimAnchor == null || previewRig.Muzzle == null ||
                assetAimAnchor == null) return;
            if (!WeaponAdsAlignment.TryGetLegacySightFrameWorldRotation(previewRig.transform,
                    previewRig.AimAnchor, previewRig.Muzzle, out Quaternion worldRotation))
            {
                Debug.LogError("无法从当前 ADS 视觉结果计算光轴；请确认枪口和 ADS 瞄准基准均有效。", this);
                return;
            }

            Quaternion localRotation = Quaternion.Inverse(previewRig.AimAnchor.parent.rotation) * worldRotation;
            ApplyAttachmentPrefabAssetEdits(visual, assetAimAnchor, calibrationTarget, true,
                assetAimAnchor.localPosition, localRotation.eulerAngles, true, false, Vector3.zero, Vector3.zero,
                Vector3.one);
        }

        private void ApplyAttachmentPrefabAssetEdits(WeaponAttachmentViewmodelVisual visual, Transform aimAnchor,
            Transform model, bool anchorChanged, Vector3 newAnchorPosition, Vector3 newAnchorRotation,
            bool orientationAuthored, bool modelChanged, Vector3 newModelPosition, Vector3 newModelRotation,
            Vector3 newModelScale, int undoGroup = -1)
        {
            if (!anchorChanged && !modelChanged) return;
            bool ownsUndoGroup = undoGroup < 0;
            if (ownsUndoGroup)
            {
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(GetAttachmentEditUndoName(anchorChanged, modelChanged));
            }

            if (anchorChanged && aimAnchor != null)
            {
                Undo.RecordObject(aimAnchor, "校准附件 ADS 瞄准基准");
                aimAnchor.localPosition = newAnchorPosition;
                aimAnchor.localEulerAngles = newAnchorRotation;
                AdsSightReference sightReference = aimAnchor.GetComponent<AdsSightReference>();
                if (sightReference == null) sightReference = Undo.AddComponent<AdsSightReference>(aimAnchor.gameObject);
                Undo.RecordObject(sightReference, "设置 ADS 光轴契约");
                sightReference.SetOrientationAuthored(orientationAuthored);
            }
            if (modelChanged && model != null)
            {
                Undo.RecordObject(model, "Fit Attachment Model To Weapon");
                RecordSiblingProbeUndo(model);
                SetModelPoseAndMoveAttachmentProbes(model, newModelPosition, newModelRotation, newModelScale);
            }

            if ((anchorChanged || modelChanged) && profile != null)
            {
                Undo.RecordObject(profile, "Clear Visual Sight Review");
                SerializedObject serializedProfile = new SerializedObject(profile);
                serializedProfile.FindProperty("visualSightPlacementReviewed").boolValue = false;
                serializedProfile.ApplyModifiedProperties();
                EditorUtility.SetDirty(profile);
            }

            ScheduleAttachmentPrefabSave();
            if (ownsUndoGroup) Undo.CollapseUndoOperations(undoGroup);
            if (previewActive)
                ApplyAttachmentPrefabEditsToLivePreviews(visual, anchorChanged, newAnchorPosition, newAnchorRotation,
                    orientationAuthored, modelChanged, newModelPosition, newModelRotation, newModelScale);
        }

        private static string GetAttachmentEditUndoName(bool anchorChanged, bool modelChanged,
            bool lensChanged = false) =>
            lensChanged
                ? "校准倍率镜 LensAnchor"
                : anchorChanged && modelChanged
                ? "校准附件模型与 ADS 瞄准基准"
                : anchorChanged ? "校准附件 ADS 瞄准基准" : "调整附件模型挂载";

        private void DrawLiveAttachmentAlignmentStatus()
        {
            if (!previewActive)
            {
                EditorGUILayout.HelpBox("启动实时武器预览后，这里的模型和 ADS 瞄准基准改动会立即反映到腰射、ADS 以及 Scene 辅助线中。",
                    MessageType.None);
                return;
            }

            if (previewMode == PreviewMode.Hip)
            {
                EditorGUILayout.HelpBox("当前是腰射预览。切换到“右键 ADS”或“并排对照”后，可在这里看到实时的准心偏差数值。",
                    MessageType.None);
                return;
            }

            if (!TryMeasureAlignment(out float screenErrorPixels))
            {
                EditorGUILayout.HelpBox("无法测量当前附件的准心偏差；请检查 ADS 瞄准基准是否位于相机前方。", MessageType.Error);
                return;
            }

            bool passed = screenErrorPixels <= ScreenTolerancePixels;
            EditorGUILayout.HelpBox(
                passed
                    ? $"当前附件对齐：通过（误差 {screenErrorPixels:0.00}px / 阈值 {ScreenTolerancePixels:0.00}px）。"
                    : $"当前附件对齐：待微调（误差 {screenErrorPixels:0.00}px / 阈值 {ScreenTolerancePixels:0.00}px）。保持 ADS 瞄准基准位于瞄具光轴中心，并使用“武器整体 ADS 姿态”移动整把武器；不要移动 Model 来追准心。",
                passed ? MessageType.Info : MessageType.Warning);

            AdsSightReference authoredReference = previewRig.AimAnchor != null
                ? previewRig.AimAnchor.GetComponent<AdsSightReference>()
                : null;
            if (authoredReference == null || !authoredReference.OrientationAuthored)
                EditorGUILayout.HelpBox("ADS 光轴方向尚未制作，当前仍由瞄准基准到枪口的连线兼容推导。",
                    MessageType.Warning);
            else
            {
                float axisError = Vector3.Angle(previewRig.AimAnchor.forward, previewCamera.transform.forward);
                EditorGUILayout.HelpBox($"ADS 光轴姿态有效；当前与相机前向夹角 {axisError:0.00}°。",
                    MessageType.Info);
            }
        }

        private void DrawWeaponProfileEditor()
        {
            showHipPresentation = EditorGUILayout.Foldout(showHipPresentation, "腰射表现", true,
                EditorStyles.foldoutHeader);
            if (showHipPresentation)
            {
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
            }

            showAdsConfiguration = EditorGUILayout.Foldout(showAdsConfiguration, "右键 ADS", true,
                EditorStyles.foldoutHeader);
            if (!showAdsConfiguration) return;

            if (profile == null)
            {
                EditorGUILayout.HelpBox("此武器配置为仅腰射：无需 ADS Profile 或 ADS 瞄准基准。工作台仍可用于调整腰射表现。",
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
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(sightDistance, new GUIContent("瞄具距离"));
            EditorGUILayout.PropertyField(zeroDistance, new GUIContent("归零距离"));
            showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "高级 ADS 微调");
            if (showAdvancedSettings)
            {
                if (selectedOptic != null)
                    EditorGUILayout.HelpBox(
                        "动态瞄具的 ADS 对齐应使用工作台下方“武器整体 ADS 姿态”或此瞄具专属 ADS Profile；它会带动整把武器，瞄具仍固定在导轨上。ADS 瞄准基准只在光轴参考位置错误时修正；不要通过移动附件 Model 来追准心。",
                        MessageType.None);
                EditorGUILayout.PropertyField(referenceOffset, new GUIContent("整体瞄具参考修正（移动整把武器）"));
                EditorGUILayout.PropertyField(positionOffset, new GUIContent("整体相机空间位置微调"));
                EditorGUILayout.PropertyField(rotationOffset, new GUIContent("整体相机空间旋转微调"));
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
            bool hasDynamicOptic = TryGetSelectedOpticVisual(out WeaponAttachmentViewmodelVisual opticVisual);
            DrawWeaponAdsPoseNudgeControls(hasDynamicOptic);
            if (hasDynamicOptic) DrawAttachmentModelNudgeControls(opticVisual);
        }

        /// <summary>
        /// This is deliberately independent of the selected attachment. It changes the active ADS presentation
        /// profile and therefore moves the entire weapon rig; a dynamic optic remains fixed to its weapon socket.
        /// </summary>
        private void DrawWeaponAdsPoseNudgeControls(bool hasDynamicOptic)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(hasDynamicOptic ? "武器整体 ADS 姿态（推荐）" : "机瞄对齐辅助",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(hasDynamicOptic
                    ? "方向按钮调整当前瞄具生效的 ADS Profile，让整把武器连同瞄具一起移动。它不会修改附件 Model，也不会让瞄具脱离 SOCKET_Scope；先用这一层把瞄具画面与黄色十字对齐。"
                    : "在 ADS 或并排视图中，让真实机瞄中心对准黄色空心十字。方向按钮修改当前武器的“机瞄参考修正”；不会影响其他武器。",
                EditorStyles.wordWrappedMiniLabel);
            DrawNudgeStepControls("每次微调（mm）");
            DrawPreviewGuideControls(hasDynamicOptic ? "显示黄色瞄具参考" : "显示黄色机瞄参考");

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
            if (GUILayout.Button(hasDynamicOptic ? "确认当前武器与瞄具整体姿态正确" : "确认当前武器机瞄位置正确"))
                SetVisualReview(true);
        }

        private void DrawPreviewGuideControls(string sightReferenceLabel)
        {
            viewmodelSafetyMargin = EditorGUILayout.Slider("Clip Probe 安全余量", viewmodelSafetyMargin, 0.005f, 0.08f);
            showSightReferenceMarker = EditorGUILayout.Toggle(sightReferenceLabel, showSightReferenceMarker);
            if (showSightReferenceMarker)
                sightReferenceMarkerRadius = EditorGUILayout.Slider("参考标记大小", sightReferenceMarkerRadius, 0.002f, 0.03f);
            showClipProbeGuides = EditorGUILayout.Toggle("在 Scene 显示 Clip Probes", showClipProbeGuides);
        }

        private void DrawAttachmentModelNudgeControls(WeaponAttachmentViewmodelVisual visual)
        {
            EditorGUILayout.Space(6f);
            showAttachmentGeometryRepair = EditorGUILayout.Foldout(showAttachmentGeometryRepair,
                "瞄具资产几何修复（高级）", true, EditorStyles.foldoutHeader);
            if (!showAttachmentGeometryRepair) return;

            EditorGUILayout.HelpBox(
                "此处只用于修复导入资源本身的枢轴、比例或机械挂载错误。它会直接修改附件的 Model，因此可能改变瞄具与导轨的相对位置。不要用它解决 ADS 准心偏差：请收起本节并使用上方“武器整体 ADS 姿态”。",
                MessageType.Warning);
            DrawNudgeStepControls("每次资产模型微调（mm）");

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("模型上移", GUILayout.Width(100f))) NudgeAttachmentModel(visual, previewCamera.transform.up);
                GUILayout.FlexibleSpace();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("模型左移")) NudgeAttachmentModel(visual, -previewCamera.transform.right);
                if (GUILayout.Button("模型右移")) NudgeAttachmentModel(visual, previewCamera.transform.right);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("模型下移", GUILayout.Width(100f))) NudgeAttachmentModel(visual, -previewCamera.transform.up);
                GUILayout.FlexibleSpace();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("模型后移")) NudgeAttachmentModel(visual, -previewCamera.transform.forward);
                if (GUILayout.Button("模型前移")) NudgeAttachmentModel(visual, previewCamera.transform.forward);
            }
        }

        private void DrawNudgeStepControls(string label)
        {
            const float minimumMillimetres = 0.01f;
            const float maximumMillimetres = 10f;

            float millimetres = Mathf.Clamp(visualNudgeStep * 1000f, minimumMillimetres, maximumMillimetres);
            millimetres = EditorGUILayout.Slider(label, millimetres, minimumMillimetres, maximumMillimetres);
            millimetres = EditorGUILayout.FloatField("自定义步距（mm）", millimetres);
            millimetres = Mathf.Clamp(millimetres, minimumMillimetres, maximumMillimetres);
            visualNudgeStep = millimetres / 1000f;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("粗调 5mm")) visualNudgeStep = 0.005f;
                if (GUILayout.Button("常规 1mm")) visualNudgeStep = 0.001f;
                if (GUILayout.Button("精调 0.1mm")) visualNudgeStep = 0.0001f;
                if (GUILayout.Button("超精 0.02mm")) visualNudgeStep = 0.00002f;
            }

            EditorGUILayout.LabelField(
                $"当前步距：{visualNudgeStep * 1000f:0.###}mm；方向始终以当前预览相机为基准。",
                EditorStyles.miniLabel);
        }

        private void DrawStatus()
        {
            EditorGUILayout.LabelField("对齐状态", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("腰射：调整该武器的 Presentation Profile；ADS：调整该武器的 ADS Profile。投射命中始终从相机准心出发，机瞄只负责让视觉与准心重合。",
                EditorStyles.wordWrappedMiniLabel);
            if (!HasAdsPreview)
            {
                EditorGUILayout.HelpBox("当前武器为仅腰射配置：无需机瞄对齐、ADS Profile 或 ADS 瞄准基准。", MessageType.Info);
                return;
            }
            if (previewActive) DrawClipProbeValidationStatus();
            if (!previewActive || !CanPreview() || previewMode == PreviewMode.Hip) return;
            if (!TryMeasureAlignment(out float screenErrorPixels))
            {
                EditorGUILayout.HelpBox("无法测量机瞄参考点：请确认 ADS 瞄准基准位于相机前方。", MessageType.Error);
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

        private void DrawClipProbeValidationStatus()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("第一人称近裁剪契约", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "只验证武器与已激活配件上人工布置的 Clip Probe；完整渲染网格、枪托和动画辅助几何不参与判定。",
                EditorStyles.wordWrappedMiniLabel);

            bool hasHipProbes = TryMeasureClipProbes(PreviewMode.Hip, hipClipProbeMeasurements);
            bool hasAdsProbes = HasAdsPreview && TryMeasureClipProbes(PreviewMode.Ads, adsClipProbeMeasurements);
            if (!hasHipProbes && !hasAdsProbes)
            {
                EditorGUILayout.HelpBox(
                    "当前武器尚未定义 Clip Probe，因此无法给出近裁剪通过结论。运行 Project Sun/Ensure Per-Weapon Viewmodel Clip Probes 创建基础契约；新武器或瞄具应在其预制体上自行添加关键可见表面的 ViewmodelClipProbe。",
                    MessageType.Warning);
                return;
            }

            if (selectedOptic != null && (HasLegacyDefaultSightProbe(hipClipProbeMeasurements) ||
                HasLegacyDefaultSightProbe(adsClipProbeMeasurements)))
            {
                EditorGUILayout.HelpBox(
                    "检测到 AR-4 原生瞄具探针尚未绑定其可见模型。装上替换瞄具后，它仍会被纳入验证，可能造成“1/3 未通过”的假阳性。执行一次迁移即可让隐藏的原生瞄具探针自动停用。",
                    MessageType.Warning);
                if (GUILayout.Button("修复原生瞄具探针可见性契约"))
                {
                    bool restartPreview = previewActive;
                    if (restartPreview) StopPreview();
                    WeaponInventoryPrefabSetup.EnsurePerWeaponViewmodelClipProbes();
                    if (restartPreview) StartPreview();
                }
                return;
            }

            if (hasHipProbes) DrawClipProbePoseStatus("腰射", hipClipProbeMeasurements);
            if (hasAdsProbes) DrawClipProbePoseStatus("右键 ADS", adsClipProbeMeasurements);
        }

        private static bool HasLegacyDefaultSightProbe(List<ClipProbeMeasurement> measurements)
        {
            foreach (ClipProbeMeasurement measurement in measurements)
                if (measurement.Probe != null && measurement.Probe.name == "ClipProbe_AR4_SightHousing" &&
                    measurement.Probe.VisibilityOwner == null)
                    return true;
            return false;
        }

        private void DrawClipProbePoseStatus(string poseName, List<ClipProbeMeasurement> measurements)
        {
            int failedCount = 0;
            string failures = string.Empty;
            for (int index = 0; index < measurements.Count; index++)
            {
                ClipProbeMeasurement measurement = measurements[index];
                bool passed = measurement.IsInFrontOfCamera && measurement.Clearance >= RequiredViewmodelClearance;
                if (passed) continue;

                failedCount++;
                if (failedCount > 3) continue;
                string value = measurement.IsInFrontOfCamera
                    ? $"{measurement.Clearance * 1000f:0.0}mm"
                    : "位于相机后方";
                failures += $"\n- {measurement.Probe.ValidationLabel}: {value}";
            }

            bool safe = failedCount == 0;
            string status = safe ? "通过" : "需要处理";
            string detail = safe
                ? $"全部 {measurements.Count} 个探针均满足 {RequiredViewmodelClearance * 1000f:0.0}mm 安全下限。"
                : $"{failedCount}/{measurements.Count} 个探针未通过：{failures}" +
                  (failedCount > 3 ? "\n- 其余失败探针已省略。" : string.Empty);
            EditorGUILayout.HelpBox($"{poseName} Clip Probe：{status}\n{detail}",
                safe ? MessageType.Info : MessageType.Error);
            DrawClipProbeMeasurements(measurements);
        }

        private void DrawClipProbeMeasurements(List<ClipProbeMeasurement> measurements)
        {
            showClipProbeDetails = EditorGUILayout.Foldout(showClipProbeDetails, "查看每个 Clip Probe 的测量明细", true);
            if (!showClipProbeDetails) return;
            foreach (ClipProbeMeasurement measurement in measurements)
            {
                bool passed = measurement.IsInFrontOfCamera && measurement.Clearance >= RequiredViewmodelClearance;
                float centreDepth = measurement.Clearance + measurement.Probe.SurfaceRadius;
                float shortfall = Mathf.Max(0f, RequiredViewmodelClearance - measurement.Clearance);
                string owner = measurement.Probe.VisibilityOwner != null
                    ? $"；可见归属：{measurement.Probe.VisibilityOwner.name}"
                    : string.Empty;
                string result = passed
                    ? "通过"
                    : measurement.IsInFrontOfCamera
                        ? $"未通过；球面还需远离相机 {shortfall * 1000f:0.0}mm"
                        : "未通过；探针中心已在相机后方";
                EditorGUILayout.HelpBox(
                    $"{measurement.Probe.ValidationLabel}（{measurement.Probe.name}）\n" +
                    $"中心深度 {centreDepth * 1000f:0.0}mm；半径 {measurement.Probe.SurfaceRadius * 1000f:0.0}mm；" +
                    $"球面净距 {measurement.Clearance * 1000f:0.0}mm / 要求 {RequiredViewmodelClearance * 1000f:0.0}mm{owner}\n" +
                    result + "。探针应覆盖真实可见的最近表面；若黄色球位置正确，应调整模型或武器姿态，而不是为了通过验证而随意移动探针。",
                    passed ? MessageType.Info : MessageType.Warning);
            }
        }

        private bool DrawScopeLensContractValidation(Transform prefabRoot, Transform assetAimAnchor,
            ViewmodelScopeLens assetLens)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("倍率镜镜片契约", EditorStyles.boldLabel);

            OpticSightProfile opticProfile = selectedOptic != null ? selectedOptic.OpticSightProfile : null;
            bool profileValid = opticProfile != null && opticProfile.UsesMagnifiedLensRendering;
            int lensCount = prefabRoot != null
                ? prefabRoot.GetComponentsInChildren<ViewmodelScopeLens>(true).Length
                : 0;
            ViewmodelAttachmentCalibrationRoot lensCalibrationRoot = assetLens != null
                ? assetLens.GetComponentInParent<ViewmodelAttachmentCalibrationRoot>()
                : null;
            ViewmodelAttachmentCalibrationRoot aimCalibrationRoot = assetAimAnchor != null
                ? assetAimAnchor.GetComponentInParent<ViewmodelAttachmentCalibrationRoot>()
                : null;
            bool sharedCalibrationRoot = lensCalibrationRoot != null && lensCalibrationRoot == aimCalibrationRoot;
            bool layerValid = assetLens != null && assetLens.gameObject.layer == CombatLayers.ViewmodelLayer;
            bool measured = TryMeasureScopeLens(adsPreviewRig, adsPreviewCamera,
                out ScopeLensMeasurement measurement);
            bool measurementValid = measured && IsScopeLensMeasurementValid(measurement);
            bool boundsMeasured = false;
            float boundsDistance = float.PositiveInfinity;
            bool insideAttachmentBounds = false;
            if (measured && TryGetLiveAttachmentRoot(adsPreviewRig, out Transform liveAttachmentRoot) &&
                TryGetRendererBounds(liveAttachmentRoot, out Bounds attachmentBounds))
            {
                boundsMeasured = true;
                boundsDistance = Vector3.Distance(measurement.Lens.transform.position,
                    attachmentBounds.ClosestPoint(measurement.Lens.transform.position));
                Vector3 lensScale = measurement.Lens.transform.lossyScale;
                float apertureScale = Mathf.Max(Mathf.Abs(lensScale.x), Mathf.Abs(lensScale.y));
                float allowedDistance = Mathf.Max(0.01f,
                    measurement.Lens.ClearApertureDiameter * apertureScale * 0.5f);
                insideAttachmentBounds = boundsDistance <= allowedDistance;
            }

            bool automaticPass = profileValid && lensCount == 1 && sharedCalibrationRoot && layerValid &&
                assetLens != null && assetLens.OrientationAuthored && measurementValid &&
                (!boundsMeasured || insideAttachmentBounds);
            bool ready = automaticPass && assetLens != null && assetLens.VisualPlacementReviewed;
            ScopeLensCentreGrade centreGrade = measured
                ? GetScopeLensCentreGrade(measurement)
                : ScopeLensCentreGrade.Failed;
            string centreGradeLabel = GetScopeLensCentreGradeLabel(centreGrade);
            string headline = ready
                ? $"READY · {centreGradeLabel}：倍率镜镜片契约与人工视觉验收均已通过。"
                : automaticPass
                    ? $"自动契约已通过（{centreGradeLabel}）；还需确认青色边界覆盖真实后镜片。"
                    : "NOT READY：请按下方失败项处理，不要通过移动 AimAnchor 修复镜片。";
            EditorGUILayout.HelpBox(headline, ready ? MessageType.Info : MessageType.Warning);

            showScopeLensValidationDetails = EditorGUILayout.Foldout(showScopeLensValidationDetails,
                "查看镜片验收明细", true);
            if (!showScopeLensValidationDetails) return automaticPass;

            string profileState = profileValid ? "通过" : "失败：OpticSightProfile 不是有效倍率镜";
            string countState = lensCount == 1 ? "通过" : $"失败：检测到 {lensCount} 个镜片组件，要求恰好 1 个";
            string rootState = sharedCalibrationRoot
                ? "通过"
                : "失败：AimAnchor 与 LensAnchor 未共享 AttachmentCalibrationRoot";
            string authoredState = assetLens != null && assetLens.OrientationAuthored
                ? "通过"
                : "失败：点击“按当前 ADS 视角播种镜片平面”";
            string layerState = layerValid
                ? "通过"
                : $"失败：LensAnchor 必须使用 Viewmodel Layer（{CombatLayers.ViewmodelLayer}）";
            string liveState;
            if (!measured)
                liveState = "失败：启动 ADS/并排实时预览后才能测量";
            else
            {
                string sightAxis = measurement.HasSightAxisMeasurement
                    ? $"；与 ADS 光轴 {measurement.SightAxisError:0.00}°"
                    : "；ADS 光轴尚处于兼容模式，暂不比较两条制作轴";
                liveState =
                    $"{centreGradeLabel}：{FormatScopeLensCentreOffset(measurement)}（{LensReferenceWidthPixels:0}×{LensReferenceHeightPixels:0} 基准）；" +
                    $"综合偏差 {measurement.CentreError * 100f:0.00}% / 工程阈值 {LensCentreToleranceViewport * 100f:0.00}%；" +
                    $"画面口径 {measurement.ViewportDiameter * 100f:0.0}%；" +
                    $"镜片平面与相机 {measurement.CameraAxisError:0.00}° / 阈值 {LensAxisToleranceDegrees:0.00}°{sightAxis}";
            }
            string boundsState = !boundsMeasured
                ? "未测量：当前没有可用的附件 Renderer 包围盒"
                : insideAttachmentBounds
                    ? "通过：镜片中心位于附件渲染包围盒附近"
                    : $"失败：镜片中心距附件包围盒 {boundsDistance * 1000f:0.0}mm，疑似脱离模型";
            string reviewState = assetLens != null && assetLens.VisualPlacementReviewed
                ? "已确认"
                : "待人工确认：青色边界必须覆盖真实后镜片开口，且不能越过镜框";
            EditorGUILayout.HelpBox(
                $"1. 倍率镜 Profile：{profileState}\n" +
                $"2. 唯一 LensAnchor：{countState}\n" +
                $"3. 统一校准根：{rootState}\n" +
                $"4. Viewmodel Layer：{layerState}\n" +
                $"5. 镜片方向契约：{authoredState}\n" +
                $"6. ADS 实时测量：{liveState}\n" +
                $"7. 模型包围盒粗检：{boundsState}\n" +
                $"8. 真实镜框覆盖：{reviewState}",
                ready ? MessageType.Info : MessageType.None);

            if (measured && measurement.CentreError > LensCentreToleranceViewport)
                EditorGUILayout.HelpBox(
                    "镜片中心偏离屏幕：若青色边界已经贴合真实镜框，应调整“武器整体 ADS 姿态”；只有青色边界没有贴合镜框时才移动 LensAnchor。",
                    MessageType.Warning);
            else if (measured && centreGrade == ScopeLensCentreGrade.Acceptable)
                EditorGUILayout.HelpBox(
                    $"当前已达到工程通过，但还不是精确对准。继续把 X/Y 最大偏差从 {measurement.CentrePixelError:0.00}px 调到 {LensPreciseTolerancePixels:0.00}px 以内即可显示“精确对准”。",
                    MessageType.Info);
            if (measured && measurement.CameraAxisError > LensAxisToleranceDegrees)
                EditorGUILayout.HelpBox(
                    "镜片平面未正对 ADS 视线：先点击播种按钮获得无跳变初值，再用“旋转镜片平面”Scene 手柄小幅修正。",
                    MessageType.Warning);
            return automaticPass;
        }

        private static bool TryMeasureScopeLens(LowPolyShooterViewmodelRig rig, Camera camera,
            out ScopeLensMeasurement measurement)
        {
            measurement = default;
            if (rig == null || camera == null) return false;
            ViewmodelScopeLens lens = null;
            foreach (ViewmodelScopeLens candidate in rig.GetComponentsInChildren<ViewmodelScopeLens>(true))
                if (candidate != null && candidate.gameObject.activeInHierarchy)
                {
                    lens = candidate;
                    break;
                }
            if (lens == null) return false;

            Vector3 centre = camera.WorldToViewportPoint(lens.transform.position);
            Vector3 lossyScale = lens.transform.lossyScale;
            float apertureScale = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y));
            float worldRadius = lens.ClearApertureDiameter * apertureScale * 0.5f;
            Vector3 rightEdge = camera.WorldToViewportPoint(lens.transform.position + camera.transform.right * worldRadius);
            Vector3 upEdge = camera.WorldToViewportPoint(lens.transform.position + camera.transform.up * worldRadius);
            float viewportDiameter = Mathf.Max(Mathf.Abs(rightEdge.x - centre.x) * 2f,
                Mathf.Abs(upEdge.y - centre.y) * 2f);
            float centreError = Mathf.Max(Mathf.Abs(centre.x - 0.5f), Mathf.Abs(centre.y - 0.5f));
            Vector2 centreOffsetPixels = new Vector2((centre.x - 0.5f) * LensReferenceWidthPixels,
                (centre.y - 0.5f) * LensReferenceHeightPixels);
            float centrePixelError = Mathf.Max(Mathf.Abs(centreOffsetPixels.x),
                Mathf.Abs(centreOffsetPixels.y));
            float cameraAxisError = Quaternion.Angle(lens.transform.rotation, camera.transform.rotation);
            AdsSightReference sightReference = rig.AimAnchor != null
                ? rig.AimAnchor.GetComponent<AdsSightReference>()
                : null;
            bool hasSightAxis = sightReference != null && sightReference.OrientationAuthored;
            float sightAxisError = hasSightAxis
                ? Vector3.Angle(lens.transform.forward, rig.AimAnchor.forward)
                : 0f;
            measurement = new ScopeLensMeasurement(lens, centre, viewportDiameter, centreError,
                centreOffsetPixels, centrePixelError, cameraAxisError, sightAxisError, hasSightAxis,
                centre.z > camera.nearClipPlane);
            return true;
        }

        private static bool IsScopeLensMeasurementValid(ScopeLensMeasurement measurement)
        {
            return measurement.Lens != null && measurement.IsInFrontOfCamera &&
                measurement.CentreError <= LensCentreToleranceViewport &&
                measurement.ViewportDiameter >= LensMinimumViewportDiameter &&
                measurement.ViewportDiameter <= LensMaximumViewportDiameter &&
                measurement.CameraAxisError <= LensAxisToleranceDegrees &&
                (!measurement.HasSightAxisMeasurement || measurement.SightAxisError <= LensAxisToleranceDegrees);
        }

        private static ScopeLensCentreGrade GetScopeLensCentreGrade(ScopeLensMeasurement measurement)
        {
            if (measurement.CentrePixelError <= LensPreciseTolerancePixels)
                return ScopeLensCentreGrade.Precise;
            return measurement.CentreError <= LensCentreToleranceViewport
                ? ScopeLensCentreGrade.Acceptable
                : ScopeLensCentreGrade.Failed;
        }

        private static string GetScopeLensCentreGradeLabel(ScopeLensCentreGrade grade)
        {
            switch (grade)
            {
                case ScopeLensCentreGrade.Precise: return "精确对准";
                case ScopeLensCentreGrade.Acceptable: return "工程通过";
                default: return "未通过";
            }
        }

        private static Color GetScopeLensCentreGradeColor(ScopeLensCentreGrade grade)
        {
            switch (grade)
            {
                case ScopeLensCentreGrade.Precise: return new Color(0.25f, 1f, 0.5f, 0.95f);
                case ScopeLensCentreGrade.Acceptable: return new Color(1f, 0.78f, 0.18f, 0.95f);
                default: return new Color(1f, 0.28f, 0.22f, 0.95f);
            }
        }

        private static string FormatScopeLensCentreOffset(ScopeLensMeasurement measurement)
        {
            float x = measurement.CentreOffsetPixels.x;
            float y = measurement.CentreOffsetPixels.y;
            string horizontal = Mathf.Abs(x) <= 0.005f ? "居中" : x > 0f ? "右" : "左";
            string vertical = Mathf.Abs(y) <= 0.005f ? "居中" : y > 0f ? "上" : "下";
            return $"X {x:+0.00;-0.00;0.00}px（{horizontal}） / " +
                $"Y {y:+0.00;-0.00;0.00}px（{vertical}）";
        }

        private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            if (root == null) return false;
            bool found = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    renderer.name == "Runtime Scope Lens Surface") continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return found;
        }

        private void StartPreview()
        {
            if (!CanPreview()) return;
            hipPosition = previewRig.transform.localPosition;
            hipRotation = previewRig.transform.localRotation;
            hasHipPose = true;
            previewActive = true;
            previewPosesPrimed = false;
            hasScenePreviewMode = false;
            previousTickTime = EditorApplication.timeSinceStartup;
            CreateRuntimeCameraPreview();
            if (!PrepareSourceWeapon())
            {
                StopPreview();
                return;
            }
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
            if (attachmentSceneDragActive && GUIUtility.hotControl == 0)
            {
                CompleteAttachmentSceneDrag();
                return;
            }
            if (attachmentSceneDragActive)
            {
                // Scene handles own the edited instance until mouse-up. Re-applying ADS/animation here moves
                // the handle reference frame underneath Unity's hot control and creates cumulative drift.
                previousTickTime = EditorApplication.timeSinceStartup;
                Repaint();
                return;
            }
            FlushDeferredAttachmentPrefabSave();
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
            return presentationProfile != null ? presentationProfile.ResolveHipPositionOffset(previewLoadout) :
                profile != null ? profile.HipCameraSpacePositionOffset : Vector3.zero;
        }

        private Vector3 ResolveHipRotationOffset()
        {
            return presentationProfile != null ? presentationProfile.ResolveHipRotationOffset(previewLoadout) :
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
            if (mode == PreviewMode.Ads)
            {
                DrawActiveOpticReticlePreview(rect);
                if (showScopeLensGuides) DrawScopeLensPreviewOverlay(rect);
            }
            GUI.Label(new Rect(rect.x + 6f, rect.y + 5f, rect.width - 12f, 18f), title, EditorStyles.whiteMiniLabel);
        }

        private void DrawScopeLensPreviewOverlay(Rect rect)
        {
            if (!TryMeasureScopeLens(adsPreviewRig, adsPreviewCamera, out ScopeLensMeasurement measurement) ||
                measurement.Lens == null || measurement.ViewportCentre.z <= 0f) return;

            ViewmodelScopeLens lens = measurement.Lens;
            Vector3 lossyScale = lens.transform.lossyScale;
            float apertureScale = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y));
            float worldRadius = lens.ClearApertureDiameter * apertureScale * 0.5f;
            Vector3 right = adsPreviewCamera.WorldToViewportPoint(
                lens.transform.position + adsPreviewCamera.transform.right * worldRadius);
            Vector3 up = adsPreviewCamera.WorldToViewportPoint(
                lens.transform.position + adsPreviewCamera.transform.up * worldRadius);
            Vector2 centre = new Vector2(rect.x + measurement.ViewportCentre.x * rect.width,
                rect.y + (1f - measurement.ViewportCentre.y) * rect.height);
            float radiusX = Mathf.Abs(right.x - measurement.ViewportCentre.x) * rect.width;
            float radiusY = Mathf.Abs(up.y - measurement.ViewportCentre.y) * rect.height;
            if (radiusX < 1f || radiusY < 1f) return;

            const int segmentCount = 48;
            Vector3[] points = new Vector3[segmentCount + 1];
            for (int index = 0; index <= segmentCount; index++)
            {
                float angle = index / (float)segmentCount * Mathf.PI * 2f;
                points[index] = new Vector3(centre.x + Mathf.Cos(angle) * radiusX,
                    centre.y + Mathf.Sin(angle) * radiusY, 0f);
            }

            ScopeLensCentreGrade centreGrade = GetScopeLensCentreGrade(measurement);
            Color statusColor = GetScopeLensCentreGradeColor(centreGrade);
            Handles.BeginGUI();
            Handles.color = statusColor;
            Handles.DrawAAPolyLine(2.5f, points);
            Handles.DrawLine(new Vector3(centre.x - 7f, centre.y), new Vector3(centre.x + 7f, centre.y));
            Handles.DrawLine(new Vector3(centre.x, centre.y - 7f), new Vector3(centre.x, centre.y + 7f));
            Handles.color = new Color(statusColor.r, statusColor.g, statusColor.b, 0.45f);
            Handles.DrawDottedLine(centre, rect.center, 3f);
            Handles.EndGUI();
            float labelWidth = Mathf.Max(80f, rect.width - 8f);
            float labelX = Mathf.Clamp(centre.x + 8f, rect.x + 4f,
                Mathf.Max(rect.x + 4f, rect.xMax - labelWidth - 4f));
            float labelY = Mathf.Clamp(centre.y + 5f, rect.y + 20f, rect.yMax - 20f);
            Color oldGuiColor = GUI.color;
            GUI.color = statusColor;
            GUI.Label(new Rect(labelX, labelY, labelWidth, 18f),
                $"LensAnchor · {GetScopeLensCentreGradeLabel(centreGrade)} · " +
                FormatScopeLensCentreOffset(measurement), EditorStyles.whiteMiniLabel);
            GUI.color = oldGuiColor;
        }

        private void DrawActiveOpticReticlePreview(Rect rect)
        {
            WeaponAttachment optic = previewLoadout != null ? previewLoadout.GetEquipped(AttachmentSlot.Optic) : null;
            if (optic == null || optic.OpticSightProfile == null) return;
            Rect reticleViewport = rect;
            if (optic.OpticSightProfile.UsesMagnifiedLensRendering)
            {
                if (!TryMeasureScopeLens(adsPreviewRig, adsPreviewCamera,
                        out ScopeLensMeasurement measurement) || measurement.ViewportCentre.z <= 0f) return;
                Vector2 centre = new Vector2(rect.x + measurement.ViewportCentre.x * rect.width,
                    rect.y + (1f - measurement.ViewportCentre.y) * rect.height);
                float diameter = Mathf.Max(8f, measurement.ViewportDiameter * Mathf.Min(rect.width, rect.height));
                reticleViewport = new Rect(centre.x - diameter * 0.5f, centre.y - diameter * 0.5f,
                    diameter, diameter);
            }
            OpticReticleGui.Draw(optic.OpticSightProfile, reticleViewport);
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
            if (previewRig != newRig)
            {
                StopPreview();
                ReleaseAttachmentPrefabContents();
            }
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
            }
            else if (previewRig != null)
            {
                presentationProfile = previewRig.PresentationProfile;
            }

            RefreshAvailableOptics();
            previewLoadout = BuildPreviewLoadout();
            WeaponAdsProfile fallbackProfile = selectedViewmodel != null ? selectedViewmodel.AdsProfile
                : previewRig != null ? previewRig.AdsProfile : null;
            profile = presentationProfile != null
                ? presentationProfile.ResolveAdsProfile(previewLoadout)
                : fallbackProfile;
        }

        private void RefreshAvailableOptics()
        {
            availableOptics.Clear();
            WeaponDefinition weapon = ResolveSelectedWeaponDefinition();
            if (weapon == null)
            {
                selectedOptic = null;
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:WeaponAttachment");
            foreach (string guid in guids)
            {
                WeaponAttachment candidate = AssetDatabase.LoadAssetAtPath<WeaponAttachment>(AssetDatabase.GUIDToAssetPath(guid));
                if (candidate == null || candidate.slot != AttachmentSlot.Optic || !candidate.IsCompatibleWith(weapon)) continue;
                if (!candidate.TryGetViewmodelVisual(weapon, out _)) continue;
                availableOptics.Add(candidate);
            }
            availableOptics.Sort((left, right) => string.Compare(left.displayName, right.displayName,
                System.StringComparison.OrdinalIgnoreCase));
            if (selectedOptic != null && !availableOptics.Contains(selectedOptic)) selectedOptic = null;
        }

        private WeaponLoadout BuildPreviewLoadout()
        {
            WeaponLoadout result = new WeaponLoadout();
            WeaponLoadout source = GetSourceLoadout();
            if (source != null) result.CopyFrom(source);

            WeaponDefinition weapon = result.Weapon != null ? result.Weapon : ResolveSelectedWeaponDefinition();
            if (weapon == null) return result;
            result.SetWeapon(weapon);
            result.Unequip(AttachmentSlot.Optic);
            if (selectedOptic != null) result.Equip(selectedOptic);
            return result;
        }

        private WeaponLoadout GetSourceLoadout()
        {
            PlayerMatchLoadout matchLoadout = inventory != null ? inventory.GetComponent<PlayerMatchLoadout>()
                : previewRig != null ? previewRig.GetComponentInParent<PlayerMatchLoadout>() : null;
            if (matchLoadout == null) return null;
            return selectedSlot == WeaponInventorySlot.Primary ? matchLoadout.Primary : matchLoadout.Secondary;
        }

        private WeaponDefinition ResolveSelectedWeaponDefinition()
        {
            WeaponLoadout source = GetSourceLoadout();
            if (source != null && source.Weapon != null) return source.Weapon;

            HitscanWeapon hitscan = inventory != null ? inventory.GetComponent<HitscanWeapon>() : null;
            if (selectedSlot == WeaponInventorySlot.Primary && hitscan != null && hitscan.Loadout != null &&
                hitscan.Loadout.Weapon != null)
                return hitscan.Loadout.Weapon;

            WeaponLoadoutCatalog catalog = AssetDatabase.LoadAssetAtPath<WeaponLoadoutCatalog>(LoadoutCatalogPath);
            return catalog != null
                ? selectedSlot == WeaponInventorySlot.Primary ? catalog.DefaultPrimaryWeapon : catalog.DefaultSecondaryWeapon
                : null;
        }

        private bool TryGetSelectedOpticVisual(out WeaponAttachmentViewmodelVisual visual)
        {
            if (selectedOptic != null && previewLoadout != null && previewLoadout.Weapon != null &&
                selectedOptic.TryGetViewmodelVisual(previewLoadout.Weapon, out visual))
                return true;
            visual = null;
            return false;
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
            sourceAttachmentPresenter = inventory.GetComponent<WeaponAttachmentViewmodelPresenter>();
            return ConfigureRigForSlot(previewRig, inventory, selectedSlot, previewLoadout, true);
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
            if (sourceAttachmentPresenter != null) sourceAttachmentPresenter.Clear();
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
            sourceAttachmentPresenter = null;
        }

        private static bool ConfigureRigForSlot(LowPolyShooterViewmodelRig rig, WeaponInventoryController sourceInventory,
            WeaponInventorySlot slot, WeaponLoadout loadout, bool transientPreview)
        {
            if (rig == null || sourceInventory == null || !sourceInventory.TryGetViewmodelSlot(slot, out WeaponViewmodelSlot selected))
                return false;
            sourceInventory.TryGetViewmodelSlot(WeaponInventorySlot.Primary, out WeaponViewmodelSlot primary);
            sourceInventory.TryGetViewmodelSlot(WeaponInventorySlot.Secondary, out WeaponViewmodelSlot secondary);
            if (primary != null && primary.VisualRoot != null)
                primary.VisualRoot.gameObject.SetActive(slot == WeaponInventorySlot.Primary);
            if (secondary != null && secondary.VisualRoot != null)
                secondary.VisualRoot.gameObject.SetActive(slot == WeaponInventorySlot.Secondary);
            WeaponAttachmentViewmodelPresenter attachmentPresenter = sourceInventory.GetComponent<WeaponAttachmentViewmodelPresenter>();
            Transform aimAnchor = attachmentPresenter != null
                ? attachmentPresenter.Apply(loadout, selected.VisualRoot, selected.AimAnchor, transientPreview)
                : selected.AimAnchor;
            WeaponAdsProfile activeAdsProfile = selected.PresentationProfile != null
                ? selected.PresentationProfile.ResolveAdsProfile(loadout)
                : selected.AdsProfile;
            rig.ConfigureWeaponPresentation(selected.ArmsController, selected.WeaponAnimator, selected.Muzzle,
                aimAnchor, selected.Magazine, activeAdsProfile, selected.PresentationProfile);
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
                (previewInventory != null && !ConfigureRigForSlot(previewModelRig, previewInventory, selectedSlot,
                    previewLoadout, true)))
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

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName)) return null;
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == objectName) return candidate;
            return null;
        }

        private static Transform FindAttachmentCalibrationTransform(Transform attachmentRoot)
        {
            if (attachmentRoot == null) return null;
            ViewmodelAttachmentCalibrationRoot calibrationRoot =
                attachmentRoot.GetComponentInChildren<ViewmodelAttachmentCalibrationRoot>(true);
            return calibrationRoot != null ? calibrationRoot.transform : attachmentRoot.Find("Model");
        }

        private static ViewmodelScopeLens FindScopeLens(Transform root)
        {
            return root != null ? root.GetComponentInChildren<ViewmodelScopeLens>(true) : null;
        }

        private bool TryGetLiveAttachmentRoot(LowPolyShooterViewmodelRig rig, out Transform attachmentRoot)
        {
            attachmentRoot = null;
            if (rig == null || selectedOptic == null) return false;
            attachmentRoot = FindDescendant(rig.transform, selectedOptic.displayName + " (Attachment Visual)");
            return attachmentRoot != null;
        }

        private void ApplyScopeLensPrefabEditsToLivePreviews(WeaponAttachmentViewmodelVisual visual,
            Vector3 lensPosition, Vector3 lensRotation, float apertureDiameter, bool orientationAuthored,
            bool visualReviewed)
        {
            ApplyScopeLensPrefabEdits(previewRig, visual, lensPosition, lensRotation, apertureDiameter,
                orientationAuthored, visualReviewed);
            if (attachmentSceneDragActive)
            {
                SceneView.RepaintAll();
                Repaint();
                return;
            }
            ApplyScopeLensPrefabEdits(cameraPreviewRig, visual, lensPosition, lensRotation, apertureDiameter,
                orientationAuthored, visualReviewed);
            ApplyScopeLensPrefabEdits(adsPreviewRig, visual, lensPosition, lensRotation, apertureDiameter,
                orientationAuthored, visualReviewed);
            TickPreview();
        }

        private void ApplyScopeLensPrefabEdits(LowPolyShooterViewmodelRig rig,
            WeaponAttachmentViewmodelVisual visual, Vector3 lensPosition, Vector3 lensRotation,
            float apertureDiameter, bool orientationAuthored, bool visualReviewed)
        {
            if (rig == null || visual == null || !TryGetLiveAttachmentRoot(rig, out Transform attachmentRoot)) return;
            ViewmodelScopeLens scopeLens = FindScopeLens(attachmentRoot);
            if (scopeLens == null) return;
            scopeLens.transform.localPosition = lensPosition;
            scopeLens.transform.localEulerAngles = lensRotation;
            scopeLens.Configure(apertureDiameter);
            scopeLens.SetOrientationAuthored(orientationAuthored);
            scopeLens.SetVisualPlacementReviewed(visualReviewed);
        }

        private void ApplyAttachmentPrefabEditsToLivePreviews(WeaponAttachmentViewmodelVisual visual,
            bool updateAnchor, Vector3 anchorPosition, Vector3 anchorRotation, bool orientationAuthored,
            bool updateModel, Vector3 modelPosition, Vector3 modelRotation, Vector3 modelScale)
        {
            ApplyAttachmentPrefabEdits(previewRig, visual, updateAnchor, anchorPosition, anchorRotation,
                orientationAuthored, updateModel, modelPosition, modelRotation, modelScale);
            if (attachmentSceneDragActive)
            {
                SceneView.RepaintAll();
                Repaint();
                return;
            }
            ApplyAttachmentPrefabEdits(cameraPreviewRig, visual, updateAnchor, anchorPosition, anchorRotation,
                orientationAuthored, updateModel, modelPosition, modelRotation, modelScale);
            ApplyAttachmentPrefabEdits(adsPreviewRig, visual, updateAnchor, anchorPosition, anchorRotation,
                orientationAuthored, updateModel, modelPosition, modelRotation, modelScale);
            TickPreview();
        }

        private void ApplyAttachmentPrefabEdits(LowPolyShooterViewmodelRig rig, WeaponAttachmentViewmodelVisual visual,
            bool updateAnchor, Vector3 anchorPosition, Vector3 anchorRotation, bool orientationAuthored,
            bool updateModel, Vector3 modelPosition, Vector3 modelRotation, Vector3 modelScale)
        {
            if (rig == null || selectedOptic == null || visual == null) return;
            Transform attachmentRoot = FindDescendant(rig.transform,
                selectedOptic.displayName + " (Attachment Visual)");
            if (attachmentRoot == null) return;

            if (updateAnchor)
            {
                Transform anchor = FindDescendant(attachmentRoot, visual.AimAnchorName);
                if (anchor != null)
                {
                    anchor.localPosition = anchorPosition;
                    anchor.localEulerAngles = anchorRotation;
                    AdsSightReference sightReference = anchor.GetComponent<AdsSightReference>();
                    if (sightReference == null) sightReference = anchor.gameObject.AddComponent<AdsSightReference>();
                    sightReference.SetOrientationAuthored(orientationAuthored);
                }
            }

            if (updateModel)
            {
                Transform model = FindAttachmentCalibrationTransform(attachmentRoot);
                if (model == null) return;
                SetModelPoseAndMoveAttachmentProbes(model, modelPosition, modelRotation, modelScale);
            }
        }

        private static void SetModelPoseAndMoveAttachmentProbes(Transform model, Vector3 localPosition,
            Vector3 localRotation, Vector3 localScale)
        {
            if (model == null) return;
            List<CalibrationDependentPose> dependentPoses = new List<CalibrationDependentPose>();
            Transform attachmentRoot = model.parent;
            if (attachmentRoot != null)
            {
                foreach (Transform candidate in attachmentRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (candidate == null || candidate == model || candidate.IsChildOf(model)) continue;
                    bool followsMechanicalFit = candidate.GetComponent<ViewmodelClipProbe>() != null ||
                        candidate.GetComponent<ViewmodelScopeLens>() != null ||
                        candidate.GetComponent<AdsSightReference>() != null || candidate.name.StartsWith("AimAnchor_");
                    if (!followsMechanicalFit) continue;
                    dependentPoses.Add(new CalibrationDependentPose(candidate,
                        model.InverseTransformPoint(candidate.position),
                        Quaternion.Inverse(model.rotation) * candidate.rotation));
                }
            }

            model.localPosition = localPosition;
            model.localEulerAngles = localRotation;
            model.localScale = localScale;
            foreach (CalibrationDependentPose dependentPose in dependentPoses)
            {
                if (dependentPose.Transform == null) continue;
                dependentPose.Transform.SetPositionAndRotation(model.TransformPoint(dependentPose.LocalPosition),
                    model.rotation * dependentPose.LocalRotation);
            }
        }

        private static void RecordSiblingProbeUndo(Transform model)
        {
            if (model == null || model.parent == null) return;
            foreach (Transform candidate in model.parent.GetComponentsInChildren<Transform>(true))
            {
                if (candidate == null || candidate == model || candidate.IsChildOf(model)) continue;
                if (candidate.GetComponent<ViewmodelClipProbe>() != null ||
                    candidate.GetComponent<ViewmodelScopeLens>() != null ||
                    candidate.GetComponent<AdsSightReference>() != null || candidate.name.StartsWith("AimAnchor_"))
                    Undo.RecordObject(candidate, "同步附件校准节点");
            }
        }

        private void DrawAttachmentSceneEditHandle()
        {
            if (attachmentSceneEditMode == AttachmentSceneEditMode.None || selectedOptic == null || previewRig == null ||
                loadedAttachmentPrefabContents == null || !TryGetSelectedOpticVisual(out WeaponAttachmentViewmodelVisual visual))
                return;

            Transform attachmentRoot = FindDescendant(previewRig.transform,
                selectedOptic.displayName + " (Attachment Visual)");
            if (attachmentRoot == null) return;

            Transform assetAnchor = FindDescendant(loadedAttachmentPrefabContents.transform, visual.AimAnchorName);
            Transform assetModel = FindAttachmentCalibrationTransform(loadedAttachmentPrefabContents.transform);
            ViewmodelScopeLens assetLens = FindScopeLens(loadedAttachmentPrefabContents.transform);
            Transform liveAnchor = FindDescendant(attachmentRoot, visual.AimAnchorName);
            Transform liveModel = FindAttachmentCalibrationTransform(attachmentRoot);
            ViewmodelScopeLens liveLens = FindScopeLens(attachmentRoot);
            bool orientationAuthored = assetAnchor != null &&
                assetAnchor.GetComponent<AdsSightReference>()?.OrientationAuthored == true;

            switch (attachmentSceneEditMode)
            {
                case AttachmentSceneEditMode.ModelPosition:
                    if (assetModel == null || liveModel == null) return;
                    Handles.color = new Color(0.25f, 0.65f, 1f, 0.95f);
                    Handles.Label(liveModel.position, " ATTACHMENT MODEL");
                    EditorGUI.BeginChangeCheck();
                    Vector3 modelPosition = Handles.PositionHandle(liveModel.position, liveModel.rotation);
                    BeginAttachmentSceneUndoOnHandleGrab(false, true);
                    if (EditorGUI.EndChangeCheck())
                    {
                        ApplyAttachmentPrefabAssetEdits(visual, assetAnchor, assetModel, false, Vector3.zero,
                            Vector3.zero, false, true, liveModel.parent.InverseTransformPoint(modelPosition), assetModel.localEulerAngles,
                            assetModel.localScale, BeginAttachmentSceneUndo(false, true));
                    }
                    break;

                case AttachmentSceneEditMode.ModelRotation:
                    if (assetModel == null || liveModel == null) return;
                    Handles.color = new Color(0.25f, 1f, 0.55f, 0.95f);
                    Handles.Label(liveModel.position, " ATTACHMENT MODEL ROTATION");
                    EditorGUI.BeginChangeCheck();
                    Quaternion modelRotation = Handles.RotationHandle(liveModel.rotation, liveModel.position);
                    BeginAttachmentSceneUndoOnHandleGrab(false, true);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Quaternion localRotation = Quaternion.Inverse(liveModel.parent.rotation) * modelRotation;
                        ApplyAttachmentPrefabAssetEdits(visual, assetAnchor, assetModel, false, Vector3.zero,
                            Vector3.zero, false, true, assetModel.localPosition, localRotation.eulerAngles, assetModel.localScale,
                            BeginAttachmentSceneUndo(false, true));
                    }
                    break;

                case AttachmentSceneEditMode.AimAnchorPosition:
                    if (assetAnchor == null || liveAnchor == null) return;
                    Handles.color = new Color(1f, 0.78f, 0.15f, 0.95f);
                    Handles.Label(liveAnchor.position, " ADS SIGHT REFERENCE");
                    EditorGUI.BeginChangeCheck();
                    Vector3 anchorPosition = Handles.PositionHandle(liveAnchor.position, liveAnchor.rotation);
                    BeginAttachmentSceneUndoOnHandleGrab(true, false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        ApplyAttachmentPrefabAssetEdits(visual, assetAnchor, assetModel, true,
                            liveAnchor.parent.InverseTransformPoint(anchorPosition), assetAnchor.localEulerAngles,
                            orientationAuthored, false, Vector3.zero, Vector3.zero, Vector3.one,
                            BeginAttachmentSceneUndo(true, false));
                    }
                    break;

                case AttachmentSceneEditMode.AimAnchorRotation:
                    if (assetAnchor == null || liveAnchor == null) return;
                    Handles.color = new Color(1f, 0.62f, 0.1f, 0.95f);
                    Handles.Label(liveAnchor.position, " ADS OPTICAL AXIS");
                    EditorGUI.BeginChangeCheck();
                    Quaternion anchorRotation = Handles.RotationHandle(liveAnchor.rotation, liveAnchor.position);
                    BeginAttachmentSceneUndoOnHandleGrab(true, false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Quaternion localRotation = Quaternion.Inverse(liveAnchor.parent.rotation) * anchorRotation;
                        ApplyAttachmentPrefabAssetEdits(visual, assetAnchor, assetModel, true,
                            assetAnchor.localPosition, localRotation.eulerAngles, true, false, Vector3.zero,
                            Vector3.zero, Vector3.one, BeginAttachmentSceneUndo(true, false));
                    }
                    break;

                case AttachmentSceneEditMode.LensPosition:
                    if (assetLens == null || liveLens == null || liveLens.transform.parent == null) return;
                    Handles.color = new Color(0.15f, 1f, 0.95f, 0.95f);
                    Handles.Label(liveLens.transform.position, " LENS ANCHOR");
                    EditorGUI.BeginChangeCheck();
                    Vector3 lensPosition = Handles.PositionHandle(liveLens.transform.position,
                        liveLens.transform.rotation);
                    BeginAttachmentSceneUndoOnHandleGrab(false, false, true);
                    if (EditorGUI.EndChangeCheck())
                        ApplyScopeLensPrefabAssetEdits(visual, assetLens,
                            liveLens.transform.parent.InverseTransformPoint(lensPosition),
                            assetLens.transform.localEulerAngles, assetLens.ClearApertureDiameter,
                            assetLens.OrientationAuthored, false, BeginAttachmentSceneUndo(false, false, true));
                    break;

                case AttachmentSceneEditMode.LensRotation:
                    if (assetLens == null || liveLens == null || liveLens.transform.parent == null) return;
                    Handles.color = new Color(0.1f, 0.85f, 1f, 0.95f);
                    Handles.Label(liveLens.transform.position, " LENS PLANE (+Z TARGET)");
                    EditorGUI.BeginChangeCheck();
                    Quaternion lensRotation = Handles.RotationHandle(liveLens.transform.rotation,
                        liveLens.transform.position);
                    BeginAttachmentSceneUndoOnHandleGrab(false, false, true);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Quaternion localRotation = Quaternion.Inverse(liveLens.transform.parent.rotation) * lensRotation;
                        ApplyScopeLensPrefabAssetEdits(visual, assetLens, assetLens.transform.localPosition,
                            localRotation.eulerAngles, assetLens.ClearApertureDiameter, true, false,
                            BeginAttachmentSceneUndo(false, false, true));
                    }
                    break;

                case AttachmentSceneEditMode.LensAperture:
                    if (assetLens == null || liveLens == null) return;
                    Handles.color = new Color(0.2f, 1f, 0.72f, 0.95f);
                    Vector3 lensScale = liveLens.transform.lossyScale;
                    float apertureScale = Mathf.Max(0.0001f,
                        Mathf.Max(Mathf.Abs(lensScale.x), Mathf.Abs(lensScale.y)));
                    float worldRadius = liveLens.ClearApertureDiameter * apertureScale * 0.5f;
                    Handles.Label(liveLens.transform.position, " CLEAR APERTURE");
                    EditorGUI.BeginChangeCheck();
                    float newWorldRadius = Handles.RadiusHandle(liveLens.transform.rotation,
                        liveLens.transform.position, worldRadius);
                    BeginAttachmentSceneUndoOnHandleGrab(false, false, true);
                    if (EditorGUI.EndChangeCheck())
                        ApplyScopeLensPrefabAssetEdits(visual, assetLens, assetLens.transform.localPosition,
                            assetLens.transform.localEulerAngles,
                            Mathf.Clamp(newWorldRadius * 2f / apertureScale, 0.005f, 0.15f),
                            assetLens.OrientationAuthored, false,
                            BeginAttachmentSceneUndo(false, false, true));
                    break;
            }
        }

        private int BeginAttachmentSceneUndo(bool anchorChanged, bool modelChanged, bool lensChanged = false)
        {
            if (attachmentSceneUndoGroup >= 0) return attachmentSceneUndoGroup;
            Undo.IncrementCurrentGroup();
            attachmentSceneUndoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(GetAttachmentEditUndoName(anchorChanged, modelChanged, lensChanged));
            attachmentSceneDragActive = true;
            attachmentSceneDragMode = attachmentSceneEditMode;
            return attachmentSceneUndoGroup;
        }

        private void BeginAttachmentSceneUndoOnHandleGrab(bool anchorChanged, bool modelChanged,
            bool lensChanged = false)
        {
            if (attachmentSceneDragActive || Event.current == null ||
                Event.current.rawType != EventType.MouseDown || GUIUtility.hotControl == 0) return;
            BeginAttachmentSceneUndo(anchorChanged, modelChanged, lensChanged);
        }

        private void EndAttachmentSceneUndoOnMouseUp()
        {
            if (!attachmentSceneDragActive || Event.current == null || Event.current.type != EventType.MouseUp) return;
            CompleteAttachmentSceneDrag();
        }

        private void CompleteAttachmentSceneDrag()
        {
            if (!attachmentSceneDragActive && attachmentSceneUndoGroup < 0) return;
            int undoGroup = attachmentSceneUndoGroup;
            attachmentSceneUndoGroup = -1;
            attachmentSceneDragActive = false;
            attachmentSceneDragMode = AttachmentSceneEditMode.None;
            if (undoGroup >= 0) Undo.CollapseUndoOperations(undoGroup);
            if (attachmentPrefabSavePending) SaveAttachmentPrefabContentsNow(true);
            SynchronizeLoadedAttachmentPrefabToLivePreviews();
            previousTickTime = EditorApplication.timeSinceStartup;
            SceneView.RepaintAll();
            Repaint();
        }

        private void DrawSceneGuides(SceneView sceneView)
        {
            if (!previewActive || !CanPreview()) return;
            if (showClipProbeGuides) DrawClipProbeGuides(previewRig, previewCamera);
            if (showScopeLensGuides) DrawScopeLensSceneGuide(previewRig, previewCamera);
            DrawAttachmentSceneEditHandle();
            EndAttachmentSceneUndoOnMouseUp();
            if (previewMode == PreviewMode.Hip || profile == null) return;
            Vector3 cameraPosition = previewCamera.transform.position;
            Vector3 aimEnd = cameraPosition + previewCamera.transform.forward * 3f;
            Handles.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            Handles.DrawAAPolyLine(3f, cameraPosition, aimEnd);
            Handles.DrawWireDisc(aimEnd, previewCamera.transform.forward, 0.04f);
            Handles.Label(aimEnd, " CAMERA AIM");

            Vector3 sightReference = WeaponAdsAlignment.GetSightReferenceWorldPosition(previewRig.AimAnchor, profile);
            if (previewRig.AimAnchor != null)
            {
                AdsSightReference authoredReference = previewRig.AimAnchor.GetComponent<AdsSightReference>();
                bool authored = authoredReference != null && authoredReference.OrientationAuthored;
                Handles.color = authored
                    ? new Color(1f, 0.72f, 0.12f, 0.95f)
                    : new Color(1f, 0.45f, 0.12f, 0.75f);
                Vector3 opticalAxisEnd = sightReference + previewRig.AimAnchor.forward * 0.8f;
                Handles.DrawAAPolyLine(authored ? 4f : 2f, sightReference, opticalAxisEnd);
                Handles.Label(opticalAxisEnd, authored ? " AUTHORED ADS AXIS" : " LEGACY ADS AXIS (NOT AUTHORED)");
            }
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

        private static void DrawScopeLensSceneGuide(LowPolyShooterViewmodelRig rig, Camera camera)
        {
            if (!TryMeasureScopeLens(rig, camera, out ScopeLensMeasurement measurement) ||
                measurement.Lens == null) return;
            ViewmodelScopeLens lens = measurement.Lens;
            Vector3 lossyScale = lens.transform.lossyScale;
            float radius = lens.ClearApertureDiameter *
                Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y)) * 0.5f;
            bool valid = IsScopeLensMeasurementValid(measurement) && lens.OrientationAuthored;
            ScopeLensCentreGrade centreGrade = GetScopeLensCentreGrade(measurement);
            Handles.color = valid ? GetScopeLensCentreGradeColor(centreGrade) : new Color(1f, 0.28f, 0.22f, 0.95f);
            Handles.DrawWireDisc(lens.transform.position, lens.transform.forward, radius);
            Handles.DrawAAPolyLine(3f, lens.transform.position,
                lens.transform.position + lens.transform.forward * Mathf.Max(0.12f, radius * 3f));
            Handles.Label(lens.transform.position + lens.transform.up * (radius + 0.006f),
                $" LENS · {lens.ClearApertureDiameter * 1000f:0.0}mm · " +
                (lens.OrientationAuthored ? "AUTHORED" : "FALLBACK") + " · " +
                GetScopeLensCentreGradeLabel(centreGrade) + " · " + FormatScopeLensCentreOffset(measurement));
        }

        private void DrawClipProbeGuides(LowPolyShooterViewmodelRig rig, Camera camera)
        {
            if (rig == null || camera == null) return;
            Transform weaponVisual = rig.WeaponAnimator != null ? rig.WeaponAnimator.transform : rig.transform;
            foreach (ViewmodelClipProbe probe in weaponVisual.GetComponentsInChildren<ViewmodelClipProbe>(true))
            {
                if (probe == null || !probe.IsActiveForValidation || !probe.gameObject.activeInHierarchy) continue;
                float clearance = camera.transform.InverseTransformPoint(probe.transform.position).z - probe.SurfaceRadius;
                bool safe = clearance >= RequiredViewmodelClearance;
                Handles.color = safe ? new Color(0.3f, 1f, 0.45f, 0.9f) : new Color(1f, 0.28f, 0.2f, 0.95f);
                Handles.DrawWireDisc(probe.transform.position, camera.transform.forward, probe.SurfaceRadius);
                Handles.Label(probe.transform.position + camera.transform.up * (probe.SurfaceRadius + 0.008f),
                    $" CLIP · {probe.ValidationLabel} · {clearance * 1000f:0.0}mm");
            }
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

        private void NudgeAttachmentModel(WeaponAttachmentViewmodelVisual visual, Vector3 desiredWorldMovement)
        {
            if (!previewActive || visual == null || desiredWorldMovement.sqrMagnitude < 0.000001f || selectedOptic == null)
                return;
            string prefabPath = visual.Prefab != null ? AssetDatabase.GetAssetPath(visual.Prefab) : null;
            if (string.IsNullOrWhiteSpace(prefabPath) ||
                !TryGetAttachmentPrefabContents(visual, prefabPath, out GameObject prefabContents)) return;
            Transform assetModel = FindAttachmentCalibrationTransform(prefabContents.transform);
            Transform assetAnchor = FindDescendant(prefabContents.transform, visual.AimAnchorName);
            Transform attachmentRoot = FindDescendant(previewRig.transform,
                selectedOptic.displayName + " (Attachment Visual)");
            if (assetModel == null || attachmentRoot == null) return;

            Vector3 localDelta = attachmentRoot.InverseTransformVector(desiredWorldMovement.normalized * visualNudgeStep);
            ApplyAttachmentPrefabAssetEdits(visual, assetAnchor, assetModel, false, Vector3.zero, Vector3.zero,
                false, true, assetModel.localPosition + localDelta, assetModel.localEulerAngles, assetModel.localScale);
        }

        private void NudgeScopeLens(WeaponAttachmentViewmodelVisual visual, Vector3 desiredWorldMovement)
        {
            if (!previewActive || visual == null || desiredWorldMovement.sqrMagnitude < 0.000001f ||
                loadedAttachmentPrefabContents == null || !TryGetLiveAttachmentRoot(previewRig,
                    out Transform attachmentRoot)) return;
            ViewmodelScopeLens assetLens = FindScopeLens(loadedAttachmentPrefabContents.transform);
            ViewmodelScopeLens liveLens = FindScopeLens(attachmentRoot);
            if (assetLens == null || liveLens == null || liveLens.transform.parent == null) return;

            Vector3 localDelta = liveLens.transform.parent.InverseTransformVector(
                desiredWorldMovement.normalized * visualNudgeStep);
            ApplyScopeLensPrefabAssetEdits(visual, assetLens, assetLens.transform.localPosition + localDelta,
                assetLens.transform.localEulerAngles, assetLens.ClearApertureDiameter,
                assetLens.OrientationAuthored, false);
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

        private float RequiredViewmodelClearance => ViewmodelCameraRenderer.NearClipPlane + viewmodelSafetyMargin;

        private bool TryMeasureClipProbes(PreviewMode pose, List<ClipProbeMeasurement> measurements)
        {
            measurements.Clear();
            if (!previewActive) return false;

            LowPolyShooterViewmodelRig measuredRig = pose == PreviewMode.Hip ? cameraPreviewRig : adsPreviewRig;
            Camera measuredCamera = pose == PreviewMode.Hip ? cameraPreviewCamera : adsPreviewCamera;
            if (measuredRig == null || measuredCamera == null) return false;

            Transform weaponVisual = measuredRig.WeaponAnimator != null
                ? measuredRig.WeaponAnimator.transform
                : measuredRig.transform;
            foreach (ViewmodelClipProbe probe in weaponVisual.GetComponentsInChildren<ViewmodelClipProbe>(true))
            {
                if (probe == null || !probe.IsActiveForValidation || !probe.gameObject.activeInHierarchy) continue;
                float centreDepth = measuredCamera.transform.InverseTransformPoint(probe.transform.position).z;
                float clearance = centreDepth - probe.SurfaceRadius;
                measurements.Add(new ClipProbeMeasurement(probe, clearance, centreDepth > 0f));
            }
            return measurements.Count > 0;
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
