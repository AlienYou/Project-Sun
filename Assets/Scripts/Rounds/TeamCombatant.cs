using ProjectSun.FPS.Core;
using UnityEngine;

namespace ProjectSun.FPS.Rounds
{
    public enum CombatTeam { None, Attackers, Defenders }

    /// <summary>
    /// Team identity shared by player and bots. It owns friendly-fire protection locally until
    /// multiplayer moves the same decision to the authoritative server.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class TeamCombatant : MonoBehaviour
    {
        [SerializeField] private CombatTeam team;
        [SerializeField] private bool blockFriendlyFire = true;

        private Health health;

        public CombatTeam Team => team;
        public Health Health => health;
        public bool IsAlive => health != null && health.IsAlive;

        private void Awake()
        {
            health = GetComponent<Health>();
            health.DamagePermissionRequested += AllowsDamage;
        }

        private void OnDestroy()
        {
            if (health != null) health.DamagePermissionRequested -= AllowsDamage;
        }

        public void SetTeam(CombatTeam value) => team = value;

        private bool AllowsDamage(DamageInfo damage)
        {
            if (!blockFriendlyFire || team == CombatTeam.None || damage.Instigator == null) return true;
            TeamCombatant instigator = damage.Instigator.GetComponentInParent<TeamCombatant>();
            return instigator == null || instigator == this || instigator.team != team;
        }
    }
}
