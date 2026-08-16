using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectSun.FPS.Editor
{
    /// <summary>以只读列表展示项目预检结果，并允许直接定位对应场景或资产。</summary>
    public sealed class ProjectValidatorWindow : EditorWindow
    {
        private IReadOnlyList<ProjectValidationResult> results;
        private Vector2 scroll;

        [MenuItem("Project Sun/Validation/Validate Project", priority = 100)]
        private static void OpenAndValidate()
        {
            ProjectValidatorWindow window = GetWindow<ProjectValidatorWindow>("Project Validator");
            window.minSize = new Vector2(720f, 420f);
            window.RunValidation();
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("重新执行只读校验", GUILayout.Width(160f))) RunValidation();
                GUILayout.FlexibleSpace();
                DrawSummary();
            }
            EditorGUILayout.Space(6f);

            if (results == null)
            {
                EditorGUILayout.HelpBox("点击校验按钮开始检查。", MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (ProjectValidationResult result in results)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label(result.Severity.ToString().ToUpperInvariant(), GUILayout.Width(62f));
                    GUILayout.Label(result.Id, GUILayout.Width(118f));
                    GUILayout.Label(result.Message, EditorStyles.wordWrappedLabel, GUILayout.ExpandWidth(true));
                    using (new EditorGUI.DisabledScope(result.Context == null))
                    {
                        if (GUILayout.Button("定位", GUILayout.Width(52f)))
                        {
                            EditorGUIUtility.PingObject(result.Context);
                            Selection.activeObject = result.Context;
                        }
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void RunValidation()
        {
            results = ProjectValidator.ValidateProject();
            foreach (ProjectValidationResult result in results.Where(item =>
                         item.Severity != ProjectValidationSeverity.Pass))
            {
                string line = $"[{result.Id}] {result.Message}";
                if (result.Severity == ProjectValidationSeverity.Error)
                    Debug.LogError(line, result.Context);
                else
                    Debug.LogWarning(line, result.Context);
            }
            Repaint();
        }

        private void DrawSummary()
        {
            if (results == null) return;
            int passed = results.Count(result => result.Severity == ProjectValidationSeverity.Pass);
            int warnings = results.Count(result => result.Severity == ProjectValidationSeverity.Warning);
            int errors = results.Count(result => result.Severity == ProjectValidationSeverity.Error);
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = errors == 0 ? new Color(0.15f, 0.6f, 0.2f) : new Color(0.85f, 0.2f, 0.15f) }
            };
            GUILayout.Label(errors == 0
                ? $"PASS · {passed} 通过 · {warnings} 警告"
                : $"FAIL · {errors} 错误 · {warnings} 警告 · {passed} 通过", style);
        }
    }
}
