using ProjectSun.FPS.Input;
using ProjectSun.FPS.Player;
using UnityEngine;

namespace ProjectSun.FPS.Weapons
{
    /// <summary>
    /// Client-side weapon presentation: camera kick, first-person weapon recoil, ADS transition and a reusable muzzle light.
    /// It intentionally consumes weapon events only; authoritative weapon simulation remains in HitscanWeapon.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponFeedbackController : MonoBehaviour
    {
        [Header("View Kick")]
        [SerializeField, Min(0f)] private float verticalKick = 0.55f;
        [SerializeField, Min(0f)] private float horizontalKick = 0.18f;
        [Header("Viewmodel")]
        [SerializeField, Min(1f)] private float returnSpeed = 18f;
        [SerializeField, Min(1f)] private float aimTransitionSpeed = 14f;
        [SerializeField, Range(1f, 30f)] private float adsFovReduction = 12f;
        [SerializeField] private Vector3 adsPositionOffset = new Vector3(-0.11f, 0.055f, -0.17f);
        [SerializeField] private Vector3 adsRotationOffset = new Vector3(-2f, 0f, 0f);

        private HitscanWeapon weapon;
        private FpsPlayerController player;
        private FpsInput input;
        private Camera viewCamera;
        private Transform weaponVisual;
        private Transform muzzle;
        private Vector3 hipPosition;
        private Quaternion hipRotation;
        private float visualKick;
        private float aimAmount;
        private Light muzzleLight;
        private float muzzleLightUntil;

        public void Configure(HitscanWeapon hitscanWeapon, FpsPlayerController controller, Camera camera,
            Transform visual, Transform muzzleTransform)
        {
            if (weapon != null) weapon.Fired -= OnWeaponFired;
            weapon = hitscanWeapon;
            player = controller;
            input = player != null ? player.Input : null;
            viewCamera = camera;
            weaponVisual = visual;
            muzzle = muzzleTransform;
            if (weapon != null) weapon.Fired += OnWeaponFired;

            if (weaponVisual != null)
            {
                hipPosition = weaponVisual.localPosition;
                hipRotation = weaponVisual.localRotation;
            }
            EnsureMuzzleLight();
        }

        private void OnDestroy()
        {
            if (weapon != null) weapon.Fired -= OnWeaponFired;
        }

        private void LateUpdate()
        {
            if (weapon == null || weaponVisual == null || viewCamera == null) return;
            float delta = Time.deltaTime;
            float targetAim = weapon.IsAiming ? 1f : 0f;
            aimAmount = Mathf.MoveTowards(aimAmount, targetAim, aimTransitionSpeed * delta);
            visualKick = Mathf.MoveTowards(visualKick, 0f, returnSpeed * delta);

            Vector3 targetPosition = hipPosition + adsPositionOffset * aimAmount + Vector3.back * visualKick * 0.045f;
            Quaternion targetRotation = hipRotation * Quaternion.Euler(adsRotationOffset * aimAmount + Vector3.left * visualKick * 5f);
            float smoothing = 1f - Mathf.Exp(-returnSpeed * delta);
            weaponVisual.localPosition = Vector3.Lerp(weaponVisual.localPosition, targetPosition, smoothing);
            weaponVisual.localRotation = Quaternion.Slerp(weaponVisual.localRotation, targetRotation, smoothing);

            float baseFov = input != null ? input.FieldOfView : 78f;
            float targetFov = baseFov - adsFovReduction * aimAmount;
            viewCamera.fieldOfView = Mathf.Lerp(viewCamera.fieldOfView, targetFov, 1f - Mathf.Exp(-aimTransitionSpeed * delta));
            if (muzzleLight != null && muzzleLight.enabled && Time.time >= muzzleLightUntil)
                muzzleLight.enabled = false;
        }

        private void OnWeaponFired()
        {
            if (player != null)
                player.AddViewKick(verticalKick, Random.Range(-horizontalKick, horizontalKick));
            visualKick = Mathf.Min(1.5f, visualKick + 1f);
            if (muzzleLight != null)
            {
                muzzleLight.enabled = true;
                muzzleLightUntil = Time.time + 0.045f;
            }
        }

        private void EnsureMuzzleLight()
        {
            if (muzzle == null || muzzleLight != null) return;
            GameObject lightObject = new GameObject("Muzzle Flash Light", typeof(Light));
            lightObject.transform.SetParent(muzzle, false);
            Light created = lightObject.GetComponent<Light>();
            created.type = LightType.Point;
            created.color = new Color(1f, 0.58f, 0.16f);
            created.intensity = 3.5f;
            created.range = 3f;
            created.enabled = false;
            muzzleLight = created;
        }
    }
}
