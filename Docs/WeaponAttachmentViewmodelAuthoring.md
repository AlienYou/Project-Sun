# 武器配件第一人称表现规范

武器配件分为两个独立层：`WeaponAttachment` 负责对局规则与数值，`WeaponAttachmentViewmodelPresenter` 负责把已制作的第一人称模型挂到当前武器。两者必须独立：没有美术模型的配件可以用于数值验证，但不能被误认为已经完成视觉交付。

## 运行时链路

`PlayerMatchLoadout` 是主武器、副武器及配件选择的唯一数据源。玩家切换武器或在准备阶段更改配件后，`WeaponInventoryController` 会重新应用当前槽位的配装；`WeaponAttachmentViewmodelPresenter` 随后执行以下工作：

1. 销毁前一套运行时配件外观，并恢复被替换的武器原生部件。
2. 按武器定义查询配件的第一人称表现绑定。
3. 在指定挂点实例化配件预制体，并继承第一人称渲染层。
4. 若该配件提供自己的 ADS 瞄准基准，则将它作为运行时 ADS 参考；否则沿用武器自身的 `AimAnchor`。
5. 倍镜通过 `ViewmodelScopeLens` 组件解析独立的 `LensAnchor`，用于镜内画面合成，不参与 ADS 姿态解算。

这样主/副武器拥有独立配置，切枪也不会把前一把武器的附件残留在当前画面中。

## 每个配件的交付要求

在 `WeaponAttachment` 的 **First-Person Visuals** 中，为每个兼容武器添加一个绑定：

| 字段 | 要求 |
| --- | --- |
| `weapon` | 该绑定对应的 `WeaponDefinition`；同一配件可为不同武器配置不同模型。 |
| `prefab` | 项目自有目录下的第一人称配件预制体，不能直接依赖外部包目录。 |
| `mountName` | 武器上的稳定挂点名称，例如 AR-4 瞄具使用 `SOCKET_Scope`。 |
| `replacedBuiltInVisualName` | 装上配件后需要隐藏的原生部件；卸下时系统会自动恢复。 |
| `aimAnchorName` | 仅瞄具需要。预制体中的 ADS 光轴参考节点名称；非瞄具留空。 |

瞄具预制体必须包含其自身的 `AimAnchor`（工具显示“ADS 瞄准基准”）和必要的 `ViewmodelClipProbe`。倍镜还必须包含独立的 `LensAnchor_<OpticId>` 并挂载 `ViewmodelScopeLens`。安装后在 Weapon Presentation Workbench 中按腰射、ADS、开火、换弹、切枪姿态完成验证，不能只在静态预览下验收。

### 挂载、瞄准与镜片的职责边界

```text
SOCKET_Scope
└─ Attachment Visual
   ├─ Model                 # 配件机械外观
   ├─ AimAnchor_*           # ADS 瞄准基准/光轴参考
   └─ LensAnchor_*          # 倍镜后镜片中心与有效口径
```

- `SOCKET_Scope` 是瞄具安置锚点。更换它会改变配件在武器导轨上的挂载基准。
- `Model` 只修复模型相对挂点的轴向、比例和机械贴合。
- `AimAnchor` 参与 ADS 姿态解算；移动它会重新摆放整把第一人称武器，但不会直接改变命中判定。
- `LensAnchor` 只承载倍镜 Render Texture 的显示平面与口径；移动它不能改变 ADS 姿态、弹道或配件挂载。完整方向制作后，本地 `+Z` 指向目标，本地 `+Y` 表示瞄具上方。

### 模型调整与锚点联动规则

模型变化是否需要重新调整锚点，取决于变化是否改变了它们共同的空间基准：

- 只替换材质、贴图、LOD，或新网格保持完全相同的原点、轴向和比例：无需调整锚点。
- 改变模型相对附件根节点的位置、旋转、比例或导入轴向：`AimAnchor`、`LensAnchor` 和 Clip Probe 必须应用相同空间变化，否则它们会脱离真实模型。
- 只修正 ADS 光轴：单独调整 `AimAnchor`，不能移动模型或 `LensAnchor`。
- 只修正镜片覆盖：单独调整 `LensAnchor` 或有效口径，不能移动模型或 `AimAnchor`。

