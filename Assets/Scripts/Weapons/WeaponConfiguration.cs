using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectSun.FPS.Weapons
{
    public enum AttachmentSlot { Optic, Muzzle, Barrel, Magazine, Stock }

    /// <summary>Whether a weapon exposes a right-click aiming state in both gameplay and presentation.</summary>
    public enum WeaponAimCapability { SupportsAds, HipFireOnly }

    [Serializable]
    public struct WeaponStats
    {
        [Min(1f)] public float damage;
        [Min(0.1f)] public float roundsPerSecond;
        [Min(1)] public int magazineSize;
        [Min(0.1f)] public float reloadSeconds;
        [Range(0f, 20f)] public float hipSpread;
        [Range(0f, 20f)] public float aimSpread;
        [Range(1f, 500f)] public float range;

        public static WeaponStats Carbine => new WeaponStats
        {
            damage = 34f, roundsPerSecond = 10f, magazineSize = 30, reloadSeconds = 2.15f,
            hipSpread = 1.45f, aimSpread = 0.32f, range = 120f
        };
    }

    public enum WeaponInventorySlot { Primary, Secondary }

    [Serializable]
    public sealed class WeaponLoadout
    {
        [SerializeField] private WeaponDefinition weapon;
        [SerializeField] private List<WeaponAttachment> attachments = new List<WeaponAttachment>();

        public WeaponDefinition Weapon => weapon;
        public IReadOnlyList<WeaponAttachment> Attachments => attachments;

        public WeaponStats BuildStats(WeaponStats fallback)
        {
            WeaponStats result = weapon != null ? weapon.baseStats : fallback;
            foreach (WeaponAttachment attachment in attachments)
            {
                if (attachment == null) continue;
                result.damage *= attachment.damageMultiplier;
                result.roundsPerSecond *= attachment.fireRateMultiplier;
                result.magazineSize = Mathf.Max(1, Mathf.RoundToInt(result.magazineSize * attachment.magazineMultiplier));
                result.reloadSeconds *= attachment.reloadMultiplier;
                result.hipSpread *= attachment.hipSpreadMultiplier;
                result.aimSpread *= attachment.aimSpreadMultiplier;
                result.range *= attachment.rangeMultiplier;
            }
            return result;
        }

        /// <summary>Equips one attachment per slot. Calling this at runtime lets a loadout UI replace parts safely.</summary>
        public void Equip(WeaponAttachment attachment)
        {
            if (attachment == null) return;
            for (int i = 0; i < attachments.Count; i++)
            {
                if (attachments[i] != null && attachments[i].slot == attachment.slot)
                {
                    attachments[i] = attachment;
                    return;
                }
            }
            attachments.Add(attachment);
        }

        public WeaponAttachment GetEquipped(AttachmentSlot slot)
        {
            foreach (WeaponAttachment attachment in attachments)
                if (attachment != null && attachment.slot == slot)
                    return attachment;
            return null;
        }

        public void SetWeapon(WeaponDefinition weaponDefinition)
        {
            weapon = weaponDefinition;
        }

        public void CopyFrom(WeaponLoadout source)
        {
            if (source == null) return;
            weapon = source.weapon;
            attachments.Clear();
            foreach (WeaponAttachment attachment in source.attachments)
                if (attachment != null) attachments.Add(attachment);
        }

        public void ClearAttachments() => attachments.Clear();

        public void Unequip(AttachmentSlot slot)
        {
            attachments.RemoveAll(attachment => attachment != null && attachment.slot == slot);
        }
    }
}
