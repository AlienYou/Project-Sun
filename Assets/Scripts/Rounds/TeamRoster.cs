using System;

namespace ProjectSun.FPS.Rounds
{
    /// <summary>
    /// 单个阵营的固定容量名册。槽位编号在整场比赛内保持稳定，后续可直接映射为服务器玩家槽、
    /// 出生点、观战目标和 HUD 成员索引，而不依赖场景查找顺序。
    /// </summary>
    public sealed class TeamRoster
    {
        private readonly TeamCombatant[] slots;

        /// <summary>
        /// 创建指定阵营的固定容量名册。
        /// </summary>
        /// <param name="team">名册所属阵营，不能为 <see cref="CombatTeam.None"/>。</param>
        /// <param name="capacity">最大槽位数量，必须大于零；团队歼灭模式当前使用 6。</param>
        public TeamRoster(CombatTeam team, int capacity)
        {
            if (team == CombatTeam.None)
                throw new ArgumentException("阵营名册不能使用 CombatTeam.None。", nameof(team));
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "阵营容量必须大于零。");

            Team = team;
            slots = new TeamCombatant[capacity];
        }

        /// <summary>该名册所属的阵营。</summary>
        public CombatTeam Team { get; }

        /// <summary>该名册可容纳的最大成员数量。</summary>
        public int Capacity => slots.Length;

        /// <summary>当前已占用的槽位数量，空槽不会计入。</summary>
        public int OccupiedCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < slots.Length; index++)
                    if (slots[index] != null) count++;
                return count;
            }
        }

        /// <summary>当前仍存活的成员数量，由成员的权威生命状态实时计算。</summary>
        public int AliveCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < slots.Length; index++)
                    if (slots[index] != null && slots[index].IsAlive) count++;
                return count;
            }
        }

        /// <summary>清空所有槽位，用于重新配置测试阵容；不会修改成员自身阵营。</summary>
        public void Clear() => Array.Clear(slots, 0, slots.Length);

        /// <summary>
        /// 将成员注册到稳定槽位。重复注册同一成员到同一槽位视为成功；槽位冲突或阵营不一致时拒绝覆盖。
        /// </summary>
        /// <param name="member">要注册的场景战斗成员，不能为空。</param>
        /// <param name="slotIndex">从 0 开始的槽位索引，必须小于 <see cref="Capacity"/>。</param>
        /// <param name="failureReason">失败时返回可直接写入 Console 的中文原因；成功时为空字符串。</param>
        /// <returns>注册成功返回 true；参数、阵营或槽位冲突时返回 false。</returns>
        public bool TryAssign(TeamCombatant member, int slotIndex, out string failureReason)
        {
            if (member == null)
            {
                failureReason = "成员为空。";
                return false;
            }
            if (slotIndex < 0 || slotIndex >= slots.Length)
            {
                failureReason = $"槽位 {slotIndex} 超出有效范围 0-{slots.Length - 1}。";
                return false;
            }
            if (member.Team != Team)
            {
                failureReason = $"成员 {member.name} 属于 {member.Team}，不能加入 {Team} 名册。";
                return false;
            }

            // 一个成员只能占用一个稳定槽位，否则出生、HUD 与未来网络身份会产生歧义。
            for (int index = 0; index < slots.Length; index++)
            {
                if (slots[index] != member || index == slotIndex) continue;
                failureReason = $"成员 {member.name} 已占用槽位 {index}。";
                return false;
            }

            TeamCombatant current = slots[slotIndex];
            if (current != null && current != member)
            {
                failureReason = $"槽位 {slotIndex} 已由 {current.name} 占用。";
                return false;
            }

            slots[slotIndex] = member;
            failureReason = string.Empty;
            return true;
        }

        /// <summary>
        /// 读取指定槽位的成员，不会因为空槽或越界抛出异常。
        /// </summary>
        /// <param name="slotIndex">从 0 开始的槽位索引。</param>
        /// <param name="member">成功时返回槽位成员；失败时返回 null。</param>
        /// <returns>槽位有效且存在成员时返回 true。</returns>
        public bool TryGetMember(int slotIndex, out TeamCombatant member)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length)
            {
                member = null;
                return false;
            }

            member = slots[slotIndex];
            return member != null;
        }

        /// <summary>
        /// 按稳定槽位循环查找下一名存活成员。该顺序可直接用于死亡观战切换，避免摄像机目标因场景查找顺序而跳变。
        /// </summary>
        /// <param name="afterSlotIndex">从该槽位之后开始查找；-1 表示从槽位 0 开始，超出范围也按 -1 处理。</param>
        /// <param name="member">成功时返回下一名存活成员；没有存活成员时返回 null。</param>
        /// <returns>至少找到一名已注册且存活的成员时返回 true。</returns>
        public bool TryGetNextLivingMember(int afterSlotIndex, out TeamCombatant member)
        {
            return TryGetLivingMember(afterSlotIndex, 1, out member);
        }

        /// <summary>
        /// 按稳定槽位反向循环查找上一名存活成员，用于观战界面的反向切换。
        /// </summary>
        /// <param name="beforeSlotIndex">从该槽位之前开始查找；-1 或越界表示从末槽位开始。</param>
        /// <param name="member">成功时返回上一名存活成员；没有存活成员时返回 null。</param>
        /// <returns>至少找到一名已注册且存活的成员时返回 true。</returns>
        public bool TryGetPreviousLivingMember(int beforeSlotIndex, out TeamCombatant member)
        {
            return TryGetLivingMember(beforeSlotIndex, -1, out member);
        }

        /// <summary>按指定方向循环查询存活成员，正数向后、负数向前。</summary>
        /// <param name="referenceSlotIndex">当前参考槽位；越界时从对应方向的边界开始。</param>
        /// <param name="direction">只接受 1 或 -1，分别表示槽位递增和递减。</param>
        /// <param name="member">成功时返回找到的存活成员；失败时返回 null。</param>
        private bool TryGetLivingMember(int referenceSlotIndex, int direction, out TeamCombatant member)
        {
            int normalizedSlot = referenceSlotIndex >= 0 && referenceSlotIndex < slots.Length
                ? referenceSlotIndex
                : direction > 0 ? -1 : 0;
            for (int offset = 1; offset <= slots.Length; offset++)
            {
                int slotIndex = (normalizedSlot + direction * offset) % slots.Length;
                if (slotIndex < 0) slotIndex += slots.Length;
                TeamCombatant candidate = slots[slotIndex];
                if (candidate == null || !candidate.IsAlive) continue;
                member = candidate;
                return true;
            }

            member = null;
            return false;
        }
    }
}
