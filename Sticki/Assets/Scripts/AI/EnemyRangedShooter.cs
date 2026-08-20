using UnityEngine;
using Sticki.Player;

namespace Sticki.AI
{
    public class EnemyRangedShooter : MonoBehaviour
    {
        [Header("Combat Settings")]
        [SerializeField] private float damage = 12f;
        [SerializeField] private float trackingSpeed = 5f;
        [SerializeField] private bool invertFacing = false;

        [Header("Animation Sync")]
        [SerializeField] private string attackTriggerName = "Attack";

        [Header("References")]
        [SerializeField] private Transform firePoint;
        [SerializeField] private LineRenderer telegraph;
        [SerializeField] private LayerMask targetMask = (1 << 0) | (1 << 9) | (1 << 10) | (1 << 11) | (1 << 12) | (1 << 13) | (1 << 15) | (1 << 16);

        private Transform player;
        private bool isShooting;
        private bool isPrepping;
        private Vector3 currentAimPoint;
        private Animator animator;
        private Collider[] selfColliders;
        private bool shotTriggeredThisCycle;
        private float baseDamage;
        private float effectiveDamage;

        public bool IsShooting => isShooting;
        public bool IsPrepping => isPrepping;

        private void Awake()
        {
            baseDamage = damage;
            effectiveDamage = damage;
            animator = GetComponentInChildren<Animator>();
            selfColliders = GetComponentsInChildren<Collider>();
            
            if (telegraph != null)
            {
                telegraph.enabled = false;
                telegraph.positionCount = 2;
                telegraph.useWorldSpace = true;
            }
        }

        public void StartShootingCycle(Transform target)
        {
            if (isShooting) return;
            player = target;
            currentAimPoint = player.position + Vector3.up * 1.2f;
            isShooting = false;
            isShooting = true;
            isPrepping = false;
            shotTriggeredThisCycle = false;
            if (animator != null && !string.IsNullOrWhiteSpace(attackTriggerName))
            {
                animator.ResetTrigger(attackTriggerName);
                animator.SetTrigger(attackTriggerName);
            }
        }

        public void CancelShooting()
        {
            isShooting = false;
            isPrepping = false;
            shotTriggeredThisCycle = false;
            if (telegraph != null) telegraph.enabled = false;
        }

        private void Update()
        {
            if (!isShooting)
            {
                return;
            }

            TrackAim();
            UpdateRotationTowardsAim();

            if (isPrepping)
            {
                UpdateTelegraph();
            }
        }

        public void AnimationEvent_FireShot()
        {
            if (!isShooting || shotTriggeredThisCycle)
            {
                return;
            }

            shotTriggeredThisCycle = true;
            Fire();
        }

        public void AnimationEvent_FireFinished()
        {
            if (!isShooting)
            {
                return;
            }

            isShooting = false;
            isPrepping = false;
        }

        public void AnimationEvent_PrepStarted()
        {
            if (!isShooting)
            {
                return;
            }

            isPrepping = true;
            if (telegraph != null)
            {
                telegraph.enabled = true;
                UpdateTelegraph();
            }
        }

        public void AnimationEvent_PrepFinished()
        {
            if (!isShooting)
            {
                return;
            }

            isPrepping = false;
            if (telegraph != null)
            {
                telegraph.enabled = false;
            }
        }

        private void TrackAim()
        {
            if (player == null)
            {
                return;
            }

            Vector3 targetPos = player.position + Vector3.up * 1.2f;
            currentAimPoint = Vector3.Lerp(currentAimPoint, targetPos, Time.deltaTime * trackingSpeed);
        }

        private void UpdateRotationTowardsAim()
        {
            Vector3 direction = currentAimPoint - transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction.normalized);
                if (invertFacing) targetRot *= Quaternion.Euler(0, 180, 0);
                
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * trackingSpeed);
            }
        }

        private void UpdateTelegraph()
        {
            if (telegraph == null || firePoint == null) return;

            telegraph.SetPosition(0, firePoint.position);
            
            Vector3 aimDirection = GetAimDirection();
            if (PerformRaycast(aimDirection, out RaycastHit hit, 60f))
            {
                telegraph.SetPosition(1, hit.point);
            }
            else
            {
                telegraph.SetPosition(1, firePoint.position + aimDirection * 60f);
            }
        }

        private void Fire()
        {
            if (firePoint == null) return;

            if (PerformRaycast(GetAimDirection(), out RaycastHit hit, 80f))
            {
                PlayerHealth ph = hit.collider.GetComponentInParent<PlayerHealth>();
                if (ph != null)
                {
                    ph.TakeDamage(effectiveDamage);
                }
            }
        }

        public void ApplyDifficultyMultiplier(float damageMultiplier)
        {
            effectiveDamage = Mathf.Max(1f, baseDamage * Mathf.Max(0.1f, damageMultiplier));
        }

        private bool PerformRaycast(Vector3 direction, out RaycastHit hit, float range)
        {
            // Multiple attempts to skip self-colliders if we hit them
            float remainingDist = range;
            Vector3 currentOrigin = firePoint.position;
            
            for (int i = 0; i < 3; i++) // Max 3 steps to avoid infinite loops
            {
                if (Physics.Raycast(currentOrigin, direction, out hit, remainingDist, targetMask, QueryTriggerInteraction.Ignore))
                {
                    if (IsSelf(hit.collider))
                    {
                        // Advance ray origin past this collider
                        currentOrigin = hit.point + direction * 0.01f;
                        remainingDist -= hit.distance;
                        if (remainingDist <= 0) break;
                        continue;
                    }
                    return true;
                }
                break;
            }
            
            hit = default;
            return false;
        }

        private Vector3 GetAimDirection()
        {
            if (firePoint == null)
            {
                return transform.forward;
            }

            Vector3 direction = currentAimPoint - firePoint.position;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return transform.forward;
            }

            return direction.normalized;
        }

        private bool IsSelf(Collider other)
        {
            if (selfColliders == null) return false;
            foreach (var c in selfColliders)
            {
                if (c == other) return true;
            }
            return false;
        }

    }
}
