# Project Sun 迭代 A 任务卡

状态：已取消。创建日期：2026-08-16。实现日期：2026-08-16。调整日期：2026-08-17。

> 范围调整说明：现阶段改为优先推进可玩原型，本卡已实现的构建入口、Validator、构建门禁和 EditMode 测试继续保留；尚未执行的故障注入、死亡观战完整签字、连续三回合、Windows 产物和日志审计不再阻塞原型开发，统一迁移至工程开发计划的 Gate 4。本文保留为历史实现记录，不再作为当前执行任务卡。

## 任务概览

| 字段 | 内容 |
| --- | --- |
| 任务编号 | `PS-VS-A01` |
| 名称 | 核心切片启动门禁与死亡观战验收 |
| 对应计划 | Gate 0；Gate 1 的观战场景签字 |
| 优先级 | P0 |
| 建议负责人 | Gameplay/Tools 程序 1 人，QA 或关卡协作验收 |
| 前置依赖 | Unity `2022.3.51f1c1`；现有 `CombatSlice`、`WeaponLab`、6v6 名册与观战实现 |
| 预期成果 | 项目能够从正确入口启动；关键配置可一键预检；死亡观战完成全路径 Play Mode 验收 |

## 为什么先做这张卡

目前玩法模块已经较多，但构建首场景仍指向第三方展示场景，项目配置检查分散，死亡观战也处于“实现完成、尚未场景签字”的状态。如果不先解决这些问题，后续击杀播报、比赛结算和自动化测试会建立在不稳定入口与未闭合生命周期上。

本卡完成后，后续迭代可以把 `CombatSlice` 当作稳定集成基线，并复用同一个验证框架扩展战斗数据、场景和构建检查。

## 工作范围

### A01-1：修正开发构建入口

目标：Windows Development Build 启动后直接进入 Project Sun 内容，而不是第三方资源展示场景。

实施要求：

1. 将 `Assets/_ProjectSun/Scenes/CombatSlice.unity` 设为当前开发构建首场景。
2. 第三方展示场景从启用的 Build Scenes 中移除；不删除第三方源资源。
3. `WeaponLab` 作为专项测试场景保留，但不能成为玩家默认入口。
4. 检查场景启动后仅存在一个玩家、一个 Base Camera、一个启用的 AudioListener、一个 HUD 和一个 `RoundManager`。
5. `F8`、`F7`、`F10` 等测试入口只允许在 Development Build、Unity Editor 或显式开发开关下工作。

验收：

- 从 Windows Development Build 启动后进入 `CombatSlice`。
- 不经过编辑器菜单也能开始准备阶段并进入战斗。
- Console/Player Log 不出现项目新增错误或警告。

### A01-2：建立 Project Validator 最小版本

目标：在进入 Play Mode 或生成开发构建前发现关键工程配置错误。

建议入口：`Project Sun > Validation > Validate Project`。

首版必须检查：

1. Unity 版本是否为 `2022.3.51f1c1`。
2. `Wall`、`First Person View`、`Character` 等必需 Layer 是否存在且与 `CombatLayers` 契约一致。
3. Input System 配置和项目使用的 Input Actions 是否可用。
4. Build Scenes 是否以 `CombatSlice` 为首个启用场景，是否错误启用了第三方展示场景。
5. `CombatSlice` 与 `WeaponLab` 是否存在，且场景中没有 Missing Script。
6. `CombatSlice` 是否具有唯一的玩家、`RoundManager`、HUD、场景安装器和双方出生点组。
7. 双方是否各有 6 个有效槽位，成员是否存在阵营/槽位冲突。
8. 武器目录、主副武器定义、战术装备资产和关键 Player Prefab 是否存在。
9. URP Renderer Data 是否安装 `ScopePeripheralRenderFeature`；缺失时只报告，不自动静默修改。

实现约束：

