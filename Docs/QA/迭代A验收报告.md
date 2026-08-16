# Project Sun 迭代 A 验收规约与报告

任务编号：`PS-VS-A01`  
报告版本：1.1  
报告日期：2026-08-16  
当前结论：**暂停执行，未完成项迁移至 Gate 4；本文仅保留历史实现与已有结果记录**。

> 2026-08-17 范围调整：现阶段优先推进可玩原型，不再要求执行本报告中的完整故障注入、逐项截图、三回合稳定性、Windows Development Build 与日志审计。本报告不得用于阻塞当前原型工作；进入 Gate 4 正式签字阶段时，应先按届时实现和风险重新审查用例，再决定复用或更新。

## 1. 文档用途

本文原为迭代 A 的测试执行规约和最终验收记录，现作为暂停工作包的历史资料保存。当前原型回归以[CombatSlice 验收清单](CombatSlice验收清单.md)中的轻量冒烟为准；Gate 4 恢复正式验收时，再按届时版本确认本文用例是否仍然适用。

后续迭代的验收文档应复用以下结构：

1. 任务与版本信息。
2. 范围、非范围和风险。
3. 测试环境与前置条件。
4. 自动化、功能、故障注入、稳定性和构建测试。
5. 每个用例的步骤、预期结果、实际结果和证据。
6. 缺陷清单、残余风险、最终判定与角色签字。

## 2. 结果与证据规范

### 2.1 允许的结果

| 结果 | 使用条件 |
| --- | --- |
| 未执行 | 尚未实际完成测试步骤；默认状态 |
| 通过 | 全部步骤符合预期，证据完整且没有未豁免异常 |
| 失败 | 任一步骤不符合预期，必须填写缺陷编号和复现信息 |
| 阻塞 | 环境或外部依赖导致无法执行，必须写明阻塞条件；不能当作通过 |
| 不适用 | 经任务负责人和 QA 确认不属于当前配置，必须填写原因 |

不得使用“基本通过”“大概正常”“代码看起来没问题”等模糊结论。测试只要出现项目新增 Error、未登记 Warning、状态泄漏或无法恢复的工程修改，该用例即为失败。

### 2.2 证据命名

体积较小的关键截图保存到仓库内 `Docs/QA/Evidence/PS-VS-A01/`，并在对应测试用例的“证据/缺陷”位置用相对路径直接嵌入本文，使验收结论可随提交审计。只写文件路径、外部链接、聊天附件或“已查看截图”不算完成截图证据；凡是用例要求截图或需要画面状态佐证，未嵌入图片不得判定为通过。体积较大的视频、`Player.log` 和构建产物仍保存到仓库外的受控测试目录，本文只记录其路径与摘要：

```text
Docs/QA/Evidence/PS-VS-A01/
├─ Environment/
├─ EditMode/
├─ Validator/
├─ Spectator/
├─ Stability/
└─ Build/
```

文件名格式为 `<用例ID>_<步骤或内容>_<日期时间>.<扩展名>`，例如：

```text
A01-VAL-001_PASS_20260816-2130.png
A01-SPEC-004_TargetDeathSwitch_20260816-2205.mp4
A01-BUILD-003_Player.log
```

截图必须包含完整窗口或足够的上下文；日志必须保留发生时间、Unity 版本和错误堆栈。视频类证据需能看清输入、状态变化和最终结果。仅在用例明确不需要截图且其他证据已充分证明结果时，才可省略图片，并在“证据/缺陷”中说明原因。

## 3. 验收范围

| 子任务 | 验收内容 | 当前实现状态 |
| --- | --- | --- |
| A01-1 | Build Scenes、正确启动入口、场景唯一对象、开发快捷键边界 | 已实现，待验收 |
| A01-2 | Project Validator 正常检查、稳定编号、定位、只读性和故障注入 | EditMode 自动化已通过；待 Unity 菜单与故障注入验收 |
| A01-3 | Error 阻断、Warning 放行、目录保护、Windows Development Build 与报告 | 已实现，待实际构建 |
| A01-4 | 死亡观战、输入阻塞、目标切换、防穿墙和新回合恢复 | 已实现，待 Play Mode 验收 |

本报告不验收击杀播报、正式比赛结束 UI、Bot 行为升级、战术装备 Prefab 化、联网或 Release 发布流程。

## 4. 测试环境记录

