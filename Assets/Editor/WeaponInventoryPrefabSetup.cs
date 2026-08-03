using ProjectSun.FPS.Bootstrap;
using ProjectSun.FPS.Presentation;
using ProjectSun.FPS.Weapons;
using UnityEditor;
using UnityEngine;

namespace ProjectSun.FPS.Editor
{
    /// <summary>Persists the inventory components and their two source-pack weapon rigs on the Player prefab.</summary>
    public static class WeaponInventoryPrefabSetup
    {
        private const string PlayerPrefabPath = "Assets/_ProjectSun/Prefabs/Characters/Player.prefab";
        private const string ViewmodelPrefabPath = "Assets/_ProjectSun/Prefabs/Characters/PFB_FP_Operator_LPSP_AR01.prefab";
        private const string HandgunAdsProfilePath = "Assets/_ProjectSun/Data/Weapons/ADS/ADS_HG3.asset";
        private const string HandgunPresentationProfilePath = "Assets/_ProjectSun/Data/Weapons/Presentation/WPP_HG3.asset";

        [MenuItem("Project Sun/Integrate Player Weapon Inventory", priority = 13)]
        public static void Integrate()
        {
            if (!EnsureAimAnchorsOnViewmodelPrefab() || !EnsureViewmodelClipProbesOnViewmodelPrefab()) return;
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                FpsPlayerInstaller installer = prefabRoot.GetComponent<FpsPlayerInstaller>();
                if (installer == null)
                {
                    Debug.LogError("Player prefab has no FpsPlayerInstaller.");
                    return;
                }

                if (prefabRoot.GetComponent<PlayerMatchLoadout>() == null)
                    prefabRoot.AddComponent<PlayerMatchLoadout>();
                WeaponInventoryController inventory = prefabRoot.GetComponent<WeaponInventoryController>();
                if (inventory == null)
                    inventory = prefabRoot.AddComponent<WeaponInventoryController>();

                inventory.ConfigureViewmodelReferences(prefabRoot.GetComponent<LowPolyShooterViewmodel>());
                AssignDedicatedAimAnchors(prefabRoot, inventory);
                inventory.SetPresentationProfiles(WeaponInventorySlot.Secondary,
                    AssetDatabase.LoadAssetAtPath<WeaponAdsProfile>(HandgunAdsProfilePath),
                    AssetDatabase.LoadAssetAtPath<WeaponPresentationProfile>(HandgunPresentationProfilePath));
                EditorUtility.SetDirty(prefabRoot);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Project Sun", "Player prefab now contains inspectable match-loadout and weapon-inventory components.", "OK");
        }

        [MenuItem("Project Sun/Ensure Per-Weapon Aim Anchors", priority = 15)]
        public static void EnsurePerWeaponAimAnchors()
        {
            if (!EnsureAimAnchorsOnViewmodelPrefab()) return;
            GameObject player = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                WeaponInventoryController inventory = player.GetComponent<WeaponInventoryController>();
                if (inventory == null)
                {
                    Debug.LogError("Player prefab has no WeaponInventoryController. Run Integrate Player Weapon Inventory first.");
                    return;
                }

                inventory.ConfigureViewmodelReferences(player.GetComponent<LowPolyShooterViewmodel>());
                AssignDedicatedAimAnchors(player, inventory);
                EditorUtility.SetDirty(player);
                PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(player);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Project Sun",
                "AR-4 and HG-3 now each use a dedicated Aim Anchor. Use the Weapon Presentation Workbench to make any final visual alignment adjustment.",
                "OK");
        }

