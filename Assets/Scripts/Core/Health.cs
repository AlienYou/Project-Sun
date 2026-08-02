using System;
using UnityEngine;

namespace ProjectSun.FPS.Core
{
    /// <summary>Shared hit-point component for players, AI and destructible world objects.</summary>
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField] private bool destroyWhenDead;

        public float Current { get; private set; }
        public float Max => maxHealth;
        public bool IsAlive => Current > 0f;
        public bool HasLastDamage { get; private set; }
        public DamageInfo LastDamage { get; private set; }

        public event Action<DamageInfo> Damaged;
        public event Action Died;
        /// <summary>Optional gameplay rules can reject damage before health is modified.</summary>
        public event Func<DamageInfo, bool> DamagePermissionRequested;

        private void Awake() => Current = maxHealth;

        public void ResetHealth()
        {
            Current = maxHealth;
            HasLastDamage = false;
        }

        public void ApplyDamage(DamageInfo damage)
        {
            if (!IsAlive || damage.Amount <= 0f)
                return;
            if (!CanReceiveDamage(damage))
                return;

            Current = Mathf.Max(0f, Current - damage.Amount);
            LastDamage = damage;
            HasLastDamage = true;
            Damaged?.Invoke(damage);
            if (!IsAlive)
            {
                Died?.Invoke();
                if (destroyWhenDead)
                    Destroy(gameObject, 0.05f);
            }
        }

        private bool CanReceiveDamage(DamageInfo damage)
        {
            if (DamagePermissionRequested == null) return true;
            foreach (Delegate subscription in DamagePermissionRequested.GetInvocationList())
                if (subscription is Func<DamageInfo, bool> rule && !rule.Invoke(damage)) return false;
            return true;
        }
    }
}