执行前填写，不允许留空后签字：

| 字段 | 实际值 |
| --- | --- |
| 测试人员 | 待填写 |
| 测试日期和时区 | 待填写；Asia/Shanghai |
| Git 提交/工作区标识 | 待填写 |
| Unity | `2022.3.51f1c1` |
| 操作系统 | 待填写 |
| CPU / GPU / 内存 | 待填写 |
| 显示分辨率和质量档 | 待填写 |
| 输入设备 | 键盘鼠标；实际型号待填写 |
| Development Build 输出路径 | 待填写 |
| 证据根目录 | 仓库内截图：`Docs/QA/Evidence/PS-VS-A01/`；大体积日志/视频：待填写 |

### 4.1 前置条件

1. 备份或提交测试前合法改动，并记录 `git status --short`；现有瞄具校准改动不得被故障注入覆盖。
2. 使用 Unity Hub 确认编辑器版本为 `2022.3.51f1c1`。
3. 打开项目并等待脚本编译、Shader 导入和 AssetDatabase 刷新完成。
4. 清空 Unity Console，开启 Error、Warning、Log 三类显示。
5. 确认测试期间不运行第二个 Unity Editor 实例。
6. 故障注入只能使用可立即恢复的临时操作；每次注入前记录原值，每次用例结束后恢复并重新运行 Validator。
7. 不修改或删除第三方源资源，不使用 `git reset --hard`、`git checkout --` 等方式恢复工作区。

## 5. 已完成的静态验证

- Build Scenes 序列化顺序已静态审计为 CombatSlice、WeaponLab，第三方展示场景未启用。
- CombatSlice 场景 YAML 已静态确认双方各六个成员槽位和各一个六槽位出生组。
- 项目 URP Renderer Data 已静态确认包含 `ScopePeripheralRenderFeature`。
- 2026-08-16 运行 `dotnet build Assembly-CSharp.csproj`：0 error；2 warning 来自第三方 Low Poly Shooter Pack 的 `CharacterKinematics.cs` 既有 CS0649。
- 新增 Editor/测试代码已完成独立编译检查：0 error。
- 2026-08-16 Unity Test Runner EditMode 全量运行：9/9 通过、0 失败、0 忽略；`ProjectValidatorTests` 和 `TeamRosterTests` 均全绿。

上述结果只作为辅助证据，不替代 Unity Console、Test Runner、Play Mode 和构建产物测试。

## 6. 测试执行总表

| 用例 ID | 名称 | 类型 | 结果 | 证据/缺陷 |
| --- | --- | --- | --- | --- |
| A01-ENV-001 | 工程版本与初始状态 | 环境 | 未执行 |  |
| A01-TEST-001 | EditMode 全量测试 | 自动化 | 通过 | Test Runner 9/9 全绿，Console 0/0/0；三张原图待归档至 `Docs/QA/Evidence/PS-VS-A01/EditMode/` |
| A01-VAL-001 | Validator 正常项目 PASS | 功能 | 未执行 |  |
| A01-VAL-002 | Build Scene 错误故障注入 | 负向 | 未执行 |  |
| A01-VAL-003 | Layer 错误故障注入 | 负向 | 未执行 |  |
| A01-VAL-004 | 阵营槽位错误故障注入 | 负向 | 未执行 |  |
| A01-VAL-005 | Validator 重复执行只读性 | 稳定性 | 未执行 |  |
| A01-SPEC-001 | 玩家死亡进入己方观战 | Play Mode | 未执行 |  |
| A01-SPEC-002 | 手动循环目标与敌方隔离 | Play Mode | 未执行 |  |
| A01-SPEC-003 | 目标死亡自动切换 | Play Mode | 未执行 |  |
| A01-SPEC-004 | 相机 Wall Layer 防穿墙 | Play Mode | 未执行 |  |
| A01-SPEC-005 | 设置菜单与死亡输入阻塞 | Play Mode | 未执行 |  |
| A01-SPEC-006 | 观战改键持久化 | Play Mode | 未执行 |  |
| A01-SPEC-007 | 结算冻结与新回合恢复 | Play Mode | 未执行 |  |
| A01-STAB-001 | 连续三回合状态稳定性 | 稳定性 | 未执行 |  |
| A01-BUILD-001 | Validator Error 阻断构建 | 负向 | 未执行 |  |
| A01-BUILD-002 | 输出目录保护 | 负向 | 未执行 |  |
| A01-BUILD-003 | Windows Development Build | 构建 | 未执行 |  |
| A01-BUILD-004 | 构建产物完整回合 | 冒烟 | 未执行 |  |
| A01-LOG-001 | Console、Player Log 与报告审计 | 日志 | 未执行 |  |

