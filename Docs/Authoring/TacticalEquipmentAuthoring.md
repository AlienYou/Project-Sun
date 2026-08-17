# 战术装备制作规范

状态：现行。本文是战术装备数据、运行时实体和回合重置流程的权威规范。

玩家的战术装备选择由对局配装数据持有。运行时实体只在当前回合消费该选择，因此准备阶段 UI、生成逻辑、回合重置和未来的网络所有权都共享同一个唯一数据源。

## 当前装备

准备阶段在 `TAB > EQUIPMENT` 中选择装备，对局开始后按 `G` 使用。每次选择只携带一个战术装备；HUD 会显示剩余次数和当前状态。

### S-1 感应雷

`Assets/_ProjectSun/Data/Weapons/Tactical/S1SensorMine.asset` 是可部署装备。瞄准 4.5 米以内的可部署场景几何体并按 `G` 布设。

感应雷会在 0.8 秒后激活，仅识别视线无遮挡的敌方角色；爆炸伤害同样必须具备清晰视线，不会穿过墙体。它会在引爆、到期、回合重置或快速重开时销毁。

### F-1 破片手雷

`Assets/_ProjectSun/Data/Weapons/Tactical/F1FragGrenade.asset` 是首个投掷物。按 `G` 后，它以定义中的初速度与抛物线投出，能在场景几何体上反弹，并在 2.5 秒后引爆。范围伤害同样只对视线无遮挡的敌方角色生效。

F-1 与 S-1 的运行时实体必须由各自 `TacticalEquipmentDefinition.worldPrefab` 实例化；控制器只负责生成、配置、回收和回合清理，不再在运行时新建球体或圆柱体占位。C01 原型资源可通过 `Project Sun > Prototype Content > Setup Tactical Equipment Prefabs` 创建：

- `Assets/_ProjectSun/Prefabs/Tactical/PFB_TAC_F1_FragGrenade.prefab`：根节点包含 `FragGrenade`、`SphereCollider`、`Rigidbody` 与可视 Renderer；碰撞反弹材质必须是项目资产，不能每次投掷运行时创建。
- `Assets/_ProjectSun/Prefabs/Tactical/PFB_TAC_S1_SensorMine.prefab`：根节点包含 `ProximityMine`，并指定状态灯 Renderer；未激活、激活和触发反馈由该 Renderer 的材质属性块驱动，不应复制材质或创建临时网格。

若定义缺少 `worldPrefab`，或 Prefab 与装备类型不匹配，HUD 会显示 `MISSING TACTICAL PREFAB` 或 `INVALID TACTICAL PREFAB`，并在 Console 输出可定位错误；这属于制作配置错误，不能静默回退到临时几何体。

## 新增装备流程

1. 在 `Assets/_ProjectSun/Data/Weapons/Tactical/` 中创建 `TacticalEquipmentDefinition`。
2. 通过 `WeaponLoadoutCatalog` 的 `SetTacticalEquipment` 加入目录；目前应通过武器数据生成器作为目录制作入口。
3. 在 `Assets/_ProjectSun/Prefabs/Tactical/` 制作项目 Prefab，并把它赋给定义的 `worldPrefab`；投掷物必须包含对应的 Rigidbody/Collider，部署物必须包含可读的状态 Renderer。
4. 为该装备类型在 `FpsTacticalEquipmentController` 中提供专属运行时实体；不要将场景逻辑或网络行为写入 ScriptableObject。
5. 在加入对局目录前，补齐验证逻辑和 Weapon Lab 测试用例。

投掷物共用 `Throwable` 分支，但每种投掷物仍应拥有独立的运行时实体或可验证的行为策略；不要将新的投掷物默认当作 F-1 处理。
