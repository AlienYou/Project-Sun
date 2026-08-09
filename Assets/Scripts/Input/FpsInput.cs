using System;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectSun.FPS.Input
{
    /// <summary>Every player-facing bind that may be displayed or rebound in the settings menu.</summary>
    public enum FpsBinding
    {
        MoveForward,
        MoveBackward,
        MoveLeft,
        MoveRight,
        Fire,
        Aim,
        Jump,
        Sprint,
        Crouch,
        Reload,
        SelectPrimary,
        SelectSecondary,
        Dash,
        Focus,
        Interact,
        UseTactical,
        Loadout,
        Settings,
        Menu,
        DebugCombat
    }

    /// <summary>
    /// Runtime Input System map for the prototype. It keeps input policy, bindings and persisted player settings in one place,
    /// so gameplay code never depends on Unity's legacy Input Manager axes.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class FpsInput : MonoBehaviour
    {
        private const float DefaultLookSensitivity = 0.12f;
        private const float DefaultFieldOfView = 78f;
        private const string LookSensitivityKey = "ProjectSun.Input.LookSensitivity";
        private const string FieldOfViewKey = "ProjectSun.Video.FieldOfView";
        private const string BindingOverridesKey = "ProjectSun.Input.BindingOverrides";
        private const string BindingOverridesFormatPrefix = "PS_BINDINGS_V1;";
        private const int BindingCount = (int)FpsBinding.DebugCombat + 1;

        [Header("Defaults")]
        [Tooltip("鼠标视角灵敏度；有效范围 0.02～0.5，运行时会读取玩家本地设置覆盖该默认值。")]
        [SerializeField, Range(0.02f, 0.5f)] private float lookSensitivity = DefaultLookSensitivity;
        [Tooltip("主玩法相机的默认垂直视场角，单位为度；有效范围 70～110，运行时会读取玩家本地设置覆盖该默认值。")]
        [SerializeField, Range(70f, 110f)] private float fieldOfView = DefaultFieldOfView;

        private InputActionMap gameplayMap;
        private InputAction move;
        private InputAction look;
        private InputAction fire;
        private InputAction aim;
        private InputAction jump;
        private InputAction sprint;
        private InputAction crouch;
        private InputAction reload;
        private InputAction selectPrimary;
        private InputAction selectSecondary;
        private InputAction dash;
        private InputAction focus;
        private InputAction interact;
        private InputAction useTactical;
        private InputAction loadout;
        private InputAction settings;
        private InputAction menu;
        private InputAction debugCombat;
        private InputActionRebindingExtensions.RebindingOperation rebindingOperation;
        private readonly bool[] testHeldBindings = new bool[BindingCount];
        private bool testInputOverrideActive;
        private bool testInputOverrideAllowed;

        public float LookSensitivity => lookSensitivity;
        public float FieldOfView => fieldOfView;
        public bool GameplayEnabled { get; private set; } = true;
        public bool IsRebinding => rebindingOperation != null;

        public event Action<float> FieldOfViewChanged;

        private void Awake()
        {
            testInputOverrideAllowed = Application.isEditor || Debug.isDebugBuild;
            CreateActions();
            LoadPersistedSettings();
            gameplayMap.Enable();
        }

        private void OnDestroy()
        {
            EndTestInputOverride();
            rebindingOperation?.Dispose();
            gameplayMap?.Dispose();
        }

        public void SetGameplayEnabled(bool enabled) => GameplayEnabled = enabled;

        public Vector2 ReadMove() => testInputOverrideActive ? Vector2.zero : move.ReadValue<Vector2>();
        public Vector2 ReadLookDelta() => testInputOverrideActive ? Vector2.zero : look.ReadValue<Vector2>();

        public bool IsHeld(FpsBinding binding)
        {
            if (testInputOverrideActive) return testHeldBindings[(int)binding];
            InputAction action = GetAction(binding);
            return action != null && action.IsPressed();
        }

        public bool WasPressed(FpsBinding binding)
        {
            // 压力测试只覆盖持续状态，避免把单帧边沿同时送给多个系统而制造非确定顺序。
            // 切枪等离散行为由 WeaponLab 直接调用公开状态机入口，因此覆盖期间统一返回 false。
            if (testInputOverrideActive) return false;
            InputAction action = GetAction(binding);
            return action != null && action.WasPressedThisFrame();
        }

        /// <summary>
        /// 为 WeaponLab 启用独占测试输入。仅 Editor 或 Development Build 允许启用；正式非开发构建返回 false，
        /// 且不会改变玩家输入。启用后移动、视角和未显式置位的按键均视为未输入。
        /// </summary>
        /// <returns>成功进入测试覆盖时返回 true；当前构建不允许测试输入时返回 false。</returns>
        public bool BeginTestInputOverride()
        {
            if (!testInputOverrideAllowed) return false;
            ClearTestHeldBindings();
            testInputOverrideActive = true;
            return true;
        }

        /// <summary>结束 WeaponLab 测试输入并恢复真实 Input System 状态；未启用时调用也安全。</summary>
        public void EndTestInputOverride()
        {
            ClearTestHeldBindings();
            testInputOverrideActive = false;
        }

        /// <summary>设置测试期间某个持续按键的状态；未处于测试覆盖时忽略。</summary>
        /// <param name="binding">需要覆盖的玩家绑定；枚举值必须是当前 `FpsBinding` 定义的有效成员。</param>
        /// <param name="held">true 表示持续按住，false 表示释放；状态保持到再次设置或测试结束。</param>
        public void SetTestBindingHeld(FpsBinding binding, bool held)
        {
            if (!testInputOverrideActive) return;
            int index = (int)binding;
            if (index < 0 || index >= testHeldBindings.Length) return;
            testHeldBindings[index] = held;
        }

        public string GetBindingDisplayName(FpsBinding binding)
        {
            InputAction action = GetAction(binding);
            if (action == null) return "UNBOUND";
            int index = GetBindingIndex(binding);
            return index >= 0 && index < action.bindings.Count
                ? action.GetBindingDisplayString(index, InputBinding.DisplayStringOptions.DontUseShortDisplayNames)
                : "UNBOUND";
        }

        public bool StartRebind(FpsBinding binding, Action<bool> completed)
        {
            if (rebindingOperation != null || testInputOverrideActive) return false;
            InputAction action = GetAction(binding);
            int index = GetBindingIndex(binding);
            if (action == null || index < 0 || index >= action.bindings.Count) return false;

            action.Disable();
            rebindingOperation = action.PerformInteractiveRebinding(index)
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Pointer>/delta")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnCancel(operation => FinishRebind(action, completed, false))
                .OnComplete(operation => FinishRebind(action, completed, true));
            rebindingOperation.Start();
            return true;
        }

        public void ResetToDefaults()
        {
            if (rebindingOperation != null) return;
            gameplayMap.RemoveAllBindingOverrides();
            lookSensitivity = DefaultLookSensitivity;
            fieldOfView = DefaultFieldOfView;
            PlayerPrefs.DeleteKey(LookSensitivityKey);
            PlayerPrefs.DeleteKey(FieldOfViewKey);
            PlayerPrefs.DeleteKey(BindingOverridesKey);
            PlayerPrefs.Save();
            FieldOfViewChanged?.Invoke(fieldOfView);
        }

        public void SetLookSensitivity(float value)
        {
            lookSensitivity = Mathf.Clamp(value, 0.02f, 0.5f);
            PlayerPrefs.SetFloat(LookSensitivityKey, lookSensitivity);
            PlayerPrefs.Save();
        }

        public void SetFieldOfView(float value)
        {
            fieldOfView = Mathf.Clamp(value, 70f, 110f);
            PlayerPrefs.SetFloat(FieldOfViewKey, fieldOfView);
            PlayerPrefs.Save();
            FieldOfViewChanged?.Invoke(fieldOfView);
        }

        private void CreateActions()
        {
            gameplayMap = new InputActionMap("Gameplay");
            move = gameplayMap.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            look = gameplayMap.AddAction("Look", InputActionType.Value, "<Mouse>/delta");
            fire = gameplayMap.AddAction("Fire", InputActionType.Button, "<Mouse>/leftButton");
            aim = gameplayMap.AddAction("Aim", InputActionType.Button, "<Mouse>/rightButton");
            jump = gameplayMap.AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
            sprint = gameplayMap.AddAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            crouch = gameplayMap.AddAction("Crouch", InputActionType.Button, "<Keyboard>/c");
            reload = gameplayMap.AddAction("Reload", InputActionType.Button, "<Keyboard>/r");
            selectPrimary = gameplayMap.AddAction("Select Primary", InputActionType.Button, "<Keyboard>/1");
            selectSecondary = gameplayMap.AddAction("Select Secondary", InputActionType.Button, "<Keyboard>/2");
            dash = gameplayMap.AddAction("Dash", InputActionType.Button, "<Keyboard>/q");
            focus = gameplayMap.AddAction("Focus", InputActionType.Button, "<Keyboard>/e");
            interact = gameplayMap.AddAction("Interact", InputActionType.Button, "<Keyboard>/f");
            useTactical = gameplayMap.AddAction("Use Tactical", InputActionType.Button, "<Keyboard>/g");
            loadout = gameplayMap.AddAction("Loadout", InputActionType.Button, "<Keyboard>/tab");
            settings = gameplayMap.AddAction("Settings", InputActionType.Button, "<Keyboard>/o");
            menu = gameplayMap.AddAction("Menu", InputActionType.Button, "<Keyboard>/escape");
            debugCombat = gameplayMap.AddAction("DebugCombat", InputActionType.Button, "<Keyboard>/f10");
        }

        private void LoadPersistedSettings()
        {
            lookSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(LookSensitivityKey, lookSensitivity), 0.02f, 0.5f);
            fieldOfView = Mathf.Clamp(PlayerPrefs.GetFloat(FieldOfViewKey, fieldOfView), 70f, 110f);
            LoadPersistedBindingOverrides();
        }

        private void ClearTestHeldBindings()
        {
            for (int index = 0; index < testHeldBindings.Length; index++)
                testHeldBindings[index] = false;
        }

        private void FinishRebind(InputAction action, Action<bool> completed, bool save)
        {
            rebindingOperation?.Dispose();
            rebindingOperation = null;
            action.Enable();
            if (save)
            {
                SavePersistedBindingOverrides();
                PlayerPrefs.Save();
            }
            completed?.Invoke(save);
        }

        private void LoadPersistedBindingOverrides()
        {
            string persisted = PlayerPrefs.GetString(BindingOverridesKey, string.Empty);
            if (string.IsNullOrEmpty(persisted)) return;
            if (!persisted.StartsWith(BindingOverridesFormatPrefix, StringComparison.Ordinal))
            {
                // 旧实现直接保存 Input System JSON，但运行时动态 Action 的 Binding GUID 每次启动都会变化，
                // 因而旧 JSON 无法可靠匹配。只清理这一份失效覆盖，保留灵敏度和 FOV 等其他玩家设置。
                PlayerPrefs.DeleteKey(BindingOverridesKey);
                PlayerPrefs.Save();
                return;
            }

            string payload = persisted.Substring(BindingOverridesFormatPrefix.Length);
            string[] entries = payload.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string entry in entries)
            {
                int separator = entry.IndexOf(':');
                if (separator <= 0 || separator >= entry.Length - 1 ||
                    !int.TryParse(entry.Substring(0, separator), out int bindingValue))
                    continue;
                if (bindingValue < 0 || bindingValue >= BindingCount) continue;

                try
                {
                    string path = Encoding.UTF8.GetString(Convert.FromBase64String(entry.Substring(separator + 1)));
                    ApplyPersistedBindingOverride((FpsBinding)bindingValue, path);
                }
                catch (FormatException)
                {
                    // 单条损坏记录不会阻止其余按键加载；下一次成功改键会覆盖整份本地数据。
                }
            }
        }

        private void SavePersistedBindingOverrides()
        {
            StringBuilder builder = new StringBuilder(BindingOverridesFormatPrefix);
            for (int bindingValue = 0; bindingValue < BindingCount; bindingValue++)
            {
                FpsBinding binding = (FpsBinding)bindingValue;
                InputAction action = GetAction(binding);
                int index = GetBindingIndex(binding);
                if (action == null || index < 0 || index >= action.bindings.Count) continue;
                string overridePath = action.bindings[index].overridePath;
                if (string.IsNullOrEmpty(overridePath)) continue;

                string encodedPath = Convert.ToBase64String(Encoding.UTF8.GetBytes(overridePath));
                builder.Append(bindingValue).Append(':').Append(encodedPath).Append(';');
            }
            PlayerPrefs.SetString(BindingOverridesKey, builder.ToString());
        }

        /// <summary>按稳定的 FpsBinding 枚举和绑定索引恢复一条玩家改键，不依赖运行时随机生成的 GUID。</summary>
        /// <param name="binding">需要恢复的玩家绑定；必须是当前枚举中的有效成员。</param>
        /// <param name="overridePath">Input System 控件路径；为空或无效索引时忽略。</param>
        private void ApplyPersistedBindingOverride(FpsBinding binding, string overridePath)
        {
            if (string.IsNullOrWhiteSpace(overridePath)) return;
            InputAction action = GetAction(binding);
            int index = GetBindingIndex(binding);
            if (action == null || index < 0 || index >= action.bindings.Count) return;
            action.ApplyBindingOverride(index, overridePath);
        }

        private InputAction GetAction(FpsBinding binding)
        {
            switch (binding)
            {
                case FpsBinding.MoveForward:
                case FpsBinding.MoveBackward:
                case FpsBinding.MoveLeft:
                case FpsBinding.MoveRight: return move;
                case FpsBinding.Fire: return fire;
                case FpsBinding.Aim: return aim;
                case FpsBinding.Jump: return jump;
                case FpsBinding.Sprint: return sprint;
                case FpsBinding.Crouch: return crouch;
                case FpsBinding.Reload: return reload;
                case FpsBinding.SelectPrimary: return selectPrimary;
                case FpsBinding.SelectSecondary: return selectSecondary;
                case FpsBinding.Dash: return dash;
                case FpsBinding.Focus: return focus;
                case FpsBinding.Interact: return interact;
                case FpsBinding.UseTactical: return useTactical;
                case FpsBinding.Loadout: return loadout;
                case FpsBinding.Settings: return settings;
                case FpsBinding.Menu: return menu;
                case FpsBinding.DebugCombat: return debugCombat;
                default: return null;
            }
        }

        private static int GetBindingIndex(FpsBinding binding)
        {
            switch (binding)
            {
                case FpsBinding.MoveForward: return 1;
                case FpsBinding.MoveBackward: return 2;
                case FpsBinding.MoveLeft: return 3;
                case FpsBinding.MoveRight: return 4;
                default: return 0;
            }
        }
    }
}
