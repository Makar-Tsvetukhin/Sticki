using Sticki.Core.Interfaces;
using UnityEngine;

namespace Sticki.Combat
{
    public class EnemyHitboxProxy : MonoBehaviour, IDamageable
    {
        [SerializeField] private EnemyHealth ownerHealth;

        public EnemyHealth OwnerHealth => ownerHealth;

        private void Awake()
        {
            if (ownerHealth == null)
            {
                ownerHealth = GetComponentInParent<EnemyHealth>();
            }

            // Hitboxes should not physically push CharacterController.
            Collider hitbox = GetComponent<Collider>();
            if (hitbox != null && !hitbox.isTrigger)
            {
                hitbox.isTrigger = true;
            }
        }

        public void SetOwner(EnemyHealth owner)
        {
            ownerHealth = owner;
        }

        public void TakeDamage(float amount)
        {
            if (ownerHealth == null)
            {
                return;
            }

            ownerHealth.TakeDamage(amount);
        }
    }
}
