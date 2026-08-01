using System;
using ProjectSun.FPS.Player;
using UnityEngine;

namespace ProjectSun.FPS.Rounds
{
    /// <summary>A tactical objective that an attacking player activates by holding the interaction key.</summary>
    [DisallowMultipleComponent]
    public sealed class ObjectiveZone : MonoBehaviour
    {
        [SerializeField] private string siteLabel = "OBJECTIVE";
        [SerializeField, Min(0.5f)] private float activationSeconds = 4f;
        [SerializeField] private Renderer indicator;

        private Material indicatorMaterial;
        private bool playerInside;
        private bool available;
        private bool activated;
        private float activationProgress;

        public string SiteLabel => siteLabel;
        public bool IsPlayerInside => playerInside;
        public bool IsAvailable => available;
        public float ActivationProgress => activationProgress;
        public float ActivationSeconds => activationSeconds;

        public event Action<ObjectiveZone> Activated;

        public void SetPresentation(string label, Renderer targetIndicator)
        {
            siteLabel = label;
            indicator = targetIndicator;
        }

        public void SetAvailable(bool isAvailable)
        {
            available = isAvailable;
            activated = false;
            activationProgress = 0f;
            RefreshIndicator();
        }

        private void Awake()
        {
            if (indicator != null)
                indicatorMaterial = indicator.material;
            RefreshIndicator();
        }

        private void Update()
        {
            if (!available || activated || !playerInside)
            {
                if (!playerInside && activationProgress > 0f)
                {
                    activationProgress = 0f;
                    RefreshIndicator();
                }
                return;
            }

            if (!Input.GetKey(KeyCode.F))
            {
                if (activationProgress > 0f)
                {
                    activationProgress = 0f;
                    RefreshIndicator();
                }
                return;
            }

            activationProgress = Mathf.Min(activationSeconds, activationProgress + Time.deltaTime);
            RefreshIndicator();
            if (activationProgress < activationSeconds) return;

            activated = true;
            available = false;
            RefreshIndicator();
            Activated?.Invoke(this);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<FpsPlayerController>() == null) return;
            playerInside = true;
            RefreshIndicator();
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<FpsPlayerController>() == null) return;
            playerInside = false;
            activationProgress = 0f;
            RefreshIndicator();
        }

        private void RefreshIndicator()
        {
            if (indicatorMaterial == null) return;
            Color color = activated ? new Color(0.25f, 1f, 0.55f) : available
                ? (playerInside ? new Color(1f, 0.75f, 0.15f) : new Color(0.2f, 0.65f, 1f))
                : new Color(0.18f, 0.22f, 0.27f);
            if (indicatorMaterial.HasProperty("_BaseColor")) indicatorMaterial.SetColor("_BaseColor", color);
            else indicatorMaterial.color = color;
        }
    }
}
