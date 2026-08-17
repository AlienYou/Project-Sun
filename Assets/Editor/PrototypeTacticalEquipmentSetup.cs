using ProjectSun.FPS.Core;
using ProjectSun.FPS.Weapons;
using UnityEditor;
using UnityEngine;

namespace ProjectSun.FPS.Editor
{
    /// <summary>
    /// 创建 C01 所需的 F-1 与 S-1 项目 Prefab，并把首次缺失的数据引用写回战术装备定义。
    /// 已有 Prefab 和人工指定的 worldPrefab 不会被覆盖，确保资源制作可重复执行且不会抹掉人工调整。
    /// </summary>
    public static class PrototypeTacticalEquipmentSetup
    {
        private const string TacticalPrefabDirectory = "Assets/_ProjectSun/Prefabs/Tactical";
        private const string TacticalMaterialDirectory = "Assets/_ProjectSun/Art/Materials/Tactical";
        private const string FragDefinitionPath = "Assets/_ProjectSun/Data/Weapons/Tactical/F1FragGrenade.asset";
        private const string MineDefinitionPath = "Assets/_ProjectSun/Data/Weapons/Tactical/S1SensorMine.asset";
        private const string FragPrefabPath = TacticalPrefabDirectory + "/PFB_TAC_F1_FragGrenade.prefab";
        private const string MinePrefabPath = TacticalPrefabDirectory + "/PFB_TAC_S1_SensorMine.prefab";
        private const string FragMaterialPath = TacticalMaterialDirectory + "/M_TAC_F1_Prototype.mat";
        private const string MineBodyMaterialPath = TacticalMaterialDirectory + "/M_TAC_S1_Body_Prototype.mat";
        private const string MineIndicatorMaterialPath = TacticalMaterialDirectory + "/M_TAC_S1_Indicator_Prototype.mat";
        private const string FragPhysicsMaterialPath = TacticalMaterialDirectory + "/PM_TAC_F1_Bounce.physicMaterial";

        /// <summary>
        /// 创建缺失的战术装备 Prefab，并在定义未绑定世界 Prefab 时建立首次引用。
        /// </summary>
        [MenuItem("Project Sun/Prototype Content/Setup Tactical Equipment Prefabs", priority = 31)]
        public static void SetupTacticalEquipmentPrefabs()
        {
            TacticalEquipmentDefinition fragDefinition = AssetDatabase.LoadAssetAtPath<TacticalEquipmentDefinition>(FragDefinitionPath);
            TacticalEquipmentDefinition mineDefinition = AssetDatabase.LoadAssetAtPath<TacticalEquipmentDefinition>(MineDefinitionPath);
            if (fragDefinition == null || mineDefinition == null)
            {
                EditorUtility.DisplayDialog("Project Sun", "F-1 或 S-1 战术装备定义缺失；未创建 Prefab。", "确定");
                return;
            }

            EnsureFolder(TacticalPrefabDirectory);
            EnsureFolder(TacticalMaterialDirectory);
            Material fragMaterial = CreateOrLoadMaterial(FragMaterialPath, "M_TAC_F1_Prototype", new Color(0.18f, 0.48f, 0.22f));
            Material mineBodyMaterial = CreateOrLoadMaterial(MineBodyMaterialPath, "M_TAC_S1_Body_Prototype", new Color(0.08f, 0.11f, 0.13f));
            Material mineIndicatorMaterial = CreateOrLoadMaterial(MineIndicatorMaterialPath, "M_TAC_S1_Indicator_Prototype", new Color(1f, 0.62f, 0.08f));
            PhysicMaterial bounceMaterial = CreateOrLoadBounceMaterial();

            GameObject fragPrefab = CreateOrLoadFragPrefab(fragMaterial, bounceMaterial);
            GameObject minePrefab = CreateOrLoadMinePrefab(mineBodyMaterial, mineIndicatorMaterial);
            AssignPrefabIfMissing(fragDefinition, fragPrefab, "绑定 F-1 项目 Prefab");
            AssignPrefabIfMissing(mineDefinition, minePrefab, "绑定 S-1 项目 Prefab");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = fragPrefab;
            EditorUtility.DisplayDialog("Project Sun",
                "F-1 与 S-1 项目 Prefab 已准备。若数据资产已绑定人工 Prefab，工具会保留该引用；请在 Play Mode 测试投掷、部署、敌我过滤和回合清理。",
                "确定");
        }

        private static GameObject CreateOrLoadFragPrefab(Material material, PhysicMaterial bounceMaterial)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(FragPrefabPath);
            if (existing != null) return existing;

