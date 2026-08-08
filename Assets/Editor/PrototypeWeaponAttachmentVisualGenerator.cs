using ProjectSun.FPS.Presentation;
using ProjectSun.FPS.Core;
using ProjectSun.FPS.Weapons;
using UnityEditor;
using UnityEngine;

namespace ProjectSun.FPS.Editor
{
    /// <summary>
    /// Creates project-owned, intentionally simple placeholder attachments. These prefabs prove the
    /// attachment presentation contract without concealing missing final art behind third-party models.
    /// </summary>
    public static class PrototypeWeaponAttachmentVisualGenerator
    {
        private const string DataRoot = "Assets/_ProjectSun/Data/Weapons";
        private const string AttachmentDataRoot = DataRoot + "/Attachments";
        private const string AdsDataRoot = DataRoot + "/ADS";
        private const string PrefabRoot = "Assets/_ProjectSun/Prefabs/Weapons/Attachments";
        private const string MaterialRoot = "Assets/_ProjectSun/Art/Materials/Weapons/Attachments";

        [MenuItem("Project Sun/Create Project-Owned Prototype Attachment Visuals", priority = 18)]
        public static void CreateOrUpdate()
        {
            CombatWeaponDataGenerator.CreateOrGetDataAssets();
            EnsureFolder(PrefabRoot);
            EnsureFolder(MaterialRoot);

            Material dark = CreateOrGetMaterial("M_ATT_Prototype_Dark", new Color(0.055f, 0.07f, 0.085f), 0.56f);
            Material graphite = CreateOrGetMaterial("M_ATT_Prototype_Graphite", new Color(0.13f, 0.16f, 0.19f), 0.42f);
            Material cyan = CreateOrGetMaterial("M_ATT_Prototype_Cyan", new Color(0.08f, 0.72f, 0.92f), 0.78f);
            Material amber = CreateOrGetMaterial("M_ATT_Prototype_Amber", new Color(1f, 0.34f, 0.055f), 0.64f);

            GameObject m2 = CreateOrGetPrefab("PFB_ATT_AR4_M2Reflex", root => BuildM2Reflex(root, dark, graphite, cyan));
            GameObject h7 = CreateOrGetPrefab("PFB_ATT_AR4_H7Holo", root => BuildH7Holo(root, dark, graphite, amber));
            GameObject compensator = CreateOrGetPrefab("PFB_ATT_AR4_Compensator", root => BuildCompensator(root, dark, graphite));
            GameObject suppressor = CreateOrGetPrefab("PFB_ATT_AR4_Suppressor", root => BuildSuppressor(root, dark, graphite));

            WeaponDefinition ar4 = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(DataRoot + "/Definitions/AR4Carbine.asset");
            WeaponAdsProfile m2Ads = CreateOrGetAdsProfile("ADS_M2Reflex", 9f, 6f);
            WeaponAdsProfile h7Ads = CreateOrGetAdsProfile("ADS_H7Holo", 10f, 7f);
            ConfigureAttachment("M2Reflex", ar4, m2, "SOCKET_Scope", "SM_AR_01_Scope_Default", "AimAnchor_M2Reflex", m2Ads);
            ConfigureAttachment("H7Holo", ar4, h7, "SOCKET_Scope", "SM_AR_01_Scope_Default", "AimAnchor_H7Holo", h7Ads);
            ConfigureAttachment("Compensator", ar4, compensator, "SOCKET_Muzzle", string.Empty, string.Empty, null);
            ConfigureAttachment("Suppressor", ar4, suppressor, "SOCKET_Muzzle", string.Empty, string.Empty, null);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Project Sun",
                "Created project-owned M2 reflex, H7 holo, compensator and suppressor prototype prefabs. Open WeaponLab, equip them from TAB > ATTACHMENTS, then validate M2/H7 ADS in play mode.", "OK");
        }

