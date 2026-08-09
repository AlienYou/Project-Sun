using NUnit.Framework;
using ProjectSun.FPS.Core;
using ProjectSun.FPS.Rounds;
using UnityEngine;

namespace ProjectSun.FPS.Editor.Tests
{
    /// <summary>验证阵营名册和出生点映射的纯编辑器契约，不启动玩法场景。</summary>
    public sealed class TeamRosterTests
    {
        [Test]
        public void TryAssign_StableSlotRejectsDifferentMemberConflict()
        {
            GameObject firstObject = CreateCombatant("First", CombatTeam.Attackers, 0, out TeamCombatant first);
            GameObject secondObject = CreateCombatant("Second", CombatTeam.Attackers, 0, out TeamCombatant second);
            try
            {
                TeamRoster roster = new TeamRoster(CombatTeam.Attackers, 6);

                Assert.That(roster.TryAssign(first, 0, out string firstFailure), Is.True, firstFailure);
                Assert.That(roster.TryAssign(second, 0, out string secondFailure), Is.False);
                Assert.That(secondFailure, Does.Contain("已由"));
                Assert.That(roster.TryGetMember(0, out TeamCombatant registered), Is.True);
                Assert.That(registered, Is.SameAs(first));
                Assert.That(roster.OccupiedCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(firstObject);
                Object.DestroyImmediate(secondObject);
            }
        }

        [Test]
        public void TryAssign_OutOfCapacityDoesNotMutateRoster()
        {
            GameObject memberObject = CreateCombatant("Overflow", CombatTeam.Defenders, 6, out TeamCombatant member);
            try
            {
                TeamRoster roster = new TeamRoster(CombatTeam.Defenders, 6);

                Assert.That(roster.TryAssign(member, 6, out string failureReason), Is.False);
                Assert.That(failureReason, Does.Contain("超出有效范围"));
                Assert.That(roster.OccupiedCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(memberObject);
            }
        }

        [Test]
        public void SpawnGroup_ReturnsPoseByRosterSlot()
        {
            GameObject groupObject = new GameObject("Spawn Group");
            GameObject firstAnchorObject = new GameObject("Slot 00");
            GameObject secondAnchorObject = new GameObject("Slot 01");
            try
            {
                firstAnchorObject.transform.SetParent(groupObject.transform);
                secondAnchorObject.transform.SetParent(groupObject.transform);
                secondAnchorObject.transform.SetPositionAndRotation(
                    new Vector3(3f, 0.2f, -4f), Quaternion.Euler(0f, 135f, 0f));
                TeamSpawnGroup group = groupObject.AddComponent<TeamSpawnGroup>();
                group.Configure(CombatTeam.Attackers,
                    new[] { firstAnchorObject.transform, secondAnchorObject.transform });

                Assert.That(group.TryGetSpawnPose(1, out Pose spawnPose), Is.True);
                Assert.That(spawnPose.position, Is.EqualTo(secondAnchorObject.transform.position));
                Assert.That(Quaternion.Angle(spawnPose.rotation, secondAnchorObject.transform.rotation), Is.LessThan(0.01f));
                Assert.That(group.TryGetSpawnPose(2, out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(groupObject);
            }
        }

        /// <summary>创建仅用于 EditMode 名册验证的最小战斗成员。</summary>
        /// <param name="objectName">临时 GameObject 名称，用于让失败信息可读。</param>
        /// <param name="team">写入成员的阵营。</param>
        /// <param name="slotIndex">写入成员的稳定槽位；测试可故意传入越界值验证拒绝路径。</param>
        /// <param name="combatant">返回创建完成的 TeamCombatant 组件。</param>
        /// <returns>需要由测试在 finally 中 DestroyImmediate 的临时根对象。</returns>
        private static GameObject CreateCombatant(string objectName, CombatTeam team, int slotIndex,
            out TeamCombatant combatant)
        {
            GameObject gameObject = new GameObject(objectName);
            gameObject.AddComponent<Health>();
            combatant = gameObject.AddComponent<TeamCombatant>();
            combatant.AssignTeamSlot(team, slotIndex);
            return gameObject;
        }
    }
}