## 7. 详细测试步骤

### A01-ENV-001：工程版本与初始状态

前置条件：Unity 尚未进入 Play Mode。

步骤：

1. 打开 `Help > About Unity`，记录完整版本。
2. 打开 `File > Build Settings`。
3. 检查首个启用场景为 `Assets/_ProjectSun/Scenes/CombatSlice.unity`。
4. 检查第二个启用场景为 `Assets/_ProjectSun/Scenes/WeaponLab.unity`。
5. 确认第三方 Low Poly Shooter 展示场景未启用。
6. 打开 Console，确认脚本刷新后没有项目代码产生的 Error 或新增 Warning。

预期结果：Unity 版本准确；场景顺序符合要求；第三方场景不进入构建；Console 无项目新增错误警告。

实际结果：未执行。  
证据/缺陷：待填写。

### A01-TEST-001：EditMode 全量测试

步骤：

1. 打开 `Window > General > Test Runner`。
2. 选择 EditMode 页签。
3. 点击 `Run All`，等待所有测试完成。
4. 展开 `ProjectValidatorTests` 和 `TeamRosterTests`，记录测试数量与耗时。
5. 保存 Test Runner 全绿截图，并导出测试结果（当前环境支持时）。
6. 检查 Console 是否出现测试引入的错误或未预期 Warning。

预期结果：所有 EditMode 测试通过；不得只单独运行新增测试来代替全量执行；Console 无项目新增异常。

实际结果：通过。EditMode 汇总为 9/9 通过、0 失败、0 忽略；`ProjectValidatorTests` 与 `TeamRosterTests` 均显示全绿。截图界面显示所选套件耗时分别约为 0.033 秒与 0.012 秒。  
证据/缺陷：会话中曾提供两张 Test Runner 截图和一张测试后 Console 截图，记录的 Console 计数为 0 Log、0 Warning、0 Error；对应图片当前未归档到仓库，因此这里只保留结果摘要，不把缺失文件作为现行证据要求。Gate 4 恢复正式验收时重新生成所需证据。

### A01-VAL-001：Validator 正常项目 PASS

步骤：

1. 退出 Play Mode，保存当前场景。
2. 执行 `Project Sun > Validation > Validate Project`。
3. 检查摘要中的 Error 和 Warning 数量。
4. 逐组展开环境、Layer、输入、场景、资产、阵营槽位和 URP 结果。
5. 对至少一个具有上下文的结果使用定位能力，确认 Project/Hierarchy 选择正确对象或资产。
6. 关闭并重新打开 Validator，确认结果一致。

预期结果：摘要为 PASS；所有必需检查均存在稳定 `PSV-*` 编号；定位对象正确；工具没有修改或保存任何场景与资产。

实际结果：未执行。  
证据/缺陷：待填写。

### A01-VAL-002：Build Scene 错误故障注入

步骤：

1. 记录 Build Settings 原始场景顺序。
2. 临时将 WeaponLab 移到首位，或启用第三方展示场景。
3. 执行 Validator。
4. 检查 `PSV-BUILD-001` 或 `PSV-BUILD-002` 为 Error，描述能说明具体问题。
5. 恢复原始场景顺序并保存 Build Settings。
6. 再次执行 Validator。

预期结果：错误配置被稳定编号捕获；恢复后对应 Error 消失；最终 Build Settings 与测试前一致。

实际结果：未执行。  
证据/缺陷：待填写。

### A01-VAL-003：Layer 错误故障注入

前置条件：先截图记录 `Project Settings > Tags and Layers` 中 8、9、10 层原值。

步骤：

1. 临时修改一个非当前运行场景依赖的必需 Layer 名称；不要进入 Play Mode。
2. 执行 Validator。
3. 确认对应 `PSV-LAYER-008/009/010` 为 Error，并显示期望名称和实际名称。
4. 立即恢复 Layer 原值。
5. 再次执行 Validator，并检查相关 Prefab/场景没有被保存为脏状态。

