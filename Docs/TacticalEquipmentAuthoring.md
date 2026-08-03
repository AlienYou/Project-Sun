# 战术装备制作规范

玩家的战术装备选择由对局配装数据持有。运行时实体只在当前回合消费该选择，因此准备阶段 UI、生成逻辑、回合重置和未来的网络所有权都共享同一个唯一数据源。

## S-1 感应雷

`Assets/_ProjectSun/Data/Weapons/Tactical/S1SensorMine.asset` 是首个正式装备定义。准备阶段在 `TAB > EQUIPMENT` 中选择它；对局开始后，瞄准 4.5 米以内的可部署场景几何体并按 `G` 布设。

感应雷会在 0.8 秒后激活，仅识别视线无遮挡的敌方角色；爆炸伤害同样必须具备清晰视线，不会穿过墙体。它会在引爆、到期、回合重置或快速重开时销毁。当前的圆柱体是便于验证玩法的临时运行时占位物，正式内容制作前应替换为美术制作的部署物预制体。

## 新增装备流程

1. 在 `Assets/_ProjectSun/Data/Weapons/Tactical/` 中创建 `TacticalEquipmentDefinition`。
2. 通过 `WeaponLoadoutCatalog` 的 `SetTacticalEquipment` 加入目录；目前应通过武器数据生成器作为目录制作入口。
3. 为该装备类型在 `FpsTacticalEquipmentController` 中提供专属运行时实体；不要将场景逻辑或网络行为写入 ScriptableObject。
4. 在加入对局目录前，补齐验证逻辑和 Weapon Lab 测试用例。

`Throwable` 专门预留给后续的投掷轨迹和投掷动画实现；它不能被默认当作感应雷处理。
