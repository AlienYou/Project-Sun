using UnityEngine;

namespace ProjectSun.FPS.Weapons
{
    public enum TacticalEquipmentType { Throwable, Deployable, Sensor }

    /// <summary>
    /// Static definition for a tactical-slot item. Gameplay behaviour is deliberately kept out of
    /// the asset: a grenade, sensor mine, or future ability can each provide its own runtime actor.
    /// </summary>
    [CreateAssetMenu(menuName = "Project Sun/FPS/Tactical Equipment Definition", fileName = "TacticalEquipment")]
    public sealed class TacticalEquipmentDefinition : ScriptableObject
    {
        public string displayName = "Sensor Mine";
        [TextArea] public string description = "Detects enemies inside its scan radius.";
        public TacticalEquipmentType type = TacticalEquipmentType.Sensor;
        [Min(0f)] public float cooldownSeconds = 20f;
        [Min(1)] public int maxCharges = 1;
    }
}
