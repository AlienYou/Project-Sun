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
            if (!EnsureAimAnchorsOnViewmodelPrefab()) return;
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