预期结果：错误索引或名称被识别；恢复后检查通过；序列化引用未受损。

实际结果：未执行。  
证据/缺陷：待填写。

### A01-VAL-004：阵营槽位错误故障注入

前置条件：复制 CombatSlice 到临时测试场景，所有故障只在副本中执行，禁止保存到正式场景。

步骤：

1. 打开临时场景副本。
2. 将一个 Bot 的槽位改为同阵营已使用槽位，执行相关纯校验或 Validator。
3. 确认报告重复槽位 Error。
4. 将该成员槽位改为越界值，确认 `PSV-TEAM-001` 或对应稳定错误。
5. 清空一个出生点锚点引用，确认对应 `PSV-SPAWN-*` 错误。
6. 关闭临时场景且不保存修改，重新打开正式 CombatSlice。
7. 执行 Validator 确认恢复 PASS。

预期结果：重复、越界和缺失分别可定位；正式场景未被修改。

实际结果：未执行。  
证据/缺陷：待填写。

### A01-VAL-005：Validator 重复执行只读性

步骤：

1. 记录 `git status --short` 并保存输出。
2. 连续执行 Validator 三次。
3. 重新记录 `git status --short`。
4. 比较两次输出，并检查当前场景是否出现未预期星号或保存提示。

预期结果：三次结果一致；Validator 不增加或修改任何场景、Prefab、配置或校准资产。

实际结果：未执行。  
证据/缺陷：待填写。

### A01-SPEC-001：玩家死亡进入己方观战

前置条件：打开 CombatSlice，确保至少两名进攻方 Bot 存活。

步骤：

1. 进入 Play Mode，等待准备阶段结束。
2. 让本地玩家被敌方击杀。
3. 观察相机接管、HUD 和第一人称表现。
4. 在 Hierarchy 中检查启用的 Camera 和 AudioListener 数量。

预期结果：立即进入己方存活 Bot 的第三人称视角；玩家手臂、武器、ADS、准星和受伤方向隐藏；只有一个有效 Base Camera 和一个启用的 AudioListener；玩家不复活。

实际结果：未执行。  
证据/缺陷：待填写。

### A01-SPEC-002：手动循环目标与敌方隔离

步骤：

1. 在至少两名己方 Bot 存活时按右方向键至少六次。
2. 记录每次目标的阵营和槽位。
3. 按左方向键至少六次。
4. 再次记录顺序。

预期结果：目标只来自进攻方存活成员；按 0-5 稳定槽位循环并正确首尾回绕；不会选择玩家自己或任何防守方。

实际结果：未执行。  
证据/缺陷：待填写。

### A01-SPEC-003：目标死亡自动切换

步骤：

1. 记录当前观战目标与槽位。
2. 让该目标被淘汰。
3. 观察自动选择的新目标和相机切换轨迹。
4. 重复一次，直到只剩一名己方成员。

预期结果：目标死亡后自动选择下一合法存活槽位；新相机姿态直接播种，不从旧目标位置横穿关卡；仅剩一名时保持该目标。

实际结果：未执行。  
证据/缺陷：待填写。

### A01-SPEC-004：相机 Wall Layer 防穿墙

步骤：

1. 观战一名靠近实心墙的己方 Bot。
2. 让目标背对、侧对墙体移动，覆盖相机期望位置被墙阻挡的情况。
3. 观察相机与目标距离变化。
4. 让非 Wall Layer 的角色或触发器进入相机路径。

预期结果：Wall Layer 阻挡时相机平滑收近且不穿墙；离墙后恢复期望距离；角色和普通触发器不会错误推动相机。

实际结果：未执行。  
证据/缺陷：待填写。

### A01-SPEC-005：设置菜单与死亡输入阻塞

步骤：

1. 在观战状态打开设置菜单。
2. 尝试移动、转向、开火、换弹、切枪、使用技能和战术装备。
3. 关闭设置菜单。
4. 再次尝试上述全部玩法输入。

预期结果：菜单开启时由 UI 占用输入；菜单关闭后玩家仍因死亡保持玩法输入禁用，不会生成射击、装备实体、技能或移动。

实际结果：未执行。  
证据/缺陷：待填写。

### A01-SPEC-006：观战改键持久化

步骤：

