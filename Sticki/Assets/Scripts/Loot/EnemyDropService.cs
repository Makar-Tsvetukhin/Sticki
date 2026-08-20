using System.Collections.Generic;
using UnityEngine;

namespace Sticki.Loot
{
    public class EnemyDropService : MonoBehaviour
    {
        [SerializeField] private EnemyDropConfig configOverride;

        private sealed class Pool
        {
            public readonly GameObject Prefab;
            public readonly Transform Root;
            public readonly Queue<GameObject> Available = new();
            public readonly HashSet<GameObject> OwnedObjects = new();
            public int MaxSize;

            public Pool(GameObject prefab, Transform root, int maxSize)
            {
                Prefab = prefab;
                Root = root;
                MaxSize = Mathf.Max(1, maxSize);
            }
        }

        private static EnemyDropService instance;
        private EnemyDropConfig config;
        private readonly Dictionary<int, Pool> pools = new();

        public static void TrySpawnDrop(Vector3 position)
        {
            Instance.InternalTrySpawnDrop(position);
        }

        private static EnemyDropService Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

                GameObject go = new GameObject("[EnemyDropService]");
                instance = go.AddComponent<EnemyDropService>();
                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            config = configOverride != null
                ? configOverride
                : Resources.Load<EnemyDropConfig>("Loot/EnemyDropConfig");
            if (config == null)
            {
                Debug.LogWarning("EnemyDropConfig not found at Resources/Loot/EnemyDropConfig. Enemy drops are disabled.", this);
                return;
            }

            Prewarm(config.ammoPickupPrefab, config.ammoPrewarmCount, config.ammoMaxPoolSize);
            Prewarm(config.healthPickupPrefab, config.healthPrewarmCount, config.healthMaxPoolSize);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void InternalTrySpawnDrop(Vector3 position)
        {
            if (config == null)
            {
                return;
            }

            if (!config.TryRollDrop(out LootPickupType type, out float amount) || amount <= 0f)
            {
                return;
            }

            GameObject prefab = type == LootPickupType.Ammo ? config.ammoPickupPrefab : config.healthPickupPrefab;
            int maxPoolSize = type == LootPickupType.Ammo ? config.ammoMaxPoolSize : config.healthMaxPoolSize;
            GameObject instanceObject = Spawn(prefab, position + Vector3.up * config.spawnHeightOffset, maxPoolSize);
            if (instanceObject == null)
            {
                return;
            }

            LootPickup pickup = instanceObject.GetComponent<LootPickup>();
            if (pickup == null)
            {
                pickup = instanceObject.AddComponent<LootPickup>();
            }

            pickup.Initialize(
                this,
                type,
                amount,
                config.pickupTriggerRadius,
                config.spinSpeed,
                config.bobAmplitude,
                config.bobFrequency);
        }

        public void Release(GameObject obj)
        {
            if (obj == null)
            {
                return;
            }

            int key = ResolvePoolKey(obj);
            if (key == 0 || !pools.TryGetValue(key, out Pool pool))
            {
                Destroy(obj);
                return;
            }

            if (pool.Available.Count >= pool.MaxSize)
            {
                pool.OwnedObjects.Remove(obj);
                Destroy(obj);
                return;
            }

            obj.SetActive(false);
            obj.transform.SetParent(pool.Root, false);
            pool.Available.Enqueue(obj);
        }

        private void Prewarm(GameObject prefab, int count, int maxPoolSize)
        {
            if (prefab == null || count <= 0)
            {
                return;
            }

            Pool pool = GetOrCreatePool(prefab, maxPoolSize);
            int target = Mathf.Min(pool.MaxSize, pool.Available.Count + count);
            while (pool.Available.Count < target)
            {
                GameObject obj = CreatePooledObject(pool);
                if (obj == null)
                {
                    break;
                }

                obj.SetActive(false);
                pool.Available.Enqueue(obj);
            }
        }

        private GameObject Spawn(GameObject prefab, Vector3 position, int maxPoolSize)
        {
            if (prefab == null)
            {
                return null;
            }

            Pool pool = GetOrCreatePool(prefab, maxPoolSize);
            GameObject obj = pool.Available.Count > 0 ? pool.Available.Dequeue() : CreatePooledObject(pool);
            if (obj == null)
            {
                return null;
            }

            obj.transform.SetParent(null, false);
            obj.transform.SetPositionAndRotation(position, Quaternion.identity);
            obj.SetActive(true);
            return obj;
        }

        private Pool GetOrCreatePool(GameObject prefab, int maxPoolSize)
        {
            int key = prefab.GetInstanceID();
            if (pools.TryGetValue(key, out Pool existing))
            {
                existing.MaxSize = Mathf.Max(existing.MaxSize, maxPoolSize);
                return existing;
            }

            GameObject rootObject = new GameObject(prefab.name + "_DropPool");
            rootObject.transform.SetParent(transform, false);
            Pool created = new Pool(prefab, rootObject.transform, maxPoolSize);
            pools.Add(key, created);
            return created;
        }

        private static GameObject CreatePooledObject(Pool pool)
        {
            if (pool.OwnedObjects.Count >= pool.MaxSize)
            {
                return null;
            }

            Object rawInstance = Object.Instantiate((Object)pool.Prefab);
            GameObject obj = rawInstance as GameObject;
            if (obj == null)
            {
                string prefabName = pool.Prefab != null ? pool.Prefab.name : "<null>";
                Debug.LogError($"EnemyDropService failed to instantiate pickup prefab '{prefabName}' as GameObject. Reassign prefab references in EnemyDropConfig.");
                if (rawInstance != null)
                {
                    Object.Destroy(rawInstance);
                }

                return null;
            }

            obj.SetActive(false);
            obj.name = pool.Prefab.name;
            obj.transform.SetParent(pool.Root, false);
            PoolIdentity identity = obj.GetComponent<PoolIdentity>();
            if (identity == null)
            {
                identity = obj.AddComponent<PoolIdentity>();
            }

            identity.PoolKey = pool.Prefab.GetInstanceID();
            pool.OwnedObjects.Add(obj);
            return obj;
        }

        private static int ResolvePoolKey(GameObject obj)
        {
            PoolIdentity identity = obj.GetComponent<PoolIdentity>();
            return identity != null ? identity.PoolKey : 0;
        }

        private sealed class PoolIdentity : MonoBehaviour
        {
            public int PoolKey;
        }
    }
}
