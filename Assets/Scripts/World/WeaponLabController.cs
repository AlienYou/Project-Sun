using System.Collections;
using System.Collections.Generic;
using ProjectSun.FPS.Bootstrap;
using ProjectSun.FPS.Input;
using ProjectSun.FPS.Player;
using ProjectSun.FPS.Presentation;
using ProjectSun.FPS.Weapons;
using UnityEngine;

namespace ProjectSun.FPS.World
{
    /// <summary>
    /// 负责 WeaponLab 场景专用的重置和倍率镜压力测试。测试始终复用正式玩家、武器、切枪与渲染组件，
    /// 不创建第二套战斗模拟；独占测试输入仅在 Editor 或 Development Build 中可启用。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class WeaponLabController : MonoBehaviour
    {
        [Header("WeaponLab References")]
        [Tooltip("WeaponLab 中的正式玩家安装器；用于访问同一套玩家、武器、背包和生命组件。")]
        [SerializeField] private FpsPlayerInstaller playerInstaller;
        [Tooltip("需要由 F6 恢复的训练靶；空数组表示只重置玩家和武器。")]
        [SerializeField] private TargetDummy[] targets = System.Array.Empty<TargetDummy>();

        [Header("Test Controls")]
        [Tooltip("立即恢复玩家、武器和全部训练靶的测试快捷键；压力测试运行时按下会先安全停止测试。")]
        [SerializeField] private KeyCode resetLabKey = KeyCode.F6;
        [Tooltip("启动或停止倍率镜压力测试的快捷键；测试会切换画质、快速 ADS、连续开火并切换主副武器。")]
        [SerializeField] private KeyCode scopeValidationKey = KeyCode.F7;
        [Tooltip("进入 WeaponLab 时是否自动恢复玩家、武器和训练靶；默认启用。")]
        [SerializeField] private bool resetOnStart = true;

        [Header("Scope Validation")]
        [Tooltip("每个 Unity 画质档位执行的完整循环数；有效范围 1～5，默认 1。一次循环包含快速 ADS、后坐和切枪。")]
        [SerializeField, Range(1, 5)] private int validationCyclesPerQuality = 1;
        [Tooltip("快速 ADS 每次保持按住的时间，单位为秒；有效范围 0.08～0.5，默认 0.16。")]
        [SerializeField, Range(0.08f, 0.5f)] private float rapidAdsHoldSeconds = 0.16f;
        [Tooltip("快速 ADS 释放后的等待时间，单位为秒；有效范围 0.05～0.5，默认 0.12。")]
        [SerializeField, Range(0.05f, 0.5f)] private float rapidAdsReleaseSeconds = 0.12f;
        [Tooltip("ADS 状态连续开火的压力时长，单位为秒；有效范围 0.2～2，默认 0.55。")]
        [SerializeField, Range(0.2f, 2f)] private float recoilBurstSeconds = 0.55f;
        [Tooltip("等待一次收枪、拔枪状态机完成的最长时间，单位为秒；有效范围 0.5～3，超时会记录失败并继续下一项。")]
        [SerializeField, Range(0.5f, 3f)] private float switchTimeoutSeconds = 1.5f;

        private readonly List<string> validationFailures = new List<string>();
        private FpsInput playerInput;
        private HitscanWeapon weapon;
        private WeaponInventoryController inventory;
        private ScopeSightRenderer scopeRenderer;
        private Coroutine validationRoutine;
        private int originalQualityLevel = -1;
        private int validationShotCount;
        private int completedValidationCycles;
        private int totalValidationCycles;
        private string validationStatus = "NOT RUN";
        private string validationSummary = "Press F7 to run scope soak";

        public KeyCode ResetLabKey => resetLabKey;
        public KeyCode ScopeValidationKey => scopeValidationKey;
        public bool ValidationRunning => validationRoutine != null;
        public float ValidationProgress => totalValidationCycles > 0
            ? Mathf.Clamp01(completedValidationCycles / (float)totalValidationCycles)
            : 0f;
        public string ValidationStatus => validationStatus;
        public string ValidationSummary => validationSummary;
        public string CurrentQualityName
        {
            get
            {
                string[] names = QualitySettings.names;
                int level = QualitySettings.GetQualityLevel();
                return names != null && level >= 0 && level < names.Length ? names[level] : $"LEVEL {level}";
            }
        }

        /// <summary>绑定 WeaponLab 使用的正式玩家和训练靶，并刷新压力测试所需引用。</summary>
        /// <param name="player">场景中的正式玩家安装器；为空时重置和压力测试都会显示不可用。</param>
        /// <param name="trainingTargets">需要随 F6 恢复的训练靶数组；允许为空，空值按空数组处理。</param>
        public void Configure(FpsPlayerInstaller player, TargetDummy[] trainingTargets)
        {
            UnsubscribeWeapon();
            playerInstaller = player;
            targets = trainingTargets ?? System.Array.Empty<TargetDummy>();
            ResolveValidationReferences();
            SubscribeWeapon();
        }

        private void Start()
        {
            ResolveValidationReferences();
            SubscribeWeapon();
            if (resetOnStart) ResetLab();
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(scopeValidationKey))
            {
                if (ValidationRunning) StopScopeValidation();
                else StartScopeValidation();
            }

            if (!UnityEngine.Input.GetKeyDown(resetLabKey)) return;
            if (ValidationRunning) StopScopeValidation();
            ResetLab();
        }