1. 在设置菜单中将“下一观战目标”和“上一观战目标”改为两个未冲突按键。
2. 回到观战并确认新按键生效，旧按键不再触发。
3. 退出 Play Mode，再次进入 Play Mode。
4. 重新进入观战并验证新按键。
5. 测试完成后使用设置菜单恢复默认键位。

预期结果：改键立即生效并跨 Play Mode 持久化；恢复默认后左右方向键重新生效；不存在双重绑定。

实际结果：未执行。  
证据/缺陷：待填写。

### A01-SPEC-007：结算冻结与新回合恢复

步骤：

1. 在玩家死亡且正在观战时让进攻方全灭。
2. 观察回合结算期间的相机。
3. 等待进入下一准备阶段。
4. 检查玩家 Camera、AudioListener、HUD、生命、武器和输入。
5. 等待下一战斗阶段并执行移动、射击和切枪。

预期结果：全队淘汰和结算期间冻结最后观战画面；准备阶段恢复第一人称相机和唯一 AudioListener；HUD、生命、弹药、装备次数与输入状态正确；准备阶段不允许战斗输入，开局后恢复。

实际结果：未执行。  
证据/缺陷：待填写。

### A01-STAB-001：连续三回合状态稳定性

步骤：

1. 连续完成三个“玩家死亡—观战—结算—恢复”循环。
2. 每回合记录双方存活数、比分、当前相机和 AudioListener 数量。
3. 第三回合检查 Hierarchy 中是否累积 Spectator Camera 或其他运行时辅助对象。
4. 检查 Console 的重复订阅、空引用、NavMesh、相机和 AudioListener 信息。
5. 使用 Profiler 的 Memory 简要比较第一回合开始与第三回合结束的相关对象数量；本卡只检查明显增长，不作为正式性能签字。

预期结果：每回合只结算一次；无重复相机、AudioListener、观战对象、订阅回调或明显运行时资源增长；下一回合均可继续。

实际结果：未执行。  
证据/缺陷：待填写。

### A01-BUILD-001：Validator Error 阻断构建

步骤：

1. 临时制造 `PSV-BUILD-001` 错误并记录恢复值。
2. 执行 `Project Sun > Build > Windows Development Build`。
3. 检查构建是否在选择/写入产物前停止，并显示可理解的错误。
4. 恢复 Build Settings，执行 Validator 确认 PASS。

预期结果：Error 级结果阻止构建；不会留下可误认为成功的可执行文件；恢复后工程状态与测试前一致。

实际结果：未执行。  
证据/缺陷：待填写。

### A01-BUILD-002：输出目录保护

步骤：

1. 执行项目构建菜单。
2. 分别尝试选择 `Assets`、`Library`、`Temp` 和一个第三方资源目录。
3. 检查工具拒绝这些路径。
4. 选择项目 `Builds` 下明确的测试目录或项目外目录。

预期结果：受 Unity 管理或第三方目录均被拒绝，合法目录可继续；取消选择不会改变工程配置。

实际结果：未执行。  
证据/缺陷：待填写。

### A01-BUILD-003：Windows Development Build

前置条件：Validator 为 PASS，关闭无关程序并确认输出目录有足够空间。

步骤：

1. 执行 `Project Sun > Build > Windows Development Build`。
2. 选择合法且本次专用的空输出目录。
3. 等待 Unity 完成构建，不中途修改场景或质量设置。
4. 检查 `ProjectSun.exe`、Data 目录和 `ProjectSun-BuildReport.md`。
5. 打开报告，核对 Unity 版本、时间、提交标识、启用场景、Validator 摘要、输出路径和构建结果。

预期结果：构建成功；报告字段完整且与实际环境一致；构建过程没有改变原场景、Build Settings 或质量档。

实际结果：未执行。  
证据/缺陷：待填写。

### A01-BUILD-004：构建产物完整回合

步骤：

1. 关闭 Unity Play Mode，从 `ProjectSun.exe` 启动产物。
2. 确认直接进入 CombatSlice，而非第三方展示场景或 WeaponLab。
3. 在准备阶段选择主武器、副武器、一个兼容配件和战术装备。
4. 等待战斗开始，执行移动、射击、ADS、换弹、切枪和使用战术装备。
5. 完成至少一个回合，并确认结算后进入下一准备阶段。
6. 退出应用，保存 `Player.log`。

预期结果：无需编辑器菜单即可完成完整回合；场景中没有重复玩家、HUD、相机、AudioListener 或 RoundManager；回合重置正常；应用可正常退出。

