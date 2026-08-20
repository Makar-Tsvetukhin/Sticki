using Sticki.Combat;
using Sticki.Player;
using UnityEngine;

namespace Sticki.Loot
{
    public enum LootPickupType
    {
        Ammo = 0,
        Health = 1
    }

    public class LootPickup : MonoBehaviour
    {
        private EnemyDropService owner;
        private LootPickupType pickupType;
        private float amount;
        private float spinSpeed;
        private float bobAmplitude;
        private float bobFrequency;
        private bool isConsumed;
        private float baseY;
        private float bobSeed;
        private SphereCollider triggerCollider;

        private void Awake()
        {
            triggerCollider = GetComponent<SphereCollider>();
            if (triggerCollider == null)
            {
                triggerCollider = gameObject.AddComponent<SphereCollider>();
            }

            triggerCollider.isTrigger = true;

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }

            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;
            rb.constraints = RigidbodyConstraints.FreezePosition;
        }

        private void OnEnable()
        {
            isConsumed = false;
            baseY = transform.position.y;
            bobSeed = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            if (spinSpeed > 0f)
            {
                transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
            }

            if (bobAmplitude > 0f && bobFrequency > 0f)
            {
                Vector3 pos = transform.position;
                pos.y = baseY + Mathf.Sin((Time.time * bobFrequency) + bobSeed) * bobAmplitude;
                transform.position = pos;
            }
        }

        public void Initialize(
            EnemyDropService service,
            LootPickupType type,
            float pickupAmount,
            float triggerRadius,
            float spinSpeedValue,
            float bobAmplitudeValue,
            float bobFrequencyValue)
        {
            owner = service;
            pickupType = type;
            amount = pickupAmount;
            spinSpeed = spinSpeedValue;
            bobAmplitude = bobAmplitudeValue;
            bobFrequency = bobFrequencyValue;

            if (triggerCollider == null)
            {
                triggerCollider = GetComponent<SphereCollider>();
            }

            if (triggerCollider != null)
            {
                triggerCollider.radius = Mathf.Max(0.1f, triggerRadius);
            }

            baseY = transform.position.y;
            bobSeed = Random.Range(0f, Mathf.PI * 2f);
            isConsumed = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryConsume(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryConsume(other);
        }

        private void TryConsume(Collider other)
        {
            if (isConsumed || other == null)
            {
                return;
            }

            Transform root = other.transform.root;
            if (root == null)
            {
                return;
            }

            switch (pickupType)
            {
                case LootPickupType.Ammo:
                {
                    PlayerCombat combat = root.GetComponentInChildren<PlayerCombat>();
                    if (combat == null)
                    {
                        return;
                    }

                    int added = combat.AddReserveAmmoToActiveWeapon(Mathf.RoundToInt(amount));
                    if (added <= 0)
                    {
                        return;
                    }

                    Consume();
                    return;
                }

                case LootPickupType.Health:
                {
                    PlayerHealth health = root.GetComponentInChildren<PlayerHealth>();
                    if (health == null || health.IsDead)
                    {
                        return;
                    }

                    float before = health.CurrentHealth;
                    health.Heal(amount);
                    if (health.CurrentHealth <= before)
                    {
                        return;
                    }

                    Consume();
                    return;
                }
            }
        }

        private void Consume()
        {
            if (isConsumed)
            {
                return;
            }

            isConsumed = true;
            owner?.Release(gameObject);
        }
    }
}