        private void OnDisable()
        {
            if (ValidationRunning) StopScopeValidation();
        }

        private void OnDestroy()
        {
            RestoreValidationEnvironment();
            UnsubscribeWeapon();
        }

        /// <summary>恢复 WeaponLab 的玩家、武器、生命和训练靶；不创建替代玩法状态。</summary>
        public void ResetLab()
        {
            if (playerInstaller != null)
            {
                WeaponInventoryController activeInventory = playerInstaller.WeaponInventory;
                if (activeInventory != null) activeInventory.ResetForRound();

                PlayerRespawnController respawn = playerInstaller.GetComponent<PlayerRespawnController>();
                if (respawn != null)
                {
                    respawn.SetRoundRespawnsEnabled(true);
                    respawn.ResetForRound();
                }
                else if (playerInstaller.Health != null)
                {
                    playerInstaller.Health.ResetHealth();
                }
            }

            foreach (TargetDummy target in targets)
                if (target != null) target.ResetTarget();
        }

        /// <summary>
        /// 启动一次可中止的倍率镜压力测试。测试会保存当前画质并在结束时恢复；缺少玩家、输入或武器时不启动。
        /// </summary>
        public void StartScopeValidation()
        {
            if (ValidationRunning) return;
            ResolveValidationReferences();
            if (playerInput == null || weapon == null || inventory == null)
            {
                validationStatus = "BLOCKED";
                validationSummary = "Missing player input, weapon or inventory";
                return;
            }
            if (!playerInput.BeginTestInputOverride())
            {
                validationStatus = "BLOCKED";
                validationSummary = "Test input requires Editor or Development Build";
                return;
            }

            originalQualityLevel = QualitySettings.GetQualityLevel();
            validationFailures.Clear();
            validationShotCount = 0;
            completedValidationCycles = 0;
            int qualityCount = Mathf.Max(1, QualitySettings.names != null ? QualitySettings.names.Length : 0);
            totalValidationCycles = qualityCount * Mathf.Max(1, validationCyclesPerQuality);
            validationStatus = "STARTING";
            validationSummary = "Exclusive test input active";
            ResetLab();
            validationRoutine = StartCoroutine(RunScopeValidation(qualityCount));
        }

        /// <summary>中止正在运行的压力测试，释放测试输入并恢复启动前的 Unity 画质档位。</summary>
        public void StopScopeValidation()
        {
            if (validationRoutine != null) StopCoroutine(validationRoutine);
            validationRoutine = null;
            RestoreValidationEnvironment();
            validationStatus = "CANCELLED";
            validationSummary = $"Cancelled at {ValidationProgress:P0}";
        }

        /// <summary>按画质档位执行快速 ADS、连续开火和主副武器切换。</summary>
        /// <param name="qualityCount">本次启动时检测到的 Unity 画质档位数量；最小按 1 处理。</param>
        private IEnumerator RunScopeValidation(int qualityCount)
        {
            yield return WaitRealtime(0.2f);
            for (int quality = 0; quality < qualityCount; quality++)
            {
                // 切换画质会同步重建当前 Renderer 的采样策略，因此只在每个档位开始时执行一次，
                // 不在 ADS 或渲染回调等高频路径中修改全局设置。
                if (QualitySettings.names != null && QualitySettings.names.Length > 0)
                    QualitySettings.SetQualityLevel(quality, true);
                ResetLab();
                validationStatus = $"QUALITY {CurrentQualityName.ToUpperInvariant()}";
                yield return WaitRealtime(0.25f);

                for (int cycle = 0; cycle < Mathf.Max(1, validationCyclesPerQuality); cycle++)
                {
                    yield return RunRapidAdsPhase(cycle);
                    yield return RunRecoilPhase(cycle);
                    yield return RunSwitchPhase(cycle);
                    completedValidationCycles++;
                }
            }

            FinishScopeValidation();
        }

