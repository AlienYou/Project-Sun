> [!WARNING]
> 历史原型记录（2026-08-13 归档）：本文不代表当前产品范围、参数或制作流程。请从 [Project Sun 文档中心](../../README.md) 进入现行规范。

# 13 Defender 掩体战术工作流

## 当前规则

每个灰盒掩体会生成左右两个锚点：

- **Cover Position**：Defender 失去视线时抵达的受保护位置；
- **Peek Position**：从掩体侧边探出、重新获取视线的位置；
- 同一时刻一个锚点只能被一台 Defender 占用，防止机器人重叠。

## 启用

1. 在 Unity 选择 `Project Sun > Add Tactical Cover Points To Combat Slice`。
2. 在 Scene 视图开启 Gizmos，蓝色球体是 Cover Position，连线末端方框是 Peek Position。
3. Play 后按 F10；Defender 状态会显示 `TAKE COVER`、`PEEK`、`CHASE` 或 `FIRE`。

## 验收

1. 玩家从掩体后消失时，至少一台 Defender 应占用可达锚点，而不是立即随机游走。
2. Defender 到达 Cover Position 后应移动到 Peek Position 尝试恢复视线。
3. 复现多台 Defender 时，F10 中不应有两台显示同一个已占用锚点。
4. 回合重置或 Defender 死亡后，锚点必须释放，可由下一回合重新领取。

## 量产要求

当前锚点依据灰盒尺寸自动生成，适合验证行为。美术地图应由关卡设计师手工布置并审核每个 Cover/Peek 对，附带掩体类型、朝向、风险等级、团队可用性和导航可达性数据。
