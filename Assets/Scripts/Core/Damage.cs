using System;
using UnityEngine;

namespace ProjectSun.FPS.Core
{
    /// <summary>Implemented by anything that can receive combat damage.</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        void ApplyDamage(DamageInfo damage);
    }

    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly Vector3 Point;
        public readonly Vector3 Direction;
        public readonly GameObject Instigator;

        public DamageInfo(float amount, Vector3 point, Vector3 direction, GameObject instigator)
        {
            Amount = amount;
            Point = point;
            Direction = direction;
            Instigator = instigator;
        }
    }

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
