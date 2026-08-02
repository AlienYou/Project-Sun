using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ProjectSun.FPS.Editor
{
    /// <summary>
    /// Copies only the Low Poly Shooter Pack assets transitively used by the Project Sun AR-01 viewmodel,
    /// then remaps Project Sun YAML references to those owned copies. It deliberately never deletes the
    /// original package: the package remains an import source and its licence remains applicable.
    /// </summary>
    public static class LowPolyShooterOwnershipMigration
    {
        private const string SourceRoot = "Assets/Infima Games/Low Poly Shooter Pack - Free Sample";
        private const string DestinationRoot = "Assets/_ProjectSun/Art/ThirdParty/Infima/LowPolyShooterSample";
        private const string ProjectRoot = "Assets/_ProjectSun";
        private const string ViewmodelPrefabPath = "Assets/_ProjectSun/Prefabs/Characters/PFB_FP_Operator_LPSP_AR01.prefab";
        private const string HandgunCharacterOverridePath =
            SourceRoot + "/Animators/Character/OC_LPSP_PCH_Handgun_03.overrideController";

        private static readonly HashSet<string> RemappableExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".anim", ".asset", ".controller", ".mat", ".meta", ".overrideController", ".playable", ".prefab"
        };

        private static readonly HashSet<string> NonRuntimeRootExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".asmdef", ".asmref", ".cs", ".md", ".meta"
        };

        [MenuItem("Project Sun/Tools/Assets/Audit AR-01 External Dependencies", priority = 61)]
        public static void Audit()
        {
            List<string> externalDependencies = GetAr01ExternalDependencies();
            string message = externalDependencies.Count == 0
                ? "AR-01 viewmodel has no remaining Low Poly Shooter Pack runtime dependencies."
                : string.Join("\n", externalDependencies);
            Debug.Log($"[Project Sun] AR-01 external dependency audit ({externalDependencies.Count} assets):\n{message}");
            EditorUtility.DisplayDialog("AR-01 External Dependency Audit",
                externalDependencies.Count == 0
                    ? "No Low Poly Shooter Pack runtime dependencies remain."
                    : $"Found {externalDependencies.Count} external runtime dependencies. See the Console for the exact list.", "OK");
        }

        [MenuItem("Project Sun/Tools/Assets/Migrate AR-01 Runtime Assets into _ProjectSun", priority = 62)]
        public static void Migrate()
        {
            MigrateDependencies("AR-01", GetAr01ExternalDependencies());
        }

        [MenuItem("Project Sun/Tools/Assets/Migrate HG-3 Handgun Arm Animation Assets", priority = 65)]
        public static void MigrateHandgunRuntimeAssetsMenu()
        {
            MigrateHandgunRuntimeAssets();
        }

        public static bool MigrateHandgunRuntimeAssets()
        {
            return MigrateDependencies("HG-3 handgun arm animations", GetHandgunExternalDependencies());
        }

        [MenuItem("Project Sun/Tools/Assets/Audit Project Sun Source Pack Dependencies", priority = 63)]
        public static void AuditProjectSunDependencies()
        {
            List<string> externalDependencies = GetProjectSunExternalDependencies();
            string message = externalDependencies.Count == 0
                ? "All Project Sun runtime resources are self-contained."
                : string.Join("\n", externalDependencies);
            Debug.Log($"[Project Sun] Project-wide source pack dependency audit ({externalDependencies.Count} assets):\n{message}");
            EditorUtility.DisplayDialog("Project Sun Dependency Audit",
                externalDependencies.Count == 0
                    ? "No Low Poly Shooter Pack runtime dependencies remain under _ProjectSun."
                    : $"Found {externalDependencies.Count} external runtime dependencies. See the Console for the exact list.", "OK");
        }

        [MenuItem("Project Sun/Tools/Assets/Migrate Project Sun Source Pack Runtime Assets", priority = 64)]
        public static void MigrateProjectSunDependencies()
        {
            MigrateDependencies("Project Sun runtime resources", GetProjectSunExternalDependencies());
        }

        private static bool MigrateDependencies(string scopeName, List<string> externalDependencies)
        {
            if (externalDependencies.Count == 0)
            {
                EditorUtility.DisplayDialog("Project Sun", $"{scopeName} are already self-contained under _ProjectSun.", "OK");
                return true;
            }

            bool confirmed = EditorUtility.DisplayDialog($"Migrate {scopeName}",
                $"Copy {externalDependencies.Count} required Low Poly Shooter Pack assets into:\n{DestinationRoot}\n\n" +
                "The tool remaps _ProjectSun references and verifies the result. It does not delete or modify the source package.",
                "Copy and Remap", "Cancel");
            if (!confirmed) return false;

            Dictionary<string, string> guidRemap = CopyDependencies(externalDependencies);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            int updatedFiles = RewriteProjectSunReferences(guidRemap);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            List<string> remaining = scopeName == "AR-01"
                ? GetAr01ExternalDependencies()
                : GetProjectSunExternalDependencies();
            string result = remaining.Count == 0
                ? $"Migration complete. Copied {guidRemap.Count} assets and remapped {updatedFiles} Project Sun files."
                : $"Migration copied {guidRemap.Count} assets and remapped {updatedFiles} files, but {remaining.Count} external dependencies remain. See the Console.";
            Debug.Log($"[Project Sun] {result}" + (remaining.Count > 0 ? $"\nRemaining:\n{string.Join("\n", remaining)}" : string.Empty));
            EditorUtility.DisplayDialog("AR-01 Asset Migration", result, "OK");
            return remaining.Count == 0;
        }

        private static List<string> GetAr01ExternalDependencies()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ViewmodelPrefabPath) == null)
                throw new InvalidOperationException($"Viewmodel prefab not found at {ViewmodelPrefabPath}.");

            return GetExternalDependencies(new[] { ViewmodelPrefabPath });
        }

        private static List<string> GetHandgunExternalDependencies()
        {
            if (AssetDatabase.LoadMainAssetAtPath(HandgunCharacterOverridePath) == null)
                throw new InvalidOperationException($"Handgun arm override was not found at {HandgunCharacterOverridePath}.");
            return GetExternalDependencies(new[] { HandgunCharacterOverridePath });
        }

        private static List<string> GetProjectSunExternalDependencies()
        {
            string[] runtimeRoots = AssetDatabase.FindAssets(string.Empty, new[] { ProjectRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !AssetDatabase.IsValidFolder(path))
                .Where(path => !NonRuntimeRootExtensions.Contains(Path.GetExtension(path)))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            return GetExternalDependencies(runtimeRoots);
        }

        private static List<string> GetExternalDependencies(IEnumerable<string> roots)
        {
            string[] rootPaths = roots.Where(path => !string.IsNullOrEmpty(path)).ToArray();
            if (rootPaths.Length == 0) return new List<string>();

            return AssetDatabase.GetDependencies(rootPaths, true)
                .Where(path => path.StartsWith(SourceRoot + "/", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
        }

        private static Dictionary<string, string> CopyDependencies(IEnumerable<string> sourcePaths)
        {
            Dictionary<string, string> guidRemap = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string sourcePath in sourcePaths)
            {
                string relativePath = sourcePath.Substring(SourceRoot.Length).TrimStart('/');
                string destinationPath = $"{DestinationRoot}/{relativePath}";
                EnsureFolderForAsset(destinationPath);

                if (AssetDatabase.LoadMainAssetAtPath(destinationPath) == null && !AssetDatabase.CopyAsset(sourcePath, destinationPath))
                    throw new InvalidOperationException($"Could not copy {sourcePath} to {destinationPath}.");

                string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
                string destinationGuid = AssetDatabase.AssetPathToGUID(destinationPath);
                if (string.IsNullOrEmpty(sourceGuid) || string.IsNullOrEmpty(destinationGuid))
                    throw new InvalidOperationException($"Could not resolve GUIDs for {sourcePath}.");
                guidRemap[sourceGuid] = destinationGuid;
            }
            return guidRemap;
        }

        private static int RewriteProjectSunReferences(IReadOnlyDictionary<string, string> guidRemap)
        {
            int updatedFileCount = 0;
            foreach (string absolutePath in Directory.GetFiles(ProjectRoot, "*", SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(absolutePath);
                if (!RemappableExtensions.Contains(extension)) continue;

                string contents = File.ReadAllText(absolutePath);
                string remappedContents = contents;
                foreach (KeyValuePair<string, string> pair in guidRemap)
                    remappedContents = remappedContents.Replace($"guid: {pair.Key}", $"guid: {pair.Value}");
                if (remappedContents == contents) continue;

                File.WriteAllText(absolutePath, remappedContents, new UTF8Encoding(false));
                updatedFileCount++;
            }
            return updatedFileCount;
        }

        private static void EnsureFolderForAsset(string assetPath)
        {
            string folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolderForAsset(parent + "/placeholder.asset");
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
