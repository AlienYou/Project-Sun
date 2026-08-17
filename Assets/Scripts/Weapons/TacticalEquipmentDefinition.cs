using UnityEngine;

namespace ProjectSun.FPS.Weapons
{
    /// <summary>战术装备的基础投放方式；运行时控制器据此选择投掷或部署路径。</summary>
    public enum TacticalEquipmentType
    {
        /// <summary>沿相机前方以初速度投出的抛射物，例如 F-1 手雷。</summary>
        Throwable,

        /// <summary>贴附到可部署表面的静态装置。</summary>
        Deployable,

        /// <summary>具备感知/触发范围的静态装置，例如 S-1 感应雷。</summary>
        Sensor
    }

    /// <summary>
    /// 战术槽位物品的静态定义。资产只持有数值、投放方式和项目 Prefab；
    /// 具体行为由独立的运行时 Actor 执行，避免 ScriptableObject 成为场景或网络状态权威。
    /// </summary>
    [CreateAssetMenu(menuName = "Project Sun/FPS/Tactical Equipment Definition", fileName = "TacticalEquipment")]
    public sealed class TacticalEquipmentDefinition : ScriptableObject
    {
        [Tooltip("装备在配装界面与 HUD 中显示的名称；为空时会降低玩家对当前装备状态的辨识。")]
        public string displayName = "Sensor Mine";

        [Tooltip("装备用途说明，仅用于 UI 与制作审查，不参与运行时伤害或触发判定。")]
        [TextArea] public string description = "Detects enemies inside its scan radius.";

        [Tooltip("装备的投掷/部署分类；必须与 worldPrefab 上的运行时 Actor 组件匹配。")]
        public TacticalEquipmentType type = TacticalEquipmentType.Sensor;

        [Tooltip("运行时生成的项目 Prefab。必须位于 Assets/_ProjectSun/Prefabs/Tactical，并包含与 type 匹配的 FragGrenade 或 ProximityMine 组件；为空时装备不可使用。")]
        public GameObject worldPrefab;

        [Tooltip("两次使用之间的冷却时间，单位秒，最小值为 0；不会跨回合保留。")]
        [Min(0f)] public float cooldownSeconds = 20f;

        [Tooltip("每个回合可使用的最大次数，最小值为 1；由回合重置重新补满。")]
        [Min(1)] public int maxCharges = 1;

        [Header("部署参数")]
        [Tooltip("从相机向前检测可部署表面的最大距离，单位米，最小值为 0.5。")]
        [Min(0.5f)] public float deployRange = 4.5f;

        [Tooltip("部署后进入可触发状态前的延迟，单位秒，最小值为 0。")]
        [Min(0f)] public float armingSeconds = 0.8f;

        [Tooltip("感应/触发范围半径，单位米，最小值为 0.1；仍受敌我与墙体视线过滤。")]
        [Min(0.1f)] public float triggerRadius = 2.6f;

        [Tooltip("爆炸伤害影响半径，单位米，最小值为 0.1；墙体会阻断伤害路径。")]
        [Min(0.1f)] public float blastRadius = 4f;

        [Tooltip("爆炸中心处的基础伤害，最小值为 0；边缘伤害由运行时 Actor 按距离衰减。")]
        [Min(0f)] public float damage = 120f;

        [Tooltip("部署物在未触发时的最长存活时间，单位秒，最小值为 1；回合结束会提前回收。")]
        [Min(1f)] public float lifetimeSeconds = 90f;

        [Header("投掷参数")]
        [Tooltip("投掷物沿相机前方的初始速度，单位米/秒，最小值为 0.1。")]
        [Min(0.1f)] public float throwSpeed = 15f;

        [Tooltip("投掷物额外向上的初始速度，单位米/秒，最小值为 0。")]
        [Min(0f)] public float throwUpwardSpeed = 2.2f;

        [Tooltip("投掷物从生成到引爆的延迟，单位秒，最小值为 0.05。")]
        [Min(0.05f)] public float fuseSeconds = 2.5f;
    }
}
