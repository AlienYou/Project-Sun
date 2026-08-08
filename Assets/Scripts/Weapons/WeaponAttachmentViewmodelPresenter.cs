using System.Collections.Generic;
using UnityEngine;

namespace ProjectSun.FPS.Weapons
{
    /// <summary>
    /// Projects data-driven attachments onto the currently equipped first-person weapon. The weapon
    /// loadout remains authoritative; this component owns only instantiated cosmetic children.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponAttachmentViewmodelPresenter : MonoBehaviour
    {
        private readonly List<GameObject> spawnedVisuals = new List<GameObject>();
        private readonly List<GameObject> hiddenBuiltInVisuals = new List<GameObject>();

        /// <summary>Applies one active loadout and returns the sight reference that runtime ADS should use.</summary>
        public Transform Apply(WeaponLoadout loadout, Transform weaponRoot, Transform defaultAimAnchor,
            bool transientPreview = false)
        {
            Clear();
            if (loadout == null || loadout.Weapon == null || weaponRoot == null) return defaultAimAnchor;

            Transform resolvedAimAnchor = defaultAimAnchor;
            foreach (WeaponAttachment attachment in loadout.Attachments)
            {
                if (attachment == null || !attachment.TryGetViewmodelVisual(loadout.Weapon, out WeaponAttachmentViewmodelVisual binding))
                    continue;
                Transform mount = FindDescendant(weaponRoot, binding.MountName);
                if (mount == null)
                {
                    Debug.LogWarning($"{attachment.displayName} cannot find its viewmodel mount '{binding.MountName}' on {loadout.Weapon.displayName}.", this);
                    continue;
                }

                GameObject visual = Instantiate(binding.Prefab, mount, false);
                visual.name = attachment.displayName + " (Attachment Visual)";
                SetLayerRecursively(visual.transform, weaponRoot.gameObject.layer);
                if (transientPreview) SetHideFlagsRecursively(visual.transform, HideFlags.HideAndDontSave);
                spawnedVisuals.Add(visual);

                GameObject replaced = FindDescendant(weaponRoot, binding.ReplacedBuiltInVisualName)?.gameObject;
                if (replaced != null && replaced.activeSelf)
                {
                    replaced.SetActive(false);
                    hiddenBuiltInVisuals.Add(replaced);
                }
                if (attachment.slot == AttachmentSlot.Optic && !string.IsNullOrWhiteSpace(binding.AimAnchorName))
                    resolvedAimAnchor = FindDescendant(visual.transform, binding.AimAnchorName) ?? resolvedAimAnchor;
            }
            return resolvedAimAnchor;
        }

        public void Clear()
        {
            foreach (GameObject visual in spawnedVisuals)
                if (visual != null)
                {
                    visual.SetActive(false);
                    if (Application.isPlaying) Destroy(visual);
                    else DestroyImmediate(visual);
                }
            spawnedVisuals.Clear();

            foreach (GameObject builtInVisual in hiddenBuiltInVisuals)
                if (builtInVisual != null)
                    builtInVisual.SetActive(true);
            hiddenBuiltInVisuals.Clear();
        }

        private void OnDestroy() => Clear();

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName)) return null;
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == objectName) return candidate;
            return null;
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            if (root == null) return;
            root.gameObject.layer = layer;
            foreach (Transform child in root) SetLayerRecursively(child, layer);
        }

        private static void SetHideFlagsRecursively(Transform root, HideFlags flags)
        {
            if (root == null) return;
            root.gameObject.hideFlags = flags;
            foreach (Transform child in root) SetHideFlagsRecursively(child, flags);
        }
    }
}
