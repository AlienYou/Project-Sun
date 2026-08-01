using ProjectSun.FPS.Abilities;
using ProjectSun.FPS.Core;
using ProjectSun.FPS.Rounds;
using ProjectSun.FPS.Weapons;
using UnityEngine;

namespace ProjectSun.FPS.UI
{
    /// <summary>Dependency-free prototype HUD. Replace this with UGUI/UI Toolkit presentation later; gameplay stays unchanged.</summary>
    public sealed class FpsHud : MonoBehaviour
    {
        private HitscanWeapon weapon;
        private FpsAbilityController abilities;
        private Health health;
        private RoundManager roundManager;
        private GUIStyle textStyle;
        private GUIStyle largeTextStyle;

        public void Configure(HitscanWeapon hitscanWeapon, FpsAbilityController abilityController, Health playerHealth,
            RoundManager combatRoundManager = null)
        {
            weapon = hitscanWeapon;
            abilities = abilityController;
            health = playerHealth;
            roundManager = combatRoundManager;
        }

        private void OnGUI()
        {
            if (weapon == null || abilities == null || health == null) return;
            EnsureStyles();
            float width = Screen.width;
            float height = Screen.height;

            GUI.Label(new Rect(28, height - 115, 220, 30), $"VITALS  {Mathf.CeilToInt(health.Current):000}", largeTextStyle);
            GUI.Label(new Rect(width - 210, height - 115, 190, 30), $"{weapon.AmmoInMagazine:00} / {weapon.Stats.magazineSize:00}", largeTextStyle);
            string reload = weapon.IsReloading ? $"RELOADING  {weapon.ReloadProgress:P0}" : weapon.IsAiming ? "AIMED" : "HIP FIRE";
            GUI.Label(new Rect(width - 210, height - 82, 180, 26), reload, textStyle);

            GUI.Label(new Rect(28, 24, 510, 25), "PROJECT SUN // COMBAT TRAINING RANGE", textStyle);
            if (roundManager != null)
            {
                GUI.Label(new Rect(width - 250f, 24, 220f, 25),
                    $"{roundManager.StateLabel}  {Mathf.CeilToInt(roundManager.TimeRemaining):000}s", textStyle);
                GUI.Label(new Rect(28, 102, 640f, 24), roundManager.ObjectiveText, textStyle);
            }
            GUI.Label(new Rect(28, 50, 550, 24),
                $"[Q] DASH {Cooldown(abilities.DashCooldownRemaining)}    [E] FOCUS {Cooldown(abilities.FocusCooldownRemaining, abilities.IsFocused)}", textStyle);
            GUI.Label(new Rect(28, 76, 680, 24), "WASD move  SHIFT sprint  SPACE jump  C crouch  RMB aim  R reload  F interact  TAB loadout", textStyle);

            GUI.Label(new Rect(width * 0.5f - 10, height * 0.5f - 13, 20, 26), "+", largeTextStyle);
        }

        private void EnsureStyles()
        {
            if (textStyle != null) return;
            textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.7f, 0.94f, 1f) }
            };
            largeTextStyle = new GUIStyle(textStyle)
            {
                fontSize = 22,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };
        }

        private static string Cooldown(float seconds, bool active = false)
        {
            if (active) return "ACTIVE";
            return seconds <= 0f ? "READY" : $"{seconds:0.0}s";
        }
    }
}
