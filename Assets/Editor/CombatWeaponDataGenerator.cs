using System.Collections.Generic;
using ProjectSun.FPS.Bootstrap;
using ProjectSun.FPS.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectSun.FPS.Editor
{
    /// <summary>Creates editable weapon assets and assigns their catalog to the CombatSlice scene.</summary>
    public static class CombatWeaponDataGenerator
    {
        private const string DataRoot = "Assets/_ProjectSun/Data/Weapons";
        private const string DefinitionPath = DataRoot + "/Definitions/AR4Carbine.asset";
        private const string SidearmDefinitionPath = DataRoot + "/Definitions/HG3Sidearm.asset";
        private const string CatalogPath = DataRoot + "/Catalogs/AR4LoadoutCatalog.asset";
        private const string ScenePath = "Assets/_ProjectSun/Scenes/CombatSlice.unity";

        [MenuItem("Project Sun/Generate Combat Weapon Data", priority = 12)]
        public static void GenerateAndAssign()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            WeaponLoadoutCatalog catalog = CreateOrGetDataAssets();
            AssignToCombatSlice(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Project Sun", "Weapon definition, attachment assets and the CombatSlice catalog are ready.", "OK");
        }

        public static WeaponLoadoutCatalog CreateOrGetDataAssets()
        {
            EnsureFolder(DataRoot);
            EnsureFolder(DataRoot + "/Definitions");
            EnsureFolder(DataRoot + "/Attachments");
            EnsureFolder(DataRoot + "/Catalogs");

            WeaponDefinition weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(DefinitionPath);
            if (weapon == null)
            {
                weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
                weapon.displayName = "AR-4 Carbine";
                weapon.baseStats = WeaponStats.Carbine;
                AssetDatabase.CreateAsset(weapon, DefinitionPath);
            }
            weapon.automatic = true;
            weapon.aimCapability = WeaponAimCapability.SupportsAds;

            WeaponDefinition sidearm = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(SidearmDefinitionPath);
            if (sidearm == null)
            {
                sidearm = ScriptableObject.CreateInstance<WeaponDefinition>();
                sidearm.displayName = "HG-3 Sidearm";
                sidearm.automatic = false;
                sidearm.baseStats = new WeaponStats
                {
                    damage = 32f, roundsPerSecond = 6.5f, magazineSize = 15, reloadSeconds = 1.65f,
                    hipSpread = 1.75f, aimSpread = 0.42f, range = 75f
                };
                AssetDatabase.CreateAsset(sidearm, SidearmDefinitionPath);
            }
            sidearm.aimCapability = WeaponAimCapability.SupportsAds;

            List<WeaponAttachment> attachments = new List<WeaponAttachment>
            {
                CreateAttachment("M2Reflex", AttachmentSlot.Optic, "M2 REFLEX", aimSpread: 0.55f),
                CreateAttachment("H7Holo", AttachmentSlot.Optic, "H7 HOLO", hipSpread: 0.9f, aimSpread: 0.72f),
                CreateAttachment("Compensator", AttachmentSlot.Muzzle, "COMPENSATOR", hipSpread: 0.76f, aimSpread: 0.82f),
                CreateAttachment("Suppressor", AttachmentSlot.Muzzle, "SUPPRESSOR", range: 0.84f, hipSpread: 0.9f),
                CreateAttachment("LongBarrel", AttachmentSlot.Barrel, "LONG BARREL", damage: 1.06f, range: 1.25f, fireRate: 0.94f),
                CreateAttachment("CqbBarrel", AttachmentSlot.Barrel, "CQB BARREL", fireRate: 1.12f, range: 0.76f, hipSpread: 0.86f),
                CreateAttachment("ExtendedMag", AttachmentSlot.Magazine, "EXTENDED MAG", magazine: 1.33f, reload: 1.12f),
                CreateAttachment("FastMag", AttachmentSlot.Magazine, "FAST MAG", magazine: 0.80f, reload: 0.74f),
                CreateAttachment("TacticalStock", AttachmentSlot.Stock, "TACTICAL STOCK", hipSpread: 0.88f, aimSpread: 0.78f),
                CreateAttachment("LightStock", AttachmentSlot.Stock, "LIGHT STOCK", fireRate: 1.08f, hipSpread: 1.12f)
            };

            WeaponLoadoutCatalog catalog = AssetDatabase.LoadAssetAtPath<WeaponLoadoutCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<WeaponLoadoutCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.SetContents(weapon, attachments);
            catalog.SetWeaponSlots(new[] { weapon }, new[] { sidearm });
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static WeaponAttachment CreateAttachment(string assetName, AttachmentSlot slot, string displayName, float damage = 1f,
            float fireRate = 1f, float magazine = 1f, float reload = 1f, float hipSpread = 1f, float aimSpread = 1f, float range = 1f)
        {
            string path = DataRoot + "/Attachments/" + assetName + ".asset";
            WeaponAttachment attachment = AssetDatabase.LoadAssetAtPath<WeaponAttachment>(path);
            if (attachment != null) return attachment;

            attachment = ScriptableObject.CreateInstance<WeaponAttachment>();
            attachment.slot = slot;
            attachment.displayName = displayName;
            attachment.damageMultiplier = damage;
            attachment.fireRateMultiplier = fireRate;
            attachment.magazineMultiplier = magazine;
            attachment.reloadMultiplier = reload;
            attachment.hipSpreadMultiplier = hipSpread;
            attachment.aimSpreadMultiplier = aimSpread;
            attachment.rangeMultiplier = range;
            AssetDatabase.CreateAsset(attachment, path);
            return attachment;
        }

        private static void AssignToCombatSlice(WeaponLoadoutCatalog catalog)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            CombatSliceSceneInstaller installer = Object.FindObjectOfType<CombatSliceSceneInstaller>();
            if (installer == null) return;
            installer.SetLoadoutCatalog(catalog);
            EditorUtility.SetDirty(installer);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
