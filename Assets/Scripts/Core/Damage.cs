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

}
