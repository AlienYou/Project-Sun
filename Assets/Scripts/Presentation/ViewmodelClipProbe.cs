using UnityEngine;

namespace ProjectSun.FPS.Presentation
{
    /// <summary>
    /// An authored first-person presentation constraint. Place it on a visible surface that must stay in
    /// front of the viewmodel camera (for example a rear sight, optic housing, or an attachment body).
    /// Editor validation intentionally evaluates these explicit probes instead of inferring constraints from
    /// a complete render mesh, which may contain intentional behind-camera geometry.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ViewmodelClipProbe : MonoBehaviour
    {
        [SerializeField] private string validationLabel;
        [SerializeField, Min(0.001f)] private float surfaceRadius = 0.012f;
        [SerializeField] private bool participatesInValidation = true;

        public string ValidationLabel => string.IsNullOrWhiteSpace(validationLabel) ? gameObject.name : validationLabel;
        public float SurfaceRadius => Mathf.Max(0.001f, surfaceRadius);
        public bool ParticipatesInValidation => participatesInValidation;

        /// <summary>Used by Project Sun setup tooling when seeding an owned weapon contract.</summary>
        public void Configure(string label, float radius)
        {
            validationLabel = label;
            surfaceRadius = Mathf.Max(0.001f, radius);
            participatesInValidation = true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.72f, 0.12f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, SurfaceRadius);
        }
    }
}