- 校验逻辑放入 `Assets/Editor`，运行时代码不得依赖 `UnityEditor`。
- 每条结果包含稳定检查编号、严重程度、中文描述及可定位的对象或资产引用。
- 校验过程默认只读；修复按钮必须逐项显式执行，并支持 Undo、标脏和保存。
- 批量校验不得修改第三方原始资源或现有 ADS/附件人工校准数据。
- 提供可由 EditMode 测试直接调用的纯校验方法，避免测试只能点击 EditorWindow。

验收：

- 正常项目得到明确 PASS 摘要。
- 人为移除一个 Layer、场景引用或出生槽位时，工具能给出对应失败项和定位对象。
- 校验可重复执行，不改变场景和资产内容。

### A01-3：接入 Development Build 前置门禁

目标：避免在关键配置已经失败时继续产生不可用构建。

实施要求：

1. 增加项目自己的 Windows Development Build 菜单入口。
2. 构建前运行 Project Validator；存在 Error 级结果时终止构建，Warning 只记录但必须在报告中展示。
3. 构建报告记录 Unity 版本、时间、当前提交标识（可获取时）、启用场景、校验摘要和输出目录。
4. 输出目录必须由用户显式选择或位于明确的项目构建目录；不得写入 `Assets`、`Library`、`Temp` 或第三方目录。
5. 本卡只建立本地开发构建，不引入联网 SDK、发布平台 SDK 或云端 CI。

验收：

- 制造一个 Error 级配置问题时构建被阻止，修复后可正常生成 Windows Development Build。
- 构建失败不会遗留被临时修改的 Build Settings、质量档或场景脏状态。

### A01-4：完成死亡观战 Play Mode 验收

目标：确认现有 `PlayerSpectatorController` 已真正闭合玩家死亡与下一回合生命周期。

执行场景：`Assets/_ProjectSun/Scenes/CombatSlice.unity`。

验收步骤：

1. 保留至少两名进攻方 Bot 存活，让本地玩家死亡。
2. 确认进入己方第三人称观战，玩家手臂、武器、ADS、准星和受伤提示隐藏。
3. 使用左右方向键循环目标，确认永不选中敌方，且顺序按稳定槽位循环。
4. 淘汰当前观战目标，确认自动切换到下一名存活队友且镜头不横穿关卡。
5. 让观战目标贴墙移动，确认相机被 Wall Layer 收近，不穿到墙外。
6. 观战期间打开并关闭设置菜单，确认死亡玩家不会恢复移动、射击、换弹、技能或装备输入。
7. 修改观战按键，重新进入 Play Mode，确认改键持久化且新按键生效。
8. 等待全队淘汰和回合结算，确认镜头冻结；下一回合准备阶段恢复原 Camera、AudioListener、HUD 与输入状态。
9. 连续重复三个回合，确认没有重复相机、重复 AudioListener、事件订阅累积或状态泄漏。

缺陷处理：发现问题时在本卡范围内修复观战、输入阻塞或回合恢复逻辑；如果根因要求重构战斗事件或回合状态机，先记录阻塞与复现步骤，不在本卡中扩展成击杀播报系统。

## 自动化测试

本卡至少新增以下 EditMode 覆盖：

- Project Validator 对正确/错误 Build Scene 顺序的判断。
- 必需 Layer、关键资产与场景唯一对象的检查结果。
- 双方出生槽位缺失、重复和越界报告。
- Error 结果阻止构建，Warning 不阻止构建的门禁规则。
- 重复运行只读校验不改变被检查对象。

观战相机切换、AudioListener 接管和下一回合恢复优先增加 PlayMode 冒烟测试；若相机画面或防穿墙结果无法稳定自动判定，必须保留人工验收记录，不能以 EditMode 测试替代。

## 交付物

