> [!WARNING]
> 历史原型记录（2026-08-13 归档）：本文不代表当前产品范围、参数或制作流程。请从 [Project Sun 文档中心](../../README.md) 进入现行规范。

# 15 Low Poly Shooter Pack 第一人称适配

## 目标

将 `Low Poly Shooter Pack - Free Sample` 中的 AR-01 第一人称手臂、步枪模型和动画，接入 Project Sun 的现有玩家与 Hitscan 武器系统。

## 边界

- 保留：骨骼、网格、材质、Animator Controller、开火和换弹动画。
- 移除：资源包的输入、相机、角色控制、Inventory、武器伤害、音频、碰撞和服务定位器脚本。
- 权威来源：Project Sun 的 `FpsInput`、`FpsPlayerController`、`HitscanWeapon`、`Health` 和 `WeaponFeedbackController`。
- 结果：资源包只作为视觉表现层，不能产生第二套输入、摄像机、子弹或弹药逻辑。

## 执行

Unity 完成脚本编译后，执行：

`Project Sun > Integrate Low Poly Shooter Arms (AR-01)`

该工具会生成 `Assets/_ProjectSun/Prefabs/Characters/PFB_FP_Operator_LPSP_AR01.prefab`，并将它作为 `Player Camera/FP Viewmodel - LPSP AR-01` 的嵌套 Prefab 写入 Project Sun 的 Player Prefab。原始 Infima 资源不会移动或修改。

## 验收

1. 打开 `CombatSlice` 并 Play；旧的白色原型枪不应显示。
2. 第一人称能看到手臂与 AR-01。
3. 射击时有枪械开火动画与既有弹道、命中反馈。
4. 换弹时播放换弹动画，且弹药逻辑仍由 Project Sun HUD 驱动。
5. 右键瞄准时，手臂姿态和现有 ADS 视角同时变化。
6. Console 中不应出现 Infima 的 ServiceLocator、输入或双相机相关异常。

## ADS 对齐

AR-01 使用 `Assets/_ProjectSun/Data/Weapons/ADS/ADS_AR01.asset` 作为独立的 `WeaponAdsProfile`。该 Profile 只保存表现层校准数据：瞄具距离、相机空间微调、ADS 过渡速度和 FOV，不保存伤害、散布或命中逻辑。

`Aim Anchor` 位于 `SOCKET_Scope` 下，表示玩家观察瞄具的正确位置；它不是枪口。运行时会使用 `Aim Anchor → SOCKET_Muzzle` 的实际轴线构造瞄准方向，再同时将瞄具位置和枪管方向对齐相机中心。这样即使第三方骨骼的局部轴不符合 Unity 的 Z 前/Y 上约定，也不会将整个手臂翻转。

校准顺序：

1. 只在枪械模型正确的后照门/光学瞄具观察点上放置 `Aim Anchor`。
2. 在 `ADS_AR01` 中微调 `Camera Space Position Offset` 与 `Camera Space Rotation Offset`；它们是相机空间的小修正，适用于枪托长度或瞄具高度差异。
3. 不要把 `Aim Anchor` 放到枪口，也不要为修正视觉表现调整 `HitscanWeapon` 的命中射线。

枪口仍只负责开火 VFX、音效与近墙阻挡检测；命中射线和枪口弹道的归零/遮挡策略属于武器模拟层，独立于本 Profile。

## 射击基准与准星

右键 ADS 不会让第一人称模型 Transform 成为伤害权威。射击先由相机中心和当前散布计算出瞄准点，再由 `SOCKET_Muzzle` 朝该点进行第二次射线检测：枪口被墙遮挡时墙体受击；未遮挡时才命中瞄准点上的目标。曳光也从枪口绘制到这次最终命中点。

因此，`Aim Anchor`、相机中心与 HUD 准星描述的是同一条玩家瞄准线；它们不直接承载伤害逻辑。默认 HUD 会在 ADS 时隐藏准星，让玩家使用枪械瞄具；按下 F10 可分别查看 `PLAYER AIM` 与 `PLAYER MUZZLE` 调试射线。

## 移除原型枪

AR-01 验收通过后，执行：

`Project Sun > Finalize Player Viewmodel (Remove Prototype)`

该命令会验证 Player 已绑定 AR-01，然后删除旧的 `Prototype Carbine` 和其回退枪口引用。之后 Player 只使用项目生成的 AR-01 第一人称视图模型。

## 发布限制

此适配不改变资源授权状态。该资源包在[现行资产授权台账](../../Compliance/资产授权台账.md)中仍为“待核验”；在确认商用和再分发条件前，只能用于本地原型验证。
