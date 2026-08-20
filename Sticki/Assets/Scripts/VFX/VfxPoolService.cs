using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Sticki.Combat
{
    public class VfxPoolService : MonoBehaviour
    {
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

        private sealed class PooledVfxObject : MonoBehaviour
        {
            public int PoolKey;
            public int SpawnToken;
            public ParticleSystem[] ParticleSystems;
        }

        private static VfxPoolService instance;

        [SerializeField] private int defaultMaxPoolSize = 64;

        private readonly Dictionary<int, Pool> pools = new();

        public static void Preload(GameObject prefab, int count, int maxPoolSize = 64)
        {
            if (prefab == null || count <= 0)
            {
                return;
            }

            Instance.InternalPreload(prefab, count, maxPoolSize);
        }

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float lifetimeOverride = -1f, int maxPoolSize = 64)
        {
            if (prefab == null)
            {
                return null;
            }

            return Instance.InternalSpawn(prefab, position, rotation, lifetimeOverride, maxPoolSize);
        }

        public static bool TryGetStats(out int poolCount, out int totalOwned, out int available, out int active)
        {
            if (instance == null)
            {
                poolCount = 0;
                totalOwned = 0;
                available = 0;
                active = 0;
                return false;
            }

            instance.InternalGetStats(out poolCount, out totalOwned, out available, out active);
            return true;
        }

        private static VfxPoolService Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

                GameObject go = new GameObject("[VfxPoolService]");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<VfxPoolService>();
                return instance;
            }
        }

        private void InternalPreload(GameObject prefab, int count, int maxPoolSize)
        {
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

        private GameObject InternalSpawn(GameObject prefab, Vector3 position, Quaternion rotation, float lifetimeOverride, int maxPoolSize)
        {
            Pool pool = GetOrCreatePool(prefab, maxPoolSize);
            GameObject obj = pool.Available.Count > 0 ? pool.Available.Dequeue() : CreatePooledObject(pool);
            if (obj == null)
            {
                return null;
            }

            obj.transform.SetPositionAndRotation(position, rotation);
            obj.SetActive(true);

            PooledVfxObject marker = obj.GetComponent<PooledVfxObject>();
            if (marker == null)
            {
                marker = obj.AddComponent<PooledVfxObject>();
                marker.PoolKey = prefab.GetInstanceID();
                marker.ParticleSystems = obj.GetComponentsInChildren<ParticleSystem>(true);
            }

            marker.SpawnToken++;
            RestartParticleSystems(marker);

            float lifetime = lifetimeOverride > 0f ? lifetimeOverride : EstimateLifetime(marker);
            StartCoroutine(ReturnAfter(marker, lifetime));
            return obj;
        }

        private IEnumerator ReturnAfter(PooledVfxObject marker, float delay)
        {
            int token = marker.SpawnToken;
            yield return new WaitForSeconds(Mathf.Max(0.05f, delay));

            if (marker == null || marker.SpawnToken != token)
            {
                yield break;
            }

            Release(marker.gameObject);
        }

        private void Release(GameObject obj)
        {
            if (obj == null)
            {
                return;
            }

            PooledVfxObject marker = obj.GetComponent<PooledVfxObject>();
            if (marker == null || !pools.TryGetValue(marker.PoolKey, out Pool pool))
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

        private Pool GetOrCreatePool(GameObject prefab, int maxPoolSize)
        {
            int key = prefab.GetInstanceID();
            if (pools.TryGetValue(key, out Pool existing))
            {
                existing.MaxSize = Mathf.Max(existing.MaxSize, maxPoolSize);
                return existing;
            }

            GameObject rootGo = new GameObject(prefab.name + "_Pool");
            rootGo.transform.SetParent(transform, false);
            Pool created = new Pool(prefab, rootGo.transform, Mathf.Max(1, maxPoolSize > 0 ? maxPoolSize : defaultMaxPoolSize));
            pools.Add(key, created);
            return created;
        }

        private GameObject CreatePooledObject(Pool pool)
        {
            if (pool.OwnedObjects.Count >= pool.MaxSize)
            {
                return null;
            }

            GameObject obj = Instantiate(pool.Prefab, pool.Root);
            obj.SetActive(false);

            PooledVfxObject marker = obj.GetComponent<PooledVfxObject>();
            if (marker == null)
            {
                marker = obj.AddComponent<PooledVfxObject>();
            }

            marker.PoolKey = pool.Prefab.GetInstanceID();
            marker.SpawnToken = 0;
            marker.ParticleSystems = obj.GetComponentsInChildren<ParticleSystem>(true);
            pool.OwnedObjects.Add(obj);
            return obj;
        }

        private static void RestartParticleSystems(PooledVfxObject marker)
        {
            ParticleSystem[] systems = marker.ParticleSystems;
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem ps = systems[i];
                if (ps == null)
                {
                    continue;
                }

                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(true);
            }
        }

        private static float EstimateLifetime(PooledVfxObject marker)
        {
            float maxLifetime = 0.5f;
            ParticleSystem[] systems = marker.ParticleSystems;
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem ps = systems[i];
                if (ps == null)
                {
                    continue;
                }

                ParticleSystem.MainModule main = ps.main;
                float startDelay = Evaluate(main.startDelay);
                float startLifetime = Evaluate(main.startLifetime);
                float speed = Mathf.Max(0.01f, main.simulationSpeed);
                float candidate = (startDelay + main.duration + startLifetime) / speed;
                if (candidate > maxLifetime)
                {
                    maxLifetime = candidate;
                }
            }

            return maxLifetime + 0.15f;
        }

        private static float Evaluate(ParticleSystem.MinMaxCurve curve)
        {
            return curve.mode switch
            {
                ParticleSystemCurveMode.Constant => curve.constant,
                ParticleSystemCurveMode.TwoConstants => curve.constantMax,
                ParticleSystemCurveMode.Curve => MaxCurve(curve.curve) * curve.curveMultiplier,
                ParticleSystemCurveMode.TwoCurves => Mathf.Max(MaxCurve(curve.curveMin), MaxCurve(curve.curveMax)) * curve.curveMultiplier,
                _ => 0f
            };
        }

        private static float MaxCurve(AnimationCurve curve)
        {
            if (curve == null || curve.length == 0)
            {
                return 0f;
            }

            float max = curve.keys[0].value;
            for (int i = 1; i < curve.length; i++)
            {
                float v = curve.keys[i].value;
                if (v > max)
                {
                    max = v;
                }
            }

            return max;
        }

        private void InternalGetStats(out int poolCount, out int totalOwned, out int available, out int active)
        {
            poolCount = pools.Count;
            totalOwned = 0;
            available = 0;
            active = 0;

            foreach (KeyValuePair<int, Pool> pair in pools)
            {
                Pool pool = pair.Value;
                if (pool == null)
                {
                    continue;
                }

                int owned = pool.OwnedObjects.Count;
                int free = pool.Available.Count;
                totalOwned += owned;
                available += free;
                active += Mathf.Max(0, owned - free);
            }
        }
    }
}