标准附件预制体现在使用 `AttachmentCalibrationRoot`：`Model`、`AimAnchor`、`LensAnchor` 与 Clip Probe 共享该坐标系。Workbench 的“整体位置/旋转/缩放”和 Scene 中“移整体/转整体”直接编辑校准根，因此机械挂载修复会自动联动全部节点。旧附件没有校准根时仍走兼容同步路径，并显示迁移警告；正式验收前必须执行 `Project Sun/Migrate Prepared Optic Calibration Contracts` 或等价的资源迁移。

### ADS 光轴姿态迁移

`AdsSightReference` 标记 `AimAnchor` 是否已经制作完整方向。未迁移资源保持兼容模式，继续用“ADS 瞄准基准到枪口”的连线推导方向，不会因为代码升级突然改变现有画面。迁移步骤：

1. 在 Workbench 选择瞄具，启动实时预览并切换到 ADS 或并排模式。
2. 确认迁移前视觉正确，点击 **按当前视觉结果播种 ADS 光轴**。
3. 工具把旧算法当前使用的方向写入 `AimAnchor` 旋转，并启用“使用已制作光轴”；理论上点击前后视觉不应跳变。
4. 使用“转 ADS 光轴”Scene 手柄做必要的小幅方向修正；黄色光轴线表示已制作姿态，橙色细线表示仍在兼容模式。
5. 保存后在 WeaponLab 复测 ADS、开火后坐、切枪与镜片位置。

完成迁移后，`AimAnchor.position` 表示理想观察参考，`AimAnchor.forward`（本地 `+Z`）指向目标，`AimAnchor.up`（本地 `+Y`）表示瞄具上方。枪口不再参与该瞄具的视觉光轴推导。

### LensAnchor 平面迁移与验收

`ViewmodelScopeLens` 使用与 ADS 光轴相同的无损迁移原则。旧倍率镜的 `orientationAuthored` 为关闭状态，运行时镜片继续面向相机，不会因为组件升级突然侧立或镜像。正式资源按以下步骤迁移：

1. 在 Workbench 选择倍率镜、启动实时预览，并展开 **倍率镜 LensAnchor 校准与验收**。
2. 先确认青色镜片边界的中心与口径覆盖真实后镜片；位置错误时移动 `LensAnchor`，口径错误时调整有效口径，不能移动 `AimAnchor` 代替。
3. 点击 **按当前 ADS 视角播种镜片平面**。工具把当前 ADS 相机方向无损写入镜片平面，并启用完整方向契约。
4. 如需小幅修正，在 Scene 直接编辑中选择“移动 LensAnchor”“旋转镜片平面”或“调整镜片有效口径”。拖拽期间工具锁定预览参考系，只更新当前 Scene 实例；鼠标松开后才同步全部预览、合并一次 Undo 并保存一次预制体。
5. 验收报告全部自动项通过后，人工确认青色边界没有越过镜框，再点击 **确认镜片边界覆盖真实后镜片**。

自动报告只对可计算的契约给出通过结论：倍率镜 Profile、唯一 `ViewmodelScopeLens`、与 `AimAnchor` 共享校准根、Viewmodel Layer、ADS 屏幕中心偏差、投影口径、镜片方向、已制作 ADS 光轴夹角和模型包围盒粗检。真实镜框的孔洞形状无法仅靠 Renderer 包围盒可靠推断，因此仍保留独立人工确认，不能为了显示 READY 而随意移动镜片。

中心偏差统一换算为 `1920×1080` 参考像素，并显示带方向的 X/Y 数值：正 X 表示偏右，负 X 表示偏左，正 Y 表示偏上，负 Y 表示偏下。最大轴偏差不超过 `2px` 显示 **精确对准**；超过 `2px` 但仍处于镜片工程容差内显示 **工程通过**；超出工程容差显示 **未通过**。窗口尺寸不会改变该分级结果。

