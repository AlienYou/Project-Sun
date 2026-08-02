using System.Collections;
using ProjectSun.FPS.Core;
using UnityEngine;

namespace ProjectSun.FPS.World
{
    [RequireComponent(typeof(Health))]
    public sealed class TargetDummy : MonoBehaviour
    {
        [SerializeField] private float respawnDelay = 2.5f;
        [SerializeField] private float idleYawDegreesPerSecond = 24f;
        private Health health;
        private Renderer cachedRenderer;
        private Collider[] colliders;
        private Material material;
        private Color baseColor;

        private void Awake()
        {
            health = GetComponent<Health>();
            cachedRenderer = GetComponentInChildren<Renderer>();
            colliders = GetComponentsInChildren<Collider>();
            if (cachedRenderer != null)
            {
                material = cachedRenderer.material;
                baseColor = ReadColor(material);
            }
            if (health == null)
            {
                Debug.LogError($"{name} requires a Health component. Disable this target until its prefab is repaired.", this);
                enabled = false;
                return;
            }
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (health == null) return;
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        private void Update()
        {
            if (health != null && health.IsAlive)
                transform.Rotate(0f, idleYawDegreesPerSecond * Time.deltaTime, 0f, Space.World);
        }

        private void OnDamaged(DamageInfo _)
        {
            if (material != null) StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            WriteColor(material, Color.white);
            yield return new WaitForSeconds(0.07f);
            if (health.IsAlive) WriteColor(material, baseColor);
        }

        private void OnDied()
        {
            foreach (Collider currentCollider in colliders) currentCollider.enabled = false;
            if (cachedRenderer != null) cachedRenderer.enabled = false;
            StartCoroutine(RespawnRoutine());
        }

        /// <summary>WeaponLab reset hook. It restores a target immediately instead of waiting for its normal respawn timer.</summary>
        public void ResetTarget()
        {
            StopAllCoroutines();
            if (health == null) return;
            health.ResetHealth();
            if (cachedRenderer != null)
            {
                cachedRenderer.enabled = true;
                if (material != null) WriteColor(material, baseColor);
            }
            if (colliders == null) colliders = GetComponentsInChildren<Collider>();
            foreach (Collider currentCollider in colliders)
                if (currentCollider != null) currentCollider.enabled = true;
        }

        /// <summary>Lets authored ranges choose static or rotating target behaviour without a second target implementation.</summary>
        public void SetIdleYawDegreesPerSecond(float degreesPerSecond)
        {
            idleYawDegreesPerSecond = degreesPerSecond;
        }

        private IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(respawnDelay);
            health.ResetHealth();
            if (cachedRenderer != null)
            {
                cachedRenderer.enabled = true;
                WriteColor(material, baseColor);
            }
            foreach (Collider currentCollider in colliders) currentCollider.enabled = true;
        }

        private static Color ReadColor(Material target)
        {
            return target.HasProperty("_BaseColor") ? target.GetColor("_BaseColor") : target.color;
        }

        private static void WriteColor(Material target, Color color)
        {
            if (target.HasProperty("_BaseColor")) target.SetColor("_BaseColor", color);
            else target.color = color;
        }
    }
}
