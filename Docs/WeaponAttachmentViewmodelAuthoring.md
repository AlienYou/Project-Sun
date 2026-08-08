# 武器配件第一人称表现规范

武器配件分为两个独立层：`WeaponAttachment` 负责对局规则与数值，`WeaponAttachmentViewmodelPresenter` 负责把已制作的第一人称模型挂到当前武器。两者必须独立：没有美术模型的配件可以用于数值验证，但不能被误认为已经完成视觉交付。

## 运行时链路

`PlayerMatchLoadout` 是主武器、副武器及配件选择的唯一数据源。玩家切换武器或在准备阶段更改配件后，`WeaponInventoryController` 会重新应用当前槽位的配装；`WeaponAttachmentViewmodelPresenter` 随后执行以下工作：

1. 销毁前一套运行时配件外观，并恢复被替换的武器原生部件。
2. 按武器定义查询配件的第一人称表现绑定。
3. 在指定挂点实例化配件预制体，并继承第一人称渲染层。
4. 若该配件提供自己的瞄准锚点，则将它作为运行时 ADS 参考；否则沿用武器自身的 `AimAnchor`。

这样主/副武器拥有独立配置，切枪也不会把前一把武器的附件残留在当前画面中。

## 每个配件的交付要求

在 `WeaponAttachment` 的 **First-Person Visuals** 中，为每个兼容武器添加一个绑定：

| 字段 | 要求 |
| --- | --- |
| `weapon` | 该绑定对应的 `WeaponDefinition`；同一配件可为不同武器配置不同模型。 |
| `prefab` | 项目自有目录下的第一人称配件预制体，不能直接依赖外部包目录。 |
| `mountName` | 武器上的稳定挂点名称，例如 AR-4 瞄具使用 `SOCKET_Scope`。 |
| `replacedBuiltInVisualName` | 装上配件后需要隐藏的原生部件；卸下时系统会自动恢复。 |
| `aimAnchorName` | 仅瞄具需要。预制体中的真实准星/孔径中心锚点；非瞄具留空。 |

瞄具预制体必须包含其自身的 `AimAnchor` 和必要的 `ViewmodelClipProbe`。安装后在 Weapon Presentation Workbench 中按腰射、ADS、开火、换弹、切枪姿态完成验证，不能只在静态预览下验收。

## 动态配件校准

通过 `Project Sun/Tools/Weapon Presentation Workbench` 选择 Player、武器槽位和 **校准瞄具**。工作台会创建临时 Loadout，并在源预览与两份隔离相机预览中调用与游戏一致的 `WeaponAttachmentViewmodelPresenter` 装配链路；停止预览后会销毁临时外观并恢复 Player 原生部件，不会把预览对象写入 Player 预制体。

校准职责固定如下：

1. 配件挂歪、穿模或接触导轨错误时，先回到附件资源的挂载基准处理 `Model` 适配层；这属于资产修复，不是每次 ADS 调参的步骤。
2. 先检查 **Aim Anchor 位置** 是否确实位于真实后目镜/镜片中心；只有锚点本身放错时才调整它。镜体外观与屏幕中心的偏差使用“武器整体 ADS 姿态”修正。
3. 锚点正确后，才使用该瞄具专属 ADS Profile 的相机空间微调、开镜速度和 FOV 缩减做小幅表现修正。
4. 命中判定仍以相机中心射线为准，枪口射线仅用于近距离遮挡；禁止通过移动模型或锚点修复墙体阻挡、命中或归零逻辑。

`AimAnchor` 不是“把镜体拖到屏幕中心”的旋钮。它必须位于真实后目镜/镜片中心，并参与 ADS 的位置与朝向解算；任意移动它都会使整把武器重新摆位。若锚点已正确但镜体没有包住屏幕中心的准星，应使用工作台的 **武器整体 ADS 姿态** 调整该瞄具生效的 ADS Profile：整把武器会一起移动，瞄具仍保持在 `SOCKET_Scope` 上。`Model` 与“移动模型”Scene 手柄只用于导入资源的枢轴、比例、机械挂载修复，不能作为准心对齐手段。

当数值输入不便于判断空间关系时，在 **Scene 直接编辑** 中选择“移动模型”、“旋转模型”或“移动 Aim Anchor”。启动实时预览后，Scene 视图会显示对应彩色手柄；一次鼠标拖拽会合并为一次 Undo，并沿用工作台的延迟保存机制。场景手柄只用于视觉资产校准，仍应以运行时 ADS 预览中的准心误差和 Clip Probe 验证作为验收依据。

工作台的方向微调以**毫米**显示与输入，提供 `5mm`、`1mm`、`0.1mm` 与 `0.02mm` 四档快捷步距；先用 `1mm`，最后用 `0.1mm` 或 `0.02mm` 收敛。方向始终以当前预览相机为基准：常规的“武器整体 ADS 姿态”会移动完整第一人称武器，瞄具资产几何修复才会直接修改 `Model`。

每个“武器 × 配件”视觉绑定都应独立验收。当前 `WeaponAttachment` 可为同一配件按不同 `WeaponDefinition` 提供不同运行时预制体、挂点和 Aim Anchor；如果后续将一支瞄具装到另一把枪，必须重新走本节流程。