### 镜片渲染与镜内准星

倍率镜使用 `_ProjectSun/Resources/ProjectSunScopeLens.shader` 完成单次透明合成：镜内世界 Render Texture、解析度无关的圆形抗锯齿边缘、可选 `lensMaskTexture`、程序化或贴图准星以及 ADS 透明度渐变。Shader 位于 Resources 是为了保证动态创建的镜片材质在正式构建中不会被剥离；运行时若只能找到通用 Unlit 后备 Shader，WeaponLab 诊断会显示 `Lens FALLBACK`，不能作为商业验收结果。

Workbench 的 **镜片渲染与镜内准星** 折叠区直接编辑 `OpticSightProfile`：倍率、RT 分辨率倍率、边缘抗锯齿、渐变时间、镜片遮罩、准星贴图、后备形状、颜色和尺寸。倍率镜准星只在物理镜片材质内绘制并受遮罩裁剪；`FpsHud` 不再为倍率镜绘制全屏准星。红点和全息仍使用独立的非倍率显示路径。

### 镜外弱化与 Eyebox

镜外表现由 `ScopePeripheralRenderFeature` 在 Viewmodel Overlay Camera 的透明物体渲染结束后合成。Renderer Pass 会以活动镜片表面的同一个 `MeshRenderer`、运行时材质和当前相机矩阵生成单通道口径 Mask，再按该 Mask 保留镜内清晰区域；它不再把三维锚点提前近似成屏幕椭圆，因此 ADS 动画、后坐和平移期间读取的是镜片网格的最终变换。`Project Sun/Ensure Scope Peripheral Renderer Feature` 可修复新建的 URP Renderer；编辑器脚本重载后也会自动检查 `Assets/Settings` 下的 Project Sun Renderer。

模糊质量服从全局画质而不是单个瞄具资源：Very Low/Low 仅暗化，Medium/High 使用 4 次周边采样，Very High/Ultra 使用 8 次周边采样。`OpticSightProfile` 只保存该瞄具的镜外暗化、模糊半径和边缘过渡强度，避免美术资源反向控制整机性能策略。

Eyebox 使用相机在镜片光学坐标系中的实时位置计算。理想眼距、安全眼距范围和光轴安全角内保持完整出瞳；超出后按各自过渡区间逐渐移动并收缩可见出瞳，最终产生镜内黑边。它只改变视觉，不改变相机中心、射击射线或弹道。WeaponLab 诊断中的 `Eye`、`Axis` 和 `Eyebox` 分别表示实时眼距、光轴夹角和黑边强度；正常静止 ADS 应接近 `Eyebox 0%`。

ADS 过渡期间，倍率相机从玩家眼睛朝当帧物理镜片中心建立取景方向；镜外口径则在 Viewmodel Overlay Camera 的 Renderer Pass 中直接重绘同一个镜片网格生成 Mask。这样镜内中心射线、运行时镜片表面和镜外清晰边界共享同一几何姿态，不依赖脚本投影时序。倍率镜内外只要求中心方向连续；镜内画面具有光学倍率，边缘物体不会也不应与未放大的镜外背景按相同比例拼接，否则等同于取消倍率。

## 动态配件校准

通过 `Project Sun/Tools/Weapon Presentation Workbench` 选择 Player、武器槽位和 **校准瞄具**。工作台会创建临时 Loadout，并在源预览与两份隔离相机预览中调用与游戏一致的 `WeaponAttachmentViewmodelPresenter` 装配链路；停止预览后会销毁临时外观并恢复 Player 原生部件，不会把预览对象写入 Player 预制体。

校准职责固定如下：

