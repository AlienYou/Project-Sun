using ProjectSun.FPS.Bootstrap;
using ProjectSun.FPS.Weapons;
using ProjectSun.FPS.World;
using UnityEngine;

namespace ProjectSun.FPS.UI
{
    /// <summary>Read-only runtime telemetry for WeaponLab. This is deliberately separate from the player HUD.</summary>
    [DisallowMultipleComponent]
    public sealed class WeaponLabTelemetryHud : MonoBehaviour
    {
        private FpsPlayerInstaller playerInstaller;
        private HitscanWeapon weapon;
        private WeaponInventoryController inventory;
        private WeaponLabController lab;
        private float lastHitDistance = -1f;
        private string lastHitName = "NO DAMAGE TARGET HIT";
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;

        public void Configure(FpsPlayerInstaller player, WeaponLabController labController)
        {
            if (weapon != null) weapon.HitConfirmed -= RecordHit;
            playerInstaller = player;
            weapon = player != null ? player.Weapon : null;
            inventory = player != null ? player.WeaponInventory : null;
            lab = labController;
            if (weapon != null) weapon.HitConfirmed += RecordHit;
        }

        private void OnDestroy()
        {
            if (weapon != null) weapon.HitConfirmed -= RecordHit;
        }

        private void OnGUI()
        {
            if (weapon == null) return;
            EnsureStyles();
            float width = Screen.width;
            Rect panel = new Rect(width - 310f, 22f, 286f, 230f);
            GUI.Box(panel, GUIContent.none);
            WeaponStats stats = weapon.Stats;
            WeaponDefinition definition = weapon.Loadout.Weapon;
            string weaponName = definition != null ? definition.displayName.ToUpperInvariant() : "UNARMED";
            string slot = inventory != null ? inventory.ActiveSlot.ToString().ToUpperInvariant() : "PRIMARY";
            string aimState = weapon.IsAiming ? "ADS" : "HIP FIRE";
            string hitState = lastHitDistance >= 0f
                ? $"{lastHitName}  {lastHitDistance:0.0}m"
                : lastHitName;

            GUI.Label(new Rect(panel.x + 14f, panel.y + 12f, panel.width - 28f, 25f), "WEAPON LAB // LIVE TELEMETRY", titleStyle);
            GUI.Label(new Rect(panel.x + 14f, panel.y + 43f, panel.width - 28f, 164f),
                $"Weapon      {weaponName}\n" +
                $"Slot        {slot}\n" +
                $"State       {aimState}\n" +
                $"Damage      {stats.damage:0.0}\n" +
                $"Rate        {stats.roundsPerSecond:0.0} rps\n" +
                $"Spread      HIP {stats.hipSpread:0.00}  ADS {stats.aimSpread:0.00}\n" +
                $"Range       {stats.range:0}m\n" +
                $"Last hit    {hitState}", bodyStyle);
            GUI.Label(new Rect(panel.x + 14f, panel.yMax - 24f, panel.width - 28f, 18f),
                $"[{(lab != null ? lab.ResetLabKey : KeyCode.F6)}] RESET LAB", bodyStyle);
        }

        private void RecordHit(RaycastHit hit)
        {
            if (playerInstaller != null && playerInstaller.PlayerCamera != null)
                lastHitDistance = Vector3.Distance(playerInstaller.PlayerCamera.transform.position, hit.point);
            else
                lastHitDistance = hit.distance;
            lastHitName = hit.collider != null ? hit.collider.name.ToUpperInvariant() : "DAMAGE TARGET";
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.8f, 0.95f, 1f) }
            };
            bodyStyle = new GUIStyle(titleStyle)
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                normal = { textColor = new Color(0.88f, 0.92f, 0.96f) }
            };
        }
    }
}
