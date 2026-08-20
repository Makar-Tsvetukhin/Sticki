using UnityEngine;
using UnityEngine.AI;
using Sticki.Combat;
using Sticki.Player;

namespace Sticki.AI
{
    public class EnemyRangedAI : MonoBehaviour
    {
        private enum AIState { Passive, Chasing, Shooting, Repositioning }

        private const int Ar1AgentTypeId = 287145453;
        private const int Ar2AgentTypeId = 658490984;
        private const int Ar3AgentTypeId = -629701670;
        private const int Ar4AgentTypeId = -515339236;

        [Header("General")]
        [SerializeField] private float shootingRange = 18f;
        [SerializeField] private float minRange = 6f;
        [SerializeField] private float repositionRadius = 12f;
        [SerializeField] private LayerMask obstacleMask = (1 << 0) | (1 << 9) | (1 << 10) | (1 << 11) | (1 << 12) | (1 << 13) | (1 << 15) | (1 << 16);

        [Header("Movement")]
        [SerializeField] private float rotationSpeed = 8f;
        [SerializeField] private bool invertFacing = false;
        [SerializeField] private float playerResolveInterval = 0.5f;
        [SerializeField] private Transform playerTarget;

        private NavMeshAgent agent;
        private Transform player;
        private EnemyRangedShooter shooter;
        private EnemyHealth health;
        private Animator animator;

        private AIState currentState = AIState.Passive;
        private bool isAggressive = false;
        private float nextPlayerSearchTime;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            shooter = GetComponent<EnemyRangedShooter>();
            health = GetComponent<EnemyHealth>();
            animator = GetComponentInChildren<Animator>();

            ConfigureAgentForCurrentArena();

            if (agent != null)
            {
                agent.updateRotation = false;
            }
        }

        private void OnEnable()
        {
            ConfigureAgentForCurrentArena();
        }

        private void Update()
        {
            if (health != null && health.IsDead) return;

            if (!isAggressive && EnemyMeleeAI.GlobalCombatActive)
            {
                isAggressive = true;
                currentState = AIState.Chasing;
            }

            if (!isAggressive) return;

            if (player == null)
            {
                if (Time.time >= nextPlayerSearchTime)
                {
                    TryFindPlayer();
                    nextPlayerSearchTime = Time.time + playerResolveInterval;
                }
                return;
            }

            UpdateStateMachine();
            UpdateAnimator();
        }

        private void UpdateStateMachine()
        {
            float dist = Vector3.Distance(transform.position, player.position);
            bool hasLOS = CheckLOS();

            switch (currentState)
            {
                case AIState.Passive:
                    break;

                case AIState.Chasing:
                    if (hasLOS && dist <= shootingRange)
                    {
                        if (agent.isOnNavMesh) agent.isStopped = true;
                        currentState = AIState.Shooting;
                    }
                    else
                    {
                        if (agent.isOnNavMesh)
                        {
                            agent.isStopped = false;
                            agent.SetDestination(player.position);
                            RotateTowards(agent.desiredVelocity);
                        }
                    }
                    break;

                case AIState.Shooting:
                    // If we are currently in the un-interruptible Prep phase, do nothing else.
                    if (shooter == null)
                    {
                        currentState = AIState.Chasing;
                        return;
                    }

                    if (shooter.IsPrepping) return;

                    // Priority 1: Check distance for reposition
                    if (dist < minRange)
                    {
                        shooter.CancelShooting();
                        FindRepositionPoint();
                        currentState = AIState.Repositioning;
                        return;
                    }

                    // Priority 2: Check LOS/Range to stop shooting
                    if (!hasLOS || dist > shootingRange + 4f)
                    {
                        shooter.CancelShooting();
                        currentState = AIState.Chasing;
                        return;
                    }

                    // Handle aiming rotation and shooter cycle
                    if (!shooter.IsShooting)
                    {
                        shooter.StartShootingCycle(player);
                    }
                    else if (!shooter.IsPrepping)
                    {
                        // Rotate towards player between shots if not in prep phase
                        RotateTowards(player.position - transform.position);
                    }
                    break;

                case AIState.Repositioning:
                    if (agent.remainingDistance <= agent.stoppingDistance + 0.5f || !agent.hasPath)
                    {
                        currentState = AIState.Shooting;
                        if (agent.isOnNavMesh) agent.isStopped = true;
                    }
                    else
                    {
                        RotateTowards(agent.desiredVelocity);
                    }
                    break;
            }
        }

