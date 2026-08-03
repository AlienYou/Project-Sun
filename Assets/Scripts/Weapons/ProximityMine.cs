using System;
using System.Collections.Generic;
using ProjectSun.FPS.Core;
using ProjectSun.FPS.Rounds;
using UnityEngine;

namespace ProjectSun.FPS.Weapons
{
    /// <summary>Round-scoped proximity mine used by the S-1 deployable definition.</summary>
    [DisallowMultipleComponent]
    public sealed class ProximityMine : MonoBehaviour
    {
        private readonly Collider[] triggerHits = new Collider[32];
        private readonly Collider[] blastHits = new Collider[32];
        private readonly HashSet<Health> damagedTargets = new HashSet<Health>();

        private TacticalEquipmentDefinition definition;
        private GameObject owner;
        private TeamCombatant ownerCombatant;
        private float armedAt;
        private float expiresAt;
        private bool resolved;
        private Renderer indicatorRenderer;
        private Action<ProximityMine, bool> resolvedCallback;

        public void Configure(TacticalEquipmentDefinition equipmentDefinition, GameObject instigator,
            Action<ProximityMine, bool> callback)
        {
            definition = equipmentDefinition;
            owner = instigator;
            ownerCombatant = owner != null ? owner.GetComponent<TeamCombatant>() : null;
            armedAt = Time.time + Mathf.Max(0f, definition != null ? definition.armingSeconds : 0f);
            expiresAt = Time.time + Mathf.Max(1f, definition != null ? definition.lifetimeSeconds : 1f);
            resolvedCallback = callback;
            CreateDebugVisual();
        }

        /// <summary>Used by round teardown so an intentional cleanup does not overwrite the fresh HUD state.</summary>
        public void DetachOwner() => resolvedCallback = null;

        private void Update()
        {
            if (resolved || definition == null) return;
            UpdateIndicator();
            if (Time.time >= expiresAt)
            {
                Resolve(false);
                return;
            }
            if (Time.time < armedAt) return;

            Health target = FindHostileInTriggerRadius();
            if (target != null) Detonate(target.transform.position);
        }

        private void OnDestroy()
        {
            // Resetting a round destroys the object from its owner. Notify that owner without
            // recursively scheduling another destruction of the same GameObject.
            if (resolved) return;
            resolved = true;
            resolvedCallback?.Invoke(this, false);
        }

        private Health FindHostileInTriggerRadius()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, definition.triggerRadius, triggerHits,
                1 << CombatLayers.CharacterLayer, QueryTriggerInteraction.Collide);
            for (int index = 0; index < count; index++)
            {
                Collider hit = triggerHits[index];
                if (hit == null) continue;
                Health target = hit.GetComponentInParent<Health>();
                if (!IsHostile(target) || !HasLineOfSight(target)) continue;
                return target;
            }
            return null;
        }

        private bool IsHostile(Health target)
        {
            if (target == null || !target.IsAlive || (owner != null && target.gameObject == owner)) return false;
            TeamCombatant targetCombatant = target.GetComponent<TeamCombatant>();
            return ownerCombatant == null || targetCombatant == null || targetCombatant.Team != ownerCombatant.Team;
        }

        private bool HasLineOfSight(Health target)
        {
            Vector3 origin = transform.position + transform.up * 0.08f;
            Vector3 targetPoint = target.transform.position + Vector3.up * 1f;
            Vector3 offset = targetPoint - origin;
            float distance = offset.magnitude;
            if (distance <= 0.001f) return true;
            if (!Physics.Raycast(origin, offset / distance, out RaycastHit hit, distance, CombatLayers.BallisticMask,
                    QueryTriggerInteraction.Ignore))
                return false;
            return hit.collider.GetComponentInParent<Health>() == target;
        }

        private void Detonate(Vector3 triggerPoint)
        {
            damagedTargets.Clear();
            int count = Physics.OverlapSphereNonAlloc(transform.position, definition.blastRadius, blastHits,
                1 << CombatLayers.CharacterLayer, QueryTriggerInteraction.Collide);
            for (int index = 0; index < count; index++)
            {
                Collider hit = blastHits[index];
                if (hit == null) continue;
                Health target = hit.GetComponentInParent<Health>();
                if (!IsHostile(target) || !HasLineOfSight(target) || !damagedTargets.Add(target)) continue;

                float distance = Vector3.Distance(transform.position, target.transform.position);
                float falloff = 1f - Mathf.Clamp01(distance / definition.blastRadius) * 0.5f;
                Vector3 direction = (target.transform.position - transform.position).normalized;
                target.ApplyDamage(new DamageInfo(definition.damage * falloff, triggerPoint, direction, owner));
            }
            Resolve(true);
        }

        private void Resolve(bool detonated)
        {
            if (resolved) return;
            resolved = true;
            resolvedCallback?.Invoke(this, detonated);
            Destroy(gameObject);
        }

        private void CreateDebugVisual()
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Mine Body (Replace With Authored Prefab)";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = Vector3.up * 0.035f;
            body.transform.localScale = new Vector3(0.16f, 0.035f, 0.16f);
            body.layer = CombatLayers.IgnoreRaycastLayer;
            Collider bodyCollider = body.GetComponent<Collider>();
            if (bodyCollider != null) Destroy(bodyCollider);
            indicatorRenderer = body.GetComponent<Renderer>();
            if (indicatorRenderer != null) indicatorRenderer.material.color = new Color(1f, 0.62f, 0.08f);
        }

        private void UpdateIndicator()
        {
            if (indicatorRenderer == null) return;
            bool armed = Time.time >= armedAt;
            float pulse = armed ? 0.45f + 0.55f * Mathf.PingPong(Time.time * 4f, 1f) : 0.25f;
            indicatorRenderer.material.color = armed
                ? Color.Lerp(new Color(0.2f, 0.9f, 0.35f), Color.white, pulse)
                : new Color(1f, 0.62f, 0.08f);
        }
    }
}
