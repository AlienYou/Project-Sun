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
        [SerializeField, Min(1f)] private float detectionRange = 20f;
        [SerializeField, Min(1f)] private float preferredRange = 11f;
        [SerializeField, Min(0.1f)] private float shotsPerSecond = 1.25f;
        [SerializeField, Min(1f)] private float shotDamage = 8f;
        [Header("Patrol")]
        [SerializeField, Min(0.5f)] private float patrolRadius = 4f;
        [SerializeField, Min(0.5f)] private float patrolInterval = 3.5f;
        [Header("Respawn")]
        [SerializeField, Min(0.5f)] private float respawnSeconds = 5f;
        [SerializeField] private Transform player;
        [SerializeField] private ObjectiveZone[] defendedObjectives = System.Array.Empty<ObjectiveZone>();
        [SerializeField] private Vector3 guardPosition;
        [SerializeField] private CombatCoverPoint[] coverPoints = System.Array.Empty<CombatCoverPoint>();
        [SerializeField, Min(0.5f)] private float coverSearchRadius = 18f;
        [SerializeField, Min(0.1f)] private float coverArrivalDistance = 0.55f;

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

        public bool IsAlive => health != null && health.IsAlive;
        public string DebugState => currentCover != null ? $"{debugState} [{currentCover.name}]" : debugState;
        public string LatestRayResult => latestRayResult;
        public float TargetDistance => targetDistance;

        public void Configure(Transform playerTransform, ObjectiveZone[] objectives, Vector3 guardPoint)
        {
            player = playerTransform;
            defendedObjectives = objectives ?? System.Array.Empty<ObjectiveZone>();
            guardPosition = guardPoint;
        }

        public void SetCoverPoints(CombatCoverPoint[] points) => coverPoints = points ?? System.Array.Empty<CombatCoverPoint>();

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

        public void SetRoundRespawnsEnabled(bool enabled) => roundRespawnsEnabled = enabled;

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
            health.Died += OnDied;
        }

        private void Start()
        {
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
            if (!combatEnabled || isRespawning || player == null || agent == null || !agent.enabled || !agent.isOnNavMesh) return;

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
            bool hitPlayer = hasHit && hit.collider.GetComponentInParent<FpsPlayerController>() != null;
            string rayStatus = hitPlayer ? "VISIBLE" : "BLOCKED";
            string rayTarget = hasHit
                ? string.Concat(hit.collider.name, " L", hit.collider.gameObject.layer, " ", rayStatus)
                : "MISS";
            latestRayResult = rayTarget;
            CombatRayDebugOverlay.Record($"DEFENDER {name}", ray, hasHit, hit,
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
            CombatRayDebugOverlay.MarkDamageApplied($"DEFENDER {name}");
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
