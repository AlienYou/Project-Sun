# 第一人称武器资产规范

状态：现行。本文是第一人称武器节点职责、命名和基础验收的权威规范。

本规范定义 Project Sun 第一人称武器、瞄具和其他可见配件的表现契约。它服务于可重复的资产制作与验证；真实命中仍由相机准心射线负责。

## 资产职责

| 数据/节点 | 职责 |
| --- | --- |
| `Muzzle` | 武器开火视觉起点，以及枪口阻挡修正的参考。 |
| `SOCKET_Scope` / `AttachmentMount_Optic` | 瞄具在武器导轨上的安装点。它只负责机械挂载。 |
| `Model` | 配件相对安装点的网格适配层；用于修复导入轴向、比例与导轨贴合。 |
| `AimAnchor` | ADS 瞄准基准。它定义瞄具光轴中心，供整把武器的 ADS 姿态解算使用。 |
| `LensAnchor` + `ViewmodelScopeLens` | 倍镜后镜片的物理中心与有效口径，只负责镜内 Render Texture 的位置和尺寸。 |
| `WeaponPresentationProfile` | 腰射位置、旋转与后坐力表现。 |
| `WeaponAdsProfile` | ADS 对齐、FOV、归零距离与视觉微调。 |
| `ViewmodelClipProbe` | 明确标记必须保持在相机前方的关键可见表面。 |

`SOCKET_Scope` 才是“瞄具安置锚点”。`AimAnchor` 的工具显示名称统一为“ADS 瞄准基准”，它不是配件挂点，也不负责镜片渲染。`LensAnchor` 可以独立移动而不改变 ADS 姿态。三者都不改变相机准心命中规则；`Muzzle` 也不是第一发命中的权威来源。

## 推荐层级与命名

武器基础节点主要由序列化引用识别；动态配件的挂点与 ADS 瞄准基准仍由视觉绑定名称解析，镜片则由 `ViewmodelScopeLens` 组件解析。迁移工具和人工检索也依赖一致命名，因此新资产应采用以下约定：

```text
WeaponRoot_<WeaponId>
├─ Muzzle
├─ AimAnchor_<WeaponId>              # 仅可 ADS 的武器需要
├─ AttachmentMount_Optic             # 当前 AR-4 名称为 SOCKET_Scope
│  └─ Optic_<OpticId>                # 运行时配件根节点
│     └─ AttachmentCalibrationRoot    # 挂 ViewmodelAttachmentCalibrationRoot
│        ├─ Model
│        ├─ AimAnchor_<OpticId>       # 挂 AdsSightReference
│        ├─ LensAnchor_<OpticId>      # 倍镜需要，挂 ViewmodelScopeLens
│        └─ ClipProbe_<OpticId>_Housing
├─ ClipProbe_<WeaponId>_SightHousing
├─ ClipProbe_<WeaponId>_Muzzle
└─ ClipProbe_<WeaponId>_Receiver     # 需要时添加
```

当前 Low Poly Shooter Pack 的已知节点使用 `SOCKET_Muzzle`、`SOCKET_Scope`。`Project Sun/Ensure Per-Weapon Viewmodel Clip Probes` 只为现有 AR-4 与 HG-3 创建基础探针，不会自动判断后续新武器的表面位置。

## Clip Probe 规则

在武器或配件第一人称可见模型的子节点上添加 `ViewmodelClipProbe` 组件。Workbench 会收集当前激活武器根节点下所有启用的 Probe；未激活的配件不会参与当前配置的验证。

- `validationLabel`：面向验收报告的名称。
- `surfaceRadius`：该 Probe 代表的可见表面半径，而不是装饰性标记大小。
- 推荐至少使用 `SightHousing` 和 `Muzzle`；镜头附近的机匣、激光器、下挂或大型瞄具应额外放置 Probe。
- Probe 应放在实际可能接近镜头的可见表面中心，半径应覆盖该局部外形；不要放在骨骼原点、隐藏面或完整模型 Bounds 上。

验证距离为“相机前向深度 - `surfaceRadius`”。通过阈值为 Viewmodel Camera 近裁剪面加 Workbench 的 Clip Probe 安全余量。Probe 失败只报告明确的资产契约问题，不会阻止设计师继续微调 ADS。

## 校准与验收流程

1. 通过 `SOCKET_Scope` 和 `Model` 校验机械挂载、轴向、比例与导轨贴合。
2. 为可 ADS 武器/瞄具配置 `AimAnchor`（工具中显示“ADS 瞄准基准”）、Presentation Profile 与 ADS Profile。
3. 为倍镜配置独立 `LensAnchor` 与 `ViewmodelScopeLens`，使其贴合后镜片中心和有效孔径。
4. 添加/检查武器本体和已激活配件的 Clip Probe。
5. 打开 `Project Sun/Tools/Weapon Presentation Workbench`，选择对应武器槽位并启动实时预览。
6. 校验腰射与 ADS：瞄准基准误差应在工具阈值内，并人工确认镜片、准星、模型构图和近裁剪均正确。
7. 保存 Profile，并将视觉审核状态设为已确认。

新增瞄具时，应同时提供其视觉模型、必要的 ADS Profile 覆盖和自身 Clip Probe；倍镜还必须提供 `LensAnchor`。不要通过修改步枪基础 Probe 来迁就某一件瞄具。

## 配件兼容性与菜单

`WeaponAttachment` 通过 `compatibleWeapons` 定义可安装的武器族。空列表表示该配件可被 Catalog 中的任意武器使用；武器专用配件必须显式列出允许的 `WeaponDefinition`。当前 Catalog 的十个步枪配件均限定为 AR-4，HG-3 在菜单中不会显示这些选项。

Loadout Menu 在准备阶段允许分别选择主武器、副武器及各自的配件。主武器配件会立即更新准备阶段的数值；副武器配件存入副武器 Loadout，并在玩家按 `2` 切换到副武器时应用。对局开始后，RoundManager 会锁定所有选择。

## 提交要求

一项武器表现变更必须同提交以下相关资产：

- 武器/配件预制体及其 Clip Probe；
- 对应 Presentation、ADS 或附件配置；
- 影响验证行为的工具或运行时代码；
- 本规范（若契约或命名发生变化）。

提交前至少在 Workbench 中检查腰射与 ADS；后续加入开火、换弹和切枪动作采样后，也必须通过全部动作姿态验证。