        /// <summary>执行一次快速开镜和退镜，覆盖 RT 创建、渐入、渐出与释放路径。</summary>
        /// <param name="cycle">当前画质档位内从 0 开始的循环索引，仅用于遥测显示。</param>
        private IEnumerator RunRapidAdsPhase(int cycle)
        {
            validationStatus = $"RAPID ADS {cycle + 1}/{validationCyclesPerQuality}";
            playerInput.SetTestBindingHeld(FpsBinding.Aim, true);
            yield return WaitRealtime(rapidAdsHoldSeconds);
            playerInput.SetTestBindingHeld(FpsBinding.Aim, false);
            yield return WaitRealtime(rapidAdsReleaseSeconds);
        }

        /// <summary>在稳定 ADS 下持续开火，并验收镜片 Shader、RT 和当前镜外画质 Pass。</summary>
        /// <param name="cycle">当前画质档位内从 0 开始的循环索引，仅用于遥测显示。</param>
        private IEnumerator RunRecoilPhase(int cycle)
        {
            validationStatus = $"ADS RECOIL {cycle + 1}/{validationCyclesPerQuality}";
            int shotsBeforeBurst = validationShotCount;
            playerInput.SetTestBindingHeld(FpsBinding.Aim, true);
            yield return WaitRealtime(Mathf.Max(0.2f, rapidAdsHoldSeconds));
            playerInput.SetTestBindingHeld(FpsBinding.Fire, true);
            yield return WaitRealtime(recoilBurstSeconds);
            playerInput.SetTestBindingHeld(FpsBinding.Fire, false);
            ValidateSettledScope();
            if (validationShotCount <= shotsBeforeBurst)
                RecordValidationFailure("NO SHOTS DURING RECOIL PHASE");
            playerInput.SetTestBindingHeld(FpsBinding.Aim, false);
            yield return WaitRealtime(rapidAdsReleaseSeconds);
        }

        /// <summary>调用正式背包公开入口完成副武器和主武器往返切换；没有副武器时记录跳过而非失败。</summary>
        /// <param name="cycle">当前画质档位内从 0 开始的循环索引，仅用于遥测显示。</param>
        private IEnumerator RunSwitchPhase(int cycle)
        {
            if (!inventory.HasSecondary)
            {
                validationStatus = "SWITCH SKIPPED (NO SECONDARY)";
                yield return null;
                yield break;
            }

            validationStatus = $"SWITCH SECONDARY {cycle + 1}/{validationCyclesPerQuality}";
            bool secondaryRequested = inventory.ActiveSlot == WeaponInventorySlot.Secondary || inventory.TrySelectSecondary();
            if (!secondaryRequested)
                RecordValidationFailure("SECONDARY SWITCH REJECTED");
            yield return WaitForInventorySlot(WeaponInventorySlot.Secondary);

            validationStatus = $"SWITCH PRIMARY {cycle + 1}/{validationCyclesPerQuality}";
            bool primaryRequested = inventory.ActiveSlot == WeaponInventorySlot.Primary || inventory.TrySelectPrimary();
            if (!primaryRequested)
                RecordValidationFailure("PRIMARY SWITCH REJECTED");
            yield return WaitForInventorySlot(WeaponInventorySlot.Primary);
        }

