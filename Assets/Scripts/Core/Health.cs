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

        public event Action<DamageInfo> Damaged;
        public event Action Died;

        private void Awake() => Current = maxHealth;

        public void ResetHealth()
        {
            Current = maxHealth;
        }

        public void ApplyDamage(DamageInfo damage)
        {
            if (!IsAlive || damage.Amount <= 0f)
                return;

            Current = Mathf.Max(0f, Current - damage.Amount);
            Damaged?.Invoke(damage);
            if (!IsAlive)
            {
                Died?.Invoke();
                if (destroyWhenDead)
                    Destroy(gameObject, 0.05f);
            }
        }
    }
}
