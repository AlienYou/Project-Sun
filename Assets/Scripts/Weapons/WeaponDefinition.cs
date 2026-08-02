using UnityEngine;

namespace ProjectSun.FPS.Weapons
{
    [CreateAssetMenu(menuName = "Project Sun/FPS/Weapon Definition", fileName = "WeaponDefinition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        public string displayName = "AR-4 Carbine";
        [Tooltip("Automatic weapons continue firing while the trigger is held; sidearms normally require one press per shot.")]
        public bool automatic = true;
        [Tooltip("Hip-fire-only weapons ignore the aim input and require no ADS profile or Aim Anchor.")]
        public WeaponAimCapability aimCapability = WeaponAimCapability.SupportsAds;
        public WeaponStats baseStats = WeaponStats.Carbine;

        public bool SupportsAds => aimCapability == WeaponAimCapability.SupportsAds;
    }
}
