using System.Collections.Generic;
using UnityEngine;

namespace ProjectSun.FPS.Weapons
{
    [CreateAssetMenu(menuName = "Project Sun/FPS/Weapon Loadout Catalog", fileName = "WeaponLoadoutCatalog")]
    public sealed class WeaponLoadoutCatalog : ScriptableObject
    {
        // Kept as a serialized migration field for the original AR-4-only catalog asset.
        [SerializeField] private WeaponDefinition defaultWeapon;
        [SerializeField] private List<WeaponDefinition> primaryWeapons = new List<WeaponDefinition>();
        [SerializeField] private List<WeaponDefinition> secondaryWeapons = new List<WeaponDefinition>();
        [SerializeField] private List<TacticalEquipmentDefinition> tacticalEquipment = new List<TacticalEquipmentDefinition>();
        [SerializeField] private List<WeaponAttachment> attachments = new List<WeaponAttachment>();

        public WeaponDefinition DefaultWeapon => DefaultPrimaryWeapon;
        public WeaponDefinition DefaultPrimaryWeapon => primaryWeapons.Count > 0 && primaryWeapons[0] != null
            ? primaryWeapons[0]
            : defaultWeapon;
        public IReadOnlyList<WeaponDefinition> PrimaryWeapons => primaryWeapons;
        public IReadOnlyList<WeaponDefinition> SecondaryWeapons => secondaryWeapons;
        public IReadOnlyList<TacticalEquipmentDefinition> TacticalEquipment => tacticalEquipment;
        public IReadOnlyList<WeaponAttachment> Attachments => attachments;

        public void SetContents(WeaponDefinition weaponDefinition, IEnumerable<WeaponAttachment> availableAttachments)
        {
            defaultWeapon = weaponDefinition;
            primaryWeapons.Clear();
            if (weaponDefinition != null) primaryWeapons.Add(weaponDefinition);
            attachments.Clear();
            if (availableAttachments == null) return;
            foreach (WeaponAttachment attachment in availableAttachments)
                if (attachment != null)
                    attachments.Add(attachment);
        }

        public bool IsPrimaryWeaponAvailable(WeaponDefinition weapon)
        {
            if (weapon == null) return false;
            if (primaryWeapons.Count == 0) return weapon == defaultWeapon;
            return primaryWeapons.Contains(weapon);
        }

        public bool IsSecondaryWeaponAvailable(WeaponDefinition weapon) => weapon != null && secondaryWeapons.Contains(weapon);

        public bool IsTacticalEquipmentAvailable(TacticalEquipmentDefinition equipment)
            => equipment != null && tacticalEquipment.Contains(equipment);
    }
}
