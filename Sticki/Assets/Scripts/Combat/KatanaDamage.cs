using System.Collections.Generic;
using Sticki.AI;
using Sticki.Core.Interfaces;
using Sticki.Player;
using UnityEngine;

namespace Sticki.Combat
{
    [RequireComponent(typeof(Collider))]
    public class KatanaDamage : MonoBehaviour
    {
        [SerializeField] private EnemyMeleeAI ownerAI;
        [SerializeField] private LayerMask targetMask = ~0;
        [SerializeField] private float hitCooldownPerTarget = 0.2f;
        [SerializeField] private bool onlyWhenAttackWindow = true;
        [SerializeField] private bool onlyOneHitPerAttackCycle = true;

        private readonly Dictionary<int, float> nextHitTimeByTarget = new();
        private readonly Dictionary<int, int> lastHitSequenceByTarget = new();
        private Collider hitCollider;

        private void Awake()
        {
            hitCollider = GetComponent<Collider>();
            if (!hitCollider.isTrigger)
            {
                hitCollider.isTrigger = true;
            }

            if (ownerAI == null)
            {
                ownerAI = GetComponentInParent<EnemyMeleeAI>();
            }
        }

        private void OnEnable()
        {
            nextHitTimeByTarget.Clear();
            lastHitSequenceByTarget.Clear();
        }

        private void OnTriggerStay(Collider other)
        {
            if (ownerAI == null || !ownerAI.IsAggressive)
            {
                return;
            }

            if (onlyWhenAttackWindow && !ownerAI.IsAttackWindowOpen)
            {
                return;
            }

            if ((targetMask.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
            if (playerHealth == null)
            {
                return;
            }

            if (playerHealth.IsInvulnerable)
            {
                // Do not consume current swing if target is still in i-frames.
                return;
            }

            int targetId = other.transform.root.GetInstanceID();
            int attackSequenceId = ownerAI.AttackSequenceId;
            if (onlyOneHitPerAttackCycle &&
                lastHitSequenceByTarget.TryGetValue(targetId, out int lastHitSequence) &&
                lastHitSequence == attackSequenceId)
            {
                return;
            }

            float now = Time.time;
            if (!onlyOneHitPerAttackCycle &&
                nextHitTimeByTarget.TryGetValue(targetId, out float nextHitTime) &&
                now < nextHitTime)
            {
                return;
            }

            if (!onlyOneHitPerAttackCycle)
            {
                nextHitTimeByTarget[targetId] = now + Mathf.Max(0.01f, hitCooldownPerTarget);
            }

            if (onlyOneHitPerAttackCycle)
            {
                lastHitSequenceByTarget[targetId] = attackSequenceId;
            }

            playerHealth.TakeDamage(ownerAI.AttackDamage);
        }
    }
}
