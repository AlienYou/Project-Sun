using System;
using UnityEngine;

namespace ProjectSun.FPS.Weapons
{
    /// <summary>
    /// The player's chosen match loadout, independent from the currently equipped weapon actor.
    /// Keeping primary, secondary and tactical selections here prevents UI state from becoming the
    /// source of truth and leaves each slot ready for a dedicated runtime implementation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerMatchLoadout : MonoBehaviour
    {
        [SerializeField] private WeaponLoadout primary = new WeaponLoadout();
        [SerializeField] private WeaponLoadout secondary = new WeaponLoadout();
        [SerializeField] private TacticalEquipmentDefinition tacticalEquipment;

        private HitscanWeapon activePrimaryWeapon;
        private WeaponLoadoutCatalog catalog;
        private bool editingEnabled = true;

        public WeaponLoadout Primary => primary;
        public WeaponLoadout Secondary => secondary;
        public WeaponDefinition PrimaryWeapon => primary.Weapon;
        public WeaponDefinition SecondaryWeapon => secondary.Weapon;
        public TacticalEquipmentDefinition TacticalEquipment => tacticalEquipment;
        public bool EditingEnabled => editingEnabled;

        public event Action Changed;

        public void Configure(HitscanWeapon primaryWeaponActor, WeaponLoadoutCatalog loadoutCatalog)
        {
            activePrimaryWeapon = primaryWeaponActor;
            catalog = loadoutCatalog;

            if (primary.Weapon == null && activePrimaryWeapon != null)
                primary.CopyFrom(activePrimaryWeapon.Loadout);
            if (primary.Weapon == null && catalog != null)
                primary.SetWeapon(catalog.DefaultPrimaryWeapon);

            if (catalog != null && !catalog.IsPrimaryWeaponAvailable(primary.Weapon))
            {
                primary.SetWeapon(catalog.DefaultPrimaryWeapon);
                primary.ClearAttachments();
            }
            if (secondary.Weapon == null && catalog != null)
                secondary.SetWeapon(catalog.DefaultSecondaryWeapon);
            if (catalog != null && secondary.Weapon != null && !catalog.IsSecondaryWeaponAvailable(secondary.Weapon))
            {
                secondary.SetWeapon(catalog.DefaultSecondaryWeapon);
                secondary.ClearAttachments();
            }

            ApplyPrimary();
        }

        public void SetEditingEnabled(bool enabled) => editingEnabled = enabled;

        public bool TrySelectPrimary(WeaponDefinition definition)
        {
            if (!editingEnabled || !CanApplyPrimary || catalog == null || !catalog.IsPrimaryWeaponAvailable(definition)) return false;
            if (primary.Weapon == definition) return true;

            primary.SetWeapon(definition);
            primary.ClearAttachments();
            if (!ApplyPrimary()) return false;
            Changed?.Invoke();
            return true;
        }

        public bool TrySelectSecondary(WeaponDefinition definition)
        {
            if (!editingEnabled || catalog == null || !catalog.IsSecondaryWeaponAvailable(definition)) return false;
            if (secondary.Weapon == definition) return true;
            secondary.SetWeapon(definition);
            secondary.ClearAttachments();
            Changed?.Invoke();
            return true;
        }

        public bool TrySelectTactical(TacticalEquipmentDefinition definition)
        {
            if (!editingEnabled || catalog == null || !catalog.IsTacticalEquipmentAvailable(definition)) return false;
            tacticalEquipment = definition;
            Changed?.Invoke();
            return true;
        }

        public bool TryEquipPrimary(WeaponAttachment attachment) => TryEquip(WeaponInventorySlot.Primary, attachment);

        public bool TryEquipSecondary(WeaponAttachment attachment) => TryEquip(WeaponInventorySlot.Secondary, attachment);

        public bool TryUnequipPrimary(AttachmentSlot slot) => TryUnequip(WeaponInventorySlot.Primary, slot);

        public bool TryUnequipSecondary(AttachmentSlot slot) => TryUnequip(WeaponInventorySlot.Secondary, slot);

        /// <summary>
        /// Stores an attachment in the requested loadout slot. Primary changes are reflected immediately in the
        /// prepared weapon actor; secondary changes are applied when WeaponInventoryController selects that slot.
        /// </summary>
        public bool TryEquip(WeaponInventorySlot slot, WeaponAttachment attachment)
        {
            WeaponLoadout target = GetLoadout(slot);
            if (!editingEnabled || attachment == null || target == null || target.Weapon == null) return false;
            if (slot == WeaponInventorySlot.Primary && !CanApplyPrimary) return false;
            if (catalog != null && !catalog.IsAttachmentAvailable(target.Weapon, attachment)) return false;
            if (catalog == null && !attachment.IsCompatibleWith(target.Weapon)) return false;

            target.Equip(attachment);
            if (slot == WeaponInventorySlot.Primary && !ApplyPrimary()) return false;
            Changed?.Invoke();
            return true;
        }

        public bool TryUnequip(WeaponInventorySlot slot, AttachmentSlot attachmentSlot)
        {
            WeaponLoadout target = GetLoadout(slot);
            if (!editingEnabled || target == null) return false;
            if (slot == WeaponInventorySlot.Primary && !CanApplyPrimary) return false;

            target.Unequip(attachmentSlot);
            if (slot == WeaponInventorySlot.Primary && !ApplyPrimary()) return false;
            Changed?.Invoke();
            return true;
        }

        private WeaponLoadout GetLoadout(WeaponInventorySlot slot)
            => slot == WeaponInventorySlot.Primary ? primary : secondary;

        private bool ApplyPrimary()
        {
            return activePrimaryWeapon == null || activePrimaryWeapon.TryApplyLoadout(primary);
        }

        private bool CanApplyPrimary => activePrimaryWeapon == null ||
            (activePrimaryWeapon.LoadoutEditingEnabled && !activePrimaryWeapon.IsReloading);
    }
}
