using System;
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
        Dash,
        Focus,
        Interact,
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

        [Header("Defaults")]
        [SerializeField, Range(0.02f, 0.5f)] private float lookSensitivity = DefaultLookSensitivity;
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
        private InputAction dash;
        private InputAction focus;
        private InputAction interact;
        private InputAction loadout;
        private InputAction settings;
        private InputAction menu;
        private InputAction debugCombat;
        private InputActionRebindingExtensions.RebindingOperation rebindingOperation;

        public float LookSensitivity => lookSensitivity;
        public float FieldOfView => fieldOfView;
        public bool GameplayEnabled { get; private set; } = true;
        public bool IsRebinding => rebindingOperation != null;

        public event Action<float> FieldOfViewChanged;

        private void Awake()
        {
            CreateActions();
            LoadPersistedSettings();
            gameplayMap.Enable();
        }

        private void OnDestroy()
        {
            rebindingOperation?.Dispose();
            gameplayMap?.Dispose();
        }

        public void SetGameplayEnabled(bool enabled) => GameplayEnabled = enabled;

        public Vector2 ReadMove() => move.ReadValue<Vector2>();
        public Vector2 ReadLookDelta() => look.ReadValue<Vector2>();

        public bool IsHeld(FpsBinding binding)
        {
            InputAction action = GetAction(binding);
            return action != null && action.IsPressed();
        }

        public bool WasPressed(FpsBinding binding)
        {
            InputAction action = GetAction(binding);
            return action != null && action.WasPressedThisFrame();
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
            if (rebindingOperation != null) return false;
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
            dash = gameplayMap.AddAction("Dash", InputActionType.Button, "<Keyboard>/q");
            focus = gameplayMap.AddAction("Focus", InputActionType.Button, "<Keyboard>/e");
            interact = gameplayMap.AddAction("Interact", InputActionType.Button, "<Keyboard>/f");
            loadout = gameplayMap.AddAction("Loadout", InputActionType.Button, "<Keyboard>/tab");
            settings = gameplayMap.AddAction("Settings", InputActionType.Button, "<Keyboard>/o");
            menu = gameplayMap.AddAction("Menu", InputActionType.Button, "<Keyboard>/escape");
            debugCombat = gameplayMap.AddAction("DebugCombat", InputActionType.Button, "<Keyboard>/f10");
        }

        private void LoadPersistedSettings()
        {
            lookSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(LookSensitivityKey, lookSensitivity), 0.02f, 0.5f);
            fieldOfView = Mathf.Clamp(PlayerPrefs.GetFloat(FieldOfViewKey, fieldOfView), 70f, 110f);
            string overrides = PlayerPrefs.GetString(BindingOverridesKey, string.Empty);
            if (!string.IsNullOrEmpty(overrides)) gameplayMap.LoadBindingOverridesFromJson(overrides);
        }

        private void FinishRebind(InputAction action, Action<bool> completed, bool save)
        {
            rebindingOperation?.Dispose();
            rebindingOperation = null;
            action.Enable();
            if (save)
            {
                PlayerPrefs.SetString(BindingOverridesKey, gameplayMap.SaveBindingOverridesAsJson());
                PlayerPrefs.Save();
            }
            completed?.Invoke(save);
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
                case FpsBinding.Dash: return dash;
                case FpsBinding.Focus: return focus;
                case FpsBinding.Interact: return interact;
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
