using UnityEngine;

namespace ProjectSun.FPS.Weapons
{
    /// <summary>Shared ADS pose calculation used by both runtime presentation and the editor workbench.</summary>
    public static class WeaponAdsAlignment
    {
        public static Vector3 GetSightReferenceWorldPosition(Transform authoredAimAnchor, WeaponAdsProfile profile)
        {
            return authoredAimAnchor != null && profile != null
                ? authoredAimAnchor.TransformPoint(profile.SightReferenceLocalOffset)
                : Vector3.zero;
        }

        public static bool TryGetCalibratedPose(Transform visualRoot, Transform sightReference, Transform muzzle,
            Camera viewCamera, WeaponAdsProfile profile, out Vector3 localPosition, out Quaternion localRotation)
        {
            localPosition = default;
            localRotation = default;
            if (visualRoot == null || visualRoot.parent == null || sightReference == null || muzzle == null ||
                viewCamera == null || profile == null) return false;

            // The frame comes from the live rig so sight alignment remains valid if an ADS animation
            // moves the weapon below the viewmodel root. It does not rely on third-party bone axes.
            Vector3 sightPositionLocal = visualRoot.InverseTransformPoint(
                GetSightReferenceWorldPosition(sightReference, profile));
            Vector3 muzzlePositionLocal = visualRoot.InverseTransformPoint(muzzle.position);
            Vector3 localForward = muzzlePositionLocal - sightPositionLocal;
            if (localForward.sqrMagnitude < 0.000001f) return false;

            localForward.Normalize();
            Vector3 localUp = Vector3.ProjectOnPlane(Vector3.up, localForward);
            if (localUp.sqrMagnitude < 0.000001f)
                localUp = Vector3.ProjectOnPlane(Vector3.right, localForward);
            Quaternion sightFrameLocalRotation = Quaternion.LookRotation(localForward, localUp.normalized);

            Transform visualParent = visualRoot.parent;
            Quaternion cameraSpaceOffset = Quaternion.Euler(profile.CameraSpaceRotationOffset);
            Quaternion desiredWorldRotation = viewCamera.transform.rotation * cameraSpaceOffset *
                Quaternion.Inverse(sightFrameLocalRotation);
            Vector3 cameraSpaceSightPosition = new Vector3(
                profile.CameraSpacePositionOffset.x,
                profile.CameraSpacePositionOffset.y,
                profile.SightDistance + profile.CameraSpacePositionOffset.z);
            Vector3 desiredSightPosition = viewCamera.transform.TransformPoint(cameraSpaceSightPosition);
            Vector3 desiredWorldPosition = desiredSightPosition - desiredWorldRotation * sightPositionLocal;
            localPosition = visualParent.InverseTransformPoint(desiredWorldPosition);
            localRotation = Quaternion.Inverse(visualParent.rotation) * desiredWorldRotation;
            return true;
        }
    }
}
