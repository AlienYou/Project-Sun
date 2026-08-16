using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectSun.FPS.Rounds;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ProjectSun.FPS.Editor.Tests
{
    /// <summary>覆盖 Project Validator 的纯规则、正常工程配置和只读重复执行契约。</summary>
    public sealed class ProjectValidatorTests
    {
        [Test]
        public void BuildScenes_CorrectOrderPassesAndThirdPartyEntryFails()
        {
            IReadOnlyList<ProjectValidationResult> correct = ProjectValidator.ValidateBuildScenePaths(new[]
            {
                ProjectValidator.CombatSlicePath, ProjectValidator.WeaponLabPath
            });
            IReadOnlyList<ProjectValidationResult> incorrect = ProjectValidator.ValidateBuildScenePaths(new[]
            {
                "Assets/Infima Games/Demo.unity", ProjectValidator.CombatSlicePath
            });

            Assert.That(correct.Single(result => result.Id == "PSV-BUILD-001").Severity,
                Is.EqualTo(ProjectValidationSeverity.Pass));
            Assert.That(correct.Single(result => result.Id == "PSV-BUILD-002").Severity,
                Is.EqualTo(ProjectValidationSeverity.Pass));
            Assert.That(incorrect.Single(result => result.Id == "PSV-BUILD-001").Severity,
                Is.EqualTo(ProjectValidationSeverity.Error));
            Assert.That(incorrect.Single(result => result.Id == "PSV-BUILD-002").Severity,
                Is.EqualTo(ProjectValidationSeverity.Error));
        }

        [Test]
        public void RequiredLayers_MissingOrWrongIndexProducesStableError()
        {
            Dictionary<string, int> layers = new Dictionary<string, int>
            {
                { "Wall", 8 }, { "First Person View", 12 }
            };
            IReadOnlyList<ProjectValidationResult> results = ProjectValidator.ValidateRequiredLayers(layers);

            Assert.That(results.Single(result => result.Id == "PSV-LAYER-008").Severity,
                Is.EqualTo(ProjectValidationSeverity.Pass));
            Assert.That(results.Single(result => result.Id == "PSV-LAYER-009").Severity,
                Is.EqualTo(ProjectValidationSeverity.Error));
            Assert.That(results.Single(result => result.Id == "PSV-LAYER-010").Severity,
                Is.EqualTo(ProjectValidationSeverity.Error));
        }

        [Test]
        public void TeamSlots_ReportsMissingDuplicateAndOutOfRange()
        {
            List<ValidationTeamSlot> slots = CompleteSlots();
            slots.RemoveAll(slot => slot.Team == CombatTeam.Defenders && slot.SlotIndex == 5);
            slots.Add(new ValidationTeamSlot("Duplicate", CombatTeam.Attackers, 0));
            slots.Add(new ValidationTeamSlot("Overflow", CombatTeam.Defenders, 6));

            IReadOnlyList<ProjectValidationResult> results = ProjectValidator.ValidateTeamSlots(slots);

            Assert.That(results.Any(result => result.Id == "PSV-TEAM-001" &&
                                              result.Severity == ProjectValidationSeverity.Error), Is.True);
            Assert.That(results.Single(result => result.Id == "PSV-TEAM-010").Severity,
                Is.EqualTo(ProjectValidationSeverity.Error));
            Assert.That(results.Single(result => result.Id == "PSV-TEAM-011").Severity,
                Is.EqualTo(ProjectValidationSeverity.Error));
        }

        [Test]
        public void BuildGate_ErrorBlocksAndWarningDoesNotBlock()
        {
            ProjectValidationResult warning = new ProjectValidationResult("TEST-WARN",
                ProjectValidationSeverity.Warning, "测试警告");
            ProjectValidationResult error = new ProjectValidationResult("TEST-ERROR",
                ProjectValidationSeverity.Error, "测试错误");

            Assert.That(ProjectValidator.CanBuild(new[] { warning }), Is.True);
            Assert.That(ProjectValidator.CanBuild(new[] { warning, error }), Is.False);
            Assert.That(ProjectValidator.CanBuild(null), Is.False);
        }

        [Test]
        public void CurrentProject_PassesAndRepeatedValidationPreservesSceneSetup()
        {
            SceneSetup[] before = EditorSceneManager.GetSceneManagerSetup();
            IReadOnlyList<ProjectValidationResult> first = ProjectValidator.ValidateProject();
            IReadOnlyList<ProjectValidationResult> second = ProjectValidator.ValidateProject();
            SceneSetup[] after = EditorSceneManager.GetSceneManagerSetup();

            Assert.That(first.Where(result => result.Severity == ProjectValidationSeverity.Error), Is.Empty,
                string.Join("\n", first.Where(result => result.Severity == ProjectValidationSeverity.Error)
                    .Select(result => $"[{result.Id}] {result.Message}")));
            Assert.That(second.Select(result => (result.Id, result.Severity, result.Message)),
                Is.EqualTo(first.Select(result => (result.Id, result.Severity, result.Message))));
            Assert.That(after.Select(setup => (setup.path, setup.isLoaded, setup.isActive)),
                Is.EqualTo(before.Select(setup => (setup.path, setup.isLoaded, setup.isActive))));
            Assert.That(EditorUtility.IsDirty(AssetDatabase.LoadAssetAtPath<SceneAsset>(
                ProjectValidator.CombatSlicePath)), Is.False);
        }

        /// <summary>生成双方各 0-5 的完整纯数据槽位集合。</summary>
        /// <returns>调用方可安全修改的十二条记录列表。</returns>
        private static List<ValidationTeamSlot> CompleteSlots()
        {
            List<ValidationTeamSlot> slots = new List<ValidationTeamSlot>();
            foreach (CombatTeam team in new[] { CombatTeam.Attackers, CombatTeam.Defenders })
            {
                for (int slot = 0; slot < ProjectValidator.TeamCapacity; slot++)
                    slots.Add(new ValidationTeamSlot($"{team}-{slot}", team, slot));
            }
            return slots;
        }
    }
}
