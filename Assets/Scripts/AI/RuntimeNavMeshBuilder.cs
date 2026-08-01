using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectSun.FPS.AI
{
    /// <summary>Builds navigation from the editable greybox at startup, keeping the prototype independent of editor-baked data.</summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class RuntimeNavMeshBuilder : MonoBehaviour
    {
        [SerializeField] private Transform navigationRoot;
        [SerializeField, Min(0f)] private float boundsPadding = 4f;

        private NavMeshDataInstance navMeshInstance;
        private bool hasNavMesh;

        public void SetNavigationRoot(Transform root) => navigationRoot = root;

        private void Awake() => Build();

        private void OnDestroy()
        {
            if (hasNavMesh) navMeshInstance.Remove();
        }

        public void Build()
        {
            if (hasNavMesh)
            {
                navMeshInstance.Remove();
                hasNavMesh = false;
            }
            if (navigationRoot == null)
            {
                Debug.LogError("RuntimeNavMeshBuilder needs an Environment root.", this);
                return;
            }

            List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(navigationRoot, ~0, NavMeshCollectGeometry.RenderMeshes, 0,
                new List<NavMeshBuildMarkup>(), sources);
            if (sources.Count == 0)
            {
                Debug.LogError("No navigable render geometry was found under the Environment root.", this);
                return;
            }

            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, CalculateBounds(), Vector3.zero, Quaternion.identity);
            if (data == null)
            {
                Debug.LogError("Runtime NavMesh generation failed.", this);
                return;
            }

            navMeshInstance = NavMesh.AddNavMeshData(data);
            hasNavMesh = true;
        }

        private Bounds CalculateBounds()
        {
            Renderer[] renderers = navigationRoot.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(navigationRoot.position, Vector3.one);
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            bounds.Expand(boundsPadding * 2f);
            return bounds;
        }
    }
}