1. 配件挂歪、穿模或接触导轨错误时，先回到附件资源的挂载基准处理 `Model` 适配层；这属于资产修复，不是每次 ADS 调参的步骤。
2. 检查 **ADS 瞄准基准** 是否位于瞄具光轴中心；只有光轴参考本身错误时才调整它。镜体整体与屏幕中心的偏差使用“武器整体 ADS 姿态”修正。
3. 倍镜单独检查 `LensAnchor` 是否覆盖真实后镜片中心与有效口径。镜内画面偏移或被镜筒遮挡时只调整这一层，不移动 ADS 瞄准基准。
4. 两个锚点正确后，才使用该瞄具专属 ADS Profile 和 Optic Sight Profile 调整姿态、开镜速度、FOV、倍率和渲染质量。
5. 命中判定仍以相机中心射线为准，枪口射线仅用于近距离遮挡；禁止通过移动模型或锚点修复墙体阻挡、命中或归零逻辑。

`AimAnchor` 不是“把镜体拖到屏幕中心”的旋钮，也不是瞄具安置点。它是 ADS 光轴参考，并参与武器位置与朝向解算；任意移动它都会使整把武器重新摆位。若光轴参考已正确但镜体没有包住屏幕中心的准星，应使用工作台的 **武器整体 ADS 姿态** 调整该瞄具生效的 ADS Profile。若只有镜内画面没有贴合后镜片，则调整 `LensAnchor`；`Model` 与“移动模型”Scene 手柄只用于导入资源的枢轴、比例、机械挂载修复。

当数值输入不便于判断空间关系时，在 **Scene 直接编辑** 中选择附件整体、ADS 瞄准基准、ADS 光轴、`LensAnchor`、镜片平面或有效口径。启动实时预览后，Scene 视图会显示对应彩色手柄；一次鼠标拖拽会合并为一次 Undo，并沿用工作台的延迟保存机制。镜片使用青色边界和中心连线，ADS 基准仍使用黄色标记，两者不能互相替代。

工作台的方向微调以**毫米**显示与输入，提供 `5mm`、`1mm`、`0.1mm` 与 `0.02mm` 四档快捷步距；先用 `1mm`，最后用 `0.1mm` 或 `0.02mm` 收敛。方向始终以当前预览相机为基准：常规的“武器整体 ADS 姿态”会移动完整第一人称武器，瞄具资产几何修复才会直接修改 `Model`。

每个“武器 × 配件”视觉绑定都应独立验收。当前 `WeaponAttachment` 可为同一配件按不同 `WeaponDefinition` 提供不同运行时预制体、挂点和 ADS 瞄准基准；如果后续将一支瞄具装到另一把枪，必须重新走本节流程。

### Clip Probe 验证约定

`ViewmodelClipProbe` 是近裁剪风险的**验证代理**，不是通过移动它来“修复”模型的工具。探针的黄色球应覆盖真实可见的最近表面；工作台会显示每个探针的中心深度、半径、球面净距、安全下限以及归属模型。探针未通过时，先根据明细中的名称确定是武器基础件还是当前附件：

1. 黄色球覆盖的位置不正确时，才调整探针的位置或半径。
2. 黄色球正确但净距不足时，调整 `Model` 挂载或武器的腰射/ADS 姿态，让真实可见表面远离相机。
3. 装上替换瞄具后，原生瞄具探针必须把原生可见模型设为 `visibilityOwner`；模型被隐藏时该探针会自动退出验证。工作台若检测到旧资源缺少该契约，会提供“修复原生瞄具探针可见性契约”按钮。

因此“1/3 探针未通过”不是笼统结论，而是一个可定位到具体表面、具体净距的资产验收项。

### 工作台保存行为

`Model`、ADS 瞄准基准与 `LensAnchor` 的数值输入会立即反映到实时预览；停止输入约 0.45 秒后，工作台才保存附件预制体，也可点击“立即保存”。Scene 手柄拖拽采用独立事务：拖拽期间暂停动画和 ADS 姿态重算，避免手柄参考系发生反馈漂移；鼠标松开后才同步腰射/ADS 预览并保存。一次拖拽会合并为一个 Unity Undo 操作，`Ctrl+Z` / `Ctrl+Y` 后会同步刷新预览并保存结果。

