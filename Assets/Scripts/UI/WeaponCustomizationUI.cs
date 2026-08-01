using System.Collections.Generic;
using ProjectSun.FPS.Abilities;
using ProjectSun.FPS.Player;
using ProjectSun.FPS.Weapons;
using UnityEngine;

namespace ProjectSun.FPS.UI
{
    /// <summary>Prototype loadout screen. It uses the same public weapon API a final UI would use.</summary>
    public sealed class WeaponCustomizationUI : MonoBehaviour
    {
        private readonly List<WeaponAttachment> options = new List<WeaponAttachment>();
        private HitscanWeapon weapon;
        private FpsPlayerController player;
        private FpsAbilityController abilities;
        private bool ownsRuntimeOptions;
        private bool isOpen;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;

        public void Configure(HitscanWeapon hitscanWeapon, FpsPlayerController controller, FpsAbilityController abilityController,
            WeaponLoadoutCatalog catalog = null)
        {
            weapon = hitscanWeapon;
            player = controller;
            abilities = abilityController;
            if (catalog != null && catalog.Attachments.Count > 0)
            {
                ReplaceOptionsWithCatalog(catalog);
            }
            else if (options.Count == 0)
            {
                CreateOptions();
                ownsRuntimeOptions = true;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
                SetOpen(!isOpen);
        }

        private void OnDestroy()
        {
            if (!ownsRuntimeOptions) return;
            foreach (WeaponAttachment option in options)
                if (option != null) Destroy(option);
        }

        private void OnGUI()
        {
            if (!isOpen || weapon == null) return;
            EnsureStyles();
            float panelWidth = Mathf.Min(960f, Screen.width - 48f);
            float panelHeight = Mathf.Min(620f, Screen.height - 48f);
            Rect panel = new Rect((Screen.width - panelWidth) * 0.5f, (Screen.height - panelHeight) * 0.5f, panelWidth, panelHeight);
            GUI.Box(panel, GUIContent.none);
            string weaponName = weapon.Loadout.Weapon != null ? weapon.Loadout.Weapon.displayName : "AR-4 CARBINE";
            GUI.Label(new Rect(panel.x + 26f, panel.y + 20f, 600f, 34f), $"{weaponName.ToUpperInvariant()} // FIELD LOADOUT", titleStyle);
            GUI.Label(new Rect(panel.x + 26f, panel.y + 55f, 660f, 24f), "Pick one component per slot. Changes apply immediately. Press TAB to resume.", bodyStyle);

            float columnWidth = (panel.width - 54f) * 0.5f;
            float left = panel.x + 26f;
            float top = panel.y + 94f;
            AttachmentSlot[] slots = { AttachmentSlot.Optic, AttachmentSlot.Muzzle, AttachmentSlot.Barrel, AttachmentSlot.Magazine, AttachmentSlot.Stock };
            for (int i = 0; i < slots.Length; i++)
                DrawSlot(slots[i], new Rect(left, top + i * 86f, columnWidth, 78f));

            DrawStats(new Rect(left + columnWidth + 28f, top, columnWidth, 280f));
            if (GUI.Button(new Rect(panel.xMax - 150f, panel.yMax - 50f, 124f, 28f), "RESUME  TAB"))
                SetOpen(false);
        }

        private void DrawSlot(AttachmentSlot slot, Rect area)
        {
            WeaponAttachment equipped = weapon.Loadout.GetEquipped(slot);
            GUI.Label(new Rect(area.x, area.y, 145f, 23f), slot.ToString().ToUpperInvariant(), bodyStyle);
            GUI.Label(new Rect(area.x + 145f, area.y, area.width - 145f, 23f), equipped != null ? equipped.displayName : "STANDARD ISSUE", bodyStyle);

            float x = area.x;
            foreach (WeaponAttachment option in options)
            {
                if (option.slot != slot) continue;
                if (GUI.Button(new Rect(x, area.y + 29f, 132f, 30f), option.displayName))
                    weapon.TryEquip(option);
                x += 140f;
            }
            if (GUI.Button(new Rect(x, area.y + 29f, 105f, 30f), "REMOVE"))
            {
                weapon.Loadout.Unequip(slot);
                weapon.RefreshLoadout();
            }
        }

        private void DrawStats(Rect area)
        {
            WeaponStats stats = weapon.Stats;
            GUI.Label(new Rect(area.x, area.y, area.width, 28f), "LIVE WEAPON STATISTICS", titleStyle);
            GUI.Label(new Rect(area.x, area.y + 43f, area.width, 190f),
                $"Damage      {stats.damage:0.0}\n" +
                $"Fire rate   {stats.roundsPerSecond:0.0} rounds/s\n" +
                $"Magazine    {stats.magazineSize} rounds\n" +
                $"Reload      {stats.reloadSeconds:0.00}s\n" +
                $"Hip spread  {stats.hipSpread:0.00}\n" +
                $"ADS spread  {stats.aimSpread:0.00}\n" +
                $"Range       {stats.range:0}m", bodyStyle);
        }

        private void SetOpen(bool open)
        {
            isOpen = open;
            if (player != null) player.SetGameplayInputEnabled(!open);
            if (weapon != null) weapon.SetGameplayInputEnabled(!open);
            if (abilities != null) abilities.SetGameplayInputEnabled(!open);
        }

        private void CreateOptions()
        {
            Add(AttachmentSlot.Optic, "M2 REFLEX", aimSpread: 0.55f);
            Add(AttachmentSlot.Optic, "H7 HOLO", hipSpread: 0.9f, aimSpread: 0.72f);
            Add(AttachmentSlot.Muzzle, "COMPENSATOR", hipSpread: 0.76f, aimSpread: 0.82f);
            Add(AttachmentSlot.Muzzle, "SUPPRESSOR", range: 0.84f, hipSpread: 0.9f);
            Add(AttachmentSlot.Barrel, "LONG BARREL", damage: 1.06f, range: 1.25f, fireRate: 0.94f);
            Add(AttachmentSlot.Barrel, "CQB BARREL", fireRate: 1.12f, range: 0.76f, hipSpread: 0.86f);
            Add(AttachmentSlot.Magazine, "EXTENDED MAG", magazine: 1.33f, reload: 1.12f);
            Add(AttachmentSlot.Magazine, "FAST MAG", magazine: 0.80f, reload: 0.74f);
            Add(AttachmentSlot.Stock, "TACTICAL STOCK", hipSpread: 0.88f, aimSpread: 0.78f);
            Add(AttachmentSlot.Stock, "LIGHT STOCK", fireRate: 1.08f, hipSpread: 1.12f);
        }

        private void ReplaceOptionsWithCatalog(WeaponLoadoutCatalog catalog)
        {
            if (ownsRuntimeOptions)
            {
                foreach (WeaponAttachment option in options)
                    if (option != null) Destroy(option);
            }
            options.Clear();
            foreach (WeaponAttachment attachment in catalog.Attachments)
                if (attachment != null)
                    options.Add(attachment);
            ownsRuntimeOptions = false;
        }

        private void Add(AttachmentSlot slot, string displayName, float damage = 1f, float fireRate = 1f, float magazine = 1f,
            float reload = 1f, float hipSpread = 1f, float aimSpread = 1f, float range = 1f)
        {
            WeaponAttachment attachment = ScriptableObject.CreateInstance<WeaponAttachment>();
            attachment.name = displayName;
            attachment.slot = slot;
            attachment.displayName = displayName;
            attachment.damageMultiplier = damage;
            attachment.fireRateMultiplier = fireRate;
            attachment.magazineMultiplier = magazine;
            attachment.reloadMultiplier = reload;
            attachment.hipSpreadMultiplier = hipSpread;
            attachment.aimSpreadMultiplier = aimSpread;
            attachment.rangeMultiplier = range;
            options.Add(attachment);
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
        }
    }
}
