using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Sticki.Player;

namespace Sticki.AI
{
    public class EnemyMeleeAI : MonoBehaviour
    {
        private const int Ar1AgentTypeId = 287145453;
        private const int Ar2AgentTypeId = 658490984;
        private const int Ar3AgentTypeId = -629701670;
        private const int Ar4AgentTypeId = -515339236;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int AttackToRunHash = Animator.StringToHash("AttackToRun");

        [SerializeField] private float attackDistance = 1.5f;
        [SerializeField] private float attackExitDistance = 1.8f;
        [SerializeField] private float attackDamage = 10f;
        [SerializeField] private float attackInterval = 1f;
        [SerializeField] private float attackActiveDuration = 0.2f;
        [SerializeField] private Transform playerTarget;
        [SerializeField] private Animator animator;
        [SerializeField] private string attackTriggerName = "Attack";
        [SerializeField] private bool invertFacing = true;
        [SerializeField] private float rotationLerpSpeed = 12f;
        [SerializeField] private float playerResolveInterval = 0.5f;
        [SerializeField] private float offNavMeshTargetSampleRadius = 6f;

        private float baseAttackDamage;
        private float effectiveAttackDamage;
        private NavMeshAgent agent;
        private NavMeshPath cachedPath;
        private Transform player;
        private float nextAttackTime;
        private float attackWindowCloseTime;
        private float nextPlayerResolveTime;
        private bool isAggressive = false;
        private bool isAttackWindowOpen;
        private bool isInAttackRange;
        private int attackSequenceId;

        public static bool GlobalCombatActive = false;

        public bool IsAggressive => isAggressive;
        public bool IsAttackWindowOpen => isAttackWindowOpen;
        public float AttackDamage => effectiveAttackDamage;
        public int AttackSequenceId => attackSequenceId;

