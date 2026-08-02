using ProjectSun.FPS.Abilities;
using ProjectSun.FPS.Input;
using ProjectSun.FPS.Player;
using ProjectSun.FPS.Weapons;
using UnityEngine;

namespace ProjectSun.FPS.UI
{
    /// <summary>Prototype presentation for persistent mouse/FOV settings and runtime key rebinding.</summary>
    public sealed class FpsSettingsMenu : MonoBehaviour
    {
        private static readonly FpsBinding[] RebindableBindings =
        {
            FpsBinding.MoveForward, FpsBinding.MoveBackward, FpsBinding.MoveLeft, FpsBinding.MoveRight,
            FpsBinding.Fire, FpsBinding.Aim, FpsBinding.Jump, FpsBinding.Sprint, FpsBinding.Crouch,
            FpsBinding.Reload, FpsBinding.SelectPrimary, FpsBinding.SelectSecondary, FpsBinding.Dash, FpsBinding.Focus,
            FpsBinding.Interact, FpsBinding.Loadout
        };

        private FpsPlayerController player;
        private HitscanWeapon weapon;
        private FpsAbilityController abilities;
        private FpsInput input;
        private bool isOpen;
        private string rebindStatus;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle buttonStyle;

        public void Configure(FpsPlayerController controller, HitscanWeapon hitscanWeapon, FpsAbilityController abilityController)
        {
            player = controller;
            weapon = hitscanWeapon;
            abilities = abilityController;
            input = player != null ? player.Input : null;
        }

        private void Update()
        {
            if (input == null) return;
            if (!input.IsRebinding && input.WasPressed(FpsBinding.Settings)) SetOpen(!isOpen);
            if (isOpen && !input.IsRebinding && input.WasPressed(FpsBinding.Menu)) SetOpen(false);
        }

        private void OnGUI()
        {
            if (!isOpen || input == null) return;
            EnsureStyles();
            float panelWidth = Mathf.Min(1040f, Screen.width - 40f);
            float panelHeight = Mathf.Min(680f, Screen.height - 40f);
            Rect panel = new Rect((Screen.width - panelWidth) * 0.5f, (Screen.height - panelHeight) * 0.5f, panelWidth, panelHeight);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 28f, panel.y + 20f, 640f, 32f), "FIELD SETTINGS // INPUT & VIEW", titleStyle);
            GUI.Label(new Rect(panel.x + 28f, panel.y + 54f, 780f, 22f),
                input.IsRebinding ? "Press a key or mouse button. ESC cancels the change." : "Changes save locally and persist after restarting the game.", bodyStyle);

            float left = panel.x + 28f;
            float top = panel.y + 96f;
            GUI.Label(new Rect(left, top, 180f, 24f), $"LOOK SENSITIVITY  {input.LookSensitivity:0.00}", bodyStyle);
            float sensitivity = GUI.HorizontalSlider(new Rect(left + 220f, top + 7f, 260f, 20f), input.LookSensitivity, 0.02f, 0.5f);
            if (!Mathf.Approximately(sensitivity, input.LookSensitivity)) input.SetLookSensitivity(sensitivity);

            GUI.Label(new Rect(left, top + 39f, 180f, 24f), $"FIELD OF VIEW  {input.FieldOfView:0}", bodyStyle);
            float fov = GUI.HorizontalSlider(new Rect(left + 220f, top + 46f, 260f, 20f), input.FieldOfView, 70f, 110f);
            if (!Mathf.Approximately(fov, input.FieldOfView)) input.SetFieldOfView(fov);

            GUI.Label(new Rect(left, top + 88f, 500f, 24f), "KEY BINDINGS", titleStyle);
            DrawBindingRows(new Rect(left, top + 124f, panel.width - 56f, panel.height - 210f));

            if (!string.IsNullOrEmpty(rebindStatus))
                GUI.Label(new Rect(left, panel.yMax - 68f, 640f, 24f), rebindStatus, bodyStyle);
            if (GUI.Button(new Rect(panel.xMax - 320f, panel.yMax - 70f, 130f, 32f), "RESET DEFAULTS", buttonStyle))
            {
                input.ResetToDefaults();
                rebindStatus = "Default controls and view settings restored.";
            }
            if (GUI.Button(new Rect(panel.xMax - 166f, panel.yMax - 70f, 138f, 32f), "RESUME  O", buttonStyle)) SetOpen(false);
        }

        private void DrawBindingRows(Rect area)
        {
            const float rowHeight = 36f;
            float columnWidth = (area.width - 18f) * 0.5f;
            int rowsPerColumn = Mathf.CeilToInt(RebindableBindings.Length * 0.5f);
            for (int i = 0; i < RebindableBindings.Length; i++)
            {
                int column = i / rowsPerColumn;
                int row = i % rowsPerColumn;
                FpsBinding binding = RebindableBindings[i];
                float x = area.x + column * (columnWidth + 18f);
                float y = area.y + row * rowHeight;
                GUI.Label(new Rect(x, y + 5f, 170f, 24f), Label(binding), bodyStyle);
                if (GUI.Button(new Rect(x + 180f, y, columnWidth - 180f, 29f), input.GetBindingDisplayName(binding), buttonStyle))
                    BeginRebind(binding);
            }
        }

        private void BeginRebind(FpsBinding binding)
        {
            if (!input.StartRebind(binding, saved =>
                rebindStatus = saved ? $"{Label(binding)} rebound to {input.GetBindingDisplayName(binding)}." : "Rebinding cancelled."))
                rebindStatus = "A binding is already waiting for input.";
        }

        private void SetOpen(bool open)
        {
            isOpen = open;
            if (player != null) player.SetGameplayInputEnabled(!open);
            if (weapon != null) weapon.SetGameplayInputEnabled(!open);
            if (abilities != null) abilities.SetGameplayInputEnabled(!open);
        }

        private static string Label(FpsBinding binding)
        {
            switch (binding)
            {
                case FpsBinding.MoveForward: return "MOVE FORWARD";
                case FpsBinding.MoveBackward: return "MOVE BACKWARD";
                case FpsBinding.MoveLeft: return "MOVE LEFT";
                case FpsBinding.MoveRight: return "MOVE RIGHT";
                case FpsBinding.SelectPrimary: return "SELECT PRIMARY";
                case FpsBinding.SelectSecondary: return "SELECT SECONDARY";
                default: return binding.ToString().ToUpperInvariant();
            }
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.72f, 0.94f, 1f) }
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold };
        }
    }
}