        private bool CheckLOS()
        {
            if (player == null) return false;
            
            Vector3 start = transform.position + Vector3.up * 1.5f;
            Vector3 targetPoint = player.position + Vector3.up * 1.2f;
            Vector3 dir = targetPoint - start;
            float dist = dir.magnitude;

            // Use Raycast to check if anything blocks the view to the player.
            // We ignore the player itself and our own colliders.
            if (Physics.Raycast(start, dir, out RaycastHit hit, dist, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                bool hitPlayer = hit.collider.GetComponentInParent<PlayerHealth>() != null;
                bool hitSelf = hit.collider.transform.IsChildOf(transform);
                
                if (!hitPlayer && !hitSelf)
                {
                    return false;
                }
            }
            return true;
}

        private void FindRepositionPoint()
        {
            for (int i = 0; i < 15; i++)
            {
                Vector2 randomCircle = Random.insideUnitCircle * repositionRadius;
                Vector3 offset = new Vector3(randomCircle.x, 0, randomCircle.y);
                Vector3 candidate = transform.position + offset;

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    float d = Vector3.Distance(hit.position, player.position);
                    if (d > minRange + 3f)
                    {
                        if (agent.isOnNavMesh)
                        {
                            agent.isStopped = false;
                            agent.SetDestination(hit.position);
                            return;
                        }
                    }
                }
            }
        }

        private void RotateTowards(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) return;

            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
            if (invertFacing) targetRotation *= Quaternion.Euler(0, 180, 0);

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }

        private void UpdateAnimator()
        {
            if (animator == null) return;
            float speed = agent != null && agent.isOnNavMesh && !agent.isStopped ? agent.velocity.magnitude : 0f;
            animator.SetFloat(SpeedHash, speed);
        }

        private void TryFindPlayer()
        {
            if (playerTarget != null)
            {
                player = playerTarget;
                return;
            }

            if (PlayerHealth.Instance != null)
            {
                player = PlayerHealth.Instance.transform;
            }
        }

        public void SetAggressive()
        {
            isAggressive = true;
            if (currentState == AIState.Passive)
            {
                currentState = AIState.Chasing;
            }
        }

        public void ResetForSpawn()
        {
            isAggressive = false;
            currentState = AIState.Passive;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }
            if (shooter != null) shooter.CancelShooting();
            ConfigureAgentForCurrentArena();
        }

        private void ConfigureAgentForCurrentArena()
        {
            if (agent == null || !TryGetArenaAgentTypeId(gameObject.scene.name, out int arenaAgentTypeId))
            {
                return;
            }

            if (agent.agentTypeID == arenaAgentTypeId)
            {
                return;
            }

            bool wasEnabled = agent.enabled;
            Vector3 agentPosition = transform.position;

            if (wasEnabled) agent.enabled = false;
            agent.agentTypeID = arenaAgentTypeId;
            if (wasEnabled)
            {
                agent.enabled = true;
                if (!agent.isOnNavMesh && NavMesh.SamplePosition(agentPosition, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
                {
                    agent.Warp(navHit.position);
                }
            }
        }

        private static bool TryGetArenaAgentTypeId(string sceneName, out int agentTypeId)
        {
            switch (sceneName)
            {
                case "Ar1": agentTypeId = Ar1AgentTypeId; return true;
                case "Ar2": agentTypeId = Ar2AgentTypeId; return true;
                case "Ar3": agentTypeId = Ar3AgentTypeId; return true;
                case "Ar4": agentTypeId = Ar4AgentTypeId; return true;
                default: agentTypeId = 0; return false;
            }
        }
    }
}
