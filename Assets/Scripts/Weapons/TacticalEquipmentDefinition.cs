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

        [Header("Deployable Parameters")]
        [Min(0.5f)] public float deployRange = 4.5f;
        [Min(0f)] public float armingSeconds = 0.8f;
        [Min(0.1f)] public float triggerRadius = 2.6f;
        [Min(0.1f)] public float blastRadius = 4f;
        [Min(0f)] public float damage = 120f;
        [Min(1f)] public float lifetimeSeconds = 90f;

        [Header("Throwable Parameters")]
        [Min(0.1f)] public float throwSpeed = 15f;
        [Min(0f)] public float throwUpwardSpeed = 2.2f;
        [Min(0.05f)] public float fuseSeconds = 2.5f;
    }
}