### 瞄具视图表现

`OpticSightProfile` 与 `WeaponAdsProfile` 分工明确：前者定义 ADS 时显示的准星纹样、颜色和尺寸，后者定义武器姿态、开镜速度与 FOV 缩减。执行 `Project Sun/Ensure Optic Sight Presentation Profiles` 后，M2 红点、H7 全息以及已导入的两支倍镜会各自绑定一份可编辑的视图配置；准星会同时显示在游戏 HUD 与 Workbench 的 ADS 预览中。

当前的点、环点和十字为程序化后备表现，便于先完成玩法与对齐验收。正式商业资源交付时，应在 `OpticSightProfile` 中填入项目自有的高分辨率准星贴图。倍镜已经具备独立相机、Render Texture、`LensAnchor`、抗锯齿遮罩、镜内准星、渐变、镜外分档弱化和 Eyebox；目标硬件上的最终性能预算仍需继续验收，不能与 ADS 瞄准基准校准混为一谈。

## 当前资源状态

已迁移的 Low Poly Shooter Sample 仅包含 AR-4 与 HG-3 的默认瞄具，没有 M2、H7、枪口、弹匣或枪托的独立成品模型。`Project Sun/Create Project-Owned Prototype Attachment Visuals` 会在 `_ProjectSun` 中生成四个项目自有的基础几何预制体：M2 红点、H7 全息、补偿器和消音器；并自动写入对应的视觉绑定与瞄具 ADS 配置。它们可直接用于玩法、挂点、ADS 与穿模验证，但仍是原型美术，不应作为商业正式外观验收。

未生成或未绑定第一人称外观的配件保持 **STAT ONLY**：数值、兼容性与装备流程有效，但没有独立可见外观。菜单会直接标记该状态，避免内容制作状态与玩法状态混淆。

导入或制作正式模型后，应将其复制到 `Assets/_ProjectSun/` 的项目自有资源目录，再填入上述绑定；不得重新引用 `Assets/Infima Games/` 的源包资产。

## 已导入的 AR-4 瞄具

### 导入模型的挂载基准

原始网格的建模原点不是武器导轨接触点，且其纵向轴与 AR-4 约定不同。AR-4 的 `SOCKET_Scope` 使用 `X` 横向、`Y` 向上、`Z` 指向枪口的局部坐标。批处理工具会先将源网格归一化到该坐标系，再以局部 XZ 平面作为导轨：沿导轨居中，并使其最低点以小幅嵌入贴合导轨。每个倍镜分别生成 `AimAnchor` 与带 `ViewmodelScopeLens` 的 `LensAnchor`；前者用于 ADS 光轴，后者用于物理镜片，后续校准互不覆盖。

当前批处理资源为 `SR_Scope_00 1.fbx` 与 `TAN_LR_Scope_01 1.fbx`。不要直接使用旧 prefab 或源 FBX：其外部网格和材质引用并不属于当前工程。执行 `Project Sun/Prepare All Imported AR-4 Optics` 后，工具会分别生成 `PFB_ATT_AR4_SRScope00` 与 `PFB_ATT_AR4_TanLrScope01`，移除导出残留节点、关闭第一人称无用阴影、补齐项目内回退材质、播种各自的 ADS 瞄准基准、`LensAnchor` 与 Clip Probe，并创建或绑定对应的附件和 ADS 配置。

也可分别执行 `Prepare SR Scope 00 As AR-4 Attachment` 或 `Prepare TAN LR Scope 01 As AR-4 Attachment`。批处理不会覆盖已存在 ADS Profile 的手工校准数据；播种锚点仅用于新建 Profile 的初始值。两支瞄具都必须在 WeaponLab 与 Weapon Presentation Workbench 中完成腰射、ADS 与后坐视觉验收后，才能标记为已审核。