        private static GameObject CreateOrGetPrefab(string prefabName, System.Action<GameObject> build)
        {
            string path = PrefabRoot + "/" + prefabName + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            GameObject root = new GameObject(prefabName);
            root.layer = CombatLayers.ViewmodelLayer;
            build(root);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static void BuildM2Reflex(GameObject root, Material dark, Material graphite, Material lens)
        {
            CreatePart(root.transform, "Base", PrimitiveType.Cube, new Vector3(0f, -0.006f, 0f), Vector3.zero,
                new Vector3(0.066f, 0.014f, 0.052f), dark);
            CreatePart(root.transform, "Housing", PrimitiveType.Cube, new Vector3(0f, 0.016f, -0.008f), Vector3.zero,
                new Vector3(0.054f, 0.034f, 0.026f), graphite);
            CreatePart(root.transform, "Left Frame", PrimitiveType.Cube, new Vector3(-0.025f, 0.031f, 0.011f), Vector3.zero,
                new Vector3(0.008f, 0.036f, 0.010f), dark);
            CreatePart(root.transform, "Right Frame", PrimitiveType.Cube, new Vector3(0.025f, 0.031f, 0.011f), Vector3.zero,
                new Vector3(0.008f, 0.036f, 0.010f), dark);
            CreatePart(root.transform, "Top Frame", PrimitiveType.Cube, new Vector3(0f, 0.047f, 0.011f), Vector3.zero,
                new Vector3(0.058f, 0.008f, 0.010f), dark);
            GameObject glass = CreatePart(root.transform, "Reflex Lens", PrimitiveType.Cube, new Vector3(0f, 0.030f, 0.011f),
                Vector3.zero, new Vector3(0.042f, 0.031f, 0.003f), lens);
            AddProbe(glass, "M2 Reflex Lens", 0.024f);
            CreateAimAnchor(root.transform, "AimAnchor_M2Reflex", new Vector3(0f, 0.030f, 0.011f));
        }

        private static void BuildH7Holo(GameObject root, Material dark, Material graphite, Material lens)
        {
            CreatePart(root.transform, "Base", PrimitiveType.Cube, new Vector3(0f, -0.006f, 0f), Vector3.zero,
                new Vector3(0.082f, 0.014f, 0.060f), dark);
            CreatePart(root.transform, "Rear Housing", PrimitiveType.Cube, new Vector3(0f, 0.014f, -0.016f), Vector3.zero,
                new Vector3(0.068f, 0.032f, 0.030f), graphite);
            CreatePart(root.transform, "Left Hood", PrimitiveType.Cube, new Vector3(-0.031f, 0.031f, 0.012f), Vector3.zero,
                new Vector3(0.010f, 0.039f, 0.012f), dark);
            CreatePart(root.transform, "Right Hood", PrimitiveType.Cube, new Vector3(0.031f, 0.031f, 0.012f), Vector3.zero,
                new Vector3(0.010f, 0.039f, 0.012f), dark);
            CreatePart(root.transform, "Top Hood", PrimitiveType.Cube, new Vector3(0f, 0.050f, 0.012f), Vector3.zero,
                new Vector3(0.072f, 0.009f, 0.012f), dark);
            GameObject glass = CreatePart(root.transform, "Holo Lens", PrimitiveType.Cube, new Vector3(0f, 0.030f, 0.012f),
                Vector3.zero, new Vector3(0.058f, 0.033f, 0.003f), lens);
            AddProbe(glass, "H7 Holo Lens", 0.031f);
            CreateAimAnchor(root.transform, "AimAnchor_H7Holo", new Vector3(0f, 0.030f, 0.012f));
        }

        private static void BuildCompensator(GameObject root, Material dark, Material graphite)
        {
            CreatePart(root.transform, "Compensator Body", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.054f),
                new Vector3(90f, 0f, 0f), new Vector3(0.020f, 0.060f, 0.020f), dark);
            CreatePart(root.transform, "Top Port", PrimitiveType.Cube, new Vector3(0f, 0.020f, 0.061f), Vector3.zero,
                new Vector3(0.018f, 0.006f, 0.044f), graphite);
            CreatePart(root.transform, "Side Port L", PrimitiveType.Cube, new Vector3(-0.020f, 0f, 0.061f), Vector3.zero,
                new Vector3(0.006f, 0.010f, 0.036f), graphite);
            CreatePart(root.transform, "Side Port R", PrimitiveType.Cube, new Vector3(0.020f, 0f, 0.061f), Vector3.zero,
                new Vector3(0.006f, 0.010f, 0.036f), graphite);
            AddProbe(root, "Compensator Front", 0.020f, new Vector3(0f, 0f, 0.115f));
        }

