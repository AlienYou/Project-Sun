# 6v6 阵营名册与出生点制作规范

## 设计目标

团队歼灭模式使用固定的阵营槽位连接成员身份、出生位置、HUD 顺序和未来网络玩家槽。场景层级顺序、`FindObjectsOfType` 返回顺序和 Bot 当前所在位置都不能成为权威身份。

- 进攻方槽位 0：本地玩家。
- 进攻方槽位 1-5：5 名友军 Bot。
- 防守方槽位 0-5：6 名敌军 Bot。
- `RoundManager` 仍是回合、存活统计和胜负规则权威。
- `TeamSpawnGroup` 只提供关卡出生姿态，不负责生命值、复活或判胜。

## 首次创建流程

1. 打开 `Assets/_ProjectSun/Scenes/CombatSlice.unity`。
2. 退出 Play Mode。
3. 执行 `Project Sun > Setup 6v6 Team Elimination Bots`。
4. 工具会创建或修复 `Team Spawn Groups/Attacker Spawn Group` 和 `Defender Spawn Group`，每组包含 `Slot 00` 到 `Slot 05`。
5. 保存后的场景应为“玩家 + 5 名进攻方 Bot 对 6 名防守方 Bot”。

该工具可以重复执行：缺失节点会补齐，阵营和槽位引用会修复，但已经存在的出生锚点位置与旋转不会被默认值覆盖。

## 人工调整约束

- 只移动或旋转 `Slot 00` 到 `Slot 05` 锚点，不要通过移动角色本体来定义下一回合出生点。
- Scene 中选中出生点组后，圆圈表示约 0.32 米角色占地，射线表示出生朝向。
- 出生锚点应落在可行走区域，避免墙体、动态门、台阶边缘和其他槽位；相邻中心建议至少间隔 0.8 米。
- 数组索引就是权威槽位。不要为了视觉排序交换数组引用；需要换位时应直接交换锚点的世界姿态。
- 缺少整个出生点组时会走旧场景兼容路径；已经配置组但缺少某个槽位时，Console 会指出具体阵营、槽位和成员。

## 运行时流程

1. `CombatSliceSceneInstaller` 在初始化阶段一次性收集并稳定排序阵容。
2. `RoundManager` 建立双方 `TeamRoster`，拒绝越界和槽位冲突。
3. 进入准备阶段时，`RoundManager` 按 `TeamCombatant.TeamSlot` 查询出生姿态。
4. 玩家由 `PlayerRespawnController` 在临时停用 `CharacterController` 后传送。
5. Bot 由 `CombatBotController` 在临时停用 `NavMeshAgent` 后投影到邻近 NavMesh 并恢复出生朝向。
6. 回合开始后关闭局中复活；死亡成员保持淘汰，直到下一回合准备阶段统一重置。

## 本阶段验收

- 首次进入和连续三次新回合中，12 名成员均回到各自固定槽位。
- 进攻方与防守方朝向正确，没有成员重叠、卡墙或落出地面。
- Console 不出现槽位冲突、出生锚点缺失、`NavMeshAgent` 或空引用错误。
- HUD 存活人数与场上实际存活成员一致；任一阵营全灭后只结算一次。
- 按 F8 快速重开后，比分清零且仍保持相同的槽位分配。

EditMode 测试只验证数据契约，不能替代上述 PlayMode 场景验收。
