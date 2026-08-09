using System;
using System.Linq;
using ProjectSun.FPS.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ProjectSun.FPS.Editor
{
    /// <summary>
    /// 确保项目自有的每个 URP Renderer Data 都安装倍率镜镜外 Pass。
    /// 脚本重载后自动检查一次；新建渲染器也可通过菜单显式补齐。
    /// </summary>
    internal static class ScopePeripheralRendererFeatureInstaller
    {
        private const string RendererSettingsFolder = "Assets/Settings";
        private const string FeatureName = "Project Sun Scope Peripheral";

        [InitializeOnLoadMethod]
        private static void ScheduleAutomaticInstall()
        {
            EditorApplication.delayCall += EnsureInstalledAfterReload;
        }

        [MenuItem("Project Sun/Ensure Scope Peripheral Renderer Feature")]
        private static void EnsureInstalledFromMenu()
        {
            int installed = EnsureInstalled();
            EditorUtility.DisplayDialog("Project Sun",
                installed > 0
                    ? $"已为 {installed} 个 URP Renderer 安装倍率镜镜外渲染功能。"
                    : "所有 Project Sun URP Renderer 均已包含倍率镜镜外渲染功能。",
                "确定");
        }

        private static void EnsureInstalledAfterReload()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            int installed = EnsureInstalled();
            if (installed > 0)
                Debug.Log($"Project Sun installed the scope peripheral renderer feature into {installed} URP renderer asset(s).");
        }

        private static int EnsureInstalled()
        {
            int installed = 0;
            string[] rendererGuids = AssetDatabase.FindAssets("t:UniversalRendererData",
                new[] { RendererSettingsFolder });
            foreach (string guid in rendererGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith(RendererSettingsFolder + "/URP-", StringComparison.OrdinalIgnoreCase) ||
                    !path.EndsWith("-Renderer.asset", StringComparison.OrdinalIgnoreCase)) continue;
                UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (rendererData == null || rendererData.rendererFeatures.Any(feature =>
                        feature is ScopePeripheralRenderFeature)) continue;

                ScopePeripheralRenderFeature feature = ScriptableObject.CreateInstance<ScopePeripheralRenderFeature>();
                feature.name = FeatureName;
                feature.SetActive(true);
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

                SerializedObject serializedRenderer = new SerializedObject(rendererData);
                serializedRenderer.Update();
                SerializedProperty features = serializedRenderer.FindProperty("m_RendererFeatures");
                SerializedProperty featureMap = serializedRenderer.FindProperty("m_RendererFeatureMap");
                int index = features.arraySize;
                features.arraySize++;
                features.GetArrayElementAtIndex(index).objectReferenceValue = feature;
                featureMap.arraySize++;
                featureMap.GetArrayElementAtIndex(index).longValue = localId;
                serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
                rendererData.SetDirty();
                EditorUtility.SetDirty(rendererData);
                installed++;
            }

            if (installed > 0) AssetDatabase.SaveAssets();
            return installed;
        }
    }
}