- 修正后的 `ProjectSettings/EditorBuildSettings.asset`。
- Project Validator 编辑器代码与对应 EditMode 测试。
- Windows Development Build 菜单及构建前门禁。
- 观战缺陷修复（如验收发现问题）及相关测试。
- 更新后的 `Docs/QA/CombatSlice验收清单.md`。
- 本卡底部的执行结果、未解决问题和验证环境记录。

如果移动、新增或删除 Unity 资源，必须同时提交对应 `.meta`；不得修改自动生成的 `.csproj`、`.sln`、`Library`、`Temp`、`Logs` 或 `obj`。

## 明确不包含

- 击杀事件模型、击杀播报和比赛结束 UI；这些属于迭代 B。
- Bot 感知、射击误差和掩体行为升级；这些属于迭代 D。
- F-1/S-1 正式 Prefab 化；这些属于迭代 E。
- 爆破模式、联网、匹配、账号、商业平台 SDK 和正式 Release 构建。
- Unity、URP、Input System 或其他 Package 升级。

## 完成定义

只有同时满足以下条件，任务状态才能改为“已通过”：

- 所有子任务完成，Project Validator 在正常工程上返回 PASS。
- C# 编译通过，Unity Console 无项目新增错误或警告。
- 新增 EditMode 测试在 Unity Test Runner 中正式通过。
- 死亡观战九步验收完成，并连续运行三个回合无状态泄漏。
- Windows Development Build 从正确场景启动并完成至少一个回合。
- QA 清单、工程开发计划中的状态和本卡执行记录同步更新。

## 执行记录

| 项目 | 结果 |
| --- | --- |
| 开始时间 | 2026-08-16 |
| 完成时间 | 实现完成；运行验收未完成 |
| Unity 版本 | `2022.3.51f1c1` |
| C# 编译 | 通过：运行时 0 error；2 条第三方 `CharacterKinematics` CS0649 warning；新增 Editor/测试代码独立编译 0 error |
| EditMode | 通过：Unity Test Runner EditMode 共 9/9 通过、0 失败、0 忽略；`ProjectValidatorTests` 与 `TeamRosterTests` 均全绿 |
| PlayMode/人工观战验收 | 九步及连续三回合记录模板已补充；待人工执行 |
| Windows Development Build | 菜单、门禁与报告已实现；实际产物启动及完整回合待验收 |
| 已知问题 | Play Mode 死亡观战九步、连续三回合及 Windows Development Build 产物仍待验收；EditMode 原始截图待按验收报告命名规则归档 |
| 最终签字 | 待 QA/关卡完成运行验收后签字 |

### 本次实现摘要

- `CombatSlice` 已调整为首个启用 Build Scene，`WeaponLab` 保留为第二专项测试场景，第三方展示场景已从启用列表移除。
- 新增 `Project Sun > Validation > Validate Project`：检查固定 Unity 版本、Layer/Input 契约、Build Scenes、关键场景与资产、Missing Script、CombatSlice 唯一对象、双方六槽位和 URP Renderer Feature。校验默认只读且可重复调用。
- 新增 `Project Sun > Build > Windows Development Build`：Error 阻断、Warning 入报告，输出目录显式选择并拒绝 Unity 受控目录，报告记录版本、时间、提交、场景、摘要和产物位置。
- 新增 EditMode 测试覆盖场景顺序、Layer、槽位缺失/重复/越界、构建门禁和重复运行不改变场景布局。
- WeaponLab 的 F6/F7 与自动重置现仅在 Editor、Development Build 或显式 Release 测试开关下可用。
- 详细环境记录、逐步测试、证据规范、缺陷分级与签字条件见[迭代 A 验收规约与报告](../QA/迭代A验收报告.md)。任务卡不得用简略结论覆盖该报告中的逐项测试结果。

## 后续任务入口

本卡通过后立即开始迭代 B：统一 `CombatEvent`/`KillEvent`、击杀播报、回合结算面板和比赛结束/再来一局流程。迭代 B 不应在 A01-1 至 A01-4 尚有 P0 阻断问题时并行合入主线。
