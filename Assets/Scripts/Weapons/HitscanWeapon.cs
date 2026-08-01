using System;
using ProjectSun.FPS.Core;
using ProjectSun.FPS.Input;
using ProjectSun.FPS.UI;
using UnityEngine;

namespace ProjectSun.FPS.Weapons
{
    /// <summary>Automatic hitscan weapon. Damage, cadence, ammo and spread all come from the current loadout.</summary>
    public sealed class HitscanWeapon : MonoBehaviour
    {
        [SerializeField] private WeaponLoadout loadout = new WeaponLoadout();
        [SerializeField] private WeaponStats fallbackStats = WeaponStats.Carbine;

        private Camera viewCamera;
        private FpsInput input;
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
            input = GetComponent<FpsInput>();
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

        public void SetWeaponDefinition(WeaponDefinition definition)
        {
            if (definition == null) return;
            loadout.SetWeapon(definition);
            RefreshLoadout();
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
            input = GetComponent<FpsInput>();
        }

        private void Update()
        {
            if (viewCamera == null || input == null || !gameplayInputEnabled || !input.GameplayEnabled) return;

            IsAiming = input.IsHeld(FpsBinding.Aim) && !reloading;
            if (input.WasPressed(FpsBinding.Reload) && ammoInMagazine < stats.magazineSize)
                StartReload();

            if (input.IsHeld(FpsBinding.Fire) && Time.time >= nextFireTime)
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
            if (Physics.Raycast(ray, out RaycastHit hit, stats.range, CombatLayers.BallisticMask, QueryTriggerInteraction.Ignore))
            {
                endPoint = hit.point;
                WeaponImpactEffect.Spawn(hit.point, hit.normal);
                bool dealtDamage = DealDamage(hit, direction);
                CombatRayDebugOverlay.Record("PLAYER", ray, true, hit,
                    dealtDamage ? CombatRayOutcome.DamageApplied : CombatRayOutcome.Blocked);
                if (dealtDamage) HitConfirmed?.Invoke(hit);
            }
            else CombatRayDebugOverlay.Record("PLAYER", ray, false, default, CombatRayOutcome.Miss);
            ShotTracer.Spawn(muzzle != null ? muzzle.position : ray.origin, endPoint);
        }

        private bool DealDamage(RaycastHit hit, Vector3 direction)
        {
            MonoBehaviour[] components = hit.collider.GetComponentsInParent<MonoBehaviour>();
            foreach (MonoBehaviour component in components)
            {
                if (component.gameObject == gameObject) continue;
                if (component is IDamageable damageable)
                {
                    damageable.ApplyDamage(new DamageInfo(stats.damage * damageMultiplier, hit.point, direction, gameObject));
                    return true;
                }
            }
            return false;
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
        private static Material sharedMaterial;
        private float remaining;

        public static void Spawn(Vector3 start, Vector3 end)
        {
            GameObject lineObject = new GameObject("Shot Tracer");
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            if (sharedMaterial == null)
                sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            line.sharedMaterial = sharedMaterial;
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

    /// <summary>Short-lived, dependency-free impact marker for prototype weapon validation.</summary>
    internal sealed class WeaponImpactEffect : MonoBehaviour
    {
        private const float Lifetime = 0.16f;
        private static Material sharedMaterial;
        private float remaining;

        public static void Spawn(Vector3 point, Vector3 normal)
        {
            GameObject impact = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            impact.name = "Weapon Impact";
            impact.layer = CombatLayers.IgnoreRaycastLayer;
            impact.transform.position = point + normal * 0.012f;
            impact.transform.localScale = Vector3.one * 0.075f;
            Collider collider = impact.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Destroy(collider);
            }
            Renderer renderer = impact.GetComponent<Renderer>();
            if (sharedMaterial == null)
            {
                sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
                sharedMaterial.color = new Color(1f, 0.62f, 0.18f, 0.9f);
            }
            renderer.sharedMaterial = sharedMaterial;
            impact.AddComponent<WeaponImpactEffect>().remaining = Lifetime;
        }

        private void Update()
        {
            remaining -= Time.deltaTime;
            transform.localScale *= 0.9f;
            if (remaining <= 0f) Destroy(gameObject);
        }
    }
}
