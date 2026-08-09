using System.Collections;
using ProjectSun.FPS.Core;
using ProjectSun.FPS.Player;
using ProjectSun.FPS.Rounds;
using ProjectSun.FPS.UI;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectSun.FPS.AI
{
    /// <summary>Simple defensive bot for validating routes, cover and objective pressure before multiplayer is introduced.</summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Health))]
    public sealed class CombatBotController : MonoBehaviour
    {
        [Header("Combat")]
        [SerializeField, Min(1f), Tooltip("Bot 可尝试发现并射击目标的最大距离，单位为米，必须大于等于 1。")]
        private float detectionRange = 20f;
        [SerializeField, Min(1f), Tooltip("Bot 希望保持的交战距离，单位为米；超出后会追击目标。")]
        private float preferredRange = 11f;
        [SerializeField, Min(0.1f), Tooltip("Bot 每秒射击次数，必须大于零；当前离线验证不模拟弹匣。")]
        private float shotsPerSecond = 1.25f;
        [SerializeField, Min(1f), Tooltip("单次命中造成的基础伤害，单位为生命值。")]
        private float shotDamage = 8f;
        [Header("Patrol")]
        [SerializeField, Min(0.5f), Tooltip("无目标时围绕本回合出生点巡逻的半径，单位为米。")]
        private float patrolRadius = 4f;
        [SerializeField, Min(0.5f), Tooltip("重新选择巡逻目的地的最短间隔，单位为秒。")]
        private float patrolInterval = 3.5f;
        [Header("Respawn")]
        [SerializeField, Min(0.5f), Tooltip("训练模式死亡后的复活等待时间，单位为秒；团队歼灭回合会关闭局中复活。")]
        private float respawnSeconds = 5f;
        [SerializeField, Tooltip("旧训练场兼容目标；存在 RoundManager 时会按敌方阵营名册动态替换。")]
        private Transform player;
        [SerializeField, Tooltip("旧攻防验证目标集合；团队歼灭模式不以该数组决定胜负。")]
        private ObjectiveZone[] defendedObjectives = System.Array.Empty<ObjectiveZone>();
        [SerializeField, Tooltip("未配置 TeamSpawnGroup 时使用的巡逻与复活基准位置，单位为世界空间米。")]
        private Vector3 guardPosition;
        [SerializeField, Tooltip("允许该 Bot 竞争使用的战术掩体点；由场景安装器在初始化阶段一次性写入。")]
        private CombatCoverPoint[] coverPoints = System.Array.Empty<CombatCoverPoint>();
        [SerializeField, Min(0.5f), Tooltip("寻找可用掩体的最大距离，单位为米。")]
        private float coverSearchRadius = 18f;
        [SerializeField, Min(0.1f), Tooltip("判定到达掩体或探身点的距离阈值，单位为米。")]
        private float coverArrivalDistance = 0.55f;

        private NavMeshAgent agent;
        private Health health;
        private Collider[] colliders;
        private Renderer[] renderers;
        private float nextShotAt;
        private float nextPatrolAt;
        private bool isRespawning;
        private bool wasEngaging;
        private bool combatEnabled;
        private bool roundRespawnsEnabled = true;
        private string debugState = "STANDBY";
        private string latestRayResult = "NO QUERY";
        private float targetDistance;
        private CombatCoverPoint currentCover;
        private bool movingToPeek;
        private RoundManager roundManager;
        private Quaternion spawnRotation;

        public bool IsAlive => health != null && health.IsAlive;
        public string DebugState => currentCover != null ? $"{debugState} [{currentCover.name}]" : debugState;
        public string LatestRayResult => latestRayResult;
        public float TargetDistance => targetDistance;

        /// <summary>配置旧场景训练目标、目标区与巡逻基准。</summary>
        /// <param name="playerTransform">旧训练模式固定目标；团队回合中会替换为最近的存活敌人。</param>
        /// <param name="objectives">Bot 需要防守的目标区；null 按空数组处理。</param>
        /// <param name="guardPoint">世界空间巡逻和兼容复活基准，单位为米。</param>
        public void Configure(Transform playerTransform, ObjectiveZone[] objectives, Vector3 guardPoint)
        {
            player = playerTransform;
            defendedObjectives = objectives ?? System.Array.Empty<ObjectiveZone>();
            guardPosition = guardPoint;
        }

        /// <summary>替换该 Bot 可使用的掩体集合。</summary>
        /// <param name="points">场景中的候选掩体；null 按空数组处理，调用方应在初始化阶段一次性配置。</param>
        public void SetCoverPoints(CombatCoverPoint[] points) => coverPoints = points ?? System.Array.Empty<CombatCoverPoint>();

        /// <summary>启用或停用 Bot 的移动与射击决策。</summary>
        /// <param name="enabled">true 允许执行战斗 AI；false 会停止导航并释放已占用掩体。</param>
        public void SetCombatEnabled(bool enabled)
        {
            combatEnabled = enabled;
            if (!enabled)
            {
                if (agent != null && agent.enabled) agent.isStopped = true;
                ReleaseCover();
                if (IsAlive) debugState = "STANDBY";
            }
            if (enabled && IsAlive)
                Patrol();
        }

        /// <summary>设置训练模式是否允许死亡后自动复活。</summary>
        /// <param name="enabled">true 允许延迟复活；false 时保持淘汰直到 RoundManager 重置回合。</param>
        public void SetRoundRespawnsEnabled(bool enabled) => roundRespawnsEnabled = enabled;

        /// <summary>
        /// 更新该 Bot 后续重置、训练复活和巡逻使用的出生姿态。实际传送发生在重置流程，
        /// 以便先安全停用 NavMeshAgent，再将位置投影到可行走表面。
        /// </summary>
        /// <param name="spawnPose">世界空间出生位置与朝向，由稳定阵营槽位对应的出生点提供。</param>
        public void SetRoundSpawn(Pose spawnPose)
        {
            guardPosition = spawnPose.position;
            spawnRotation = spawnPose.rotation;
        }

        public void ResetForRound()
        {
            StopAllCoroutines();
            isRespawning = false;
            wasEngaging = false;
            nextPatrolAt = 0f;
            debugState = "STANDBY";
            ReleaseCover();
            if (agent != null && agent.enabled) agent.enabled = false;
            if (NavMesh.SamplePosition(guardPosition, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                transform.position = hit.position;
            else
                transform.position = guardPosition;
            transform.rotation = spawnRotation;
            health.ResetHealth();
            foreach (Collider currentCollider in colliders) currentCollider.enabled = true;
            foreach (Renderer currentRenderer in renderers) currentRenderer.enabled = true;
            if (agent != null) agent.enabled = true;
            if (agent != null && agent.enabled) agent.isStopped = true;
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            colliders = GetComponentsInChildren<Collider>();
            renderers = GetComponentsInChildren<Renderer>();
            spawnRotation = transform.rotation;
            health.Died += OnDied;
        }

        private void Start()
        {
            roundManager = FindObjectOfType<RoundManager>();
            if (player == null)
            {
                FpsPlayerController playerController = FindObjectOfType<FpsPlayerController>();
                if (playerController != null) player = playerController.transform;
            }
            if (guardPosition == Vector3.zero) guardPosition = transform.position;
        }

        private void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
            ReleaseCover();
        }

        private void Update()
        {
            if (!combatEnabled || isRespawning || agent == null || !agent.enabled || !agent.isOnNavMesh) return;

            if (roundManager != null)
                player = roundManager.GetTargetFor(this);
            if (player == null)
            {
                ReleaseCover();
                debugState = "NO ENEMY";
                Patrol();
                return;
            }

            Vector3 toPlayer = player.position - transform.position;
            float distance = toPlayer.magnitude;
            targetDistance = distance;
            bool canSeePlayer = distance <= detectionRange && HasLineOfSight();
            if (!canSeePlayer)
            {
                if (MoveThroughCover()) return;
                debugState = "PATROL";
                if (wasEngaging)
                {
                    wasEngaging = false;
                    nextPatrolAt = 0f;
                }
                Patrol();
                return;
            }

            wasEngaging = true;
            Face(player.position);
            if (currentCover != null && !movingToPeek)
            {
                movingToPeek = true;
                SetDestination(currentCover.PeekPosition);
                debugState = "PEEK";
                return;
            }
            if (distance > preferredRange)
            {
                ReleaseCover();
                debugState = "CHASE";
                agent.isStopped = false;
                agent.SetDestination(player.position);
            }
            else
            {
                debugState = "FIRE";
                agent.isStopped = true;
                TryShootPlayer();
            }
        }

        private void Patrol()
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
            bool stillTravelling = agent.hasPath && !agent.pathPending && agent.remainingDistance > 0.6f;
            if (Time.time < nextPatrolAt && stillTravelling) return;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * patrolRadius;
                Vector3 desired = guardPosition + new Vector3(offset.x, 0f, offset.y);
                if (!NavMesh.SamplePosition(desired, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas)) continue;

                agent.isStopped = false;
                agent.SetDestination(hit.position);
                nextPatrolAt = Time.time + patrolInterval;
                return;
            }

            // Keep trying even if a local section of greybox geometry is temporarily not navigable.
            agent.isStopped = false;
            agent.SetDestination(guardPosition);
            nextPatrolAt = Time.time + patrolInterval;
        }

        private bool MoveThroughCover()
        {
            if (currentCover == null && !ClaimBestCover()) return false;
            if (currentCover == null) return false;

            Vector3 destination = movingToPeek ? currentCover.PeekPosition : currentCover.CoverPosition;
            if (Vector3.Distance(transform.position, destination) > coverArrivalDistance)
            {
                SetDestination(destination);
                debugState = movingToPeek ? "PEEK" : "TAKE COVER";
                return true;
            }
            if (!movingToPeek)
            {
                movingToPeek = true;
                SetDestination(currentCover.PeekPosition);
                debugState = "PEEK";
                return true;
            }

            ReleaseCover();
            return false;
        }

        private bool ClaimBestCover()
        {
            CombatCoverPoint best = null;
            float bestDistance = float.MaxValue;
            foreach (CombatCoverPoint point in coverPoints)
            {
                if (point == null || point.IsOccupied) continue;
                if (!NavMesh.SamplePosition(point.CoverPosition, out NavMeshHit navHit, 1.5f, NavMesh.AllAreas)) continue;
                float distance = Vector3.SqrMagnitude(navHit.position - transform.position);
                if (distance > coverSearchRadius * coverSearchRadius || distance >= bestDistance) continue;
                if (best != null) best.Release(this);
                best = point;
                bestDistance = distance;
            }
            if (best == null) return false;
            if (!best.TryClaim(this)) return false;
            currentCover = best;
            movingToPeek = false;
            return true;
        }

        private void ReleaseCover()
        {
            if (currentCover != null) currentCover.Release(this);
            currentCover = null;
            movingToPeek = false;
        }

        private void SetDestination(Vector3 destination)
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
                destination = hit.position;
            agent.isStopped = false;
            agent.SetDestination(destination);
        }

        private bool HasLineOfSight() => TryGetPlayerHit(out _);

        private bool TryGetPlayerHit(out RaycastHit hit)
        {
            Vector3 origin = transform.position + Vector3.up * 1.35f;
            Vector3 target = player.position + Vector3.up * 1.35f;
            Vector3 offset = target - origin;
            float rayDistance = offset.magnitude;
            Ray ray = new Ray(origin, offset.normalized);
            bool hasHit = Physics.Raycast(ray, out hit, rayDistance, CombatLayers.BallisticMask, QueryTriggerInteraction.Ignore);
            Health targetHealth = player != null ? player.GetComponent<Health>() : null;
            Health hitHealth = hasHit ? hit.collider.GetComponentInParent<Health>() : null;
            bool hitPlayer = targetHealth != null && hitHealth == targetHealth;
            string rayStatus = hitPlayer ? "VISIBLE" : "BLOCKED";
            string rayTarget = hasHit
                ? string.Concat(hit.collider.name, " L", hit.collider.gameObject.layer, " ", rayStatus)
                : "MISS";
            latestRayResult = rayTarget;
            CombatRayDebugOverlay.Record($"BOT {name}", ray, hasHit, hit,
                hitPlayer ? CombatRayOutcome.Visible : hasHit ? CombatRayOutcome.Blocked : CombatRayOutcome.Miss);
            return hitPlayer;
        }

        private void Face(Vector3 point)
        {
            Vector3 direction = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 540f * Time.deltaTime);
        }

        private void TryShootPlayer()
        {
            if (Time.time < nextShotAt) return;
            if (!TryGetPlayerHit(out RaycastHit hit)) return;
            Health playerHealth = player.GetComponent<Health>();
            if (playerHealth == null || !playerHealth.IsAlive) return;
            nextShotAt = Time.time + 1f / shotsPerSecond;
            Vector3 direction = (player.position - transform.position).normalized;
            playerHealth.ApplyDamage(new DamageInfo(shotDamage, hit.point, direction, gameObject));
            CombatRayDebugOverlay.MarkDamageApplied($"BOT {name}");
        }

        private void OnDied()
        {
            if (isRespawning) return;
            debugState = "ELIMINATED";
            ReleaseCover();
            if (roundRespawnsEnabled) StartCoroutine(RespawnRoutine());
            else DisableAfterElimination();
        }

        private void DisableAfterElimination()
        {
            if (agent != null) agent.enabled = false;
            foreach (Collider currentCollider in colliders) currentCollider.enabled = false;
            foreach (Renderer currentRenderer in renderers) currentRenderer.enabled = false;
        }

        private IEnumerator RespawnRoutine()
        {
            isRespawning = true;
            debugState = "RESPAWNING";
            ReleaseCover();
            if (agent != null) agent.enabled = false;
            foreach (Collider currentCollider in colliders) currentCollider.enabled = false;
            foreach (Renderer currentRenderer in renderers) currentRenderer.enabled = false;
            yield return new WaitForSeconds(respawnSeconds);

            if (NavMesh.SamplePosition(guardPosition, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                transform.position = hit.position;
            else
                transform.position = guardPosition;
            transform.rotation = spawnRotation;
            health.ResetHealth();
            foreach (Collider currentCollider in colliders) currentCollider.enabled = true;
            foreach (Renderer currentRenderer in renderers) currentRenderer.enabled = true;
            if (agent != null) agent.enabled = true;
            isRespawning = false;
            wasEngaging = false;
            nextPatrolAt = 0f;
            if (combatEnabled) Patrol();
        }
    }
}
