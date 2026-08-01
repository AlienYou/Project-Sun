using UnityEngine;

namespace ProjectSun.FPS.Weapons
{
    [CreateAssetMenu(menuName = "Project Sun/FPS/Weapon Definition", fileName = "WeaponDefinition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        public string displayName = "AR-4 Carbine";
        public WeaponStats baseStats = WeaponStats.Carbine;
    }
}
