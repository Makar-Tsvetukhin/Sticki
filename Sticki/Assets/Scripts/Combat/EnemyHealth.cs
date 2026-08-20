using UnityEngine;
using Sticki.Core.Interfaces;
using System;
using System.Collections;
using Sticki.AI;
using Sticki.Loot;
using UnityEngine.AI;

namespace Sticki.Combat
{
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        public static int RegisteredCount { get; private set; }
        public static int AliveCount { get; private set; }

        [SerializeField] private float maxHealth = 50f;
        [SerializeField] private string hitboxLayerName = "EnemyHitbox";
        [Header("Death VFX")]
        [SerializeField] private GameObject deathVfxPrefab;
        [SerializeField] private Transform deathVfxAnchor;
        [SerializeField] private int deathVfxPrewarmCount = 32;
        [SerializeField] private int deathVfxMaxPoolSize = 64;
        [SerializeField] private float deathVfxLifetimeOverride = -1f;
        [SerializeField] private float destroyDelay = 0.15f;
        [Header("Debug")]
        [SerializeField] private bool logDamage;
        private float baseMaxHealth;
        private float effectiveMaxHealth;
        private float currentHealth;
        private bool usePoolReturn;
        private bool countedAsAlive;

        public bool IsDead { get; private set; }
        public float MaxHealth => effectiveMaxHealth;
        public float CurrentHealth => currentHealth;

        public event Action OnDied;
        public event Action<EnemyHealth> OnReturnToPoolRequested;

        private void Awake()
        {
            baseMaxHealth = maxHealth;
            effectiveMaxHealth = maxHealth;
            currentHealth = effectiveMaxHealth;
            EnsureHitboxProxies();

            if (deathVfxPrefab != null)
            {
                VfxPoolService.Preload(deathVfxPrefab, deathVfxPrewarmCount, deathVfxMaxPoolSize);
            }
        }

        private void OnEnable()
        {
            RegisteredCount++;
            SetAliveRegistration(!IsDead);
        }

        private void OnDisable()
        {
            if (RegisteredCount > 0)
            {
                RegisteredCount--;
            }

            SetAliveRegistration(false);
        }

        public void TakeDamage(float amount)
        {
            if (IsDead) return;

            EnemyMeleeAI.GlobalCombatActive = true;

            EnemyMeleeAI meleeAI = GetComponent<EnemyMeleeAI>();
            if (meleeAI != null)
            {
                meleeAI.SetAggressive();
            }

            EnemyRangedAI rangedAI = GetComponent<EnemyRangedAI>();
            if (rangedAI != null)
            {
                rangedAI.SetAggressive();
            }

            currentHealth -= amount;
            if (logDamage)
            {
                Debug.Log($"Enemy {gameObject.name} took {amount} damage. HP: {currentHealth}");
            }

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        private void Die()
        {
            if (IsDead) return;
            IsDead = true;
            SetAliveRegistration(false);

            EnemyDropService.TrySpawnDrop(transform.position);
            SpawnDeathVfx();
            DisableEnemyRuntime();
            OnDied?.Invoke();
            StartCoroutine(FinalizeDeathRoutine());
        }

        private void EnsureHitboxProxies()
        {
            int hitboxLayer = LayerMask.NameToLayer(hitboxLayerName);
            if (hitboxLayer < 0)
            {
                Debug.LogWarning($"Layer '{hitboxLayerName}' was not found. Hitbox proxies were not configured.", this);
                return;
            }

            EnemyHitboxProxy[] proxies = GetComponentsInChildren<EnemyHitboxProxy>(true);
            if (proxies.Length == 0)
            {
                Debug.LogWarning("Enemy has no EnemyHitboxProxy components. Add them to all hitbox colliders.", this);
                return;
            }

            for (int i = 0; i < proxies.Length; i++)
            {
                EnemyHitboxProxy proxy = proxies[i];
                proxy.SetOwner(this);

                if (proxy.gameObject.layer != hitboxLayer)
                {
                    Debug.LogWarning(
                        $"Hitbox '{proxy.gameObject.name}' should be on layer '{hitboxLayerName}'.",
                        proxy.gameObject);
                }
            }
        }

        private void SpawnDeathVfx()
        {
            if (deathVfxPrefab == null)
            {
                return;
            }

            Vector3 spawnPos = deathVfxAnchor != null ? deathVfxAnchor.position : transform.position;
            Quaternion spawnRot = deathVfxAnchor != null ? deathVfxAnchor.rotation : Quaternion.identity;
            VfxPoolService.Spawn(deathVfxPrefab, spawnPos, spawnRot, deathVfxLifetimeOverride, deathVfxMaxPoolSize);
        }

        private void DisableEnemyRuntime()
        {
            EnemyMeleeAI meleeAI = GetComponent<EnemyMeleeAI>();
            if (meleeAI != null)
            {
                meleeAI.enabled = false;
            }

            EnemyRangedAI rangedAI = GetComponent<EnemyRangedAI>();
            if (rangedAI != null)
            {
                rangedAI.enabled = false;
            }

            NavMeshAgent agent = GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = false;
            }

            Animator animator = GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.enabled = false;
            }

            KatanaDamage katanaDamage = GetComponentInChildren<KatanaDamage>(true);
            if (katanaDamage != null)
            {
                katanaDamage.enabled = false;
            }

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = false;
            }
        }

        private IEnumerator FinalizeDeathRoutine()
        {
            float delay = Mathf.Max(0f, destroyDelay);
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            if (usePoolReturn)
            {
                OnReturnToPoolRequested?.Invoke(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void ConfigurePoolReturn(bool enabled)
        {
            usePoolReturn = enabled;
        }

        public void ApplyDifficultyMultiplier(float healthMultiplier)
        {
            float safeMultiplier = Mathf.Max(0.1f, healthMultiplier);
            effectiveMaxHealth = Mathf.Max(1f, baseMaxHealth * safeMultiplier);

            if (IsDead)
            {
                return;
            }

            currentHealth = effectiveMaxHealth;
        }

        public void ResetForSpawnFromPool()
        {
            StopAllCoroutines();

            IsDead = false;
            currentHealth = effectiveMaxHealth;
            SetAliveRegistration(isActiveAndEnabled);

            EnemyMeleeAI meleeAI = GetComponent<EnemyMeleeAI>();
            if (meleeAI != null)
            {
                meleeAI.enabled = true;
                meleeAI.ResetForSpawn();
            }

            EnemyRangedAI rangedAI = GetComponent<EnemyRangedAI>();
            if (rangedAI != null)
            {
                rangedAI.enabled = true;
                rangedAI.ResetForSpawn();
            }

            NavMeshAgent agent = GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = true;
                if (agent.isOnNavMesh)
                {
                    agent.ResetPath();
                    agent.isStopped = true;
                }
            }

            Animator animator = GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.enabled = true;
                animator.Rebind();
                animator.Update(0f);
            }

            KatanaDamage katanaDamage = GetComponentInChildren<KatanaDamage>(true);
            if (katanaDamage != null)
            {
                katanaDamage.enabled = true;
            }

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = true;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = true;
            }
        }

        private void SetAliveRegistration(bool alive)
        {
            if (alive)
            {
                if (countedAsAlive)
                {
                    return;
                }

                countedAsAlive = true;
                AliveCount++;
                return;
            }

            if (!countedAsAlive)
            {
                return;
            }

            countedAsAlive = false;
            if (AliveCount > 0)
            {
                AliveCount--;
            }
        }
    }
}
