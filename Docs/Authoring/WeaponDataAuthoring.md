# 武器与配装数据制作规范

状态：现行。本文负责玩法数据和配装目录；第一人称模型、ADS、镜片与 Clip Probe 由对应视觉制作规范负责。

## 数据职责

| 数据资产 | 职责 |
| --- | --- |
| `WeaponDefinition` | 武器身份、基础伤害、射速、弹匣、换弹、散布、射程和对应表现配置。 |
| `WeaponAttachment` | 配件槽位、兼容武器、玩法倍率、可选 ADS 覆盖和每把武器的第一人称视觉绑定。 |
| `WeaponLoadoutCatalog` | 对局可选择的武器、兼容配件和战术装备目录。 |
| `WeaponPresentationProfile` | 腰射构图、默认 ADS 与后坐力表现，不保存玩法伤害。 |
| `WeaponAdsProfile` | ADS 姿态、过渡、FOV、归零和光轴校准，不决定实际命中。 |
| `OpticSightProfile` | 瞄具类型、倍率、镜片、准星和镜外合成表现。 |

`PlayerMatchLoadout` 是本地当局主武器、副武器、各自配件与战术装备选择的唯一运行时数据源。UI 只提交选择，`RoundManager` 决定准备阶段是否允许修改，`WeaponInventoryController` 负责把当前槽位应用到武器系统。

## 当前目录

```text
Assets/_ProjectSun/Data/Weapons/
├─ Definitions/     # AR4Carbine、HG3Sidearm 等武器定义
├─ Attachments/     # 数值、兼容性和第一人称绑定
├─ Catalogs/        # 对局可用内容目录
├─ Presentation/    # 每把武器的腰射与表现基准
├─ ADS/             # 武器机瞄或瞄具专用 ADS 配置
├─ Optics/          # 瞄具渲染配置
└─ Tactical/        # 手雷、感应雷等战术装备定义
```

## 新武器流程

1. 在 `Definitions` 新建 `WeaponDefinition`，使用稳定且唯一的武器标识，填写合法的基础战斗参数。
2. 为武器创建独立 `WeaponPresentationProfile` 和默认机瞄 `WeaponAdsProfile`，不要复用另一把武器的可调资产。
3. 制作第一人称 Prefab、枪口、动画与 Clip Probe，并按武器资产规范完成腰射、ADS、开火、换弹和切枪验收。
4. 把武器加入 `WeaponLoadoutCatalog` 的正确槽位；主武器和副武器不得仅靠名称推断分类。
5. 为可用配件显式填写兼容武器；空兼容列表表示目录内通用，使用前必须确认这是设计意图。
6. 在准备阶段选择该武器，进入战斗后确认配装锁定、弹药重置、死亡等待和新回合恢复均正确。

## 新配件流程

1. 选择唯一配件槽位并填写玩法倍率；`1` 表示不改变，禁止用零或负数制造无效伤害、射速、容量、换弹或射程。
2. 填写兼容武器列表。武器专用配件不得依赖 UI 隐藏来阻止非法装备。
3. 可视配件按兼容武器分别添加第一人称绑定；同一模型装到另一把武器时仍需重新验收挂点和 ADS。
4. 瞄具需要自己的 ADS Profile 覆盖；倍率镜还需要 `OpticSightProfile`、`LensAnchor` 和镜片组件。
5. 在 Workbench 检查模型挂载、腰射、ADS 和 Clip Probe，再到 WeaponLab/CombatSlice 验证运行时装配、切枪、换弹和射击。

## 数值与联网边界

- ScriptableObject 是离线验证与客户端展示所需的受控定义，不是未来客户端可自行声明的权威数值。
- 联网时客户端只提交稳定 ID；服务器从自己的受控目录重建武器与配件组合，并验证槽位、兼容性和解锁资格。
- 数值调整必须记录测试场景、目标距离和结果；正式平衡流程建立前，不通过复制多份近似资产来保存临时版本。
- 生成器适合首次播种和补齐目录，不得覆盖已经人工校准的 ADS、瞄具或表现资产。
