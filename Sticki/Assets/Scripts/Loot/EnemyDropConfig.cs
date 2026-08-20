using UnityEngine;

namespace Sticki.Loot
{
    [CreateAssetMenu(menuName = "Sticki/Loot/Enemy Drop Config", fileName = "EnemyDropConfig")]
    public class EnemyDropConfig : ScriptableObject
    {
        [Header("Prefabs")]
        public GameObject ammoPickupPrefab;
        public GameObject healthPickupPrefab;

        [Header("Drop Chances")]
        [Range(0f, 1f)] public float ammoDropChance = 0.25f;
        [Range(0f, 1f)] public float healthDropChance = 0.15f;

        [Header("Pickup Values")]
        [Min(1)] public int ammoPickupAmount = 24;
        [Min(1f)] public float healthPickupAmount = 25f;

        [Header("Spawn")]
        [Min(0f)] public float spawnHeightOffset = 0.35f;

        [Header("Pickup Collision")]
        [Min(0.1f)] public float pickupTriggerRadius = 0.85f;

        [Header("Pooling")]
        [Min(0)] public int ammoPrewarmCount = 12;
        [Min(0)] public int healthPrewarmCount = 8;
        [Min(1)] public int ammoMaxPoolSize = 32;
        [Min(1)] public int healthMaxPoolSize = 24;

        [Header("Presentation")]
        [Min(0f)] public float spinSpeed = 90f;
        [Min(0f)] public float bobAmplitude = 0.08f;
        [Min(0f)] public float bobFrequency = 2f;

        public bool TryRollDrop(out LootPickupType type, out float amount)
        {
            float ammoChance = Mathf.Clamp01(ammoDropChance);
            float healthChance = Mathf.Clamp01(healthDropChance);
            float totalChance = Mathf.Clamp01(ammoChance + healthChance);
            float roll = Random.value;

            if (roll > totalChance)
            {
                type = LootPickupType.Ammo;
                amount = 0f;
                return false;
            }

            if (roll <= ammoChance)
            {
                type = LootPickupType.Ammo;
                amount = Mathf.Max(1f, ammoPickupAmount);
                return ammoPickupPrefab != null;
            }

            type = LootPickupType.Health;
            amount = Mathf.Max(1f, healthPickupAmount);
            return healthPickupPrefab != null;
        }
    }
}
