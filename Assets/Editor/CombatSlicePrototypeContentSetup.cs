using ProjectSun.FPS.AI;
using ProjectSun.FPS.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectSun.FPS.Editor
{
    /// <summary>
    /// 为 CombatSlice 创建可重复使用的原型环境 Prefab 与最小测试布局。
    /// 工具只创建缺失的项目资源和 C01 命名对象，绝不覆盖关卡人员已有的出生点、掩体或人工位置调整。
    /// </summary>
    public static class CombatSlicePrototypeContentSetup
    {
        private const string ScenePath = "Assets/_ProjectSun/Scenes/CombatSlice.unity";
        private const string PrefabDirectory = "Assets/_ProjectSun/Prefabs/Environment/Prototype";
        private const string WallMaterialPath = "Assets/_ProjectSun/Art/Materials/PrototypeWall.mat";
        private const string ObjectiveMaterialPath = "Assets/_ProjectSun/Art/Materials/PrototypeObjective.mat";
        private const string ContentRootName = "PrototypeContent";
        private const string TacticalCoverRootName = "Tactical Cover Points";

        private const string LowCoverPrefabPath = PrefabDirectory + "/PFB_ENV_Prototype_LowCover.prefab";
        private const string HighCoverPrefabPath = PrefabDirectory + "/PFB_ENV_Prototype_HighCover.prefab";
        private const string SolidWallPrefabPath = PrefabDirectory + "/PFB_ENV_Prototype_SolidWall.prefab";
        private const string DoorwayWallPrefabPath = PrefabDirectory + "/PFB_ENV_Prototype_DoorwayWall.prefab";
        private const string CrateBarricadePrefabPath = PrefabDirectory + "/PFB_ENV_Prototype_CrateBarricade.prefab";

        /// <summary>
        /// 在现有 CombatSlice 场景内创建 C01 灰盒资源和标准测试布局。
        /// </summary>
        [MenuItem("Project Sun/Prototype Content/Setup CombatSlice Greybox", priority = 30)]
        public static void SetupCombatSliceGreybox()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                EditorUtility.DisplayDialog("Project Sun", "未找到 CombatSlice 场景；未创建任何原型内容。", "确定");
                return;
            }

            // 打开专项场景前先让使用者决定是否保存当前工作，避免工具切场景时丢失其他场景的人工修改。
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Transform combatSliceRoot = FindRequiredRoot("Combat Slice");
            Transform environmentRoot = combatSliceRoot != null ? combatSliceRoot.Find("Environment") : null;
            if (combatSliceRoot == null || environmentRoot == null)
            {
                EditorUtility.DisplayDialog("Project Sun", "Combat Slice/Environment 根节点缺失；未修改场景。", "确定");
                return;
            }

            Material wallMaterial = AssetDatabase.LoadAssetAtPath<Material>(WallMaterialPath);
            Material objectiveMaterial = AssetDatabase.LoadAssetAtPath<Material>(ObjectiveMaterialPath);
            if (wallMaterial == null || objectiveMaterial == null)
            {
                EditorUtility.DisplayDialog("Project Sun", "原型墙体或目标材质缺失；请先修复基础材质引用。", "确定");
                return;
            }

            EnsureFolder(PrefabDirectory);
            GameObject lowCoverPrefab = CreateOrLoadBoxPrefab(LowCoverPrefabPath, "PFB_ENV_Prototype_LowCover",
                new Vector3(3.6f, 1.15f, 0.75f), wallMaterial);
            GameObject highCoverPrefab = CreateOrLoadBoxPrefab(HighCoverPrefabPath, "PFB_ENV_Prototype_HighCover",
                new Vector3(3.2f, 2.2f, 0.75f), wallMaterial);
            GameObject solidWallPrefab = CreateOrLoadBoxPrefab(SolidWallPrefabPath, "PFB_ENV_Prototype_SolidWall",
                new Vector3(5.2f, 3.2f, 0.8f), wallMaterial);
            GameObject doorwayWallPrefab = CreateOrLoadDoorwayPrefab(doorwayWallPrefabPath: DoorwayWallPrefabPath,
                wallMaterial: wallMaterial);
            GameObject cratePrefab = CreateOrLoadBoxPrefab(CrateBarricadePrefabPath, "PFB_ENV_Prototype_CrateBarricade",
                new Vector3(2.0f, 1.4f, 1.4f), objectiveMaterial);

            Transform contentRoot = GetOrCreateSceneChild(environmentRoot, ContentRootName, "创建 C01 原型内容根节点");
            // 新布局只填充 C01 命名对象。再次执行时保留已存在对象的位置，方便关卡人员继续手动微调。
            CreateOrKeepInstance(contentRoot, lowCoverPrefab, "C01_LowCover_West", new Vector3(-12f, 0f, 2f), Quaternion.identity);
            Transform highCover = CreateOrKeepInstance(contentRoot, highCoverPrefab, "C01_HighCover_East",
                new Vector3(12f, 0f, 2f), Quaternion.identity);
            CreateOrKeepInstance(contentRoot, solidWallPrefab, "C01_CenterSolidWall", new Vector3(0f, 0f, 5f), Quaternion.identity);
            CreateOrKeepInstance(contentRoot, doorwayWallPrefab, "C01_CrossingDoorway", new Vector3(0f, 0f, 11f), Quaternion.identity);
            CreateOrKeepInstance(contentRoot, cratePrefab, "C01_CrateBarricade", new Vector3(7f, 0f, -4f), Quaternion.identity);

            CreateOrKeepRangeMarker(contentRoot, "C01_RangeMarker_10m", new Vector3(-7f, 0.02f, -16f), "10m");
            CreateOrKeepRangeMarker(contentRoot, "C01_RangeMarker_25m", new Vector3(8f, 0.02f, -16f), "25m");
            CreateOrKeepCoverAnchors(combatSliceRoot, highCover);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = contentRoot.gameObject;
            EditorUtility.DisplayDialog("Project Sun",
                "C01 灰盒内容已准备：5 个项目 Prefab、CombatSlice 测试布局和高掩体 Cover Anchors。请进入 Play Mode 验证 NavMesh、出生和弹道遮挡。",
                "确定");
        }

        /// <summary>
        /// 查找场景根节点。
        /// </summary>
        /// <param name="rootName">场景根节点名称；使用稳定名称避免在编辑器工具中模糊查找多个对象。</param>
        /// <returns>找到的根节点；不存在时返回空。</returns>
        private static Transform FindRequiredRoot(string rootName)
        {
            GameObject root = GameObject.Find(rootName);
            return root != null ? root.transform : null;
        }

        /// <summary>
        /// 创建或读取单体方盒环境 Prefab。
        /// </summary>
        /// <param name="prefabPath">项目内 Prefab 路径；已有资源不会被本工具覆盖。</param>
        /// <param name="prefabName">Prefab 与根节点名称，必须保持 C01 命名契约。</param>
        /// <param name="dimensionsMeters">碰撞和可视网格尺寸，单位为米，三个分量必须大于零。</param>
        /// <param name="material">原型材质；为空时不创建资源，防止生成不可读的无材质 Prefab。</param>
        /// <returns>已有或新建的项目 Prefab；创建条件不满足时返回空。</returns>
        private static GameObject CreateOrLoadBoxPrefab(string prefabPath, string prefabName, Vector3 dimensionsMeters,
            Material material)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null) return existing;
            if (material == null || dimensionsMeters.x <= 0f || dimensionsMeters.y <= 0f || dimensionsMeters.z <= 0f) return null;

            GameObject root = new GameObject(prefabName);
            root.layer = CombatLayers.WallLayer;
            root.isStatic = true;
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = dimensionsMeters;
            collider.center = Vector3.up * (dimensionsMeters.y * 0.5f);
            CreateVisualBlock(root.transform, "Visual", Vector3.up * (dimensionsMeters.y * 0.5f), dimensionsMeters, material,
                addCollider: false);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        /// <summary>
        /// 创建或读取带可穿行开口的门洞墙 Prefab。
        /// </summary>
        /// <param name="doorwayWallPrefabPath">项目内门洞墙 Prefab 路径；已有资源保持不变。</param>
        /// <param name="wallMaterial">门洞墙材质；用于可视网格且不得为空。</param>
        /// <returns>已有或新建的门洞墙 Prefab；材质缺失时返回空。</returns>
        private static GameObject CreateOrLoadDoorwayPrefab(string doorwayWallPrefabPath, Material wallMaterial)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(doorwayWallPrefabPath);
            if (existing != null) return existing;
            if (wallMaterial == null) return null;

            const float totalWidth = 4.8f;
            const float totalHeight = 3.2f;
            const float openingWidth = 1.8f;
            const float openingHeight = 2.1f;
            const float thickness = 0.65f;
            float sideWidth = (totalWidth - openingWidth) * 0.5f;
            float headerHeight = totalHeight - openingHeight;

            GameObject root = new GameObject("PFB_ENV_Prototype_DoorwayWall");
            root.layer = CombatLayers.WallLayer;
            root.isStatic = true;

            // 门洞由三个独立碰撞块组成，中央开口保持真正可穿行，避免视觉门洞与物理阻挡不一致。
            CreateVisualBlock(root.transform, "LeftPillar", new Vector3(-(openingWidth + sideWidth) * 0.5f, totalHeight * 0.5f, 0f),
                new Vector3(sideWidth, totalHeight, thickness), wallMaterial);
            CreateVisualBlock(root.transform, "RightPillar", new Vector3((openingWidth + sideWidth) * 0.5f, totalHeight * 0.5f, 0f),
                new Vector3(sideWidth, totalHeight, thickness), wallMaterial);
            CreateVisualBlock(root.transform, "Header", new Vector3(0f, openingHeight + headerHeight * 0.5f, 0f),
                new Vector3(openingWidth, headerHeight, thickness), wallMaterial);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, doorwayWallPrefabPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        /// <summary>
        /// 创建带独立碰撞体的可视方块。
        /// </summary>
        /// <param name="parent">所属 Prefab 根节点；子节点继承其 Wall Layer 和静态标记策略。</param>
        /// <param name="blockName">子块稳定名称，用于在 Prefab Mode 中定位碰撞结构。</param>
        /// <param name="localPositionMeters">相对于根节点的位置，单位为米。</param>
        /// <param name="dimensionsMeters">子块三轴尺寸，单位为米。</param>
        /// <param name="material">渲染材质；调用方已保证非空。</param>
        /// <param name="addCollider">是否在可视子块上保留 BoxCollider；单体方盒由根节点持有唯一 Collider，门洞分段必须各自持有 Collider。</param>
        private static void CreateVisualBlock(Transform parent, string blockName, Vector3 localPositionMeters,
            Vector3 dimensionsMeters, Material material, bool addCollider = true)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = blockName;
            block.layer = CombatLayers.WallLayer;
            block.isStatic = true;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = localPositionMeters;
            block.transform.localScale = dimensionsMeters;
            block.GetComponent<Renderer>().sharedMaterial = material;
            // 单体掩体由根节点统一承担碰撞；移除子块 Collider 可避免同一次弹道查询命中重叠的两层碰撞体。
            if (!addCollider) Object.DestroyImmediate(block.GetComponent<Collider>());
        }

        /// <summary>
        /// 在指定根节点下创建场景子节点，并为首次创建注册 Unity Undo。
        /// </summary>
        /// <param name="parent">父节点；不能为空。</param>
        /// <param name="childName">稳定子节点名称；已有同名节点将直接复用。</param>
        /// <param name="undoLabel">Undo 面板中显示的操作名称。</param>
        /// <returns>已有或新建的子节点。</returns>
        private static Transform GetOrCreateSceneChild(Transform parent, string childName, string undoLabel)
        {
            Transform existing = parent.Find(childName);
            if (existing != null) return existing;

            GameObject child = new GameObject(childName);
            Undo.RegisterCreatedObjectUndo(child, undoLabel);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        /// <summary>
        /// 创建缺失的 Prefab 实例，保留已有实例的位置和旋转。
        /// </summary>
        /// <param name="parent">原型内容根节点。</param>
        /// <param name="prefab">要实例化的项目 Prefab；为空时不创建，防止场景产生丢失引用。</param>
        /// <param name="instanceName">场景实例稳定名称；同名实例存在时视为关卡人工调整结果。</param>
        /// <param name="worldPositionMeters">首次创建时的世界坐标，单位为米。</param>
        /// <param name="worldRotation">首次创建时的世界旋转。</param>
        /// <returns>已有或新建的实例 Transform；Prefab 缺失时返回空。</returns>
        private static Transform CreateOrKeepInstance(Transform parent, GameObject prefab, string instanceName,
            Vector3 worldPositionMeters, Quaternion worldRotation)
        {
            Transform existing = parent.Find(instanceName);
            if (existing != null) return existing;
            if (prefab == null) return null;

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene) as GameObject;
            if (instance == null) return null;
            Undo.RegisterCreatedObjectUndo(instance, $"创建 {instanceName}");
            instance.name = instanceName;
            instance.transform.SetParent(parent, true);
            instance.transform.SetPositionAndRotation(worldPositionMeters, worldRotation);
            return instance.transform;
        }

        /// <summary>
        /// 创建只作视觉参考的距离标记，不参与弹道和 NavMesh 几何收集。
        /// </summary>
        /// <param name="parent">原型内容根节点。</param>
        /// <param name="markerName">场景标记稳定名称；重复执行时不覆盖已有标记。</param>
        /// <param name="worldPositionMeters">标记中心世界坐标，单位为米。</param>
        /// <param name="label">显示的距离文本，例如 10m；不作为实际弹道或伤害距离。</param>
        private static void CreateOrKeepRangeMarker(Transform parent, string markerName, Vector3 worldPositionMeters,
            string label)
        {
            if (parent.Find(markerName) != null) return;

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = markerName;
            Undo.RegisterCreatedObjectUndo(marker, $"创建 {markerName}");
            marker.transform.SetParent(parent, true);
            marker.transform.SetPositionAndRotation(worldPositionMeters, Quaternion.identity);
            marker.transform.localScale = new Vector3(2.2f, 0.04f, 0.18f);
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.layer = CombatLayers.IgnoreRaycastLayer;
            marker.isStatic = true;

            TextMesh text = new GameObject("Label", typeof(TextMesh)).GetComponent<TextMesh>();
            text.transform.SetParent(marker.transform, false);
            text.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            text.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            text.text = label;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.22f;
            text.fontSize = 48;
            text.color = new Color(0.95f, 0.82f, 0.25f, 1f);
            CombatLayers.SetLayerRecursively(marker, CombatLayers.IgnoreRaycastLayer);
        }

        /// <summary>
        /// 为新增高掩体创建左右两个可抢占的 Cover Anchor，并把完整锚点集合重新交给现有 Bot。
        /// </summary>
        /// <param name="combatSliceRoot">CombatSlice 场景根节点；用于找到 Bot 和统一的战术掩体根节点。</param>
        /// <param name="highCover">高掩体实例；为空时不创建锚点，避免产生与实际掩体脱节的 AI 目标。</param>
        private static void CreateOrKeepCoverAnchors(Transform combatSliceRoot, Transform highCover)
        {
            if (combatSliceRoot == null || highCover == null) return;
            Transform coverRoot = GetOrCreateSceneChild(combatSliceRoot, TacticalCoverRootName, "创建战术掩体根节点");
            Bounds bounds = GetWorldBounds(highCover);
            CreateOrKeepCoverAnchor(coverRoot, "C01_HighCover_East_Left", bounds,
                new Vector3(-0.75f, 0f, -1f), new Vector3(-1.45f, 0f, -0.2f));
            CreateOrKeepCoverAnchor(coverRoot, "C01_HighCover_East_Right", bounds,
                new Vector3(0.75f, 0f, -1f), new Vector3(1.45f, 0f, -0.2f));

            CombatCoverPoint[] allPoints = combatSliceRoot.GetComponentsInChildren<CombatCoverPoint>(true);
            foreach (CombatBotController bot in combatSliceRoot.GetComponentsInChildren<CombatBotController>(true))
            {
                // Cover Anchor 数组属于场景序列化数据，记录 Undo 与标脏后才能安全保留并支持撤销。
                Undo.RecordObject(bot, "更新 Bot 掩体锚点");
                bot.SetCoverPoints(allPoints);
                EditorUtility.SetDirty(bot);
            }
        }

        /// <summary>
        /// 创建单个 Cover Anchor。
        /// </summary>
        /// <param name="parent">战术掩体根节点。</param>
        /// <param name="anchorName">稳定锚点名称；已存在时保留关卡人员调整的坐标。</param>
        /// <param name="coverBounds">掩体世界包围盒；用于把锚点放在真实碰撞体外侧。</param>
        /// <param name="coverOffsetMeters">相对于掩体中心的藏身位置偏移，单位为米。</param>
        /// <param name="peekOffsetMeters">相对于掩体中心的探头位置偏移，单位为米。</param>
        private static void CreateOrKeepCoverAnchor(Transform parent, string anchorName, Bounds coverBounds,
            Vector3 coverOffsetMeters, Vector3 peekOffsetMeters)
        {
            if (parent.Find(anchorName) != null) return;

            GameObject anchor = new GameObject(anchorName);
            Undo.RegisterCreatedObjectUndo(anchor, $"创建 {anchorName}");
            anchor.transform.SetParent(parent, true);
            CombatCoverPoint point = anchor.AddComponent<CombatCoverPoint>();
            Vector3 coverPosition = coverBounds.center + coverOffsetMeters;
            Vector3 peekPosition = coverBounds.center + peekOffsetMeters;
            point.SetPositions(coverPosition, peekPosition);
        }

        /// <summary>
        /// 获取实例全部 Renderer/Collider 的世界包围盒，优先反映 Prefab 的真实占用范围。
        /// </summary>
        /// <param name="target">需要计算包围盒的场景实例。</param>
        /// <returns>目标包围盒；未找到 Renderer/Collider 时返回以 Transform 为中心的单位包围盒。</returns>
        private static Bounds GetWorldBounds(Transform target)
        {
            Collider[] colliders = target.GetComponentsInChildren<Collider>();
            if (colliders.Length > 0)
            {
                Bounds bounds = colliders[0].bounds;
                for (int index = 1; index < colliders.Length; index++) bounds.Encapsulate(colliders[index].bounds);
                return bounds;
            }

            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
                return bounds;
            }

            return new Bounds(target.position, Vector3.one);
        }

        /// <summary>
        /// 确保 AssetDatabase 中存在目标目录及其父目录。
        /// </summary>
        /// <param name="assetPath">Unity 项目相对目录路径，例如 Assets/_ProjectSun/Prefabs/Environment。</param>
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
