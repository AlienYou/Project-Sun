using System.Collections.Generic;
using ProjectSun.FPS.Abilities;
using ProjectSun.FPS.Input;
using ProjectSun.FPS.Player;
using ProjectSun.FPS.Rounds;
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
        private FpsInput input;
        private RoundManager roundManager;
        private PlayerMatchLoadout matchLoadout;
        private WeaponLoadoutCatalog catalog;
        private bool ownsRuntimeOptions;
        private bool isOpen;
        private bool wasInPreparation;
        private LoadoutPage activePage = LoadoutPage.Weapons;
        private WeaponInventorySlot attachmentTargetSlot = WeaponInventorySlot.Primary;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;

        private enum LoadoutPage { Weapons, Attachments, Equipment }

        public void Configure(HitscanWeapon hitscanWeapon, FpsPlayerController controller, FpsAbilityController abilityController,
            WeaponLoadoutCatalog catalog = null, RoundManager matchRoundManager = null,
            PlayerMatchLoadout playerMatchLoadout = null)
        {
            weapon = hitscanWeapon;
            player = controller;
            input = player != null ? player.Input : null;
            abilities = abilityController;
            roundManager = matchRoundManager;
            matchLoadout = playerMatchLoadout;
            this.catalog = catalog;
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
            bool isPreparation = roundManager != null && roundManager.State == RoundState.Preparation;
            if (roundManager != null && isPreparation && !wasInPreparation)
                SetOpen(true);
            wasInPreparation = isPreparation;

            if (isOpen && !CanEditLoadout)
                SetOpen(false);

            if (input != null && CanEditLoadout && !input.IsRebinding && input.WasPressed(FpsBinding.Loadout))
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
            string primaryName = PrimaryLoadout.Weapon != null ? PrimaryLoadout.Weapon.displayName : "UNASSIGNED";
            string secondaryName = SecondaryLoadout.Weapon != null ? SecondaryLoadout.Weapon.displayName : "UNASSIGNED";
            string phase = roundManager != null ? $"PREP {roundManager.TimeRemaining:0.0}s" : "WEAPON LAB";
            GUI.Label(new Rect(panel.x + 26f, panel.y + 20f, 700f, 34f), $"PRE-ROUND LOADOUT // {phase}", titleStyle);
            GUI.Label(new Rect(panel.x + 26f, panel.y + 55f, 760f, 24f),
                $"PRIMARY  {primaryName.ToUpperInvariant()}   //   SECONDARY  {secondaryName.ToUpperInvariant()}   //   LOCKS WHEN ROUND GOES LIVE", bodyStyle);

            DrawPageTabs(panel);
            Rect content = new Rect(panel.x + 26f, panel.y + 128f, panel.width - 52f, panel.height - 192f);
            switch (activePage)
            {
                case LoadoutPage.Weapons:
                    DrawWeaponsPage(content);
                    break;
                case LoadoutPage.Attachments:
                    DrawAttachmentsPage(content);
                    break;
                case LoadoutPage.Equipment:
                    DrawEquipmentPage(content);
                    break;
            }

            if (GUI.Button(new Rect(panel.xMax - 150f, panel.yMax - 50f, 124f, 28f), "CLOSE  TAB"))
                SetOpen(false);
        }

        private void DrawPageTabs(Rect panel)
        {
            float x = panel.x + 26f;
            foreach (LoadoutPage page in new[] { LoadoutPage.Weapons, LoadoutPage.Attachments, LoadoutPage.Equipment })
            {
                GUI.enabled = activePage != page;
                if (GUI.Button(new Rect(x, panel.y + 91f, 130f, 27f), page.ToString().ToUpperInvariant()))
                    activePage = page;
                GUI.enabled = true;
                x += 138f;
            }
        }

        private void DrawWeaponsPage(Rect area)
        {
            GUI.Label(new Rect(area.x, area.y, area.width, 28f), "WEAPON SLOTS", titleStyle);
            DrawWeaponChoices("PRIMARY WEAPON", CatalogPrimaryWeapons, CurrentPrimaryWeapon, area.x, area.y + 43f, true);
            DrawWeaponChoices("SECONDARY WEAPON", CatalogSecondaryWeapons, matchLoadout != null ? matchLoadout.SecondaryWeapon : null,
                area.x, area.y + 150f, false);
            GUI.Label(new Rect(area.x, area.y + 263f, area.width, 52f),
                "Selections are stored in the match loadout. At round start the primary is equipped; press 1 / 2 during the round to switch between the configured weapons.", bodyStyle);
        }

        private void DrawWeaponChoices(string label, System.Collections.Generic.IReadOnlyList<WeaponDefinition> choices,
            WeaponDefinition selected, float left, float top, bool primary)
        {
            GUI.Label(new Rect(left, top, 220f, 24f), label, bodyStyle);
            if (choices == null || choices.Count == 0)
            {
                GUI.Label(new Rect(left, top + 30f, 620f, 24f), "NO ELIGIBLE WEAPON HAS BEEN ADDED TO THE CATALOG.", bodyStyle);
                return;
            }

            float x = left;
            bool previousGuiEnabled = GUI.enabled;
            GUI.enabled = CanEditLoadout;
            foreach (WeaponDefinition choice in choices)
            {
                if (choice == null) continue;
                string state = choice == selected ? "EQUIPPED" : "SELECT";
                if (GUI.Button(new Rect(x, top + 30f, 190f, 32f), $"{choice.displayName}  //  {state}"))
                {
                    if (matchLoadout != null)
                    {
                        if (primary) matchLoadout.TrySelectPrimary(choice);
                        else matchLoadout.TrySelectSecondary(choice);
                    }
                    else if (primary)
                    {
                        weapon.SetWeaponDefinition(choice);
                    }
                }
                x += 198f;
            }
            GUI.enabled = previousGuiEnabled;
        }

        private void DrawAttachmentsPage(Rect area)
        {
            GUI.Label(new Rect(area.x, area.y, area.width, 28f), "CONFIGURE ATTACHMENTS", titleStyle);
            float selectorWidth = 170f;
            bool previousGuiEnabled = GUI.enabled;
            GUI.enabled = CanEditLoadout;
            if (GUI.Button(new Rect(area.x, area.y + 35f, selectorWidth, 29f),
                    attachmentTargetSlot == WeaponInventorySlot.Primary ? "PRIMARY // SELECTED" : "PRIMARY"))
                attachmentTargetSlot = WeaponInventorySlot.Primary;
            if (GUI.Button(new Rect(area.x + selectorWidth + 8f, area.y + 35f, selectorWidth, 29f),
                    attachmentTargetSlot == WeaponInventorySlot.Secondary ? "SECONDARY // SELECTED" : "SECONDARY"))
                attachmentTargetSlot = WeaponInventorySlot.Secondary;
            GUI.enabled = previousGuiEnabled;

            WeaponLoadout targetLoadout = AttachmentTargetLoadout;
            WeaponDefinition targetWeapon = targetLoadout != null ? targetLoadout.Weapon : null;
            if (targetWeapon == null)
            {
                GUI.Label(new Rect(area.x, area.y + 82f, area.width, 30f),
                    "SELECT A WEAPON FOR THIS SLOT BEFORE CONFIGURING ATTACHMENTS.", bodyStyle);
                return;
            }

            float columnWidth = (area.width - 28f) * 0.5f;
            AttachmentSlot[] slots = { AttachmentSlot.Optic, AttachmentSlot.Muzzle, AttachmentSlot.Barrel, AttachmentSlot.Magazine, AttachmentSlot.Stock };
            for (int i = 0; i < slots.Length; i++)
                DrawAttachmentSlot(targetLoadout, targetWeapon, slots[i],
                    new Rect(area.x, area.y + 82f + i * 64f, columnWidth, 59f));
            DrawStats(targetLoadout, new Rect(area.x + columnWidth + 28f, area.y + 82f, columnWidth, 280f));
        }

        private void DrawAttachmentSlot(WeaponLoadout loadout, WeaponDefinition targetWeapon, AttachmentSlot slot, Rect area)
        {
            WeaponAttachment equipped = loadout.GetEquipped(slot);
            GUI.Label(new Rect(area.x, area.y, 145f, 23f), slot.ToString().ToUpperInvariant(), bodyStyle);
            GUI.Label(new Rect(area.x + 145f, area.y, area.width - 145f, 23f), equipped != null ? equipped.displayName : "STANDARD ISSUE", bodyStyle);

            bool previousGuiEnabled = GUI.enabled;
            GUI.enabled = CanEditLoadout;
            float x = area.x;
            bool hasCompatibleOption = false;
            foreach (WeaponAttachment option in options)
            {
                if (option.slot != slot || !IsAttachmentAvailable(targetWeapon, option)) continue;
                hasCompatibleOption = true;
                string presentationState = option.TryGetViewmodelVisual(targetWeapon, out _)
                    ? "VIEWMODEL READY"
                    : "STAT ONLY";
                if (GUI.Button(new Rect(x, area.y + 29f, 132f, 30f), $"{option.displayName}\n{presentationState}"))
                {
                    if (matchLoadout != null) matchLoadout.TryEquip(attachmentTargetSlot, option);
                    else weapon.TryEquip(option);
                }
                x += 140f;
            }
            if (!hasCompatibleOption)
                GUI.Label(new Rect(x, area.y + 34f, 180f, 22f), "NO COMPATIBLE PART", bodyStyle);
            else if (GUI.Button(new Rect(x, area.y + 29f, 105f, 30f), "REMOVE"))
            {
                if (matchLoadout != null) matchLoadout.TryUnequip(attachmentTargetSlot, slot);
                else weapon.TryUnequip(slot);
            }
            GUI.enabled = previousGuiEnabled;
        }

        private void DrawEquipmentPage(Rect area)
        {
            GUI.Label(new Rect(area.x, area.y, area.width, 28f), "TACTICAL EQUIPMENT", titleStyle);
            if (CatalogTacticalEquipment == null || CatalogTacticalEquipment.Count == 0)
            {
                GUI.Label(new Rect(area.x, area.y + 44f, area.width, 48f),
                    "NO TACTICAL EQUIPMENT HAS BEEN ADDED TO THE CATALOG.\nSensor mines and throwables will use this slot without changing the match-loadout format.", bodyStyle);
                return;
            }

            float x = area.x;
            bool previousGuiEnabled = GUI.enabled;
            GUI.enabled = CanEditLoadout && matchLoadout != null;
            foreach (TacticalEquipmentDefinition choice in CatalogTacticalEquipment)
            {
                if (choice == null) continue;
                string state = choice == (matchLoadout != null ? matchLoadout.TacticalEquipment : null) ? "EQUIPPED" : "SELECT";
                if (GUI.Button(new Rect(x, area.y + 44f, 190f, 32f), $"{choice.displayName}  //  {state}"))
                    matchLoadout.TrySelectTactical(choice);
                GUI.Label(new Rect(x, area.y + 82f, 190f, 52f),
                    $"[G] {choice.description}\n{choice.maxCharges} charge  //  {choice.cooldownSeconds:0}s cooldown", bodyStyle);
                x += 198f;
            }
            GUI.enabled = previousGuiEnabled;
        }

        private void DrawStats(WeaponLoadout loadout, Rect area)
        {
            WeaponStats stats = loadout != null ? loadout.BuildStats(WeaponStats.Carbine) : weapon.Stats;
            GUI.Label(new Rect(area.x, area.y, area.width, 28f), "CONFIGURED WEAPON STATISTICS", titleStyle);
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
            if (open && !CanEditLoadout) return;
            isOpen = open;

            // RoundManager is the authority for gameplay input. The loadout screen must not
            // accidentally re-enable movement while the team is still in its preparation phase.
            if (roundManager != null) return;
            if (player != null) player.SetGameplayInputEnabled(!open);
            if (weapon != null) weapon.SetGameplayInputEnabled(!open);
            if (abilities != null) abilities.SetGameplayInputEnabled(!open);
        }

        private bool CanEditLoadout => roundManager == null || roundManager.CanEditLoadout;
        private WeaponLoadout PrimaryLoadout => matchLoadout != null ? matchLoadout.Primary : weapon.Loadout;
        private WeaponLoadout SecondaryLoadout => matchLoadout != null ? matchLoadout.Secondary : new WeaponLoadout();
        private WeaponLoadout AttachmentTargetLoadout => attachmentTargetSlot == WeaponInventorySlot.Primary
            ? PrimaryLoadout
            : SecondaryLoadout;
        private WeaponDefinition CurrentPrimaryWeapon => PrimaryLoadout.Weapon;
        private System.Collections.Generic.IReadOnlyList<WeaponDefinition> CatalogPrimaryWeapons
            => catalog != null ? catalog.PrimaryWeapons : System.Array.Empty<WeaponDefinition>();
        private System.Collections.Generic.IReadOnlyList<WeaponDefinition> CatalogSecondaryWeapons
            => catalog != null ? catalog.SecondaryWeapons : System.Array.Empty<WeaponDefinition>();
        private System.Collections.Generic.IReadOnlyList<TacticalEquipmentDefinition> CatalogTacticalEquipment
            => catalog != null ? catalog.TacticalEquipment : System.Array.Empty<TacticalEquipmentDefinition>();

        private bool IsAttachmentAvailable(WeaponDefinition weaponDefinition, WeaponAttachment attachment)
        {
            if (weaponDefinition == null || attachment == null) return false;
            return catalog != null
                ? catalog.IsAttachmentAvailable(weaponDefinition, attachment)
                : attachment.IsCompatibleWith(weaponDefinition);
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
