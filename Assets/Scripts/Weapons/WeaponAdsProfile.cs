using UnityEngine;

namespace ProjectSun.FPS.Weapons
{
    /// <summary>
    /// Per-weapon first-person ADS calibration. It contains presentation data only;
    /// weapon spread, hit detection and obstruction rules remain in the weapon simulation.
    /// </summary>
    [CreateAssetMenu(fileName = "ADS_Weapon", menuName = "Project Sun/Weapons/ADS Profile")]
    public sealed class WeaponAdsProfile : ScriptableObject
    {
        [Header("Sight Alignment")]
        [SerializeField, Min(0.01f)] private float sightDistance = 0.18f;
        [SerializeField, Min(1f)] private float zeroDistance = 25f;
        [Tooltip("Model-space correction from the authored Aim Anchor to the actual visual sight centre.")]
        [SerializeField] private Vector3 sightReferenceLocalOffset;
        [Tooltip("Small camera-space adjustment after automatic sight alignment.")]
        [SerializeField] private Vector3 cameraSpacePositionOffset;
        [Tooltip("Small camera-space adjustment after automatic barrel-axis alignment.")]
        [SerializeField] private Vector3 cameraSpaceRotationOffset;
        [SerializeField] private bool visualSightPlacementReviewed;
        [Header("Hip Presentation")]
        [Tooltip("Camera-space offset applied only while the weapon is in its hip-fire presentation pose. Positive Z moves the complete viewmodel farther forward from the camera.")]
        [SerializeField] private Vector3 hipCameraSpacePositionOffset = new Vector3(0f, 0f, 0.18f);
        [Tooltip("Camera-space rotation applied only while the weapon is in its hip-fire presentation pose.")]
        [SerializeField] private Vector3 hipCameraSpaceRotationOffset;
        [Header("Presentation")]
        [SerializeField, Min(1f)] private float transitionSpeed = 14f;
        [SerializeField, Range(1f, 30f)] private float fovReduction = 12f;

        public float SightDistance => sightDistance;
        public float ZeroDistance => zeroDistance;
        public Vector3 SightReferenceLocalOffset => sightReferenceLocalOffset;
        public Vector3 CameraSpacePositionOffset => cameraSpacePositionOffset;
        public Vector3 CameraSpaceRotationOffset => cameraSpaceRotationOffset;
        public bool VisualSightPlacementReviewed => visualSightPlacementReviewed;
        public Vector3 HipCameraSpacePositionOffset => hipCameraSpacePositionOffset;
        public Vector3 HipCameraSpaceRotationOffset => hipCameraSpaceRotationOffset;
        public float TransitionSpeed => transitionSpeed;
        public float FovReduction => fovReduction;

        public void ConfigureDefaults(float distance, float speed, float fov)
        {
            sightDistance = Mathf.Max(0.01f, distance);
            transitionSpeed = Mathf.Max(1f, speed);
            fovReduction = Mathf.Clamp(fov, 1f, 30f);
            sightReferenceLocalOffset = Vector3.zero;
            cameraSpacePositionOffset = Vector3.zero;
            cameraSpaceRotationOffset = Vector3.zero;
            visualSightPlacementReviewed = false;
            hipCameraSpacePositionOffset = new Vector3(0f, 0f, 0.18f);
            hipCameraSpaceRotationOffset = Vector3.zero;
        }
    }
}
