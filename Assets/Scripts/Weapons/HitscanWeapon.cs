using System;
using ProjectSun.FPS.Core;
using ProjectSun.FPS.Input;
using ProjectSun.FPS.UI;
using UnityEngine;

namespace ProjectSun.FPS.Weapons
{
    /// <summary>
    /// Automatic hitscan weapon. The camera chooses the intended aim point, then a second ray from the
    /// muzzle validates the physical path. This preserves screen-centre aiming without allowing a muzzle
    /// hidden behind cover to damage a target.
    /// </summary>
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
        private int reloadRevision;
        private float nextFireTime;
        private float damageMultiplier = 1f;
        private float spreadMultiplier = 1f;
        private bool gameplayInputEnabled = true;
        private bool loadoutEditingEnabled = true;
        private readonly RaycastHit[] ballisticHits = new RaycastHit[16];

        public WeaponStats Stats => stats;
        public WeaponLoadout Loadout => loadout;
        public Camera ViewCamera => viewCamera;
        public int AmmoInMagazine => ammoInMagazine;
        public bool IsReloading => reloading;
        public bool IsAiming { get; private set; }
        public float ReloadProgress { get; private set; }
        public bool LoadoutEditingEnabled => loadoutEditingEnabled;
        public bool GameplayInputEnabled => gameplayInputEnabled;

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

