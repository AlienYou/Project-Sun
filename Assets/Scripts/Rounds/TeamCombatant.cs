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
        [SerializeField]
        [Tooltip("该成员所属阵营。None 仅用于尚未加入对局的对象，正式回合成员必须由 RoundManager 分配阵营。")]
        private CombatTeam team;

        [SerializeField, Range(-1, 5)]
        [Tooltip("阵营内稳定槽位，范围 0-5；-1 表示尚未加入阵营名册。槽位同时决定出生点、HUD 顺序与未来网络身份映射。")]
        private int teamSlot = -1;

        [SerializeField]
        [Tooltip("启用后拒绝来自同阵营其他成员的伤害。自伤仍交给具体武器或战术装备规则决定。")]
        private bool blockFriendlyFire = true;

        private Health health;

        public CombatTeam Team => team;
        public int TeamSlot => teamSlot;
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

        /// <summary>
        /// 设置阵营但不指定名册槽位。该兼容入口用于旧场景预配置；正式对局应由 RoundManager 调用
        /// <see cref="AssignTeamSlot"/> 建立完整身份。
        /// </summary>
        /// <param name="value">新的阵营；None 会同时清除现有槽位。</param>
        public void SetTeam(CombatTeam value)
        {
            if (team != value) teamSlot = -1;
            team = value;
            if (team == CombatTeam.None) teamSlot = -1;
        }

        /// <summary>同时写入阵营和稳定名册槽位。</summary>
        /// <param name="value">成员所属阵营；None 只允许与 -1 槽位配合表示未分配。</param>
        /// <param name="slotIndex">从 0 开始的阵营槽位；-1 表示未分配，RoundManager 会校验上限。</param>
        public void AssignTeamSlot(CombatTeam value, int slotIndex)
        {
            team = value;
            teamSlot = value == CombatTeam.None ? -1 : Mathf.Max(-1, slotIndex);
        }

        private bool AllowsDamage(DamageInfo damage)
        {
            if (!blockFriendlyFire || team == CombatTeam.None || damage.Instigator == null) return true;
            TeamCombatant instigator = damage.Instigator.GetComponentInParent<TeamCombatant>();
            return instigator == null || instigator == this || instigator.team != team;
        }
    }
}
