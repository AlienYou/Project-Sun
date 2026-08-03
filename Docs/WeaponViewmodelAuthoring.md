# 第一人称武器资产规范

本规范定义 Project Sun 第一人称武器、瞄具和其他可见配件的表现契约。它服务于可重复的资产制作与验证；真实命中仍由相机准心射线负责。

## 资产职责

| 数据/节点 | 职责 |
| --- | --- |
| `Muzzle` | 武器开火视觉起点，以及枪口阻挡修正的参考。 |
| `AimAnchor` | ADS 时的视觉机瞄/瞄具中心。它应位于真实可见准星中心。 |
| `WeaponPresentationProfile` | 腰射位置、旋转与后坐力表现。 |
| `WeaponAdsProfile` | ADS 对齐、FOV、归零距离与视觉微调。 |
| `ViewmodelClipProbe` | 明确标记必须保持在相机前方的关键可见表面。 |

`AimAnchor` 只负责视觉对齐，不改变相机准心命中规则；`Muzzle` 也不是第一发命中的权威来源。

## 推荐层级与命名

运行时由序列化引用识别节点，不依赖名称字符串；但迁移工具和人工检索依赖一致命名，因此新资产应采用以下约定：

```text
WeaponRoot_<WeaponId>
├─ Muzzle
├─ AimAnchor_<WeaponId>              # 仅可 ADS 的武器需要
├─ AttachmentMount_Optic
│  └─ Optic_<OpticId>
│     └─ ClipProbe_<OpticId>_Housing
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

1. 为武器配置 `Muzzle`、`AimAnchor`（如支持 ADS）、Presentation Profile 与 ADS Profile。
2. 添加/检查武器本体和已激活配件的 Clip Probe。
3. 打开 `Project Sun/Tools/Weapon Presentation Workbench`，选择对应武器槽位并启动实时预览。
4. 校验腰射与 ADS：机瞄参考点误差应在工具阈值内，并人工确认视图无穿模或不自然构图。
5. 在“第一人称近裁剪契约”中确认所有 Probe 通过；Scene 视图中绿色圆环为通过，红色为失败。
6. 保存 Profile，并将视觉审核状态设为已确认。

新增瞄具时，应同时提供其视觉模型、必要的 ADS Profile 覆盖和自身 Clip Probe。不要通过修改步枪基础 Probe 来迁就某一件瞄具。

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
