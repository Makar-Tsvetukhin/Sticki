using UnityEngine;
using UnityEngine.AI;
using Sticki.Combat;

namespace Sticki.AI
{
    public class EnemyStressSpawner : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private Transform spawnParent;

        [Header("Spawn Setup")]
        [SerializeField] private bool spawnOnStart = true;
        [SerializeField] private int spawnCount = 100;
        [SerializeField] private Vector2 areaSize = new Vector2(50f, 50f);
        [SerializeField] private float navMeshSampleRadius = 4f;
        [SerializeField] private float yOffset = 0.05f;
        [SerializeField] private int maxAttemptsPerEnemy = 12;
        [SerializeField] private float minSpacing = 1.1f;

        [Header("Performance")]
        [SerializeField] private bool spreadSpawningAcrossFrames = true;
        [SerializeField] private int batchSizePerFrame = 10;

        private readonly System.Collections.Generic.List<Vector3> usedPositions = new();
        private int spawnedCount;

        public int SpawnedCount => spawnedCount;

        private void Start()
        {
            if (!spawnOnStart)
            {
                return;
            }

            SpawnEnemies();
        }

        [ContextMenu("Spawn Enemies")]
        public void SpawnEnemies()
        {
            if (enemyPrefab == null)
            {
                Debug.LogWarning("EnemyStressSpawner: enemyPrefab is not assigned.", this);
                return;
            }

            StopAllCoroutines();
            usedPositions.Clear();
            spawnedCount = 0;
            EnemyMeleeAI.GlobalCombatActive = false;

            if (spreadSpawningAcrossFrames)
            {
                StartCoroutine(SpawnRoutine());
            }
            else
            {
                SpawnImmediate();
            }
        }

        [ContextMenu("Clear Spawned Enemies")]
        public void ClearSpawnedEnemies()
        {
            if (spawnParent != null)
            {
                for (int i = spawnParent.childCount - 1; i >= 0; i--)
                {
                    Transform child = spawnParent.GetChild(i);
                    DestroyImmediate(child.gameObject);
                }
            }
            else
            {
                EnemyHealth[] enemies = FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
                for (int i = 0; i < enemies.Length; i++)
                {
                    DestroyImmediate(enemies[i].gameObject);
                }
            }

            usedPositions.Clear();
            spawnedCount = 0;
            EnemyMeleeAI.GlobalCombatActive = false;
        }

        private System.Collections.IEnumerator SpawnRoutine()
        {
            int spawnedThisFrame = 0;
            int failStreak = 0;
            while (spawnedCount < spawnCount)
            {
                if (TrySpawnOne())
                {
                    spawnedThisFrame++;
                    failStreak = 0;
                }
                else
                {
                    failStreak++;
                    if (failStreak > 64)
                    {
                        Debug.LogWarning("EnemyStressSpawner: Spawn stopped early because no valid NavMesh points were found.", this);
                        yield break;
                    }
                }

                if (spawnedThisFrame >= Mathf.Max(1, batchSizePerFrame))
                {
                    spawnedThisFrame = 0;
                    yield return null;
                }
            }
        }

        private void SpawnImmediate()
        {
            int failStreak = 0;
            while (spawnedCount < spawnCount)
            {
                if (TrySpawnOne())
                {
                    failStreak = 0;
                    continue;
                }

                failStreak++;
                if (failStreak > 64)
                {
                    Debug.LogWarning("EnemyStressSpawner: Spawn stopped early because no valid NavMesh points were found.", this);
                    break;
                }
            }
        }

        private bool TrySpawnOne()
        {
            for (int attempt = 0; attempt < Mathf.Max(1, maxAttemptsPerEnemy); attempt++)
            {
                Vector3 candidate = SampleAreaPoint();
                if (!TryFindNavMeshPoint(candidate, out Vector3 navPoint))
                {
                    continue;
                }

                if (!IsFarEnough(navPoint))
                {
                    continue;
                }

                SpawnAt(navPoint + Vector3.up * yOffset);
                usedPositions.Add(navPoint);
                spawnedCount++;
                return true;
            }

            // Fallback spawn with relaxed spacing so setup still completes.
            Vector3 fallback = SampleAreaPoint();
            if (TryFindNavMeshPoint(fallback, out Vector3 relaxedPoint))
            {
                SpawnAt(relaxedPoint + Vector3.up * yOffset);
                spawnedCount++;
                return true;
            }

            return false;
        }

        private void SpawnAt(Vector3 position)
        {
            Instantiate(enemyPrefab, position, Quaternion.identity, spawnParent);
        }

        private Vector3 SampleAreaPoint()
        {
            float halfX = areaSize.x * 0.5f;
            float halfZ = areaSize.y * 0.5f;
            return transform.position + new Vector3(
                Random.Range(-halfX, halfX),
                0f,
                Random.Range(-halfZ, halfZ));
        }

        private bool TryFindNavMeshPoint(Vector3 point, out Vector3 navPoint)
        {
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, Mathf.Max(0.2f, navMeshSampleRadius), NavMesh.AllAreas))
            {
                navPoint = hit.position;
                return true;
            }

            navPoint = point;
            return false;
        }

        private bool IsFarEnough(Vector3 point)
        {
            float sqrMinDist = minSpacing * minSpacing;
            for (int i = 0; i < usedPositions.Count; i++)
            {
                if ((usedPositions[i] - point).sqrMagnitude < sqrMinDist)
                {
                    return false;
                }
            }

            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.25f, 0.9f, 1f, 0.35f);
            Gizmos.DrawWireCube(transform.position, new Vector3(areaSize.x, 0.1f, areaSize.y));
        }
    }
}
