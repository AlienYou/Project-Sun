# 16 ADS 校准工作台

## 目的

`ADS Calibration Workbench` 用于武器和瞄具的一次性编辑器校准。它不需要进入 Play，也不需要持续按住右键；预览使用与运行时完全相同的 ADS 对齐计算。

## 使用步骤

1. 若尚未生成 AR-01 Profile，先执行 `Project Sun > Integrate Low Poly Shooter Arms (AR-01)`。
2. 在 Project 中打开 `Assets/_ProjectSun/Prefabs/Characters/Player.prefab`，展开 `Player Camera`，选中 `FP Viewmodel - LPSP AR-01`。
3. 执行 `Project Sun > Tools > ADS Calibration Workbench`，或在窗口中点击 `Use Selected Viewmodel`。
4. 点击 `Start Persistent ADS Preview`。工作台会临时保持 ADS 姿势；使用 Scene 视图查看模型，不进入 Play。
   同时会显示 `Runtime ADS Camera Preview`：这是隔离的玩家右键相机画面，已应用 ADS FOV、视模型姿势和屏幕中心准线，推荐以该面板进行微调。
5. 选中 `Player Camera` 后按 `Ctrl+Shift+F` 对齐 Scene 视图。蓝线表示相机瞄准线；黄色空心环和十字表示 Profile 管理的虚拟瞄具中心；紫色圆靶位于 Profile 的 `Calibration Zero Distance`（默认 25 米）；黄色虚线表示运行时从枪口到该靶心的零点路径。
6. 使用 `Visual Sight Assist` 的 `Weapon Left/Right/Up/Down/Forward/Back` 按钮，以毫米级移动枪械视觉模型，直至模型上真实的红点/机械瞄具中心压住黄色空心标记。该操作只写入 Profile 的 `Visual Reference Offset`，不会修改原始第三方预制体。
7. `Gameplay Muzzle Path: LOCKED TO CAMERA TARGET` 是正常的只读状态，不需要调整。若第一项显示 `OFF`，点击 `Auto Centre Sight Reference`；当真实瞄具中心压住黄色球后，点击 `Mark Visual Sight Placement Reviewed`。显示 `CALIBRATION READY` 后点击 `Save Profile`，再点击 `Stop & Restore Hip Pose`。只保存 Profile，临时预览姿势会自动还原。

## 校准规则

- 先把 `Aim Anchor` 放到机械瞄具后照门/光学瞄具的真实观察点。
- `Sight Distance` 用于眼距，优先调整 Z；X/Y 和旋转偏移只用于极小修正。
- 工作台会明确显示三个状态：`Sight reference → camera centre` 必须为 `PASS`（不超过 2 px，以 1080p/16:9 换算）；黄色球在真实瞄具中心的位置必须由美术确认并勾选 `Visual Sight Placement Reviewed`；运行时枪口路径固定收敛到相机瞄准目标。三项完成后显示 `CALIBRATION READY`。
- 运行时命中权威仍是玩家相机瞄准线；瞄具视觉必须与它一致，而不是让本地模型决定伤害。
- 每个“武器 + 瞄具”组合应有独立的 `WeaponAdsProfile`。安装瞄具后，运行时将切换到该瞄具的 Profile。

## 注意

预览激活时不要保存 `Player.prefab`。先停止预览以恢复原始持枪姿势，再保存 Player 或提交变更。
