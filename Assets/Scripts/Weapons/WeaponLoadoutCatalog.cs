using System.Collections.Generic;
using UnityEngine;

namespace ProjectSun.FPS.Weapons
{
    [CreateAssetMenu(menuName = "Project Sun/FPS/Weapon Loadout Catalog", fileName = "WeaponLoadoutCatalog")]
    public sealed class WeaponLoadoutCatalog : ScriptableObject
    {
        [SerializeField] private WeaponDefinition defaultWeapon;
        [SerializeField] private List<WeaponAttachment> attachments = new List<WeaponAttachment>();

        public WeaponDefinition DefaultWeapon => defaultWeapon;
        public IReadOnlyList<WeaponAttachment> Attachments => attachments;

        public void SetContents(WeaponDefinition weaponDefinition, IEnumerable<WeaponAttachment> availableAttachments)
        {
            defaultWeapon = weaponDefinition;
            attachments.Clear();
            if (availableAttachments == null) return;
            foreach (WeaponAttachment attachment in availableAttachments)
                if (attachment != null)
                    attachments.Add(attachment);
        }
    }
}
