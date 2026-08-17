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

        /// <summary>
        /// 用装备定义和本次投掷参数初始化已由项目 Prefab 实例化的手雷 Actor。
        /// </summary>
        /// <param name="equipmentDefinition">本次投掷采用的装备定义；为空或与 Prefab 结构不匹配时返回 false。</param>
        /// <param name="instigator">投掷者对象；用于敌我过滤、伤害来源和忽略自身碰撞。</param>
        /// <param name="initialVelocity">生成瞬间施加给 Rigidbody 的世界速度，单位米/秒。</param>
        /// <param name="callback">引爆、到期或回合回收后通知控制器的回调；可为空。</param>
        /// <returns>Prefab 具备所需 Collider、Rigidbody 与可视 Renderer 时返回 true；否则不会创建运行时占位物。</returns>
        public bool Configure(TacticalEquipmentDefinition equipmentDefinition, GameObject instigator, Vector3 initialVelocity,
            Action<FragGrenade, bool> callback)
        {
            if (equipmentDefinition == null)
            {
                Debug.LogError($"{name} cannot configure FragGrenade without TacticalEquipmentDefinition.", this);
                return false;
            }

            SphereCollider collider = GetComponent<SphereCollider>();
            Rigidbody body = GetComponent<Rigidbody>();
            Renderer visualRenderer = GetComponentInChildren<Renderer>(true);
            if (collider == null || body == null || visualRenderer == null)
            {
                Debug.LogError($"{name} FragGrenade prefab requires SphereCollider, Rigidbody and a visible Renderer.", this);
                return false;
            }

            definition = equipmentDefinition;
            owner = instigator;
            ownerCombatant = owner != null ? owner.GetComponent<TeamCombatant>() : null;
            detonateAt = Time.time + Mathf.Max(0.05f, definition.fuseSeconds);
            resolvedCallback = callback;
            body.velocity = initialVelocity;
            IgnoreOwnerCollisions(collider);
            return true;
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

    }
}