        /// <summary>Changes the primary weapon only while the match has enabled pre-round editing.</summary>
        public bool SetWeaponDefinition(WeaponDefinition definition)
        {
            if (!loadoutEditingEnabled || definition == null) return false;
            loadout.SetWeapon(definition);
            RefreshLoadout();
            return true;
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

        /// <summary>
        /// Controlled by the round authority. It protects the gameplay API as well as the visible
        /// loadout menu, so active-round edits cannot change a weapon's replicated state later.
        /// </summary>
        public void SetLoadoutEditingEnabled(bool enabled) => loadoutEditingEnabled = enabled;

        /// <summary>Updates the ballistic origin after an inventory switch without rebuilding weapon state.</summary>
        public void SetMuzzle(Transform muzzleTransform) => muzzle = muzzleTransform;

        private void Awake()
        {
            stats = loadout.BuildStats(fallbackStats);
            ammoInMagazine = stats.magazineSize;
            input = GetComponent<FpsInput>();
        }

        private void Update()
        {
            if (viewCamera == null || input == null || !gameplayInputEnabled || !input.GameplayEnabled) return;

            IsAiming = SupportsAds() && input.IsHeld(FpsBinding.Aim) && !reloading;
            if (input.WasPressed(FpsBinding.Reload) && ammoInMagazine < stats.magazineSize)
                StartReload();

            bool fireRequested = loadout.Weapon == null || loadout.Weapon.automatic
                ? input.IsHeld(FpsBinding.Fire)
                : input.WasPressed(FpsBinding.Fire);
            if (fireRequested && Time.time >= nextFireTime)
                Fire();
        }

        public bool TryEquip(WeaponAttachment attachment)
        {
            if (!loadoutEditingEnabled || reloading || attachment == null) return false;
            loadout.Equip(attachment);
            RefreshLoadout();
            return true;
        }

        public bool TryUnequip(AttachmentSlot slot)
        {
            if (!loadoutEditingEnabled || reloading) return false;
            loadout.Unequip(slot);
            RefreshLoadout();
            return true;
        }

        /// <summary>Applies an authored primary-slot configuration without exposing mutable weapon state to the UI.</summary>
        public bool TryApplyLoadout(WeaponLoadout configuredLoadout)
        {
            if (!loadoutEditingEnabled || reloading || configuredLoadout == null) return false;
            loadout.CopyFrom(configuredLoadout);
            RefreshLoadout();
            return true;
        }

        /// <summary>
        /// Inventory-only state transition. It intentionally does not consult the pre-round edit lock:
        /// selecting an already locked secondary during an active round must remain legal.
        /// </summary>
        public void ApplyRuntimeLoadout(WeaponLoadout configuredLoadout, int savedAmmoInMagazine)
        {
            if (configuredLoadout == null) return;
            reloading = false;
            reloadRevision++;
            ReloadProgress = 0f;
            IsAiming = false;
            loadout.CopyFrom(configuredLoadout);
            stats = loadout.BuildStats(fallbackStats);
            ammoInMagazine = savedAmmoInMagazine < 0
                ? stats.magazineSize
                : Mathf.Clamp(savedAmmoInMagazine, 0, stats.magazineSize);
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

            Ray aimRay = new Ray(viewCamera.transform.position, direction);
            bool aimHasHit = TryGetFirstBallisticHit(aimRay, stats.range, out RaycastHit aimHit);
            Vector3 aimPoint = aimHasHit ? aimHit.point : aimRay.GetPoint(stats.range);

            Vector3 muzzleOrigin = muzzle != null ? muzzle.position : aimRay.origin;
            Vector3 muzzleToAimPoint = aimPoint - muzzleOrigin;
            float muzzleDistance = muzzleToAimPoint.magnitude;
            Ray muzzleRay = muzzleDistance > 0.001f
                ? new Ray(muzzleOrigin, muzzleToAimPoint / muzzleDistance)
                : aimRay;
            bool muzzleHasHit = TryGetFirstBallisticHit(muzzleRay, muzzleDistance, out RaycastHit muzzleHit);

            Vector3 endPoint = muzzleHasHit ? muzzleHit.point : aimPoint;
            bool dealtDamage = false;
            if (muzzleHasHit)
            {
                WeaponImpactEffect.Spawn(muzzleHit.point, muzzleHit.normal);
                dealtDamage = DealDamage(muzzleHit, muzzleRay.direction);
                if (dealtDamage) HitConfirmed?.Invoke(muzzleHit);
            }

            CombatRayOutcome aimOutcome = !aimHasHit
                ? CombatRayOutcome.Miss
                : aimHit.collider.gameObject.layer == CombatLayers.WallLayer
                    ? CombatRayOutcome.Blocked
                    : CombatRayOutcome.Visible;
            CombatRayDebugOverlay.Record("PLAYER AIM", aimRay, aimHasHit, aimHit, aimOutcome);
            CombatRayDebugOverlay.Record("PLAYER MUZZLE", muzzleRay, muzzleHasHit, muzzleHit,
                dealtDamage ? CombatRayOutcome.DamageApplied : muzzleHasHit ? CombatRayOutcome.Blocked : CombatRayOutcome.Miss);
            ShotTracer.Spawn(muzzleOrigin, endPoint);
        }

        private bool TryGetFirstBallisticHit(Ray ray, float maxDistance, out RaycastHit closestHit)
        {
            closestHit = default;
            if (maxDistance <= 0f) return false;

            int hitCount = Physics.RaycastNonAlloc(ray, ballisticHits, maxDistance, CombatLayers.BallisticMask,
                QueryTriggerInteraction.Ignore);
            float closestDistance = float.PositiveInfinity;
            bool found = false;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit candidate = ballisticHits[index];
                if (candidate.collider == null || candidate.collider.transform.IsChildOf(transform)) continue;
                if (candidate.distance >= closestDistance) continue;
                closestDistance = candidate.distance;
                closestHit = candidate;
                found = true;
            }
            return found;
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
                StartCoroutine(ReloadRoutine(++reloadRevision));
        }

        private bool SupportsAds() => loadout.Weapon == null || loadout.Weapon.SupportsAds;

        private System.Collections.IEnumerator ReloadRoutine(int revision)
        {
            reloading = true;
            ReloadProgress = 0f;
            float elapsed = 0f;
            while (elapsed < stats.reloadSeconds)
            {
                if (revision != reloadRevision) yield break;
                elapsed += Time.deltaTime;
                ReloadProgress = Mathf.Clamp01(elapsed / stats.reloadSeconds);
                yield return null;
            }
            if (revision != reloadRevision) yield break;
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