### Clip Probe 验证约定

`ViewmodelClipProbe` 是近裁剪风险的**验证代理**，不是通过移动它来“修复”模型的工具。探针的黄色球应覆盖真实可见的最近表面；工作台会显示每个探针的中心深度、半径、球面净距、安全下限以及归属模型。探针未通过时，先根据明细中的名称确定是武器基础件还是当前附件：

1. 黄色球覆盖的位置不正确时，才调整探针的位置或半径。
2. 黄色球正确但净距不足时，调整 `Model` 挂载或武器的腰射/ADS 姿态，让真实可见表面远离相机。
3. 装上替换瞄具后，原生瞄具探针必须把原生可见模型设为 `visibilityOwner`；模型被隐藏时该探针会自动退出验证。工作台若检测到旧资源缺少该契约，会提供“修复原生瞄具探针可见性契约”按钮。

因此“1/3 探针未通过”不是笼统结论，而是一个可定位到具体表面、具体净距的资产验收项。

### 工作台保存行为

`Model` 与 `Aim Anchor` 的改动会立即反映到实时预览；停止编辑约 0.45 秒后，工作台才保存附件预制体，也可点击“立即保存”。这样连续拖动或键入数值不会反复触发资源保存和导入。一次模型与锚点编辑会合并为一个 Unity Undo 操作，`Ctrl+Z` / `Ctrl+Y` 后会同步刷新预览并保存结果。

### 瞄具视图表现

`OpticSightProfile` 与 `WeaponAdsProfile` 分工明确：前者定义 ADS 时显示的准星纹样、颜色和尺寸，后者定义武器姿态、开镜速度与 FOV 缩减。执行 `Project Sun/Ensure Optic Sight Presentation Profiles` 后，M2 红点、H7 全息以及已导入的两支倍镜会各自绑定一份可编辑的视图配置；准星会同时显示在游戏 HUD 与 Workbench 的 ADS 预览中。

当前的点、环点和十字为代码后备表现，便于先完成玩法与对齐验收。正式商业资源交付时，应在 `OpticSightProfile` 中填入项目自有的高分辨率准星贴图；倍镜的 FOV 缩减已由 ADS Profile 生效，但镜片遮罩、镜外模糊和真实透镜渲染属于后续独立的倍镜渲染阶段，不能与 Aim Anchor 校准混为一谈。

## 当前资源状态

已迁移的 Low Poly Shooter Sample 仅包含 AR-4 与 HG-3 的默认瞄具，没有 M2、H7、枪口、弹匣或枪托的独立成品模型。`Project Sun/Create Project-Owned Prototype Attachment Visuals` 会在 `_ProjectSun` 中生成四个项目自有的基础几何预制体：M2 红点、H7 全息、补偿器和消音器；并自动写入对应的视觉绑定与瞄具 ADS 配置。它们可直接用于玩法、挂点、ADS 与穿模验证，但仍是原型美术，不应作为商业正式外观验收。

未生成或未绑定第一人称外观的配件保持 **STAT ONLY**：数值、兼容性与装备流程有效，但没有独立可见外观。菜单会直接标记该状态，避免内容制作状态与玩法状态混淆。

导入或制作正式模型后，应将其复制到 `Assets/_ProjectSun/` 的项目自有资源目录，再填入上述绑定；不得重新引用 `Assets/Infima Games/` 的源包资产。

## 已导入的 AR-4 瞄具

### 导入模型的挂载基准

原始网格的建模原点不是武器导轨接触点，且其纵向轴与 AR-4 约定不同。AR-4 的 `SOCKET_Scope` 使用 `X` 横向、`Y` 向上、`Z` 指向枪口的局部坐标。批处理工具会先将源网格归一化到该坐标系，再以局部 XZ 平面作为导轨：沿导轨居中，并使其最低点以小幅嵌入贴合导轨。每个瞄具的 `AimAnchor` 保持在独立的武器参考系中，并从模型后方目镜端播种初始位置，因此外观适配不会把镜体中心误当作 ADS 参考点。

当前批处理资源为 `SR_Scope_00 1.fbx` 与 `TAN_LR_Scope_01 1.fbx`。不要直接使用旧 prefab 或源 FBX：其外部网格和材质引用并不属于当前工程。执行 `Project Sun/Prepare All Imported AR-4 Optics` 后，工具会分别生成 `PFB_ATT_AR4_SRScope00` 与 `PFB_ATT_AR4_TanLrScope01`，移除导出残留节点、关闭第一人称无用阴影、补齐项目内回退材质、播种各自的 `AimAnchor` 与 Clip Probe，并创建或绑定对应的附件和 ADS 配置。

也可分别执行 `Prepare SR Scope 00 As AR-4 Attachment` 或 `Prepare TAN LR Scope 01 As AR-4 Attachment`。批处理不会覆盖已存在 ADS Profile 的手工校准数据；播种锚点仅用于新建 Profile 的初始值。两支瞄具都必须在 WeaponLab 与 Weapon Presentation Workbench 中完成腰射、ADS 与后坐视觉验收后，才能标记为已审核。
