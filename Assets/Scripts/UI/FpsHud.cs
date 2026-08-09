using ProjectSun.FPS.Abilities;
using ProjectSun.FPS.Core;
using ProjectSun.FPS.Presentation;
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
        private FpsTacticalEquipmentController tacticalEquipment;
        private ScopeSightRenderer scopeSightRenderer;
        private WeaponAttachmentViewmodelPresenter attachmentPresenter;
        private float hitMarkerUntil;
        private float damageWarningUntil;
        private string damageDirection = string.Empty;
        private string damageSource = string.Empty;
        private GUIStyle textStyle;
        private GUIStyle largeTextStyle;

        public void Configure(HitscanWeapon hitscanWeapon, FpsAbilityController abilityController, Health playerHealth,
            RoundManager combatRoundManager = null, FpsTacticalEquipmentController playerTacticalEquipment = null)
        {
            if (weapon != null) weapon.HitConfirmed -= ShowHitMarker;
            if (health != null) health.Damaged -= ShowDamageWarning;
            weapon = hitscanWeapon;
            abilities = abilityController;
            health = playerHealth;
            roundManager = combatRoundManager;
            tacticalEquipment = playerTacticalEquipment;
            attachmentPresenter = null;
            if (weapon != null) weapon.HitConfirmed += ShowHitMarker;
            if (health != null) health.Damaged += ShowDamageWarning;
        }

        private void OnDestroy()
        {
            if (weapon != null) weapon.HitConfirmed -= ShowHitMarker;
            if (health != null) health.Damaged -= ShowDamageWarning;
            if (scopeSightRenderer != null) scopeSightRenderer.SetSight(null, false, null, null);
        }

        private void Update()
        {
            UpdateMagnifiedSight();
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
            if (tacticalEquipment != null)
                GUI.Label(new Rect(28, 126, 620, 24), $"[G] {tacticalEquipment.StatusLabel}", textStyle);
            GUI.Label(new Rect(28, 76, 840, 24), roundManager != null
                ? "WASD move  SHIFT sprint  SPACE jump  C crouch  RMB aim  R reload  G tactical  1/2 weapons  TAB loadout  O settings  F8 restart match"
                : "WASD move  SHIFT sprint  SPACE jump  C crouch  RMB aim  R reload  G tactical  1/2 weapons  F interact  TAB loadout  O settings", textStyle);

            if (!hideCrosshairWhileAiming || !weapon.IsAiming)
                DrawCrosshair(width, height);
            if (weapon.IsAiming) DrawActiveOpticReticle(width, height);
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

        private void DrawActiveOpticReticle(float width, float height)
        {
            WeaponAttachment optic = weapon.Loadout != null ? weapon.Loadout.GetEquipped(AttachmentSlot.Optic) : null;
            if (optic == null || optic.OpticSightProfile == null) return;
            // Magnified reticles are composited inside the physical lens material. Drawing the same
            // reticle through IMGUI would leak it outside the aperture and produce a duplicate overlay.
            if (optic.OpticSightProfile.UsesMagnifiedLensRendering) return;
            OpticReticleGui.Draw(optic.OpticSightProfile, new Rect(0f, 0f, width, height));
        }

        private void UpdateMagnifiedSight()
        {
            if (weapon == null) return;
            WeaponAttachment optic = weapon.Loadout != null ? weapon.Loadout.GetEquipped(AttachmentSlot.Optic) : null;
            OpticSightProfile profile = optic != null ? optic.OpticSightProfile : null;
            if (profile == null || !profile.UsesMagnifiedLensRendering)
            {
                if (scopeSightRenderer != null) scopeSightRenderer.SetSight(null, false, null, null);
                return;
            }

            Camera camera = weapon.ViewCamera;
            if (camera == null) return;
            if (scopeSightRenderer == null)
            {
                scopeSightRenderer = weapon.GetComponent<ScopeSightRenderer>();
                if (scopeSightRenderer == null) scopeSightRenderer = weapon.gameObject.AddComponent<ScopeSightRenderer>();
            }
            if (attachmentPresenter == null) attachmentPresenter = weapon.GetComponent<WeaponAttachmentViewmodelPresenter>();
            scopeSightRenderer.Configure(camera);
            scopeSightRenderer.SetSight(profile, weapon.IsAiming,
                attachmentPresenter != null ? attachmentPresenter.GetActiveAimAnchor(optic, weapon.Loadout.Weapon) : null,
                attachmentPresenter != null ? attachmentPresenter.GetActiveScopeLens(optic) : null);
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

    /// <summary>Shared immediate-mode fallback renderer for runtime HUD and editor workbench optic previews.</summary>
    public static class OpticReticleGui
    {
        public static void Draw(OpticSightProfile profile, Rect viewport)
        {
            if (profile == null || !profile.HasReticle || viewport.width <= 0f || viewport.height <= 0f) return;
            Color originalColor = GUI.color;
            GUI.color = profile.ReticleColor;
            if (profile.ReticleTexture != null)
            {
                float side = Mathf.Min(profile.FrameSizePixels, Mathf.Min(viewport.width, viewport.height));
                GUI.DrawTexture(new Rect(viewport.center.x - side * 0.5f, viewport.center.y - side * 0.5f, side, side),
                    profile.ReticleTexture, ScaleMode.ScaleToFit, true);
            }
            else
            {
                DrawFallback(profile, viewport.center);
            }
            GUI.color = originalColor;
        }

        private static void DrawFallback(OpticSightProfile profile, Vector2 centre)
        {
            float thickness = Mathf.Max(1f, profile.ReticleSizePixels * 0.32f);
            switch (profile.FallbackReticleStyle)
            {
                case OpticReticleStyle.Dot:
                    DrawSquare(centre, profile.ReticleSizePixels);
                    break;
                case OpticReticleStyle.RingDot:
                    float half = profile.FrameSizePixels * 0.5f;
                    DrawLine(new Rect(centre.x - half, centre.y - half, profile.FrameSizePixels, thickness));
                    DrawLine(new Rect(centre.x - half, centre.y + half - thickness, profile.FrameSizePixels, thickness));
                    DrawLine(new Rect(centre.x - half, centre.y - half, thickness, profile.FrameSizePixels));
                    DrawLine(new Rect(centre.x + half - thickness, centre.y - half, thickness, profile.FrameSizePixels));
                    DrawSquare(centre, profile.ReticleSizePixels);
                    break;
                case OpticReticleStyle.Cross:
                    float length = profile.FrameSizePixels * 0.5f;
                    float gap = Mathf.Max(2f, profile.ReticleSizePixels * 0.75f);
                    DrawLine(new Rect(centre.x - length, centre.y - thickness * 0.5f, length - gap, thickness));
                    DrawLine(new Rect(centre.x + gap, centre.y - thickness * 0.5f, length - gap, thickness));
                    DrawLine(new Rect(centre.x - thickness * 0.5f, centre.y - length, thickness, length - gap));
                    DrawLine(new Rect(centre.x - thickness * 0.5f, centre.y + gap, thickness, length - gap));
                    break;
            }
        }

        private static void DrawSquare(Vector2 centre, float side)
        {
            GUI.DrawTexture(new Rect(centre.x - side * 0.5f, centre.y - side * 0.5f, side, side), Texture2D.whiteTexture);
        }

        private static void DrawLine(Rect rect) => GUI.DrawTexture(rect, Texture2D.whiteTexture);
    }
}
