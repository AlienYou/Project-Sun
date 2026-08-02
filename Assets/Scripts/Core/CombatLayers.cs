using UnityEngine;

namespace ProjectSun.FPS.Core
{
    /// <summary>Central layer policy for prototype combat queries. Reuses the project layers supplied by the imported FPS packs.</summary>
    public static class CombatLayers
    {
        public static int WallLayer => Resolve("Wall", 8);
        public static int ViewmodelLayer => Resolve("First Person View", 9);
        public static int CharacterLayer => Resolve("Character", 10);
        public static int IgnoreRaycastLayer => Resolve("Ignore Raycast", 2);
        public static int BallisticMask => (1 << WallLayer) | (1 << CharacterLayer);
        public static int WallMask => 1 << WallLayer;

        public static void SetLayerRecursively(GameObject target, int layer)
        {
            if (target == null) return;
            target.layer = layer;
            foreach (Transform child in target.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        public static void ApplyCombatSliceLayers(Transform combatSliceRoot)
        {
            if (combatSliceRoot == null) return;
            Transform environment = combatSliceRoot.Find("Environment");
            if (environment != null) SetLayerRecursively(environment.gameObject, WallLayer);

            foreach (Health health in combatSliceRoot.GetComponentsInChildren<Health>(true))
                SetLayerRecursively(health.gameObject, CharacterLayer);
        }

        private static int Resolve(string layerName, int fallback)
        {
            int layer = LayerMask.NameToLayer(layerName);
            return layer >= 0 ? layer : fallback;
        }
    }
}