        private static void BuildSuppressor(GameObject root, Material dark, Material graphite)
        {
            CreatePart(root.transform, "Suppressor Body", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.102f),
                new Vector3(90f, 0f, 0f), new Vector3(0.025f, 0.112f, 0.025f), dark);
            CreatePart(root.transform, "Suppressor Collar", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.015f),
                new Vector3(90f, 0f, 0f), new Vector3(0.029f, 0.020f, 0.029f), graphite);
            CreatePart(root.transform, "Suppressor Cap", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.212f),
                new Vector3(90f, 0f, 0f), new Vector3(0.027f, 0.008f, 0.027f), graphite);
            AddProbe(root, "Suppressor Front", 0.026f, new Vector3(0f, 0f, 0.220f));
        }

        private static GameObject CreatePart(Transform parent, string partName, PrimitiveType type, Vector3 position,
            Vector3 rotation, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(rotation));
            part.transform.localScale = scale;
            part.layer = CombatLayers.ViewmodelLayer;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            return part;
        }

        private static void CreateAimAnchor(Transform parent, string anchorName, Vector3 localPosition)
        {
            GameObject anchor = new GameObject(anchorName);
            anchor.layer = CombatLayers.ViewmodelLayer;
            anchor.transform.SetParent(parent, false);
            anchor.transform.SetLocalPositionAndRotation(localPosition, Quaternion.identity);
        }

        private static void AddProbe(GameObject target, string label, float radius, Vector3? localPosition = null)
        {
            if (localPosition.HasValue)
            {
                GameObject probeObject = new GameObject("ClipProbe_" + label.Replace(" ", string.Empty));
                probeObject.layer = CombatLayers.ViewmodelLayer;
                probeObject.transform.SetParent(target.transform, false);
                probeObject.transform.SetLocalPositionAndRotation(localPosition.Value, Quaternion.identity);
                target = probeObject;
            }
            ViewmodelClipProbe probe = target.GetComponent<ViewmodelClipProbe>();
            if (probe == null) probe = target.AddComponent<ViewmodelClipProbe>();
            probe.Configure(label, radius);
        }

        private static WeaponAdsProfile CreateOrGetAdsProfile(string assetName, float transitionSpeed, float fovReduction)
        {
            string path = AdsDataRoot + "/" + assetName + ".asset";
            WeaponAdsProfile profile = AssetDatabase.LoadAssetAtPath<WeaponAdsProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<WeaponAdsProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            profile.ConfigureDefaults(0.18f, transitionSpeed, fovReduction);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void ConfigureAttachment(string assetName, WeaponDefinition weapon, GameObject visualPrefab,
            string mountName, string replacedVisualName, string aimAnchorName, WeaponAdsProfile adsProfile)
        {
            WeaponAttachment attachment = AssetDatabase.LoadAssetAtPath<WeaponAttachment>(AttachmentDataRoot + "/" + assetName + ".asset");
            if (attachment == null || weapon == null || visualPrefab == null) return;
            attachment.SetViewmodelVisual(weapon, visualPrefab, mountName, replacedVisualName, aimAnchorName);
            if (adsProfile != null) attachment.SetAdsProfileOverride(adsProfile);
            EditorUtility.SetDirty(attachment);
        }

        private static Material CreateOrGetMaterial(string materialName, Color color, float smoothness)
        {
            string path = MaterialRoot + "/" + materialName + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = materialName };
                AssetDatabase.CreateAsset(material, path);
            }
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
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
