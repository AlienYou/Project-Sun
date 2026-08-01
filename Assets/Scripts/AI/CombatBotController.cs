using System.Collections;
using ProjectSun.FPS.Core;
using ProjectSun.FPS.Player;
using ProjectSun.FPS.Rounds;
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

        private NavMeshAgent agent;
        private Health health;
        private Collider[] colliders;
        private Renderer[] renderers;
        private float nextShotAt;
        private float nextPatrolAt;
        private bool isRespawning;
        private bool wasEngaging;

        public void Configure(Transform playerTransform, ObjectiveZone[] objectives, Vector3 guardPoint)
        {
            player = playerTransform;
            defendedObjectives = objectives ?? System.Array.Empty<ObjectiveZone>();
            guardPosition = guardPoint;
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
            Patrol();
        }

        private void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
        }

        private void Update()
        {
            if (isRespawning || player == null || agent == null || !agent.enabled || !agent.isOnNavMesh) return;

            Vector3 toPlayer = player.position - transform.position;
            float distance = toPlayer.magnitude;
            bool canSeePlayer = distance <= detectionRange && HasLineOfSight(distance);
            if (!canSeePlayer)
            {
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
            if (distance > preferredRange)
            {
                agent.isStopped = false;
                agent.SetDestination(player.position);
            }
            else
            {
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

        private bool HasLineOfSight(float distance)
        {
            Vector3 origin = transform.position + Vector3.up * 1.35f;
            Vector3 target = player.position + Vector3.up * 1.35f;
            Vector3 direction = (target - origin).normalized;
            if (!Physics.Raycast(origin, direction, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore)) return false;
            return hit.collider.GetComponentInParent<FpsPlayerController>() != null;
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
            nextShotAt = Time.time + 1f / shotsPerSecond;
            Health playerHealth = player.GetComponent<Health>();
            if (playerHealth == null || !playerHealth.IsAlive) return;
            Vector3 direction = (player.position - transform.position).normalized;
            playerHealth.ApplyDamage(new DamageInfo(shotDamage, player.position, direction, gameObject));
        }

        private void OnDied()
        {
            if (!isRespawning) StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            isRespawning = true;
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
            Patrol();
        }
    }
}