        [MenuItem("Project Sun/Ensure Per-Weapon Viewmodel Clip Probes", priority = 16)]
        public static void EnsurePerWeaponViewmodelClipProbes()
        {
            if (!EnsureViewmodelClipProbesOnViewmodelPrefab()) return;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Project Sun",
                "AR-4 and HG-3 now have explicit first-person Clip Probes. The Weapon Presentation Workbench validates these authored points in hip and ADS poses instead of scanning the complete mesh.",
                "OK");
        }

        private static bool EnsureAimAnchorsOnViewmodelPrefab()
        {
            GameObject viewmodel = PrefabUtility.LoadPrefabContents(ViewmodelPrefabPath);
            try
            {
                Transform ar4 = FindDescendant(viewmodel.transform, "P_LPSP_WEP_AR_01");
                Transform hg3 = FindDescendant(viewmodel.transform, "P_LPSP_WEP_Handgun_03");
                Transform ar4Scope = FindDescendant(ar4, "SOCKET_Scope");
                Transform hg3Scope = FindDescendant(hg3, "SOCKET_Scope");
                if (ar4Scope == null || hg3Scope == null)
                {
                    Debug.LogError("The owned first-person viewmodel is missing a weapon scope socket.");
                    return false;
                }

                bool changed = false;
                changed |= EnsureAimAnchor(ar4Scope, "AimAnchor_AR4", "Aim Anchor");
                changed |= EnsureAimAnchor(hg3Scope, "AimAnchor_HG3");
                if (changed)
                {
                    EditorUtility.SetDirty(viewmodel);
                    PrefabUtility.SaveAsPrefabAsset(viewmodel, ViewmodelPrefabPath);
                }
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(viewmodel);
            }
        }

        private static bool EnsureViewmodelClipProbesOnViewmodelPrefab()
        {
            GameObject viewmodel = PrefabUtility.LoadPrefabContents(ViewmodelPrefabPath);
            try
            {
                Transform ar4 = FindDescendant(viewmodel.transform, "P_LPSP_WEP_AR_01");
                Transform hg3 = FindDescendant(viewmodel.transform, "P_LPSP_WEP_Handgun_03");
                Transform ar4Aim = FindDescendant(ar4, "AimAnchor_AR4");
                Transform hg3Aim = FindDescendant(hg3, "AimAnchor_HG3");
                Transform ar4Muzzle = FindDescendant(ar4, "SOCKET_Muzzle");
                Transform hg3Muzzle = FindDescendant(hg3, "SOCKET_Muzzle");
                if (ar4Aim == null || hg3Aim == null || ar4Muzzle == null || hg3Muzzle == null)
                {
                    Debug.LogError("The owned first-person viewmodel is missing the anchors required to seed its Clip Probe contract.");
                    return false;
                }

                bool changed = false;
                // The sight probe is intentionally larger than a point: it represents the visible sight/optic housing.
                // Future optics carry their own probes and are collected automatically when active below this weapon root.
                changed |= EnsureClipProbe(ar4Aim, "ClipProbe_AR4_SightHousing", "AR-4 Sight Housing", 0.026f);
                changed |= EnsureClipProbe(ar4Muzzle, "ClipProbe_AR4_Muzzle", "AR-4 Muzzle", 0.012f);
                changed |= EnsureClipProbe(hg3Aim, "ClipProbe_HG3_SightHousing", "HG-3 Sight Housing", 0.020f);
                changed |= EnsureClipProbe(hg3Muzzle, "ClipProbe_HG3_Muzzle", "HG-3 Muzzle", 0.010f);
                if (changed)
                {
                    EditorUtility.SetDirty(viewmodel);
                    PrefabUtility.SaveAsPrefabAsset(viewmodel, ViewmodelPrefabPath);
                }
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(viewmodel);
            }
        }

        private static bool EnsureAimAnchor(Transform parent, string anchorName, string legacyAnchorName = null)
        {
            Transform canonical = FindDescendant(parent, anchorName);
            Transform legacy = string.IsNullOrEmpty(legacyAnchorName) ? null : FindDescendant(parent, legacyAnchorName);
            if (legacy != null)
            {
                // Keep the historical transform: it was already positioned at the AR-4's actual sight centre.
                // The later canonical placeholder was created at the socket origin and must not replace it.
                if (canonical != null && canonical != legacy) Object.DestroyImmediate(canonical.gameObject);
                legacy.name = anchorName;
                return true;
            }
            if (canonical != null) return false;
            GameObject anchor = new GameObject(anchorName);
            anchor.layer = parent.gameObject.layer;
            anchor.transform.SetParent(parent, false);
            anchor.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            anchor.transform.localScale = Vector3.one;
            return true;
        }

        private static bool EnsureClipProbe(Transform parent, string probeName, string label, float radius)
        {
            Transform existing = FindDescendant(parent, probeName);
            if (existing != null) return false;

            GameObject probeObject = new GameObject(probeName);
            probeObject.layer = parent.gameObject.layer;
            probeObject.transform.SetParent(parent, false);
            probeObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            probeObject.transform.localScale = Vector3.one;
            ViewmodelClipProbe probe = probeObject.AddComponent<ViewmodelClipProbe>();
            probe.Configure(label, radius);
            return true;
        }

        private static void AssignDedicatedAimAnchors(GameObject player, WeaponInventoryController inventory)
        {
            Transform ar4 = FindDescendant(player.transform, "P_LPSP_WEP_AR_01");
            Transform hg3 = FindDescendant(player.transform, "P_LPSP_WEP_Handgun_03");
            inventory.SetAimAnchor(WeaponInventorySlot.Primary, FindDescendant(ar4, "AimAnchor_AR4"));
            inventory.SetAimAnchor(WeaponInventorySlot.Secondary, FindDescendant(hg3, "AimAnchor_HG3"));
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null) return null;
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == objectName) return candidate;
            return null;
        }
    }
}
