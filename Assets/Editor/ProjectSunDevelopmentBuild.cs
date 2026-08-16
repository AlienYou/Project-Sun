using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ProjectSun.FPS.Editor
{
    /// <summary>统一 Windows Development Build 入口，在构建前执行只读门禁并保存可审计报告。</summary>
    public static class ProjectSunDevelopmentBuild
    {
        [MenuItem("Project Sun/Build/Windows Development Build", priority = 200)]
        private static void BuildWindowsDevelopment()
        {
            IReadOnlyList<ProjectValidationResult> validation = ProjectValidator.ValidateProject();
            if (!ProjectValidator.CanBuild(validation))
            {
                LogValidation(validation);
                EditorUtility.DisplayDialog("Project Sun 构建已阻止",
                    "Project Validator 存在 Error。请通过 Project Sun > Validation > Validate Project 定位问题。", "确定");
                return;
            }

            string defaultBuildRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Builds"));
            string outputRoot = EditorUtility.SaveFolderPanel("选择 Project Sun 开发构建输出目录",
                defaultBuildRoot, string.Empty);
            if (string.IsNullOrEmpty(outputRoot)) return;
            if (!TryValidateOutputDirectory(outputRoot, out string failureReason))
            {
                EditorUtility.DisplayDialog("输出目录无效", failureReason, "确定");
                return;
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string buildDirectory = Path.Combine(outputRoot, $"ProjectSun-Windows-Development-{timestamp}");
            Directory.CreateDirectory(buildDirectory);
            string executablePath = Path.Combine(buildDirectory, "ProjectSun.exe");
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = executablePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            };

            BuildReport buildReport = null;
            Exception buildException = null;
            try
            {
                buildReport = BuildPipeline.BuildPlayer(options);
            }
            catch (Exception exception)
            {
                buildException = exception;
                Debug.LogException(exception);
            }
            finally
            {
                // 报告与构建产物放在同一明确目录；构建失败也保留门禁摘要和失败原因。
                WriteBuildReport(Path.Combine(buildDirectory, "ProjectSun-BuildReport.md"), validation,
                    options.scenes, buildReport, buildException, buildDirectory);
            }

            if (buildException != null || buildReport == null || buildReport.summary.result != BuildResult.Succeeded)
            {
                EditorUtility.DisplayDialog("Project Sun 构建失败",
                    $"报告已写入：{Path.Combine(buildDirectory, "ProjectSun-BuildReport.md")}", "确定");
                return;
            }
            EditorUtility.RevealInFinder(executablePath);
        }

        /// <summary>检查输出目录不会进入 Unity 受控、第三方或导入目录。</summary>
        /// <param name="outputDirectory">用户选择的现有或待创建目录绝对路径。</param>
        /// <param name="failureReason">失败时返回中文原因；成功时为空。</param>
        /// <returns>目录可安全用于本地构建时返回 true。</returns>
        public static bool TryValidateOutputDirectory(string outputDirectory, out string failureReason)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                failureReason = "必须显式选择输出目录。";
                return false;
            }
            string projectRoot = Path.GetFullPath(Directory.GetCurrentDirectory()).TrimEnd(Path.DirectorySeparatorChar);
            string candidate = Path.GetFullPath(outputDirectory).TrimEnd(Path.DirectorySeparatorChar);
            string[] forbidden =
            {
                Path.Combine(projectRoot, "Assets"), Path.Combine(projectRoot, "Library"),
                Path.Combine(projectRoot, "Temp"), Path.Combine(projectRoot, "Packages"),
                Path.Combine(projectRoot, "Logs"), Path.Combine(projectRoot, "obj")
            };
            if (forbidden.Any(path => IsSameOrChild(candidate, path)))
            {
                failureReason = "构建目录不得位于 Assets、Library、Temp、Packages、Logs 或 obj 中。";
                return false;
            }
            failureReason = string.Empty;
            return true;
        }

        private static bool IsSameOrChild(string candidate, string parent)
        {
            string normalizedParent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar);
            return candidate.Equals(normalizedParent, StringComparison.OrdinalIgnoreCase) ||
                   candidate.StartsWith(normalizedParent + Path.DirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static void LogValidation(IEnumerable<ProjectValidationResult> validation)
        {
            foreach (ProjectValidationResult result in validation.Where(item =>
                         item.Severity != ProjectValidationSeverity.Pass))
            {
                string line = $"[{result.Id}] {result.Message}";
                if (result.Severity == ProjectValidationSeverity.Error) Debug.LogError(line, result.Context);
                else Debug.LogWarning(line, result.Context);
            }
        }

        private static void WriteBuildReport(string reportPath,
            IReadOnlyList<ProjectValidationResult> validation, IReadOnlyList<string> scenes,
            BuildReport report, Exception exception, string outputDirectory)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# Project Sun Windows Development Build 报告");
            builder.AppendLine();
            builder.AppendLine($"- 时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
            builder.AppendLine($"- Unity：{Application.unityVersion}");
            builder.AppendLine($"- 提交：{TryGetCommitIdentifier()}");
            builder.AppendLine($"- 输出目录：`{outputDirectory}`");
            builder.AppendLine($"- 构建结果：{(exception != null ? "Exception" : report?.summary.result.ToString() ?? "NotStarted")}");
            if (report != null)
            {
                builder.AppendLine($"- 错误/警告：{report.summary.totalErrors}/{report.summary.totalWarnings}");
                builder.AppendLine($"- 总大小：{report.summary.totalSize} bytes");
            }
            if (exception != null) builder.AppendLine($"- 异常：{exception.GetType().Name}: {exception.Message}");
            builder.AppendLine();
            builder.AppendLine("## 启用场景");
            foreach (string scene in scenes) builder.AppendLine($"- `{scene}`");
            builder.AppendLine();
            builder.AppendLine("## Validator 摘要");
            foreach (ProjectValidationResult result in validation)
                builder.AppendLine($"- [{result.Severity}] `{result.Id}` {result.Message}");
            File.WriteAllText(reportPath, builder.ToString(), new UTF8Encoding(false));
        }

        private static string TryGetCommitIdentifier()
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo("git", "rev-parse --short HEAD")
                {
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (Process process = Process.Start(startInfo))
                {
                    if (process == null) return "不可用";
                    string commit = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit(3000);
                    return process.ExitCode == 0 && !string.IsNullOrEmpty(commit) ? commit : "不可用";
                }
            }
            catch
            {
                // Git 不属于构建必要依赖；不可获取提交时在报告中明确降级，不阻断本地开发构建。
                return "不可用";
            }
        }
    }

    /// <summary>覆盖其他 BuildPipeline 调用，确保所有 Development Build 都服从同一 Error 门禁。</summary>
    public sealed class ProjectSunDevelopmentBuildGate : IPreprocessBuildWithReport
    {
        /// <summary>优先于普通构建预处理器执行，确保错误尽早阻断且不产生部分产物。</summary>
        public int callbackOrder => -1000;

        /// <summary>在 Unity 写入构建产物前执行只读项目预检。</summary>
        /// <param name="report">Unity 即将执行的构建报告；非 Development Build 不属于本任务门禁。</param>
        public void OnPreprocessBuild(BuildReport report)
        {
            if ((report.summary.options & BuildOptions.Development) == 0) return;
            IReadOnlyList<ProjectValidationResult> validation = ProjectValidator.ValidateProject();
            if (!ProjectValidator.CanBuild(validation))
                throw new BuildFailedException("Project Validator 存在 Error，Development Build 已阻止。请使用 Project Sun/Validation/Validate Project 定位。");
        }
    }
}
