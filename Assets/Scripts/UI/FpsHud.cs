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
        [SerializeField] private bool hideCrosshairWhileAiming = true;
        private HitscanWeapon weapon;
        private FpsAbilityController abilities;
        private Health health;
        private RoundManager roundManager;
        private float hitMarkerUntil;
        private float damageWarningUntil;
        private string damageDirection = string.Empty;
        private string damageSource = string.Empty;
        private GUIStyle textStyle;
        private GUIStyle largeTextStyle;

        public void Configure(HitscanWeapon hitscanWeapon, FpsAbilityController abilityController, Health playerHealth,
            RoundManager combatRoundManager = null)
        {
            if (weapon != null) weapon.HitConfirmed -= ShowHitMarker;
            if (health != null) health.Damaged -= ShowDamageWarning;
            weapon = hitscanWeapon;
            abilities = abilityController;
            health = playerHealth;
            roundManager = combatRoundManager;
            if (weapon != null) weapon.HitConfirmed += ShowHitMarker;
            if (health != null) health.Damaged += ShowDamageWarning;
        }

        private void OnDestroy()
        {
            if (weapon != null) weapon.HitConfirmed -= ShowHitMarker;
            if (health != null) health.Damaged -= ShowDamageWarning;
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
            string weaponName = weapon.Loadout.Weapon != null ? weapon.Loadout.Weapon.displayName.ToUpperInvariant() : "UNARMED";
            GUI.Label(new Rect(width - 300, height - 54, 280, 24), weaponName, textStyle);

            GUI.Label(new Rect(28, 24, 510, 25), roundManager != null
                ? "PROJECT SUN // TEAM ELIMINATION"
                : "PROJECT SUN // COMBAT TRAINING RANGE", textStyle);
            if (roundManager != null)
            {
                GUI.Label(new Rect(width - 250f, 24, 220f, 25),
                    $"{roundManager.StateLabel}  {Mathf.CeilToInt(roundManager.TimeRemaining):000}s", textStyle);
                GUI.Label(new Rect(width - 250f, 50f, 220f, 25), roundManager.ScoreLabel, textStyle);
                GUI.Label(new Rect(28, 102, 640f, 24), roundManager.ObjectiveText, textStyle);
            }
            GUI.Label(new Rect(28, 50, 550, 24),
                $"[Q] DASH {Cooldown(abilities.DashCooldownRemaining)}    [E] FOCUS {Cooldown(abilities.FocusCooldownRemaining, abilities.IsFocused)}", textStyle);
            GUI.Label(new Rect(28, 76, 840, 24), roundManager != null
                ? "WASD move  SHIFT sprint  SPACE jump  C crouch  RMB aim  R reload  1/2 weapons  TAB loadout  O settings  F8 restart match"
                : "WASD move  SHIFT sprint  SPACE jump  C crouch  RMB aim  R reload  1/2 weapons  F interact  TAB loadout  O settings", textStyle);

            if (!hideCrosshairWhileAiming || !weapon.IsAiming)
                DrawCrosshair(width, height);
            if (Time.time < hitMarkerUntil)
                GUI.Label(new Rect(width * 0.5f - 15f, height * 0.5f - 20f, 30f, 36f), "X", largeTextStyle);
            if (Time.time < damageWarningUntil)
                GUI.Label(new Rect(width * 0.5f - 180f, height * 0.5f + 64f, 360f, 28f),
                    $"UNDER FIRE  {damageDirection}  //  {damageSource}", largeTextStyle);
        }

        private void ShowHitMarker(RaycastHit hit) => hitMarkerUntil = Time.time + 0.12f;

        private void ShowDamageWarning(DamageInfo damage)
        {
            damageWarningUntil = Time.time + 0.6f;
            if (damage.Instigator == null || health == null)
            {
                damageDirection = "UNKNOWN";
                damageSource = "UNKNOWN";
                return;
            }
            damageSource = damage.Instigator.name.ToUpperInvariant();
            Vector3 local = health.transform.InverseTransformDirection(damage.Instigator.transform.position - health.transform.position);
            if (Mathf.Abs(local.x) > Mathf.Abs(local.z)) damageDirection = local.x > 0f ? "RIGHT" : "LEFT";
            else damageDirection = local.z > 0f ? "FRONT" : "REAR";
        }

        private void DrawCrosshair(float width, float height)
        {
            float gap = weapon.IsAiming ? 4f : 8f;
            const float length = 6f;
            const float thickness = 2f;
            Texture2D texture = Texture2D.whiteTexture;
            Color oldColor = GUI.color;
            GUI.color = new Color(0.8f, 0.96f, 1f, 0.9f);
            GUI.DrawTexture(new Rect(width * 0.5f - thickness * 0.5f, height * 0.5f - gap - length, thickness, length), texture);
            GUI.DrawTexture(new Rect(width * 0.5f - thickness * 0.5f, height * 0.5f + gap, thickness, length), texture);
            GUI.DrawTexture(new Rect(width * 0.5f - gap - length, height * 0.5f - thickness * 0.5f, length, thickness), texture);
            GUI.DrawTexture(new Rect(width * 0.5f + gap, height * 0.5f - thickness * 0.5f, length, thickness), texture);
            GUI.color = oldColor;
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
