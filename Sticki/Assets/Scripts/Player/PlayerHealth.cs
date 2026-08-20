using System;
using Sticki.Core.Interfaces;
using UnityEngine;

namespace Sticki.Player
{
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        public static PlayerHealth Instance { get; private set; }

        [SerializeField] private PlayerStats stats;
        [SerializeField] private float invulnerabilitySecondsAfterHit = 0.2f;
        [SerializeField] private bool debugDamageLogs;

        public event Action<float, float> OnDamaged;
        public event Action<float, float> OnHealed;
        public event Action OnDied;

        public float CurrentHealth { get; private set; }
        public float MaxHealth => stats != null ? Mathf.Max(0f, stats.MaxHealth) : 0f;
        public bool IsDead { get; private set; }
        public bool IsInvulnerable => Time.time < invulnerableUntil;

        private float invulnerableUntil;
        private float lastKnownMaxHealth;

        private void Awake()
        {
            if (Instance == null || Instance == this)
            {
                Instance = this;
            }

            if (stats == null)
            {
                Debug.LogError("PlayerHealth requires PlayerStats reference.", this);
                enabled = false;
                return;
            }

            stats.Recalculate();
            CurrentHealth = Mathf.Max(0f, stats.MaxHealth);
            lastKnownMaxHealth = CurrentHealth;
        }

        private void OnEnable()
        {
            stats.OnStatsChanged += OnStatsChanged;
        }

        private void OnDisable()
        {
            if (stats != null)
            {
                stats.OnStatsChanged -= OnStatsChanged;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (IsDead || stats == null)
            {
                return;
            }

            float regenPerSecond = Mathf.Max(0f, stats.HealthRegenPerSecond);
            if (regenPerSecond <= 0f || CurrentHealth >= MaxHealth)
            {
                return;
            }

            Heal(regenPerSecond * Time.deltaTime);
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f)
            {
                return;
            }

            amount *= Mathf.Max(0f, stats.DamageTakenMultiplier);

            if (IsInvulnerable)
            {
                if (debugDamageLogs)
                {
                    Debug.Log($"Player damage blocked by i-frames. Incoming: {amount:0.##}", this);
                }
                return;
            }

            float previous = CurrentHealth;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            float applied = previous - CurrentHealth;

            if (applied > 0f)
            {
                if (invulnerabilitySecondsAfterHit > 0f)
                {
                    invulnerableUntil = Time.time + invulnerabilitySecondsAfterHit;
                }

                OnDamaged?.Invoke(applied, CurrentHealth);

                if (debugDamageLogs)
                {
                    Debug.Log($"Player took {applied:0.##} damage. HP: {CurrentHealth:0.##}", this);
                }
            }

            if (CurrentHealth <= 0f)
            {
                Die();
            }
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f)
            {
                return;
            }

            float previous = CurrentHealth;
            CurrentHealth = Mathf.Min(stats.MaxHealth, CurrentHealth + amount);
            float applied = CurrentHealth - previous;

            if (applied > 0f)
            {
                OnHealed?.Invoke(applied, CurrentHealth);
            }
        }

        public void RestoreToFull()
        {
            if (IsDead || stats == null)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, stats.MaxHealth);
            lastKnownMaxHealth = CurrentHealth;
            OnHealed?.Invoke(0f, CurrentHealth);
        }

        private void Die()
        {
            if (IsDead)
            {
                return;
            }

            IsDead = true;
            OnDied?.Invoke();
        }

        private void OnStatsChanged()
        {
            if (IsDead)
            {
                return;
            }

            float maxHealth = Mathf.Max(0f, stats.MaxHealth);
            float delta = maxHealth - lastKnownMaxHealth;
            if (delta > 0f)
            {
                CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + delta);
            }
            else
            {
                CurrentHealth = Mathf.Min(CurrentHealth, maxHealth);
            }

            lastKnownMaxHealth = maxHealth;

            if (CurrentHealth <= 0f)
            {
                Die();
            }
        }
    }
}
