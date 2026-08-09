using UnityEngine;

namespace ProjectSun.FPS.Presentation
{
    /// <summary>
    /// Authored physical aperture for a magnified first-person optic. This is deliberately separate
    /// from the ADS Aim Anchor: moving the lens must never change weapon alignment or ballistics.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ViewmodelScopeLens : MonoBehaviour
    {
        [SerializeField, Range(0.005f, 0.15f)] private float clearApertureDiameter = 0.045f;
        [SerializeField] private bool orientationAuthored;
        [SerializeField] private bool visualPlacementReviewed;

        public float ClearApertureDiameter => clearApertureDiameter;
        public bool OrientationAuthored => orientationAuthored;
        public bool VisualPlacementReviewed => visualPlacementReviewed;

        public void Configure(float diameter)
        {
            clearApertureDiameter = Mathf.Clamp(diameter, 0.005f, 0.15f);
        }

        public void SetOrientationAuthored(bool authored)
        {
            orientationAuthored = authored;
        }

        public void SetVisualPlacementReviewed(bool reviewed)
        {
            visualPlacementReviewed = reviewed;
        }

        private void OnValidate()
        {
            clearApertureDiameter = Mathf.Clamp(clearApertureDiameter, 0.005f, 0.15f);
        }
    }
}