        /// <summary>等待正式切枪协程进入目标槽位，超时后记录失败并继续后续画质档位。</summary>
        /// <param name="targetSlot">期望到达的武器槽位；只允许 Primary 或 Secondary。</param>
        private IEnumerator WaitForInventorySlot(WeaponInventorySlot targetSlot)
        {
            float deadline = Time.realtimeSinceStartup + switchTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline &&
                   (inventory.IsSwitching || inventory.ActiveSlot != targetSlot))
                yield return null;
            if (inventory.ActiveSlot != targetSlot)
                RecordValidationFailure($"SWITCH TIMEOUT: {targetSlot}");
            yield return WaitRealtime(0.08f);
        }

        private void ValidateSettledScope()
        {
            if (scopeRenderer == null && weapon != null)
                scopeRenderer = weapon.GetComponent<ScopeSightRenderer>();
            if (scopeRenderer == null)
            {
                RecordValidationFailure("NO SCOPE RENDERER");
                return;
            }
            if (!scopeRenderer.IsActive) RecordValidationFailure("SCOPE NOT ACTIVE");
            if (!scopeRenderer.UsesIntegratedLensShader) RecordValidationFailure("LENS SHADER FALLBACK");
            if (scopeRenderer.ScopeTexture == null) RecordValidationFailure("NO SCOPE RENDER TEXTURE");
            if (scopeRenderer.DiagnosticStatus != "READY")
                RecordValidationFailure($"SCOPE STATUS {scopeRenderer.DiagnosticStatus}");

            string expectedPeripheral = ResolveExpectedPeripheralQuality();
            if (scopeRenderer.PeripheralDiagnosticStatus != expectedPeripheral)
                RecordValidationFailure($"OUTSIDE {scopeRenderer.PeripheralDiagnosticStatus}, EXPECTED {expectedPeripheral}");
        }

        private string ResolveExpectedPeripheralQuality()
        {
            int qualityLevel = QualitySettings.GetQualityLevel();
            if (qualityLevel <= 1) return "DIM ONLY";
            if (qualityLevel <= 3) return "BLUR 4 TAP";
            return "BLUR 8 TAP";
        }

        /// <summary>记录同一画质档位下的唯一失败原因，避免每帧重复字符串和无界列表增长。</summary>
        /// <param name="reason">稳定的失败代码或诊断状态；为空时忽略。</param>
        private void RecordValidationFailure(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) return;
            string failure = $"{CurrentQualityName}: {reason}";
            if (!validationFailures.Contains(failure)) validationFailures.Add(failure);
        }

        private void FinishScopeValidation()
        {
            validationRoutine = null;
            RestoreValidationEnvironment();
            if (validationFailures.Count == 0)
            {
                validationStatus = "PASS";
                validationSummary = $"{completedValidationCycles} cycles, {validationShotCount} shots";
                Debug.Log($"WeaponLab scope validation PASS: {validationSummary}", this);
                return;
            }

            validationStatus = $"FAIL ({validationFailures.Count})";
            validationSummary = validationFailures[0];
            Debug.LogWarning($"WeaponLab scope validation {validationStatus}: " +
                string.Join(" | ", validationFailures), this);
        }

        private void RestoreValidationEnvironment()
        {
            if (playerInput != null)
            {
                playerInput.SetTestBindingHeld(FpsBinding.Fire, false);
                playerInput.SetTestBindingHeld(FpsBinding.Aim, false);
                playerInput.EndTestInputOverride();
            }
            if (originalQualityLevel >= 0 && QualitySettings.names != null &&
                originalQualityLevel < QualitySettings.names.Length)
                QualitySettings.SetQualityLevel(originalQualityLevel, true);
            originalQualityLevel = -1;
        }

        private void ResolveValidationReferences()
        {
            playerInput = playerInstaller != null && playerInstaller.Player != null
                ? playerInstaller.Player.Input
                : playerInstaller != null ? playerInstaller.GetComponent<FpsInput>() : null;
            weapon = playerInstaller != null ? playerInstaller.Weapon : null;
            inventory = playerInstaller != null ? playerInstaller.WeaponInventory : null;
            scopeRenderer = weapon != null ? weapon.GetComponent<ScopeSightRenderer>() : null;
        }

        private void SubscribeWeapon()
        {
            if (weapon == null) return;
            weapon.Fired -= OnWeaponFired;
            weapon.Fired += OnWeaponFired;
        }

        private void UnsubscribeWeapon()
        {
            if (weapon != null) weapon.Fired -= OnWeaponFired;
        }

        private void OnWeaponFired()
        {
            if (ValidationRunning) validationShotCount++;
        }

        /// <summary>使用不受 Time.timeScale 影响的等待，确保暂停或调试慢速不会改变验收节拍。</summary>
        /// <param name="seconds">等待时长，单位为秒；小于等于 0 时至少让出一帧。</param>
        private static IEnumerator WaitRealtime(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0f, seconds);
            do
            {
                yield return null;
            } while (Time.realtimeSinceStartup < deadline);
        }
    }
}