        private void Awake()
        {
            baseAttackDamage = attackDamage;
            effectiveAttackDamage = attackDamage;
            agent = GetComponent<NavMeshAgent>();
            cachedPath = new NavMeshPath();
            ConfigureAgentForCurrentArena();
            if (agent != null)
            {
                // We rotate manually to handle rigs with inverted forward axis.
                agent.updateRotation = false;
                if (agent.stoppingDistance <= 0.01f)
                {
                    agent.stoppingDistance = Mathf.Max(0.1f, attackDistance * 0.8f);
                }
            }
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        private void OnEnable()
        {
            ConfigureAgentForCurrentArena();
        }

        private void Start()
        {
            TryResolvePlayer();
        }

        private void Update()
        {
            if (!isAggressive && GlobalCombatActive)
            {
                isAggressive = true;
            }

            if (player == null || !player.gameObject.activeInHierarchy)
            {
                if (Time.time >= nextPlayerResolveTime)
                {
                    TryResolvePlayer();
                }

                if (CanControlAgent())
                {
                    agent.isStopped = true;
                }
                return;
            }

            if (!isAggressive)
            {
                KeepPassiveIdle();
                return;
            }

            float distanceToPlayer = Vector3.Distance(transform.position, player.position);
            TryForceExitFinishedAttack(distanceToPlayer);
            bool attackAnimationActive = IsAttackAnimationActive();

            // During attack animation enemy must stay committed and not start chasing.
            if (attackAnimationActive)
            {
                if (CanControlAgent())
                {
                    agent.isStopped = true;
                }

                RotateTowards(player.position - transform.position);
                UpdateAnimatorSpeed(0f);
                return;
            }

            isInAttackRange = isInAttackRange
                ? distanceToPlayer <= Mathf.Max(attackDistance, attackExitDistance)
                : distanceToPlayer <= attackDistance;

            if (isInAttackRange)
            {
                if (CanControlAgent())
                {
                    agent.isStopped = true;
                }
                RotateTowards(player.position - transform.position);
                UpdateAttackCycle();
                UpdateAnimatorSpeed(0f);
            }
            else
            {
                isAttackWindowOpen = false;
                if (CanControlAgent())
                {
                    agent.isStopped = false;
                    Vector3 chaseTarget = ResolveChaseDestination();
                    agent.SetDestination(chaseTarget);
                    Vector3 lookDirection = agent.desiredVelocity.sqrMagnitude > 0.0001f
                        ? agent.desiredVelocity
                        : chaseTarget - transform.position;
                    RotateTowards(lookDirection);
                    UpdateAnimatorSpeed(agent.velocity.magnitude);
                }
                else
                {
                    UpdateAnimatorSpeed(0f);
                }
            }
        }

        private void UpdateAttackCycle()
        {
            float now = Time.time;

            if (isAttackWindowOpen && now >= attackWindowCloseTime)
            {
                isAttackWindowOpen = false;
            }

            if (now < nextAttackTime)
            {
                return;
            }

            if (IsInUninterruptibleAttackPhase())
            {
                return;
            }

            StartNewAttackCycle(now);

            if (animator != null)
            {
                if (!animator.IsInTransition(0))
                {
                    AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
                    if (current.IsName("Attack") && current.normalizedTime >= 0.98f && animator.HasState(0, AttackHash))
                    {
                        // If target stays in melee range, repeat Attack directly without IdleToAttack.
                        animator.Play(AttackHash, 0, 0f);
                        return;
                    }
                }

                if (!string.IsNullOrWhiteSpace(attackTriggerName))
                {
                    animator.ResetTrigger(attackTriggerName);
                    animator.SetTrigger(attackTriggerName);
                }
            }
        }

        public void SetAggressive()
        {
            isAggressive = true;
        }

        public void ApplyDifficultyMultiplier(float damageMultiplier)
        {
            effectiveAttackDamage = Mathf.Max(1f, baseAttackDamage * Mathf.Max(0.1f, damageMultiplier));
        }

        public void ResetForSpawn()
        {
            isAggressive = false;
            isAttackWindowOpen = false;
            isInAttackRange = false;
            attackSequenceId = 0;
            attackWindowCloseTime = 0f;
            nextAttackTime = 0f;
            nextPlayerResolveTime = 0f;

            if (agent != null)
            {
                if (CanControlAgent())
                {
                    agent.ResetPath();
                    agent.isStopped = true;
                }
            }

            if (animator != null)
            {
                if (!string.IsNullOrWhiteSpace(attackTriggerName))
                {
                    animator.ResetTrigger(attackTriggerName);
                }
                animator.SetFloat(SpeedHash, 0f);
                animator.CrossFadeInFixedTime("Idle", 0.02f);
            }
        }

        private void TryResolvePlayer()
        {
            nextPlayerResolveTime = Time.time + Mathf.Max(0.1f, playerResolveInterval);

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

        private bool IsAttackAnimationActive()
        {
            if (animator == null)
            {
                return false;
            }

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName("IdleToAttack") || stateInfo.IsName("RunToAttack") || stateInfo.IsName("Attack") || stateInfo.IsName("AttackToRun"))
            {
                return true;
            }

            if (!animator.IsInTransition(0))
            {
                return false;
            }

            AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(0);
            return nextState.IsName("IdleToAttack") || nextState.IsName("RunToAttack") || nextState.IsName("Attack") || nextState.IsName("AttackToRun");
        }

        private bool IsInUninterruptibleAttackPhase()
        {
            if (animator == null)
            {
                return false;
            }

            if (animator.IsInTransition(0))
            {
                AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(0);
                return nextState.IsName("IdleToAttack") || nextState.IsName("RunToAttack") || nextState.IsName("Attack") || nextState.IsName("AttackToRun");
            }

            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);

            if (current.IsName("IdleToAttack") || current.IsName("RunToAttack") || current.IsName("AttackToRun"))
            {
                return true;
            }

            if (current.IsName("Attack"))
            {
                return current.normalizedTime < 0.98f;
            }

            return false;
        }

