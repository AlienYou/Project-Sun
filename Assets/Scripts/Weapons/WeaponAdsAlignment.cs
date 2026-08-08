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
            if (visualRoot == null || visualRoot.parent == null || sightReference == null ||
                viewCamera == null || profile == null) return false;

            // The frame comes from the live rig so sight alignment remains valid if an ADS animation
            // moves the weapon below the viewmodel root. It does not rely on third-party bone axes.
            Vector3 sightPositionLocal = visualRoot.InverseTransformPoint(
                GetSightReferenceWorldPosition(sightReference, profile));
            if (!TryGetSightFrameLocalRotation(visualRoot, sightReference, muzzle,
                    out Quaternion sightFrameLocalRotation)) return false;

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

        /// <summary>
        /// Returns the legacy muzzle-derived frame so an editor migration can bake the current visual
        /// result into the Aim Anchor rotation before enabling the authored orientation contract.
        /// </summary>
        public static bool TryGetLegacySightFrameWorldRotation(Transform visualRoot, Transform sightReference,
            Transform muzzle, out Quaternion worldRotation)
        {
            worldRotation = Quaternion.identity;
            if (!TryGetLegacySightFrameLocalRotation(visualRoot, sightReference, muzzle,
                    out Quaternion localRotation)) return false;
            worldRotation = visualRoot.rotation * localRotation;
            return true;
        }

        private static bool TryGetSightFrameLocalRotation(Transform visualRoot, Transform sightReference,
            Transform muzzle, out Quaternion localRotation)
        {
            AdsSightReference authoredReference = sightReference.GetComponent<AdsSightReference>();
            if (authoredReference == null || !authoredReference.OrientationAuthored)
                return TryGetLegacySightFrameLocalRotation(visualRoot, sightReference, muzzle, out localRotation);

            Vector3 localForward = visualRoot.InverseTransformDirection(sightReference.forward);
            Vector3 localUp = visualRoot.InverseTransformDirection(sightReference.up);
            return TryCreateFrame(localForward, localUp, out localRotation);
        }

        private static bool TryGetLegacySightFrameLocalRotation(Transform visualRoot, Transform sightReference,
            Transform muzzle, out Quaternion localRotation)
        {
            localRotation = Quaternion.identity;
            if (visualRoot == null || sightReference == null || muzzle == null) return false;
            Vector3 sightPositionLocal = visualRoot.InverseTransformPoint(sightReference.position);
            Vector3 muzzlePositionLocal = visualRoot.InverseTransformPoint(muzzle.position);
            Vector3 localForward = muzzlePositionLocal - sightPositionLocal;
            Vector3 localUp = Vector3.ProjectOnPlane(Vector3.up, localForward);
            if (localUp.sqrMagnitude < 0.000001f)
                localUp = Vector3.ProjectOnPlane(Vector3.right, localForward);
            return TryCreateFrame(localForward, localUp, out localRotation);
        }

        private static bool TryCreateFrame(Vector3 forward, Vector3 up, out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            if (forward.sqrMagnitude < 0.000001f) return false;
            forward.Normalize();
            up = Vector3.ProjectOnPlane(up, forward);
            if (up.sqrMagnitude < 0.000001f) up = Vector3.ProjectOnPlane(Vector3.right, forward);
            if (up.sqrMagnitude < 0.000001f) return false;
            rotation = Quaternion.LookRotation(forward, up.normalized);
            return true;
        }
    }
}