实际结果：未执行。  
证据/缺陷：待填写。

### A01-LOG-001：Console、Player Log 与报告审计

步骤：

1. 汇总本轮 Unity Console、Test Runner 结果、`Player.log` 和构建报告。
2. 按项目代码、第三方代码和环境信息分类每条 Error/Warning。
3. 核对第三方 `CharacterKinematics.cs` 的两条 CS0649 是否为已登记既有 warning。
4. 将所有其他 Error/Warning 关联到缺陷编号；修复后重新执行受影响用例。

预期结果：项目新增 Error 为 0，项目新增 Warning 为 0；已知第三方 warning 有明确来源且没有数量变化；所有报告之间的版本和场景信息一致。

实际结果：部分执行。EditMode 测试后的 Unity Console 截图显示 0 Log、0 Warning、0 Error；尚未生成并审计 Windows Development Build 的 `Player.log` 与构建报告，因此本用例暂不能判定为通过。  
证据/缺陷：Console 证据复用 `Evidence/PS-VS-A01/EditMode/A01-TEST-001_Console_Zero_20260816.png`；Player Log 与构建报告待补充。

## 8. Validator 编号速查

| 范围 | 编号 | 阻断条件 |
| --- | --- | --- |
| 环境 | `PSV-ENV-001` | Unity 不是 `2022.3.51f1c1` |
| Layer | `PSV-LAYER-008/009/010` | Wall、First Person View、Character 缺失或索引不符 |
| 输入 | `PSV-INPUT-001/002/003` | Input System 设置或运行时 `FpsInput` Action 定义不可用 |
| 构建场景 | `PSV-BUILD-001/002` | CombatSlice 非首场景，或启用第三方展示场景 |
| 关键资产 | `PSV-ASSET-001` 至 `004` | 场景、Player Prefab、主副武器/战术装备目录不完整 |
| 场景 | `PSV-SCENE-*` | Missing Script 或玩家、相机、AudioListener、HUD、RoundManager、Installer 不唯一 |
| 阵营槽位 | `PSV-SPAWN-*`、`PSV-TEAM-*` | 出生组或双方 0-5 成员槽位缺失、重复、空引用或越界 |
| URP | `PSV-URP-001` | 当前 Renderer Data 未安装 `ScopePeripheralRenderFeature` |

`PSV-BUILD-003` 是 Warning：WeaponLab 未启用时记录但不阻断构建。

## 9. 缺陷记录

| 缺陷 ID | 关联用例 | 严重级别 | 复现摘要 | 状态 | 回归证据 |
| --- | --- | --- | --- | --- | --- |
| 待填写 |  |  |  |  |  |

严重级别：

- Blocker：无法启动、无法构建、数据损坏或无法继续测试。
- Critical：核心回合、死亡观战或构建门禁错误，无安全绕过方式。
- Major：主要功能错误但存在明确绕过方式。
- Minor：不阻断流程的表现、文案或低风险问题。

Blocker/Critical 必须修复并全量回归；Major 必须由任务负责人和 QA 明确决定修复或延期；Minor 可登记延期，但不得影响本卡完成定义。

## 10. 最终放行规则

只有同时满足以下条件，最终结论才能从“待验收”改为“已通过”：

- 总表所有必需用例均为通过，没有阻塞或不适用的核心用例。
- EditMode 全量测试通过。
- Validator 正常项目 PASS，三类故障注入均能发现问题并安全恢复。
- 死亡观战七项功能测试和连续三回合稳定性通过。
- Windows Development Build 成功，产物从 CombatSlice 启动并完成一个回合。
- Unity Console 和 Player Log 没有项目新增 Error/Warning。
- 没有未关闭的 Blocker、Critical 或未获批准延期的 Major 缺陷。
- 任务卡、工程开发计划和本报告状态保持一致。

## 11. 最终签字

| 角色 | 姓名 | 日期 | 结论 | 证据根目录/备注 |
| --- | --- | --- | --- | --- |
| Gameplay/Tools | 待填写 | 待填写 | 待填写 |  |
| QA/关卡 | 待填写 | 待填写 | 待填写 |  |

任何签字都只对第 4 节记录的具体版本和环境有效；代码、场景、Prefab、Build Settings、URP 或输入配置变化后，应按影响范围重新执行对应测试。