        private void TryForceExitFinishedAttack(float distanceToPlayer)
        {
            if (animator == null || animator.IsInTransition(0))
            {
                return;
            }

            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            if (!current.IsName("Attack"))
            {
                return;
            }

            // Non-loop Attack can miss Exit Time transition window and get stuck on final frame.
            if (current.normalizedTime < 0.99f)
            {
                return;
            }

            bool shouldAttackAgain = distanceToPlayer <= attackDistance;
            if (shouldAttackAgain && Time.time >= nextAttackTime && animator.HasState(0, AttackHash))
            {
                // Recovery path: if Attack got stuck at end, replay Attack directly.
                StartNewAttackCycle(Time.time);
                animator.Play(AttackHash, 0, 0f);
                return;
            }

            if (animator.HasState(0, AttackToRunHash))
            {
                animator.CrossFadeInFixedTime(AttackToRunHash, 0.03f);
            }
        }

        private void RotateTowards(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            if (invertFacing)
            {
                targetRotation *= Quaternion.Euler(0f, 180f, 0f);
            }

            float t = Mathf.Clamp01(Time.deltaTime * Mathf.Max(1f, rotationLerpSpeed));
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, t);
        }

        private void UpdateAnimatorSpeed(float speed)
        {
            if (animator == null)
            {
                return;
            }

            animator.SetFloat(SpeedHash, Mathf.Max(0f, speed));
        }

        private void KeepPassiveIdle()
        {
            if (animator == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(attackTriggerName))
            {
                animator.ResetTrigger(attackTriggerName);
            }

            animator.SetFloat(SpeedHash, 0f);

            if (IsAttackAnimationActive())
            {
                animator.CrossFadeInFixedTime("Idle", 0.05f);
            }
        }

        private void StartNewAttackCycle(float now)
        {
            isAttackWindowOpen = true;
            attackWindowCloseTime = now + Mathf.Max(0.05f, attackActiveDuration);
            nextAttackTime = now + Mathf.Max(0.05f, attackInterval);
            attackSequenceId++;
        }

        private Vector3 ResolveChaseDestination()
        {
            if (player == null)
            {
                return transform.position;
            }

            Vector3 playerPosition = player.position;
            float sampleRadius = Mathf.Max(1f, offNavMeshTargetSampleRadius);
            int areaMask = agent != null ? agent.areaMask : NavMesh.AllAreas;
            Vector3 sampledTarget = playerPosition;

            if (NavMesh.SamplePosition(playerPosition, out NavMeshHit navHit, sampleRadius, areaMask))
            {
                sampledTarget = navHit.position;
            }

            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                return sampledTarget;
            }

            if (cachedPath == null)
            {
                cachedPath = new NavMeshPath();
            }

            if (agent.CalculatePath(sampledTarget, cachedPath))
            {
                if (cachedPath.status == NavMeshPathStatus.PathComplete)
                {
                    return sampledTarget;
                }

                if (cachedPath.corners != null && cachedPath.corners.Length > 0)
                {
                    return cachedPath.corners[cachedPath.corners.Length - 1];
                }
            }

            if (NavMesh.Raycast(agent.transform.position, sampledTarget, out NavMeshHit blockedHit, areaMask))
            {
                return blockedHit.position;
            }

            return sampledTarget;
        }

        private bool CanControlAgent()
        {
            return agent != null && agent.enabled && agent.isOnNavMesh;
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

            if (wasEnabled)
            {
                agent.enabled = false;
            }

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
                case "Ar1":
                    agentTypeId = Ar1AgentTypeId;
                    return true;
                case "Ar2":
                    agentTypeId = Ar2AgentTypeId;
                    return true;
                case "Ar3":
                    agentTypeId = Ar3AgentTypeId;
                    return true;
                case "Ar4":
                    agentTypeId = Ar4AgentTypeId;
                    return true;
                default:
                    agentTypeId = 0;
                    return false;
            }
        }
    }
}
