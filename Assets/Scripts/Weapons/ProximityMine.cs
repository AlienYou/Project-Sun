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
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

        [SerializeField, Tooltip("感应雷状态灯的 Renderer。未配置时首次运行会尝试使用 Prefab 的第一个 Renderer；Prefab 没有 Renderer 时部署失败。")]
        private Renderer indicatorRenderer;

        private readonly Collider[] triggerHits = new Collider[32];
        private readonly Collider[] blastHits = new Collider[32];
        private readonly HashSet<Health> damagedTargets = new HashSet<Health>();
        private MaterialPropertyBlock indicatorPropertyBlock;

        private TacticalEquipmentDefinition definition;
        private GameObject owner;
        private TeamCombatant ownerCombatant;
        private float armedAt;
        private float expiresAt;
        private bool resolved;
        private int indicatorColorPropertyId;
        private Action<ProximityMine, bool> resolvedCallback;

        private void Awake()
        {
            // MaterialPropertyBlock 的构造会创建 Unity 原生对象，不能在 MonoBehaviour 字段初始化阶段执行。
            // Awake 在组件完成反序列化后调用，既满足 Unity 生命周期约束，也能确保 Configure 使用前已准备完毕。
            indicatorPropertyBlock = new MaterialPropertyBlock();
        }

        /// <summary>
        /// 用装备定义初始化已由项目 Prefab 实例化的感应雷 Actor。
        /// </summary>
        /// <param name="equipmentDefinition">本次部署采用的装备定义；为空或 Prefab 缺少可视 Renderer 时返回 false。</param>
        /// <param name="instigator">部署者对象；用于敌我过滤与伤害来源。</param>
        /// <param name="callback">引爆、到期或回合回收后通知控制器的回调；可为空。</param>
        /// <returns>Prefab 可作为感应雷 Actor 运行时返回 true；错误配置不会生成运行时几何占位物。</returns>
        public bool Configure(TacticalEquipmentDefinition equipmentDefinition, GameObject instigator,
            Action<ProximityMine, bool> callback)
        {
            if (equipmentDefinition == null || !TryResolveIndicatorRenderer()) return false;

            definition = equipmentDefinition;
            owner = instigator;
            ownerCombatant = owner != null ? owner.GetComponent<TeamCombatant>() : null;
            armedAt = Time.time + Mathf.Max(0f, definition.armingSeconds);
            expiresAt = Time.time + Mathf.Max(1f, definition.lifetimeSeconds);
            resolvedCallback = callback;
            SetIndicatorColor(new Color(1f, 0.62f, 0.08f));
            return true;
        }

        /// <summary>
        /// 指定感应雷状态灯 Renderer，供项目 Prefab 制作工具在保存时写入稳定引用。
        /// </summary>
        /// <param name="renderer">用于显示未激活、激活和触发状态的 Renderer；为空时 Configure 会尝试寻找第一个可视 Renderer。</param>
        public void SetIndicatorRenderer(Renderer renderer) => indicatorRenderer = renderer;

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

        private void UpdateIndicator()
        {
            if (indicatorRenderer == null) return;
            bool armed = Time.time >= armedAt;
            float pulse = armed ? 0.45f + 0.55f * Mathf.PingPong(Time.time * 4f, 1f) : 0.25f;
            Color color = armed
                ? Color.Lerp(new Color(0.2f, 0.9f, 0.35f), Color.white, pulse)
                : new Color(1f, 0.62f, 0.08f);
            SetIndicatorColor(color);
        }

        private bool TryResolveIndicatorRenderer()
        {
            if (indicatorRenderer == null) indicatorRenderer = GetComponentInChildren<Renderer>(true);
            if (indicatorRenderer == null || indicatorRenderer.sharedMaterial == null)
            {
                Debug.LogError($"{name} ProximityMine prefab requires an indicator Renderer with a shared material.", this);
                return false;
            }

            // URP Lit 使用 _BaseColor，旧材质可能只提供 _Color；两者都没有时仍保留默认 ID，便于自定义 Shader 选择忽略该覆盖。
            Material material = indicatorRenderer.sharedMaterial;
            indicatorColorPropertyId = material.HasProperty(BaseColorPropertyId) ? BaseColorPropertyId : ColorPropertyId;
            return true;
        }

        private void SetIndicatorColor(Color color)
        {
            if (indicatorRenderer == null) return;

            // 正常运行时由 Awake 完成初始化；此处兜底兼容编辑器或测试环境中绕过生命周期的直接调用。
            if (indicatorPropertyBlock == null) indicatorPropertyBlock = new MaterialPropertyBlock();
            indicatorRenderer.GetPropertyBlock(indicatorPropertyBlock);
            indicatorPropertyBlock.SetColor(indicatorColorPropertyId, color);
            indicatorRenderer.SetPropertyBlock(indicatorPropertyBlock);
        }
    }
}
