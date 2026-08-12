using ProjectSun.FPS.Abilities;
using ProjectSun.FPS.Core;
using ProjectSun.FPS.Presentation;
using ProjectSun.FPS.Player;
using ProjectSun.FPS.Rounds;
using ProjectSun.FPS.Weapons;
using UnityEngine;

namespace ProjectSun.FPS.UI
{
    /// <summary>
    /// 无外部 UI 资源依赖的对局 HUD。它只读取玩法权威发布的状态，后续替换为 UGUI/UI Toolkit 时不改变回合、
    /// 名册或伤害逻辑。
    /// </summary>
    public sealed class FpsHud : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("启用后进入 ADS 会隐藏腰射准星；倍率镜准星仍由镜片渲染链路负责。")]
        private bool hideCrosshairWhileAiming = true;
        private HitscanWeapon weapon;
        private FpsAbilityController abilities;
        private Health health;
        private RoundManager roundManager;
        private PlayerSpectatorController spectatorController;
        private FpsTacticalEquipmentController tacticalEquipment;
        private ScopeSightRenderer scopeSightRenderer;
        private WeaponAttachmentViewmodelPresenter attachmentPresenter;
        private float hitMarkerUntil;
        private float damageWarningUntil;
        private string damageDirection = string.Empty;
        private string damageSource = string.Empty;
        private GUIStyle textStyle;
        private GUIStyle largeTextStyle;
        private GUIStyle centeredTextStyle;
        private GUIStyle overlayTitleStyle;
        private GUIStyle overlayBodyStyle;

        /// <summary>配置 HUD 所需的只读玩法引用，并安全替换命中与受伤事件订阅。</summary>
        /// <param name="hitscanWeapon">本地玩家当前武器控制器，不能为空。</param>
        /// <param name="abilityController">本地玩家技能控制器，不能为空。</param>
        /// <param name="playerHealth">本地玩家权威生命组件，不能为空。</param>
        /// <param name="combatRoundManager">团队对局权威；null 时显示训练场 HUD。</param>
        /// <param name="playerTacticalEquipment">本地玩家战术装备控制器；null 时隐藏装备状态。</param>
        /// <param name="playerSpectatorController">死亡观战表现控制器；null 时仅显示等待状态，不显示相机目标。</param>
        public void Configure(HitscanWeapon hitscanWeapon, FpsAbilityController abilityController, Health playerHealth,
            RoundManager combatRoundManager = null, FpsTacticalEquipmentController playerTacticalEquipment = null,
            PlayerSpectatorController playerSpectatorController = null)
        {
            if (weapon != null) weapon.HitConfirmed -= ShowHitMarker;
            if (health != null) health.Damaged -= ShowDamageWarning;
            weapon = hitscanWeapon;
            abilities = abilityController;
            health = playerHealth;
            roundManager = combatRoundManager;
            tacticalEquipment = playerTacticalEquipment;
            spectatorController = playerSpectatorController;
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
            bool playerAlive = health.IsAlive;

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
                DrawTeamRosters(width);
            }
            GUI.Label(new Rect(28, 50, 550, 24),
                $"[Q] DASH {Cooldown(abilities.DashCooldownRemaining)}    [E] FOCUS {Cooldown(abilities.FocusCooldownRemaining, abilities.IsFocused)}", textStyle);
            if (tacticalEquipment != null)
                GUI.Label(new Rect(28, 126, 620, 24), $"[G] {tacticalEquipment.StatusLabel}", textStyle);
            GUI.Label(new Rect(28, 76, 840, 24), roundManager != null
                ? "WASD move  SHIFT sprint  SPACE jump  C crouch  RMB aim  R reload  G tactical  1/2 weapons  TAB loadout  O settings  F8 restart match"
                : "WASD move  SHIFT sprint  SPACE jump  C crouch  RMB aim  R reload  G tactical  1/2 weapons  F interact  TAB loadout  O settings", textStyle);

            // 死亡后移除所有仍暗示可射击的准星与瞄具叠加，只保留权威对局状态和等待信息。
            if (playerAlive && (!hideCrosshairWhileAiming || !weapon.IsAiming))
                DrawCrosshair(width, height);
            if (playerAlive && weapon.IsAiming) DrawActiveOpticReticle(width, height);
            if (playerAlive && Time.time < hitMarkerUntil)
                GUI.Label(new Rect(width * 0.5f - 15f, height * 0.5f - 20f, 30f, 36f), "X", largeTextStyle);
            if (playerAlive && Time.time < damageWarningUntil)
                GUI.Label(new Rect(width * 0.5f - 180f, height * 0.5f + 64f, 360f, 28f),
                    $"UNDER FIRE  {damageDirection}  //  {damageSource}", largeTextStyle);
            if (!playerAlive && roundManager != null)
                DrawEliminatedOverlay(width, height);
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
            if (health != null && !health.IsAlive)
            {
                if (scopeSightRenderer != null) scopeSightRenderer.SetSight(null, false, null, null);
                return;
            }
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
            centeredTextStyle = new GUIStyle(textStyle)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                normal = { textColor = Color.white }
            };
            overlayTitleStyle = new GUIStyle(largeTextStyle)
            {
                fontSize = 26,
                alignment = TextAnchor.MiddleCenter
            };
            overlayBodyStyle = new GUIStyle(textStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
        }

        /// <summary>绘制双方固定容量名册面板。</summary>
        /// <param name="screenWidth">当前 Game View 宽度，单位为像素，用于响应式居中布局。</param>
        private void DrawTeamRosters(float screenWidth)
        {
            const float gap = 10f;
            float totalWidth = Mathf.Min(Mathf.Max(260f, screenWidth - 20f), 660f);
            float panelWidth = (totalWidth - gap) * 0.5f;
            float left = (screenWidth - panelWidth * 2f - gap) * 0.5f;
            const float top = 154f;
            const float height = 56f;

            DrawTeamRosterPanel(roundManager.AttackerRoster, new Rect(left, top, panelWidth, height),
                "ATTACKERS", new Color(0.18f, 0.55f, 1f, 1f));
            DrawTeamRosterPanel(roundManager.DefenderRoster, new Rect(left + panelWidth + gap, top, panelWidth, height),
                "DEFENDERS", new Color(1f, 0.28f, 0.22f, 1f));
        }

        /// <summary>绘制单个阵营的存活统计与 6 个稳定槽位。</summary>
        /// <param name="roster">RoundManager 发布的只读名册；初始化前允许为 null。</param>
        /// <param name="panel">面板在屏幕 GUI 空间中的像素矩形。</param>
        /// <param name="teamLabel">面向玩家显示的阵营名称。</param>
        /// <param name="teamColor">该阵营的识别色；淘汰槽位会自动弱化。</param>
        private void DrawTeamRosterPanel(TeamRoster roster, Rect panel, string teamLabel, Color teamColor)
        {
            Color previousColor = GUI.color;
            GUI.color = new Color(0.025f, 0.04f, 0.065f, 0.84f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);

            int aliveCount = roster?.AliveCount ?? 0;
            int capacity = roster?.Capacity ?? roundManager.MaxTeamSize;
            GUI.color = teamColor;
            GUI.Label(new Rect(panel.x + 8f, panel.y + 3f, panel.width - 16f, 20f),
                $"{teamLabel}  {aliveCount}/{capacity}", textStyle);

            float slotsTop = panel.y + 26f;
            float slotGap = 4f;
            float slotWidth = (panel.width - 16f - slotGap * (capacity - 1)) / capacity;
            for (int slotIndex = 0; slotIndex < capacity; slotIndex++)
            {
                TeamCombatant member = null;
                bool occupied = roster != null && roster.TryGetMember(slotIndex, out member);
                bool alive = occupied && member.IsAlive;
                bool isLocalPlayer = occupied && member == roundManager.LocalPlayerCombatant;
                Rect slotRect = new Rect(panel.x + 8f + slotIndex * (slotWidth + slotGap), slotsTop, slotWidth, 22f);

                GUI.color = !occupied
                    ? new Color(0.18f, 0.2f, 0.24f, 0.75f)
                    : alive
                        ? teamColor
                        : new Color(0.14f, 0.15f, 0.18f, 0.92f);
                GUI.DrawTexture(slotRect, Texture2D.whiteTexture);

                GUI.color = alive || isLocalPlayer ? Color.white : new Color(0.55f, 0.58f, 0.62f, 1f);
                string slotLabel = !occupied ? "-" : !alive ? "X" : isLocalPlayer ? "P" : (slotIndex + 1).ToString("00");
                GUI.Label(slotRect, slotLabel, centeredTextStyle);

                // 白色细边仅标记本地玩家槽位，避免阵营色与“这是我”的语义混在一起。
                if (isLocalPlayer) DrawRectOutline(slotRect, 1f, Color.white);
            }

            GUI.color = previousColor;
        }

        /// <summary>绘制本地玩家被淘汰后的回合等待状态，不直接控制观战摄像机。</summary>
        /// <param name="screenWidth">当前 Game View 宽度，单位为像素。</param>
        /// <param name="screenHeight">当前 Game View 高度，单位为像素。</param>
        private void DrawEliminatedOverlay(float screenWidth, float screenHeight)
        {
            float overlayWidth = Mathf.Min(520f, screenWidth - 40f);
            Rect overlay = new Rect((screenWidth - overlayWidth) * 0.5f, screenHeight * 0.32f, overlayWidth, 116f);
            Color previousColor = GUI.color;
            GUI.color = new Color(0.015f, 0.02f, 0.03f, 0.9f);
            GUI.DrawTexture(overlay, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(overlay.x + 12f, overlay.y + 10f, overlay.width - 24f, 38f),
                "YOU ARE ELIMINATED", overlayTitleStyle);

            string waitingMessage;
            if (spectatorController != null && spectatorController.IsSpectating &&
                spectatorController.CurrentTarget != null)
            {
                waitingMessage = $"SPECTATING  {spectatorController.CurrentTarget.name.ToUpperInvariant()}\n" +
                                 $"[{spectatorController.PreviousBindingDisplayName}] PREVIOUS    " +
                                 $"[{spectatorController.NextBindingDisplayName}] NEXT";
            }
            else if (roundManager.CanLocalPlayerSpectate &&
                     roundManager.TryGetNextLocalSpectatorTarget(-1, out TeamCombatant pendingTarget))
            {
                waitingMessage = $"WAITING FOR ROUND END\nSPECTATOR TARGET READY: " +
                                 pendingTarget.name.ToUpperInvariant();
            }
            else if (roundManager.State == RoundState.Active)
            {
                waitingMessage = "TEAM ELIMINATED  //  ROUND RESOLVING";
            }
            else
            {
                waitingMessage = "ROUND COMPLETE  //  PREPARING NEXT ROUND";
            }

            GUI.Label(new Rect(overlay.x + 18f, overlay.y + 52f, overlay.width - 36f, 52f),
                waitingMessage, overlayBodyStyle);
            GUI.color = previousColor;
        }

        /// <summary>使用共享白纹理绘制无材质分配的 GUI 矩形边框。</summary>
        /// <param name="rect">边框外沿的 GUI 像素矩形。</param>
        /// <param name="thickness">边框厚度，单位为像素，调用方应传入正值。</param>
        /// <param name="color">边框颜色和透明度。</param>
        private static void DrawRectOutline(Rect rect, float thickness, Color color)
        {
            Color previousColor = GUI.color;
            GUI.color = color;
            Texture2D texture = Texture2D.whiteTexture;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), texture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), texture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), texture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), texture);
            GUI.color = previousColor;
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