            GameObject root = new GameObject("PFB_TAC_F1_FragGrenade");
            root.layer = CombatLayers.IgnoreRaycastLayer;
            SphereCollider collider = root.AddComponent<SphereCollider>();
            collider.radius = 0.085f;
            collider.material = bounceMaterial;
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 0.35f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            root.AddComponent<FragGrenade>();
            CreateVisualPrimitive(root.transform, PrimitiveType.Sphere, "Visual", Vector3.zero, Vector3.one * 0.16f, material);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, FragPrefabPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static GameObject CreateOrLoadMinePrefab(Material bodyMaterial, Material indicatorMaterial)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(MinePrefabPath);
            if (existing != null) return existing;

            GameObject root = new GameObject("PFB_TAC_S1_SensorMine");
            root.layer = CombatLayers.IgnoreRaycastLayer;
            ProximityMine mine = root.AddComponent<ProximityMine>();
            CreateVisualPrimitive(root.transform, PrimitiveType.Cylinder, "Body", Vector3.up * 0.035f,
                new Vector3(0.18f, 0.035f, 0.18f), bodyMaterial);
            Renderer indicatorRenderer = CreateVisualPrimitive(root.transform, PrimitiveType.Sphere, "Indicator", Vector3.up * 0.09f,
                Vector3.one * 0.045f, indicatorMaterial);
            mine.SetIndicatorRenderer(indicatorRenderer);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, MinePrefabPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        /// <summary>
        /// 创建仅负责渲染的基础几何体，移除默认 Collider 以避免视觉子物体干扰 Actor 的物理或弹道查询。
        /// </summary>
        /// <param name="parent">项目 Prefab 根节点。</param>
        /// <param name="primitiveType">用于原型展示的 Unity 基础几何类型。</param>
        /// <param name="visualName">可视子物体稳定名称。</param>
        /// <param name="localPositionMeters">子物体相对根节点的位置，单位米。</param>
        /// <param name="localScale">子物体三轴缩放；基础网格尺寸为 1 米，因此该值也是近似展示尺寸。</param>
        /// <param name="material">共享材质；为空时保留 Unity 默认材质，仅供错误定位，正常流程不得为空。</param>
        /// <returns>新建子物体的 Renderer；调用方可把它配置为状态灯。</returns>
        private static Renderer CreateVisualPrimitive(Transform parent, PrimitiveType primitiveType, string visualName,
            Vector3 localPositionMeters, Vector3 localScale, Material material)
        {
            GameObject visual = GameObject.CreatePrimitive(primitiveType);
            visual.name = visualName;
            visual.layer = CombatLayers.IgnoreRaycastLayer;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPositionMeters;
            visual.transform.localScale = localScale;
            Collider defaultCollider = visual.GetComponent<Collider>();
            if (defaultCollider != null) Object.DestroyImmediate(defaultCollider);
            Renderer renderer = visual.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            return renderer;
        }

        /// <summary>
        /// 首次把生成的项目 Prefab 写入战术定义，保留已由内容人员手工绑定的替代 Prefab。
        /// </summary>
        /// <param name="definition">需要检查的战术装备定义。</param>
        /// <param name="prefab">本工具创建或读取的项目 Prefab。</param>
        /// <param name="undoLabel">Undo 面板中显示的首次绑定操作名称。</param>
        private static void AssignPrefabIfMissing(TacticalEquipmentDefinition definition, GameObject prefab, string undoLabel)
        {
            if (definition == null || prefab == null || definition.worldPrefab != null) return;
            Undo.RecordObject(definition, undoLabel);
            definition.worldPrefab = prefab;
            EditorUtility.SetDirty(definition);
        }

        private static Material CreateOrLoadMaterial(string path, string materialName, Color baseColor)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader) { name = materialName };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", baseColor);
            else material.color = baseColor;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static PhysicMaterial CreateOrLoadBounceMaterial()
        {
            PhysicMaterial existing = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(FragPhysicsMaterialPath);
            if (existing != null) return existing;

            PhysicMaterial material = new PhysicMaterial("PM_TAC_F1_Bounce")
            {
                bounciness = 0.52f,
                dynamicFriction = 0.28f,
                staticFriction = 0.28f,
                bounceCombine = PhysicMaterialCombine.Maximum,
                frictionCombine = PhysicMaterialCombine.Minimum
            };
            AssetDatabase.CreateAsset(material, FragPhysicsMaterialPath);
            return material;
        }

        /// <summary>
        /// 确保 AssetDatabase 中存在目标目录及其父目录。
        /// </summary>
        /// <param name="assetPath">Unity 项目相对目录路径，例如 Assets/_ProjectSun/Prefabs/Tactical。</param>
        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            string parentPath = System.IO.Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            string folderName = System.IO.Path.GetFileName(assetPath);
            if (string.IsNullOrEmpty(parentPath) || string.IsNullOrEmpty(folderName)) return;
            if (!AssetDatabase.IsValidFolder(parentPath)) EnsureFolder(parentPath);
            AssetDatabase.CreateFolder(parentPath, folderName);
        }
    }
}
