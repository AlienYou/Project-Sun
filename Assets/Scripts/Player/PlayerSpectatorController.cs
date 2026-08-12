using ProjectSun.FPS.Core;
using ProjectSun.FPS.Input;
using ProjectSun.FPS.Rounds;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ProjectSun.FPS.Player
{
    /// <summary>
    /// 本地玩家死亡后的只读观战表现控制器。合法目标完全由 RoundManager 提供；本组件只负责相机、
    /// 音频监听器、切换输入和墙体碰撞，不参与成员存活、回合结算或复活。
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class PlayerSpectatorController : MonoBehaviour
    {
        private const string SpectatorCameraName = "Spectator Camera";

        [Header("Follow Framing")]
        [SerializeField, Min(0.5f)]
        [Tooltip("观战相机注视点相对目标脚底的世界空间高度，单位为米。")]
        private float focusHeight = 1.3f;

        [SerializeField]
        [Tooltip("相对目标水平朝向的观战相机偏移，单位为米：X 为右侧肩偏移，Y 为高度，Z 为前后距离；负 Z 位于目标身后。")]
        private Vector3 cameraLocalOffset = new Vector3(0.55f, 0.75f, -3.4f);

        [SerializeField, Min(0f)]
        [Tooltip("注视点沿目标前方增加的距离，单位为米，用于让画面保留目标前方战场空间。")]
        private float lookAheadDistance = 0.55f;

        [Header("Smoothing")]
        [SerializeField, Min(0.1f)]
        [Tooltip("位置跟随锐度，单位为每秒；数值越大越紧跟，默认 12 在帧率变化时保持近似一致。")]
        private float positionSharpness = 12f;

        [SerializeField, Min(0.1f)]
        [Tooltip("旋转跟随锐度，单位为每秒；数值越大越快对准目标。")]
        private float rotationSharpness = 14f;

        [Header("Collision")]
        [SerializeField, Min(0.05f)]
        [Tooltip("相机防穿墙球形检测半径，单位为米。只检测项目 Wall Layer，不与角色碰撞。")]
        private float collisionRadius = 0.2f;

        [SerializeField, Min(0f)]
        [Tooltip("检测到墙面后额外保留的安全距离，单位为米，避免相机近裁剪面贴入墙体。")]
        private float collisionPadding = 0.08f;

        private RoundManager roundManager;
        private Camera playerCamera;
        private FpsInput input;
        private AudioListener playerAudioListener;
        private Camera spectatorCamera;
        private AudioListener spectatorAudioListener;
        private TeamCombatant currentTarget;
        private int currentTargetSlot = -1;
        private bool playerCameraWasEnabled;
        private bool playerAudioWasEnabled;

        /// <summary>是否正在由观战相机接管本地画面。</summary>
        public bool IsSpectating { get; private set; }

        /// <summary>当前己方观战目标；回合结算或没有存活队友时可能为 null。</summary>
        public TeamCombatant CurrentTarget => currentTarget;

        /// <summary>当前目标的稳定阵营槽位；没有目标时为 -1。</summary>
        public int CurrentTargetSlot => currentTargetSlot;

        /// <summary>“下一目标”按键的玩家配置显示名称。</summary>
        public string NextBindingDisplayName => input != null
            ? input.GetBindingDisplayName(FpsBinding.SpectateNext)
            : "RIGHT ARROW";

        /// <summary>“上一目标”按键的玩家配置显示名称。</summary>
        public string PreviousBindingDisplayName => input != null
            ? input.GetBindingDisplayName(FpsBinding.SpectatePrevious)
            : "LEFT ARROW";

        /// <summary>配置本地观战所需的权威状态、原玩家相机和输入引用。</summary>
        /// <param name="matchRoundManager">提供死亡状态与合法己方观战目标的回合权威，不能为空。</param>
        /// <param name="gameplayCamera">正常第一人称 Base Camera；观战期间会暂时停用并在新回合恢复。</param>
        /// <param name="playerInput">本地 Input System 包装器；为空时仍可自动观战，但不能手动切换目标。</param>
        public void Configure(RoundManager matchRoundManager, Camera gameplayCamera, FpsInput playerInput)
        {
            StopSpectating();
            if (input != null) input.FieldOfViewChanged -= OnFieldOfViewChanged;

            roundManager = matchRoundManager;
            playerCamera = gameplayCamera;
            input = playerInput;
            playerAudioListener = playerCamera != null ? playerCamera.GetComponent<AudioListener>() : null;

            if (input != null) input.FieldOfViewChanged += OnFieldOfViewChanged;
            if (roundManager == null || playerCamera == null)
                Debug.LogError("死亡观战缺少 RoundManager 或玩家相机，观战功能不会启用。", this);
        }

        private void Update()
        {
            if (roundManager == null || playerCamera == null) return;

            bool shouldOwnCamera = roundManager.IsLocalPlayerEliminated &&
                                   roundManager.State != RoundState.Preparation;
            if (!shouldOwnCamera)
            {
                StopSpectating();
                return;
            }

            if (!IsSpectating) StartSpectating();
            if (!roundManager.CanLocalPlayerSpectate)
            {
                // 全队淘汰或进入结算后冻结最后观战画面，直到准备阶段恢复第一人称相机。
                currentTarget = null;
                currentTargetSlot = -1;
                return;
            }

            if (currentTarget == null || !currentTarget.IsAlive)
            {
                SelectNextTarget(currentTargetSlot);
                return;
            }

            if (input != null && input.WasPressed(FpsBinding.SpectateNext))
                SelectNextTarget(currentTargetSlot);
            else if (input != null && input.WasPressed(FpsBinding.SpectatePrevious))
                SelectPreviousTarget(currentTargetSlot);
        }

        private void LateUpdate()
        {
            if (!IsSpectating || spectatorCamera == null || currentTarget == null || !currentTarget.IsAlive) return;

            CalculateTargetPose(currentTarget.transform, out Vector3 desiredPosition, out Quaternion desiredRotation);
            float deltaTime = Mathf.Max(0f, Time.unscaledDeltaTime);
            float positionBlend = 1f - Mathf.Exp(-positionSharpness * deltaTime);
            float rotationBlend = 1f - Mathf.Exp(-rotationSharpness * deltaTime);
            spectatorCamera.transform.position = Vector3.Lerp(spectatorCamera.transform.position, desiredPosition, positionBlend);
            spectatorCamera.transform.rotation = Quaternion.Slerp(spectatorCamera.transform.rotation, desiredRotation, rotationBlend);
        }

        private void OnDisable() => StopSpectating();

        private void OnDestroy()
        {
            StopSpectating();
            if (input != null) input.FieldOfViewChanged -= OnFieldOfViewChanged;
            if (spectatorCamera != null) Destroy(spectatorCamera.gameObject);
        }

        private void StartSpectating()
        {
            EnsureSpectatorCamera();
            if (spectatorCamera == null) return;

            playerCameraWasEnabled = playerCamera.enabled;
            playerAudioWasEnabled = playerAudioListener != null && playerAudioListener.enabled;
            if (playerAudioListener != null) playerAudioListener.enabled = false;
            playerCamera.enabled = false;
            spectatorCamera.transform.SetPositionAndRotation(playerCamera.transform.position, playerCamera.transform.rotation);
            spectatorCamera.enabled = true;
            if (spectatorAudioListener != null) spectatorAudioListener.enabled = true;
            IsSpectating = true;

            SelectNextTarget(-1);
        }

        private void StopSpectating()
        {
            if (!IsSpectating) return;

            if (spectatorAudioListener != null) spectatorAudioListener.enabled = false;
            if (spectatorCamera != null) spectatorCamera.enabled = false;
            if (playerCamera != null) playerCamera.enabled = playerCameraWasEnabled;
            if (playerAudioListener != null) playerAudioListener.enabled = playerAudioWasEnabled;
            currentTarget = null;
            currentTargetSlot = -1;
            IsSpectating = false;
        }

        /// <summary>选择当前槽位之后的合法己方存活目标。</summary>
        /// <param name="afterSlotIndex">当前目标槽位；-1 表示从槽位 0 开始。</param>
        private void SelectNextTarget(int afterSlotIndex)
        {
            if (roundManager.TryGetNextLocalSpectatorTarget(afterSlotIndex, out TeamCombatant target))
                SetTarget(target);
        }

        /// <summary>选择当前槽位之前的合法己方存活目标。</summary>
        /// <param name="beforeSlotIndex">当前目标槽位；-1 表示从末槽位开始。</param>
        private void SelectPreviousTarget(int beforeSlotIndex)
        {
            if (roundManager.TryGetPreviousLocalSpectatorTarget(beforeSlotIndex, out TeamCombatant target))
                SetTarget(target);
        }

        /// <summary>切换目标并立即播种相机姿态，避免相机跨越关卡飞向新队友。</summary>
        /// <param name="target">RoundManager 已确认合法且存活的己方成员。</param>
        private void SetTarget(TeamCombatant target)
        {
            if (target == null || spectatorCamera == null) return;
            currentTarget = target;
            currentTargetSlot = target.TeamSlot;
            CalculateTargetPose(target.transform, out Vector3 position, out Quaternion rotation);
            spectatorCamera.transform.SetPositionAndRotation(position, rotation);
        }

        private void EnsureSpectatorCamera()
        {
            if (spectatorCamera != null || playerCamera == null) return;

            GameObject cameraObject = new GameObject(SpectatorCameraName, typeof(Camera),
                typeof(UniversalAdditionalCameraData), typeof(AudioListener));
            cameraObject.transform.SetParent(transform, false);
            cameraObject.tag = "MainCamera";
            spectatorCamera = cameraObject.GetComponent<Camera>();
            spectatorCamera.CopyFrom(playerCamera);
            spectatorCamera.enabled = false;
            spectatorCamera.targetTexture = null;
            spectatorCamera.fieldOfView = input != null ? input.FieldOfView : playerCamera.fieldOfView;

            UniversalAdditionalCameraData spectatorData = cameraObject.GetComponent<UniversalAdditionalCameraData>();
            UniversalAdditionalCameraData playerData = playerCamera.GetComponent<UniversalAdditionalCameraData>();
            spectatorData.renderType = CameraRenderType.Base;
            spectatorData.cameraStack.Clear();
            spectatorData.renderPostProcessing = playerData != null && playerData.renderPostProcessing;

            spectatorAudioListener = cameraObject.GetComponent<AudioListener>();
            spectatorAudioListener.enabled = false;
        }

        /// <summary>根据目标水平朝向计算经过墙体约束的第三人称观战相机姿态。</summary>
        /// <param name="targetTransform">当前存活队友的根 Transform。</param>
        /// <param name="position">返回相机世界空间位置，单位为米。</param>
        /// <param name="rotation">返回朝向目标与前方战场的世界空间旋转。</param>
        private void CalculateTargetPose(Transform targetTransform, out Vector3 position, out Quaternion rotation)
        {
            Vector3 forward = Vector3.ProjectOnPlane(targetTransform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 focus = targetTransform.position + Vector3.up * focusHeight;
            Vector3 desiredPosition = focus + right * cameraLocalOffset.x + Vector3.up * cameraLocalOffset.y +
                                      forward * cameraLocalOffset.z;

            Vector3 cameraOffset = desiredPosition - focus;
            float desiredDistance = cameraOffset.magnitude;
            if (desiredDistance > 0.001f && Physics.SphereCast(focus, collisionRadius,
                    cameraOffset / desiredDistance, out RaycastHit hit, desiredDistance,
                    CombatLayers.WallMask, QueryTriggerInteraction.Ignore))
            {
                float safeDistance = Mathf.Max(collisionRadius, hit.distance - collisionPadding);
                desiredPosition = focus + cameraOffset.normalized * safeDistance;
            }

            Vector3 lookPoint = focus + forward * lookAheadDistance;
            Vector3 lookDirection = lookPoint - desiredPosition;
            if (lookDirection.sqrMagnitude < 0.0001f) lookDirection = forward;
            position = desiredPosition;
            rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        }

        /// <summary>同步玩家设置中的基础 FOV；观战相机不继承死亡瞬间可能残留的 ADS FOV。</summary>
        /// <param name="fieldOfView">新的垂直视场角，单位为度，已由 FpsInput 限制在 70-110。</param>
        private void OnFieldOfViewChanged(float fieldOfView)
        {
            if (spectatorCamera != null) spectatorCamera.fieldOfView = fieldOfView;
        }
    }
}
