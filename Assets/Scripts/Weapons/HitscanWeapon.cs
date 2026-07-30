using System;
using ProjectSun.FPS.Core;
using UnityEngine;

namespace ProjectSun.FPS.Weapons
{
    /// <summary>Automatic hitscan weapon. Damage, cadence, ammo and spread all come from the current loadout.</summary>
    public sealed class HitscanWeapon : MonoBehaviour
    {
        [SerializeField] private WeaponLoadout loadout = new WeaponLoadout();
        [SerializeField] private WeaponStats fallbackStats = WeaponStats.Carbine;

        private Camera viewCamera;
        private Transform muzzle;
        private WeaponStats stats;
        private int ammoInMagazine;
        private bool reloading;
        private float nextFireTime;
        private float damageMultiplier = 1f;
        private float spreadMultiplier = 1f;
        private bool gameplayInputEnabled = true;

        public WeaponStats Stats => stats;
        public WeaponLoadout Loadout => loadout;
        public int AmmoInMagazine => ammoInMagazine;
        public bool IsReloading => reloading;
        public bool IsAiming { get; private set; }
        public float ReloadProgress { get; private set; }

        public event Action<RaycastHit> HitConfirmed;
        public event Action Fired;

        public void Configure(Camera playerCamera, Transform muzzleTransform)
        {
            viewCamera = playerCamera;
            muzzle = muzzleTransform;
            RefreshLoadout();
        }

        public void RefreshLoadout()
        {
            stats = loadout.BuildStats(fallbackStats);
            ammoInMagazine = Mathf.Clamp(ammoInMagazine, 0, stats.magazineSize);
            if (ammoInMagazine == 0 && !reloading)
                ammoInMagazine = stats.magazineSize;
        }

        public void SetAbilityModifiers(float newDamageMultiplier, float newSpreadMultiplier)
        {
            damageMultiplier = Mathf.Max(0f, newDamageMultiplier);
            spreadMultiplier = Mathf.Max(0f, newSpreadMultiplier);
        }

        public void SetGameplayInputEnabled(bool enabled)
        {
            gameplayInputEnabled = enabled;
            if (!enabled) IsAiming = false;
        }

        private void Awake()
        {
            stats = loadout.BuildStats(fallbackStats);
            ammoInMagazine = stats.magazineSize;
        }

        private void Update()
        {
            if (viewCamera == null || !gameplayInputEnabled) return;

            IsAiming = Input.GetMouseButton(1) && !reloading;
            if (Input.GetKeyDown(KeyCode.R) && ammoInMagazine < stats.magazineSize)
                StartReload();

            if (Input.GetMouseButton(0) && Time.time >= nextFireTime)
                Fire();
        }

        public bool TryEquip(WeaponAttachment attachment)
        {
            if (reloading || attachment == null) return false;
            loadout.Equip(attachment);
            RefreshLoadout();
            return true;
        }

        private void Fire()
        {
            if (reloading) return;
            if (ammoInMagazine <= 0)
            {
                StartReload();
                return;
            }

            ammoInMagazine--;
            nextFireTime = Time.time + 1f / stats.roundsPerSecond;
            Fired?.Invoke();

            float spread = (IsAiming ? stats.aimSpread : stats.hipSpread) * spreadMultiplier;
            Vector2 random = UnityEngine.Random.insideUnitCircle * spread;
            Vector3 direction = viewCamera.transform.forward;
            direction = Quaternion.AngleAxis(random.x, viewCamera.transform.up) * direction;
            direction = Quaternion.AngleAxis(random.y, viewCamera.transform.right) * direction;

            Ray ray = new Ray(viewCamera.transform.position, direction);
            Vector3 endPoint = ray.GetPoint(stats.range);
            if (Physics.Raycast(ray, out RaycastHit hit, stats.range, ~0, QueryTriggerInteraction.Ignore))
            {
                endPoint = hit.point;
                DealDamage(hit, direction);
                HitConfirmed?.Invoke(hit);
            }
            ShotTracer.Spawn(muzzle != null ? muzzle.position : ray.origin, endPoint);
        }

        private void DealDamage(RaycastHit hit, Vector3 direction)
        {
            MonoBehaviour[] components = hit.collider.GetComponentsInParent<MonoBehaviour>();
            foreach (MonoBehaviour component in components)
            {
                if (component is IDamageable damageable)
                {
                    damageable.ApplyDamage(new DamageInfo(stats.damage * damageMultiplier, hit.point, direction, gameObject));
                    return;
                }
            }
        }

        private void StartReload()
        {
            if (!reloading && ammoInMagazine < stats.magazineSize)
                StartCoroutine(ReloadRoutine());
        }

        private System.Collections.IEnumerator ReloadRoutine()
        {
            reloading = true;
            ReloadProgress = 0f;
            float elapsed = 0f;
            while (elapsed < stats.reloadSeconds)
            {
                elapsed += Time.deltaTime;
                ReloadProgress = Mathf.Clamp01(elapsed / stats.reloadSeconds);
                yield return null;
            }
            ammoInMagazine = stats.magazineSize;
            ReloadProgress = 1f;
            reloading = false;
        }
    }

    internal sealed class ShotTracer : MonoBehaviour
    {
        private const float Lifetime = 0.045f;
        private float remaining;

        public static void Spawn(Vector3 start, Vector3 end)
        {
            GameObject lineObject = new GameObject("Shot Tracer");
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            line.startColor = new Color(1f, 0.78f, 0.25f, 0.9f);
            line.endColor = new Color(1f, 0.78f, 0.25f, 0f);
            line.startWidth = 0.025f;
            line.endWidth = 0.004f;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            ShotTracer tracer = lineObject.AddComponent<ShotTracer>();
            tracer.remaining = Lifetime;
        }

        private void Update()
        {
            remaining -= Time.deltaTime;
            if (remaining <= 0f) Destroy(gameObject);
        }
    }
}
