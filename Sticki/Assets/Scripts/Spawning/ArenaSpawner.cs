using System;
using System.Collections;
using System.Collections.Generic;
using Sticki.AI;
using Sticki.Combat;
using Sticki.Core;
using Sticki.Player;
using Sticki.Spawning.Config;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Sticki.Spawning
{
    public class ArenaSpawner : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private ArenaSpawnConfig config;

        [Header("Scene References")]
        [SerializeField] private Transform initialEnemiesParent;
        [SerializeField] private Transform spawnPointsRoot;
        [SerializeField] private Transform player;
        
        [Header("Spawn Sampling")]
        [SerializeField] private bool generatePointsFromNavMesh = true;
        [SerializeField] [Min(16)] private int navMeshPointCount = 300;
        [SerializeField] private bool includeSpawnPointsRootPoints = true;
        [SerializeField] private string noSpawnAreaName = "NoSpawn";

        [Header("Flow")]
        [SerializeField] private bool autoStartWhenCombatActive = true;
        [SerializeField] private bool debugLogs;
        public event Action OnArenaCleared;

        private readonly Dictionary<string, EnemyPool> poolsById = new();
        private readonly List<Vector3> spawnPoints = new();
        private readonly HashSet<EnemyHealth> trackedAlive = new();
        private readonly List<RuntimeEnemyType> runtimeEnemyTypes = new();
        private readonly List<RuntimeWave> runtimeWaves = new();

        private int initialEnemyCount;
        private int dynamicSpawnedCount;
        private int totalKilledCount;
        private bool dynamicLoopStarted;
        private bool arenaClearedRaised;
        private int navMeshAllowedAreaMask = NavMesh.AllAreas;
        private readonly List<EnemyHealth> pruneBuffer = new();
        private bool pruneScheduled;
        private ArenaDifficultySnapshot difficulty;

        public int TotalKilledCount => totalKilledCount;
        public int TargetKillCount => initialEnemyCount + CalculateDynamicSpawnTargetCount();

        public void Configure(ArenaSpawnConfig arenaConfig, Transform enemiesRoot, Transform playerTransform, Transform pointsRoot = null)
        {
            if (arenaConfig != null)
            {
                config = arenaConfig;
            }

            if (enemiesRoot != null)
            {
                initialEnemiesParent = enemiesRoot;
            }

            if (playerTransform != null)
            {
                player = playerTransform;
            }

            if (pointsRoot != null)
            {
                spawnPointsRoot = pointsRoot;
            }
        }

        private void Awake()
        {
            if (config == null)
            {
                Debug.LogError("ArenaSpawner: ArenaSpawnConfig is not assigned.", this);
                enabled = false;
                return;
            }

            if (player == null && PlayerHealth.Instance != null)
            {
                player = PlayerHealth.Instance.transform;
            }

            ResolveDifficulty();
            BuildSpawnPointList();
            RegisterInitialEnemies();
            BuildRuntimeSpawnDefinitions();
            PrewarmPools();

            EnemyMeleeAI.GlobalCombatActive = false;
        }

        private void Start()
        {
            if (player == null && PlayerHealth.Instance != null)
            {
                player = PlayerHealth.Instance.transform;
            }
        }

        private void Update()
        {
            if (!autoStartWhenCombatActive || dynamicLoopStarted || runtimeWaves.Count == 0)
            {
                return;
            }

            if (EnemyMeleeAI.GlobalCombatActive)
            {
                StartDynamicLoop();
            }
        }

        public void StartDynamicLoop()
        {
            if (dynamicLoopStarted || runtimeWaves.Count == 0)
            {
                return;
            }

            dynamicLoopStarted = true;
            StartCoroutine(DynamicSpawnRoutine());
        }

        private IEnumerator DynamicSpawnRoutine()
        {
            float startDelay = GetStartDelayAfterCombat();
            if (startDelay > 0f)
            {
                yield return new WaitForSeconds(startDelay);
            }

            if (runtimeWaves.Count == 0)
            {
                Debug.LogWarning("ArenaSpawner: no runtime waves are available.", this);
                yield break;
            }

            int waveIndex = 0;
            int dynamicSpawnTotal = CalculateDynamicSpawnTargetCount();
            while (dynamicSpawnedCount < dynamicSpawnTotal)
            {
                RuntimeWave wave = runtimeWaves[Mathf.Clamp(waveIndex, 0, runtimeWaves.Count - 1)];
                int remainingTotal = dynamicSpawnTotal - dynamicSpawnedCount;
                int waveTarget = Mathf.Min(wave.enemiesInWave, remainingTotal);
                int waveSpawned = 0;
                int killedAtWaveStart = totalKilledCount;

                while (waveSpawned < waveTarget)
                {
                    int perTick = UnityEngine.Random.Range(
                        Mathf.Max(1, wave.minPerTick),
                        Mathf.Max(wave.minPerTick, wave.maxPerTick) + 1);

                    int requested = Mathf.Min(perTick, waveTarget - waveSpawned);
                    int spawnedNow = SpawnBatch(requested);
                    waveSpawned += spawnedNow;
                    dynamicSpawnedCount += spawnedNow;

                    if (waveSpawned < waveTarget)
                    {
                        yield return new WaitForSeconds(Mathf.Max(0.05f, wave.tickInterval));
                    }
                }

                float delay = Mathf.Max(0f, wave.interWaveDelay);
                if (delay > 0f)
                {
                    float endAt = Time.time + delay;
                    while (Time.time < endAt)
                    {
                        int killedInWaveWindow = totalKilledCount - killedAtWaveStart;
                        float ratio = waveTarget > 0 ? (float)killedInWaveWindow / waveTarget : 1f;
                        if (ratio >= Mathf.Clamp01(wave.earlyNextWaveKillRatio))
                        {
                            break;
                        }

                        yield return null;
                    }
                }

                waveIndex = Mathf.Min(waveIndex + 1, runtimeWaves.Count - 1);
                CheckArenaCleared();
            }
        }

        private int SpawnBatch(int requested)
        {
            if (requested <= 0)
            {
                return 0;
            }

            int spawned = 0;
            int hardAliveCap = Mathf.Max(1, GetHardAliveCap());
            int freeSlots = Mathf.Max(0, hardAliveCap - trackedAlive.Count);
            int toTry = Mathf.Min(requested, freeSlots);
            if (toTry <= 0)
            {
                return 0;
            }

            for (int i = 0; i < toTry; i++)
            {
                if (!TrySelectSpawnPosition(out Vector3 spawnPos, true))
                {
                    if (!TrySelectSpawnPosition(out spawnPos, false))
                    {
                        if (!TrySelectEmergencySpawnPosition(out spawnPos))
                        {
                            if (debugLogs)
                            {
                                Debug.Log("ArenaSpawner: no valid spawn points (strict + relaxed + emergency).", this);
                            }
                            break;
                        }
                    }
                }

                EnemyPool pool = PickPoolByWeight();
                if (pool == null)
                {
                    break;
                }

                EnemyHealth enemy = pool.Get();
                if (enemy == null)
                {
                    continue;
                }

                SpawnEnemy(enemy, spawnPos);
                spawned++;
            }

            if (debugLogs && spawned > 0)
            {
                Debug.Log($"ArenaSpawner: spawned {spawned}, alive now {trackedAlive.Count}.", this);
            }

            return spawned;
        }

        private void SpawnEnemy(EnemyHealth enemy, Vector3 position)
        {
            if (NavMesh.SamplePosition(position, out NavMeshHit navHit, 2.0f, navMeshAllowedAreaMask))
            {
                position = navHit.position;
            }

            enemy.transform.SetPositionAndRotation(position, Quaternion.identity);
            enemy.gameObject.SetActive(true);
            ApplyDifficultyToEnemy(enemy);
            enemy.ResetForSpawnFromPool();

            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled)
            {
                if (!agent.isOnNavMesh && NavMesh.SamplePosition(enemy.transform.position, out NavMeshHit warpHit, 2.0f, navMeshAllowedAreaMask))
                {
                    agent.Warp(warpHit.position);
                }

                if (agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                }
            }

            TrackEnemy(enemy);
        }

        private bool TrySelectSpawnPosition(out Vector3 position, bool strict)
        {
            position = Vector3.zero;
            if (spawnPoints.Count == 0)
            {
                return false;
            }

            if (player == null)
            {
                position = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Count)];
                return true;
            }

            int bestIndex = -1;
            float bestScore = float.MinValue;
            Vector3 playerPos = player.position;
            Vector3 playerForward = player.forward;
            playerForward.y = 0f;

            for (int i = 0; i < spawnPoints.Count; i++)
            {
                Vector3 p = spawnPoints[i];
                float distance = Vector3.Distance(playerPos, p);
                if (distance < GetFallbackMinDistance())
                {
                    continue;
                }

                if (Physics.CheckSphere(p, Mathf.Max(0.05f, GetPointBlockedRadius()), GetSpawnBlockedByMask(), QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                bool hidden = false;
                if (strict)
                {
                    hidden = IsHiddenFromPlayer(playerPos, p);
                    if (GetPreferHiddenSpawnPoints() && distance < GetMinDistanceFromPlayer() && !hidden)
                    {
                        continue;
                    }
                }

                Vector3 toPoint = (p - playerPos);
                toPoint.y = 0f;
                float behindFactor = 0f;
                if (playerForward.sqrMagnitude > 0.0001f && toPoint.sqrMagnitude > 0.0001f)
                {
                    behindFactor = Vector3.Dot(playerForward.normalized, toPoint.normalized) < 0f ? 1f : 0f;
                }

                float hiddenBonus = hidden ? 30f : 0f;
                float score = distance + hiddenBonus + behindFactor * 12f;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                return false;
            }

            position = spawnPoints[bestIndex];
            return true;
        }

        private bool TrySelectEmergencySpawnPosition(out Vector3 position)
        {
            position = Vector3.zero;
            if (spawnPoints.Count == 0 && generatePointsFromNavMesh)
            {
                BuildSpawnPointList();
            }

            if (spawnPoints.Count == 0)
            {
                return false;
            }

            if (player == null)
            {
                position = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Count)];
                return true;
            }

            int bestIndex = -1;
            float bestDistance = float.MinValue;
            Vector3 playerPos = player.position;

            for (int i = 0; i < spawnPoints.Count; i++)
            {
                Vector3 p = spawnPoints[i];
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 1.5f, navMeshAllowedAreaMask))
                {
                    continue;
                }

                if (IsOccupiedByAliveEnemy(hit.position, 0.8f))
                {
                    continue;
                }

                float distance = (playerPos - hit.position).sqrMagnitude;
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                    position = hit.position;
                }
            }

            if (bestIndex >= 0)
            {
                return true;
            }

            for (int i = 0; i < spawnPoints.Count; i++)
            {
                Vector3 p = spawnPoints[i];
                float distance = (playerPos - p).sqrMagnitude;
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                    position = p;
                }
            }

            return bestIndex >= 0;
        }

        private bool IsHiddenFromPlayer(Vector3 playerPos, Vector3 spawnPos)
        {
            Vector3 from = playerPos + Vector3.up * 1.4f;
            Vector3 to = spawnPos + Vector3.up * 1.0f;
            return Physics.Linecast(from, to, GetLineOfSightMask(), QueryTriggerInteraction.Ignore);
        }

        private EnemyPool PickPoolByWeight()
        {
            float totalWeight = 0f;
            foreach (RuntimeEnemyType type in runtimeEnemyTypes)
            {
                if (type.prefab == null || type.weight <= 0f || !poolsById.ContainsKey(type.id))
                {
                    continue;
                }
                totalWeight += type.weight;
            }

            if (totalWeight <= 0f)
            {
                return null;
            }

            float pick = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;
            foreach (RuntimeEnemyType type in runtimeEnemyTypes)
            {
                if (type.prefab == null || type.weight <= 0f || !poolsById.ContainsKey(type.id))
                {
                    continue;
                }

                cumulative += type.weight;
                if (pick <= cumulative)
                {
                    return poolsById[type.id];
                }
            }

            return null;
        }

        private void RegisterInitialEnemies()
        {
            EnemyHealth[] initialEnemies;
            if (initialEnemiesParent == null)
            {
                Debug.LogError("ArenaSpawner: initialEnemiesParent is not assigned.", this);
                initialEnemies = Array.Empty<EnemyHealth>();
            }
            else
            {
                initialEnemies = initialEnemiesParent.GetComponentsInChildren<EnemyHealth>(true);
            }

            for (int i = 0; i < initialEnemies.Length; i++)
            {
                EnemyHealth enemy = initialEnemies[i];
                if (enemy == null)
                {
                    continue;
                }

                ApplyDifficultyToEnemy(enemy);
                TrackEnemy(enemy);
                initialEnemyCount++;
            }
        }

        private void BuildSpawnPointList()
        {
            spawnPoints.Clear();

            if (includeSpawnPointsRootPoints && spawnPointsRoot != null)
            {
                for (int i = 0; i < spawnPointsRoot.childCount; i++)
                {
                    spawnPoints.Add(spawnPointsRoot.GetChild(i).position);
                }
            }

            if (generatePointsFromNavMesh)
            {
                BuildNavMeshSpawnPointList();
            }

            if (debugLogs)
            {
                Debug.Log($"ArenaSpawner: spawn points prepared = {spawnPoints.Count}.", this);
            }
        }

        private void BuildNavMeshSpawnPointList()
        {
            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            if (triangulation.indices == null || triangulation.indices.Length < 3 || triangulation.vertices == null || triangulation.vertices.Length == 0)
            {
                Debug.LogWarning("ArenaSpawner: NavMesh triangulation is empty. Bake NavMesh first.", this);
                return;
            }

            int noSpawnArea = NavMesh.GetAreaFromName(noSpawnAreaName);
            navMeshAllowedAreaMask = NavMesh.AllAreas;
            if (noSpawnArea >= 0)
            {
                navMeshAllowedAreaMask &= ~(1 << noSpawnArea);
            }

            List<int> candidateTriangles = new();
            List<float> triangleAreas = new();
            float areaSum = 0f;

            int triangleCount = triangulation.indices.Length / 3;
            for (int tri = 0; tri < triangleCount; tri++)
            {
                int triArea = triangulation.areas[tri];
                if ((navMeshAllowedAreaMask & (1 << triArea)) == 0)
                {
                    continue;
                }

                int i0 = triangulation.indices[tri * 3];
                int i1 = triangulation.indices[tri * 3 + 1];
                int i2 = triangulation.indices[tri * 3 + 2];

                Vector3 a = triangulation.vertices[i0];
                Vector3 b = triangulation.vertices[i1];
                Vector3 c = triangulation.vertices[i2];
                float triSurface = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                if (triSurface <= 0.0001f)
                {
                    continue;
                }

                areaSum += triSurface;
                candidateTriangles.Add(tri);
                triangleAreas.Add(areaSum);
            }

            if (candidateTriangles.Count == 0 || areaSum <= 0.0001f)
            {
                Debug.LogWarning("ArenaSpawner: no valid NavMesh triangles for spawn sampling.", this);
                return;
            }

            int requestedPoints = Mathf.Max(16, navMeshPointCount);
            int attempts = requestedPoints * 8;
            while (spawnPoints.Count < requestedPoints && attempts-- > 0)
            {
                float pick = UnityEngine.Random.Range(0f, areaSum);
                int triIndex = 0;
                while (triIndex < triangleAreas.Count && pick > triangleAreas[triIndex])
                {
                    triIndex++;
                }

                triIndex = Mathf.Clamp(triIndex, 0, candidateTriangles.Count - 1);
                int tri = candidateTriangles[triIndex];

                int i0 = triangulation.indices[tri * 3];
                int i1 = triangulation.indices[tri * 3 + 1];
                int i2 = triangulation.indices[tri * 3 + 2];

                Vector3 a = triangulation.vertices[i0];
                Vector3 b = triangulation.vertices[i1];
                Vector3 c = triangulation.vertices[i2];
                Vector3 sample = RandomPointInTriangle(a, b, c);

                if (!NavMesh.SamplePosition(sample, out NavMeshHit hit, 1.0f, navMeshAllowedAreaMask))
                {
                    continue;
                }

                if (Physics.CheckSphere(hit.position, Mathf.Max(0.05f, GetPointBlockedRadius()), GetSpawnBlockedByMask(), QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                if (HasNearbyPoint(hit.position, 0.75f))
                {
                    continue;
                }

                spawnPoints.Add(hit.position);
            }
        }

        private static Vector3 RandomPointInTriangle(Vector3 a, Vector3 b, Vector3 c)
        {
            float r1 = Mathf.Sqrt(UnityEngine.Random.value);
            float r2 = UnityEngine.Random.value;
            return (1f - r1) * a + (r1 * (1f - r2)) * b + (r1 * r2) * c;
        }

        private bool HasNearbyPoint(Vector3 position, float minDistance)
        {
            float sqr = minDistance * minDistance;
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                if ((spawnPoints[i] - position).sqrMagnitude < sqr)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsOccupiedByAliveEnemy(Vector3 position, float minDistance)
        {
            float sqr = minDistance * minDistance;
            foreach (EnemyHealth enemy in trackedAlive)
            {
                if (enemy == null || enemy.IsDead)
                {
                    continue;
                }

                if ((enemy.transform.position - position).sqrMagnitude < sqr)
                {
                    return true;
                }
            }

            return false;
        }

        private void PrewarmPools()
        {
            if (runtimeEnemyTypes.Count == 0)
            {
                return;
            }

            foreach (RuntimeEnemyType type in runtimeEnemyTypes)
            {
                if (type.prefab == null || string.IsNullOrWhiteSpace(type.id))
                {
                    continue;
                }

                EnemyPool pool = new EnemyPool(type.id, type.prefab, transform, type.maxPoolSize, this);
                pool.Prewarm(type.prewarmCount);
                poolsById[type.id] = pool;
            }
        }

        private void TrackEnemy(EnemyHealth enemy)
        {
            if (enemy == null)
            {
                return;
            }

            if (trackedAlive.Add(enemy))
            {
                enemy.OnDied += HandleEnemyDied;
            }
        }

        private void UntrackEnemy(EnemyHealth enemy)
        {
            if (enemy == null)
            {
                return;
            }

            if (trackedAlive.Remove(enemy))
            {
                enemy.OnDied -= HandleEnemyDied;
            }
        }

        private void HandleEnemyDied()
        {
            totalKilledCount++;
            RunSessionController.Instance.RegisterKill();
            // Dead enemy remains tracked until pooled object is actually returned.
            if (!pruneScheduled)
            {
                pruneScheduled = true;
                StartCoroutine(PruneDeadAfterFrame());
            }
            CheckArenaCleared();
        }

        private IEnumerator PruneDeadAfterFrame()
        {
            yield return null;
            pruneScheduled = false;
            pruneBuffer.Clear();
            foreach (EnemyHealth enemy in trackedAlive)
            {
                if (enemy == null || enemy.IsDead)
                {
                    pruneBuffer.Add(enemy);
                }
            }

            for (int i = 0; i < pruneBuffer.Count; i++)
            {
                UntrackEnemy(pruneBuffer[i]);
            }

            CheckArenaCleared();
        }

        private void CheckArenaCleared()
        {
            if (arenaClearedRaised)
            {
                return;
            }

            bool allDynamicSpawned = dynamicSpawnedCount >= CalculateDynamicSpawnTargetCount();
            if (!allDynamicSpawned)
            {
                return;
            }

            if (trackedAlive.Count > 0)
            {
                return;
            }

            arenaClearedRaised = true;
            if (debugLogs)
            {
                Debug.Log($"ArenaSpawner: arena cleared. Killed {totalKilledCount} (initial {initialEnemyCount}, dynamic {dynamicSpawnedCount}).", this);
            }
            OnArenaCleared?.Invoke();
        }

        private void ResolveDifficulty()
        {
            int roomNumber = 1;
            if (SceneManager.GetActiveScene().name.StartsWith("Ar", StringComparison.Ordinal))
            {
                roomNumber = Mathf.Max(1, Sticki.Core.RunFlowController.Instance.CurrentRoomNumber);
            }

            difficulty = ArenaDifficulty.Evaluate(roomNumber);
        }

        private void BuildRuntimeSpawnDefinitions()
        {
            runtimeEnemyTypes.Clear();
            runtimeWaves.Clear();

            if (config.enemyTypes != null)
            {
                for (int i = 0; i < config.enemyTypes.Count; i++)
                {
                    EnemyTypeConfig type = config.enemyTypes[i];
                    if (type == null || type.prefab == null || string.IsNullOrWhiteSpace(type.id))
                    {
                        continue;
                    }

                    runtimeEnemyTypes.Add(new RuntimeEnemyType(
                        type.id,
                        type.prefab,
                        type.weight,
                        type.prewarmCount,
                        type.maxPoolSize));
                }
            }

            if (config.waves != null)
            {
                for (int i = 0; i < config.waves.Count; i++)
                {
                    WaveConfig wave = config.waves[i];
                    if (wave == null)
                    {
                        continue;
                    }

                    runtimeWaves.Add(new RuntimeWave(
                        Mathf.Max(1, Mathf.RoundToInt(wave.enemiesInWave * difficulty.WaveSizeMultiplier)),
                        Mathf.Max(1, wave.minPerTick),
                        Mathf.Max(wave.minPerTick, wave.maxPerTick),
                        Mathf.Max(0.05f, wave.tickInterval * difficulty.TickIntervalMultiplier),
                        Mathf.Max(0f, wave.interWaveDelay * difficulty.InterWaveDelayMultiplier),
                        Mathf.Clamp01(wave.earlyNextWaveKillRatio)));
                }
            }

            if (runtimeEnemyTypes.Count == 0)
            {
                Debug.LogError("ArenaSpawner: config has no enemy types.", this);
            }

            if (runtimeWaves.Count == 0)
            {
                Debug.LogError("ArenaSpawner: config has no waves.", this);
            }
        }

        private void ApplyDifficultyToEnemy(EnemyHealth enemy)
        {
            if (enemy == null)
            {
                return;
            }

            enemy.ApplyDifficultyMultiplier(difficulty.EnemyHealthMultiplier);

            Sticki.AI.EnemyMeleeAI meleeAI = enemy.GetComponent<Sticki.AI.EnemyMeleeAI>();
            if (meleeAI != null)
            {
                meleeAI.ApplyDifficultyMultiplier(difficulty.MeleeDamageMultiplier);
            }

            Sticki.AI.EnemyRangedShooter rangedShooter = enemy.GetComponent<Sticki.AI.EnemyRangedShooter>();
            if (rangedShooter != null)
            {
                rangedShooter.ApplyDifficultyMultiplier(difficulty.MeleeDamageMultiplier);
            }
        }

        private int CalculateDynamicSpawnTargetCount()
        {
            return Mathf.Max(0, Mathf.RoundToInt(config.dynamicSpawnTotal * difficulty.DynamicSpawnMultiplier));
        }

        private int GetHardAliveCap()
        {
            return Mathf.Max(1, Mathf.RoundToInt(config.hardAliveCap * difficulty.AliveCapMultiplier));
        }

        private float GetStartDelayAfterCombat()
        {
            return Mathf.Max(0f, config.startDelayAfterCombat);
        }

        private float GetMinDistanceFromPlayer()
        {
            return Mathf.Max(0f, config.minDistanceFromPlayer);
        }

        private float GetFallbackMinDistance()
        {
            return Mathf.Max(0f, config.fallbackMinDistance);
        }

        private float GetPointBlockedRadius()
        {
            return Mathf.Max(0.05f, config.pointBlockedRadius);
        }

        private LayerMask GetSpawnBlockedByMask()
        {
            return config.spawnBlockedBy;
        }

        private LayerMask GetLineOfSightMask()
        {
            return config.lineOfSightMask;
        }

        private bool GetPreferHiddenSpawnPoints()
        {
            return config.preferHiddenSpawnPoints;
        }

        private readonly struct RuntimeEnemyType
        {
            public RuntimeEnemyType(string id, GameObject prefab, float weight, int prewarmCount, int maxPoolSize)
            {
                this.id = id;
                this.prefab = prefab;
                this.weight = weight;
                this.prewarmCount = prewarmCount;
                this.maxPoolSize = maxPoolSize;
            }

            public readonly string id;
            public readonly GameObject prefab;
            public readonly float weight;
            public readonly int prewarmCount;
            public readonly int maxPoolSize;
        }

        private readonly struct RuntimeWave
        {
            public RuntimeWave(int enemiesInWave, int minPerTick, int maxPerTick, float tickInterval, float interWaveDelay, float earlyNextWaveKillRatio)
            {
                this.enemiesInWave = enemiesInWave;
                this.minPerTick = minPerTick;
                this.maxPerTick = maxPerTick;
                this.tickInterval = tickInterval;
                this.interWaveDelay = interWaveDelay;
                this.earlyNextWaveKillRatio = earlyNextWaveKillRatio;
            }

            public readonly int enemiesInWave;
            public readonly int minPerTick;
            public readonly int maxPerTick;
            public readonly float tickInterval;
            public readonly float interWaveDelay;
            public readonly float earlyNextWaveKillRatio;
        }

        private sealed class EnemyPool
        {
            private readonly string id;
            private readonly GameObject prefab;
            private readonly Transform root;
            private readonly int maxPoolSize;
            private readonly ArenaSpawner owner;
            private readonly Queue<EnemyHealth> available = new();
            private readonly HashSet<EnemyHealth> all = new();

            public EnemyPool(string id, GameObject prefab, Transform parent, int maxPoolSize, ArenaSpawner owner)
            {
                this.id = id;
                this.prefab = prefab;
                this.owner = owner;
                this.maxPoolSize = Mathf.Max(1, maxPoolSize);

                GameObject poolRoot = new GameObject($"[{id}]Pool");
                poolRoot.transform.SetParent(parent, false);
                root = poolRoot.transform;
            }

            public void Prewarm(int count)
            {
                int target = Mathf.Clamp(count, 0, maxPoolSize);
                while (all.Count < target)
                {
                    EnemyHealth created = Create();
                    if (created == null)
                    {
                        break;
                    }
                    ReturnToPool(created);
                }
            }

            public EnemyHealth Get()
            {
                if (available.Count > 0)
                {
                    return available.Dequeue();
                }

                if (all.Count >= maxPoolSize)
                {
                    return null;
                }

                return Create();
            }

            private EnemyHealth Create()
            {
                GameObject go = UnityEngine.Object.Instantiate(prefab, root);
                EnemyHealth enemy = go.GetComponent<EnemyHealth>();
                if (enemy == null)
                {
                    UnityEngine.Object.Destroy(go);
                    return null;
                }

                enemy.ConfigurePoolReturn(true);
                enemy.OnReturnToPoolRequested += HandleReturnRequested;
                all.Add(enemy);
                return enemy;
            }

            private void HandleReturnRequested(EnemyHealth enemy)
            {
                ReturnToPool(enemy);
            }

            private void ReturnToPool(EnemyHealth enemy)
            {
                if (enemy == null || !all.Contains(enemy))
                {
                    return;
                }

                owner.UntrackEnemy(enemy);
                enemy.gameObject.SetActive(false);
                enemy.transform.SetParent(root, false);
                available.Enqueue(enemy);
            }
        }
    }
}
