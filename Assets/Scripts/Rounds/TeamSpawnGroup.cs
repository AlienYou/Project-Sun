using System;
using UnityEngine;

namespace ProjectSun.FPS.Rounds
{
    /// <summary>
    /// 将阵营槽位映射为场景出生姿态。它只描述关卡数据，不负责复活、生命值或回合胜负，
    /// 因而可被团队歼灭、爆破模式和未来服务器出生规则共同复用。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TeamSpawnGroup : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("该出生点组服务的阵营。None 表示配置无效，RoundManager 会拒绝使用。")]
        private CombatTeam team;

        [SerializeField]
        [Tooltip("按稳定队伍槽位排列的出生锚点。数组索引就是队伍槽位；空引用会让对应成员沿用旧出生位置。")]
        private Transform[] slotAnchors = Array.Empty<Transform>();

        /// <summary>该出生点组服务的阵营。</summary>
        public CombatTeam Team => team;

        /// <summary>当前已声明的槽位数量，空引用仍计入数组长度。</summary>
        public int SlotCount => slotAnchors.Length;

        /// <summary>
        /// 配置阵营及其有序出生锚点。编辑器建场工具使用该入口写入稳定数据，运行时不应每帧调用。
        /// </summary>
        /// <param name="targetTeam">出生点组所属阵营，不能为 None。</param>
        /// <param name="orderedSlotAnchors">按槽位 0 到 N-1 排列的锚点；null 会转换为空数组。</param>
        public void Configure(CombatTeam targetTeam, Transform[] orderedSlotAnchors)
        {
            team = targetTeam;
            slotAnchors = orderedSlotAnchors ?? Array.Empty<Transform>();
        }

        /// <summary>
        /// 尝试读取指定队伍槽位的世界空间出生姿态。
        /// </summary>
        /// <param name="slotIndex">从 0 开始的队伍槽位索引。</param>
        /// <param name="spawnPose">成功时返回锚点的世界位置与旋转；失败时返回默认姿态。</param>
        /// <returns>索引有效且锚点存在时返回 true。</returns>
        public bool TryGetSpawnPose(int slotIndex, out Pose spawnPose)
        {
            if (slotIndex < 0 || slotIndex >= slotAnchors.Length || slotAnchors[slotIndex] == null)
            {
                spawnPose = default;
                return false;
            }

            Transform anchor = slotAnchors[slotIndex];
            spawnPose = new Pose(anchor.position, anchor.rotation);
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Color teamColor = team == CombatTeam.Attackers
                ? new Color(0.18f, 0.55f, 1f, 0.9f)
                : new Color(1f, 0.25f, 0.2f, 0.9f);
            Gizmos.color = teamColor;

            // 圆环近似显示成员占地，前向射线明确出生朝向，便于关卡设计师发现背向或重叠配置。
            for (int index = 0; index < slotAnchors.Length; index++)
            {
                Transform anchor = slotAnchors[index];
                if (anchor == null) continue;
                Gizmos.DrawWireSphere(anchor.position + Vector3.up * 0.08f, 0.32f);
                Gizmos.DrawRay(anchor.position + Vector3.up * 0.08f, anchor.forward * 0.9f);
            }
        }
    }
}
