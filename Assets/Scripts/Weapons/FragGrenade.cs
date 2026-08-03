using System;
using System.Collections.Generic;
using ProjectSun.FPS.Core;
using ProjectSun.FPS.Rounds;
using UnityEngine;

namespace ProjectSun.FPS.Weapons
{
    /// <summary>
    /// Physics-driven throwable runtime actor. The definition owns tuning while this actor owns
    /// transient motion, fuse timing, blast validation and cleanup for one use of that equipment.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FragGrenade : MonoBehaviour
    {
        private readonly Collider[] blastHits = new Collider[32];
        private readonly HashSet<Health> damagedTargets = new HashSet<Health>();

        private TacticalEquipmentDefinition definition;
        private GameObject owner;
        private TeamCombatant ownerCombatant;
        private float detonateAt;
        private bool resolved;
        private Action<FragGrenade, bool> resolvedCallback;

        public void Configure(TacticalEquipmentDefinition equipmentDefinition, GameObject instigator, Vector3 initialVelocity,
            Action<FragGrenade, bool> callback)
        {
            definition = equipmentDefinition;
            owner = instigator;
            ownerCombatant = owner != null ? owner.GetComponent<TeamCombatant>() : null;
            detonateAt = Time.time + Mathf.Max(0.05f, definition != null ? definition.fuseSeconds : 0.05f);
            resolvedCallback = callback;

            SphereCollider collider = gameObject.AddComponent<SphereCollider>();
            collider.radius = 0.085f;
            collider.material = CreateBounceMaterial();
            Rigidbody body = gameObject.AddComponent<Rigidbody>();
            body.mass = 0.35f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.velocity = initialVelocity;
            IgnoreOwnerCollisions(collider);
            CreateDebugVisual();
        }

        /// <summary>Used by round teardown so an intentional cleanup does not overwrite the fresh HUD state.</summary>
        public void DetachOwner() => resolvedCallback = null;

        private void Update()
        {
            if (!resolved && Time.time >= detonateAt)
                Detonate();
        }

        private void OnDestroy()
        {
            if (resolved) return;
            resolved = true;
            resolvedCallback?.Invoke(this, false);
        }

        private void Detonate()
        {
            if (resolved || definition == null) return;
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
                target.ApplyDamage(new DamageInfo(definition.damage * falloff, transform.position, direction, owner));
            }
            Resolve(true);
        }

        private bool IsHostile(Health target)
        {
            if (target == null || !target.IsAlive || (owner != null && target.gameObject == owner)) return false;
            TeamCombatant targetCombatant = target.GetComponent<TeamCombatant>();
            return ownerCombatant == null || targetCombatant == null || targetCombatant.Team != ownerCombatant.Team;
        }

        private bool HasLineOfSight(Health target)
        {
            Vector3 origin = transform.position;
            Vector3 targetPoint = target.transform.position + Vector3.up;
            Vector3 offset = targetPoint - origin;
            float distance = offset.magnitude;
            if (distance <= 0.001f) return true;
            if (!Physics.Raycast(origin, offset / distance, out RaycastHit hit, distance, CombatLayers.BallisticMask,
                    QueryTriggerInteraction.Ignore))
                return false;
            return hit.collider.GetComponentInParent<Health>() == target;
        }

        private void Resolve(bool detonated)
        {
            if (resolved) return;
            resolved = true;
            resolvedCallback?.Invoke(this, detonated);
            Destroy(gameObject);
        }

        private void IgnoreOwnerCollisions(Collider grenadeCollider)
        {
            if (grenadeCollider == null || owner == null) return;
            foreach (Collider ownerCollider in owner.GetComponentsInChildren<Collider>())
                if (ownerCollider != null)
                    Physics.IgnoreCollision(grenadeCollider, ownerCollider);
        }

        private static PhysicMaterial CreateBounceMaterial()
        {
            PhysicMaterial material = new PhysicMaterial("Runtime Frag Grenade Bounce")
            {
                bounciness = 0.52f,
                dynamicFriction = 0.28f,
                staticFriction = 0.28f,
                bounceCombine = PhysicMaterialCombine.Maximum,
                frictionCombine = PhysicMaterialCombine.Minimum
            };
            return material;
        }

        private void CreateDebugVisual()
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "Frag Grenade Body (Replace With Authored Prefab)";
            visual.transform.SetParent(transform, false);
            visual.transform.localScale = Vector3.one * 0.16f;
            visual.layer = CombatLayers.IgnoreRaycastLayer;
            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null) Destroy(visualCollider);
            Renderer renderer = visual.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = new Color(0.22f, 0.68f, 0.25f);
        }
    }
}
