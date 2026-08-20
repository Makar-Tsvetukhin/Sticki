using System;
using Sticki.Combat.Config;
using Sticki.Core.Interfaces;
using Sticki.Player;
using Sticki.Upgrades;
using UnityEngine;
using System.Collections.Generic;

namespace Sticki.Combat
{
    public class PlayerCombat : MonoBehaviour
    {
        private enum ReloadPhase
        {
            None,
            Magazine,
            ShellStart,
            ShellInsert,
            ShellEnd
        }

        [SerializeField] private MonoBehaviour inputSourceComponent;
        [SerializeField] private PlayerStats stats;
        [SerializeField] private PlayerHealth health;
        [SerializeField] private WeaponConfig weaponConfig;
        [SerializeField] private Transform shootOrigin;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private bool canControl = true;

        public event Action OnShot;
        public event Action<bool> OnHit;
        public event Action<Vector3, Vector3> OnTracerGenerated;
        public event Action OnReloadStarted;
        public event Action OnReloadShellPresented;
        public event Action OnReloadShellInserted;
        public event Action OnReloadEnded;
        public event Action OnReloadFinished;
        public event Action<WeaponConfig> OnWeaponChanged;

        private IInputSource inputSource;
        private float nextShotTime;
        private float actionLockUntil;
        private bool isReloading;
        private bool pendingShotAfterReloadEnd;
        private int currentAmmo;
        private int currentReserveAmmo;
        private bool hasInfiniteReserveAmmo;
        private ReloadPhase reloadPhase;
        private WeaponRuntimeStats weaponRuntimeStats;
        private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
        private readonly Dictionary<WeaponConfig, WeaponAmmoState> ammoStatesByWeapon = new();
        private const string PresentReloadShellMethodName = nameof(PresentReloadShell);

        private struct WeaponAmmoState
        {
            public int CurrentAmmo;
            public int CurrentReserveAmmo;
            public bool HasInfiniteReserveAmmo;
            public int MagazineSize;
        }

        public WeaponConfig CurrentWeaponConfig => weaponConfig;
        public WeaponRuntimeStats CurrentWeaponRuntimeStats => weaponRuntimeStats;
        public int CurrentAmmo => currentAmmo;
        public int CurrentReserveAmmo => currentReserveAmmo;
        public bool HasInfiniteReserveAmmo => hasInfiniteReserveAmmo;
        public int ActiveReserveAmmoCapacity => ResolveReserveAmmoCapacity(weaponConfig);

        public bool CanControl
        {
            get => canControl;
            set => canControl = value;
        }

        private void Awake()
        {
            inputSource = inputSourceComponent as IInputSource;
            if (inputSource == null)
            {
                Debug.LogError("PlayerCombat requires a component that implements IInputSource.", this);
            }
            if (stats == null)
            {
                Debug.LogError("PlayerCombat requires PlayerStats reference.", this);
            }
            if (health == null)
            {
                Debug.LogError("PlayerCombat requires PlayerHealth reference.", this);
            }
            if (weaponConfig == null)
            {
                Debug.LogError("PlayerCombat requires WeaponConfig reference.", this);
            }
            if (shootOrigin == null)
            {
                Debug.LogError("PlayerCombat requires shootOrigin reference.", this);
            }

            if (weaponConfig != null)
            {
                weaponRuntimeStats = new WeaponRuntimeStats(weaponConfig);
                LoadWeaponAmmoState(weaponConfig, refillAmmo: true);
            }
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.OnDied += HandleDeath;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.OnDied -= HandleDeath;
            }
        }

        private void Update()
        {
            if (!canControl || inputSource == null || stats == null || weaponConfig == null || shootOrigin == null)
            {
                return;
            }

            if (isReloading)
            {
                if (inputSource.FireHeld)
                {
                    pendingShotAfterReloadEnd = true;
                }

                if (Time.time < actionLockUntil)
                {
                    return;
                }

                HandleReloadInput();
                return;
            }

            if (Time.time < actionLockUntil)
            {
                return;
            }

            if (inputSource.ReloadPressed && currentAmmo < weaponRuntimeStats.MagazineSize)
            {
                StartReload();
                return;
            }

            if (!inputSource.FireHeld)
            {
                return;
            }

            if (Time.time < nextShotTime)
            {
                return;
            }

            if (currentAmmo <= 0)
            {
                StartReload();
                return;
            }

            Shoot();
        }

        private void Shoot()
        {
            currentAmmo--;

            float fireInterval = weaponRuntimeStats.FireIntervalSeconds / Mathf.Max(0.01f, stats.FireRateMultiplier);
            nextShotTime = Time.time + fireInterval;

            int pelletCount = weaponRuntimeStats.PelletsPerShot;
            float spread = weaponRuntimeStats.SpreadAngle;

            for (int i = 0; i < pelletCount; i++)
            {
                Vector3 shotDirection = shootOrigin.forward;
                if (spread > 0f)
                {
                    shotDirection = Quaternion.Euler(
                        UnityEngine.Random.Range(-spread, spread),
                        UnityEngine.Random.Range(-spread, spread),
                        0f) * shotDirection;
                }

                Vector3 targetPoint;
                if (TryGetValidHit(shotDirection, out RaycastHit hit))
                {
                    targetPoint = hit.point;
                    IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
                    if (damageable != null)
                    {
                        float finalDamage = weaponRuntimeStats.Damage * Mathf.Max(0f, stats.DamageMultiplier);
                        damageable.TakeDamage(finalDamage);

                        // Check if it was a kill
                        bool isKill = false;
                        if (damageable is EnemyHealth enemyHealth)
                        {
                            isKill = enemyHealth.IsDead;
                        }
                        else if (damageable is EnemyHitboxProxy proxy && proxy.OwnerHealth != null)
                        {
                            isKill = proxy.OwnerHealth.IsDead;
                        }
                        
                        OnHit?.Invoke(isKill);
                    }
                }
                else
                {
                    targetPoint = shootOrigin.position + shotDirection * weaponRuntimeStats.Range;
                }

                OnTracerGenerated?.Invoke(shootOrigin.position, targetPoint);
            }

            OnShot?.Invoke();

            if (currentAmmo <= 0)
            {
                StartReload();
            }
        }

        private bool TryGetValidHit(Vector3 direction, out RaycastHit hit)
        {
            hit = default;

            Ray ray = new Ray(shootOrigin.position, direction);
            int hitCount = Physics.RaycastNonAlloc(ray, hitBuffer, weaponRuntimeStats.Range, hitMask, QueryTriggerInteraction.Collide);
            if (hitCount <= 0)
            {
                return false;
            }

            Transform selfRoot = transform.root;
            int bestIndex = -1;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = hitBuffer[i];
                if (candidate.collider != null && candidate.collider.transform.root == selfRoot)
                {
                    continue;
                }

                if (candidate.distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = candidate.distance;
                bestIndex = i;
            }

            if (bestIndex < 0)
            {
                return false;
            }

            hit = hitBuffer[bestIndex];
            return true;
        }

        private void StartReload()
        {
            if (isReloading || weaponRuntimeStats == null || currentAmmo >= weaponRuntimeStats.MagazineSize)
            {
                return;
            }

            if (!hasInfiniteReserveAmmo && currentReserveAmmo <= 0)
            {
                return;
            }

            isReloading = true;
            reloadPhase = ReloadPhase.None;
            pendingShotAfterReloadEnd = false;

            if (weaponRuntimeStats.ReloadType == WeaponReloadType.ShellByShell)
            {
                BeginShellReloadStart();
                return;
            }

            reloadPhase = ReloadPhase.Magazine;
            OnReloadStarted?.Invoke();
            Invoke(nameof(FinishMagazineReload), weaponRuntimeStats.ReloadSeconds);
        }

        public void SetWeapon(WeaponConfig newWeaponConfig, bool refillAmmo = true)
        {
            if (newWeaponConfig == null)
            {
                Debug.LogWarning("PlayerCombat.SetWeapon received null config.", this);
                return;
            }

            CancelReloadSequence();
            SaveWeaponAmmoState(weaponConfig);

            weaponConfig = newWeaponConfig;
            weaponRuntimeStats = new WeaponRuntimeStats(weaponConfig);
            LoadWeaponAmmoState(newWeaponConfig, refillAmmo);

            OnWeaponChanged?.Invoke(weaponConfig);
        }

        public void ApplyWeaponRuntimeStats(WeaponRuntimeStats runtimeStats)
        {
            int previousMagazineSize = weaponRuntimeStats != null
                ? weaponRuntimeStats.MagazineSize
                : (weaponConfig != null ? weaponConfig.magazineSize : currentAmmo);

            weaponRuntimeStats = runtimeStats ?? (weaponConfig != null ? new WeaponRuntimeStats(weaponConfig) : null);
            if (weaponRuntimeStats != null)
            {
                int newMagazineSize = weaponRuntimeStats.MagazineSize;
                if (newMagazineSize > previousMagazineSize && currentAmmo >= previousMagazineSize)
                {
                    currentAmmo = newMagazineSize;
                }
                else if (currentAmmo > newMagazineSize)
                {
                    currentAmmo = newMagazineSize;
                }
            }

            SaveWeaponAmmoState(weaponConfig);
        }

        public bool CanReceiveAmmoPickup(int amount = 1)
        {
            if (weaponConfig == null || hasInfiniteReserveAmmo || amount <= 0)
            {
                return false;
            }

            return currentReserveAmmo < ResolveReserveAmmoCapacity(weaponConfig);
        }

        public int AddReserveAmmoToActiveWeapon(int amount)
        {
            if (!CanReceiveAmmoPickup(amount))
            {
                return 0;
            }

            int capacity = ResolveReserveAmmoCapacity(weaponConfig);
            int added = Mathf.Clamp(amount, 0, Mathf.Max(0, capacity - currentReserveAmmo));
            if (added <= 0)
            {
                return 0;
            }

            currentReserveAmmo += added;
            SaveWeaponAmmoState(weaponConfig);
            return added;
        }

        public void SetActionLock(float seconds)
        {
            if (seconds <= 0f)
            {
                return;
            }

            float until = Time.time + seconds;
            if (until > actionLockUntil)
            {
                actionLockUntil = until;
            }
        }

        private void FinishMagazineReload()
        {
            isReloading = false;
            reloadPhase = ReloadPhase.None;
            if (weaponRuntimeStats != null)
            {
                int ammoNeeded = Mathf.Max(0, weaponRuntimeStats.MagazineSize - currentAmmo);
                if (hasInfiniteReserveAmmo)
                {
                    currentAmmo += ammoNeeded;
                }
                else
                {
                    int loadedAmmo = Mathf.Min(ammoNeeded, currentReserveAmmo);
                    currentAmmo += loadedAmmo;
                    currentReserveAmmo -= loadedAmmo;
                }
            }

            SaveWeaponAmmoState(weaponConfig);
            OnReloadFinished?.Invoke();
        }

        private void HandleDeath()
        {
            canControl = false;
            CancelReloadSequence();
        }

        private void HandleReloadInput()
        {
            if (weaponRuntimeStats == null || weaponRuntimeStats.ReloadType != WeaponReloadType.ShellByShell)
            {
                return;
            }

            if ((reloadPhase == ReloadPhase.ShellStart || reloadPhase == ReloadPhase.ShellInsert) &&
                pendingShotAfterReloadEnd &&
                currentAmmo > 0)
            {
                BeginShellReloadEnd();
            }
        }

        private void BeginShellReloadStart()
        {
            reloadPhase = ReloadPhase.ShellStart;
            OnReloadStarted?.Invoke();

            float duration = weaponRuntimeStats.ReloadStartSeconds;
            ScheduleReloadShellPresentation(Mathf.Min(weaponRuntimeStats.ReloadShellAppearStartDelay, duration));
            if (duration <= 0f)
            {
                FinishShellReloadStart();
                return;
            }

            SetActionLock(duration);
            Invoke(nameof(FinishShellReloadStart), duration);
        }

        private void FinishShellReloadStart()
        {
            if (!isReloading)
            {
                return;
            }

            CancelInvoke(PresentReloadShellMethodName);
            if (!TryLoadSingleRound())
            {
                BeginShellReloadEnd();
                return;
            }
            
            if (pendingShotAfterReloadEnd && currentAmmo > 0)
            {
                BeginShellReloadEnd();
                return;
            }

            if (currentAmmo >= weaponRuntimeStats.MagazineSize)
            {
                BeginShellReloadEnd();
                return;
            }

            OnReloadShellInserted?.Invoke();
            BeginShellReloadInsert();
        }

        private void BeginShellReloadInsert()
        {
            reloadPhase = ReloadPhase.ShellInsert;
            float duration = weaponRuntimeStats.ReloadInsertSeconds;
            ScheduleReloadShellPresentation(Mathf.Min(weaponRuntimeStats.ReloadShellAppearInsertDelay, duration));
            SetActionLock(duration);
            Invoke(nameof(FinishShellReloadInsert), duration);
        }

        private void FinishShellReloadInsert()
        {
            if (!isReloading)
            {
                return;
            }

            CancelInvoke(PresentReloadShellMethodName);
            if (!TryLoadSingleRound())
            {
                BeginShellReloadEnd();
                return;
            }

            if (pendingShotAfterReloadEnd && currentAmmo > 0)
            {
                BeginShellReloadEnd();
                return;
            }

            if (currentAmmo >= weaponRuntimeStats.MagazineSize)
            {
                BeginShellReloadEnd();
                return;
            }

            OnReloadShellInserted?.Invoke();
            BeginShellReloadInsert();
        }

        private void BeginShellReloadEnd()
        {
            CancelInvoke(nameof(FinishShellReloadStart));
            CancelInvoke(nameof(FinishShellReloadInsert));
            CancelInvoke(nameof(FinishShellReloadEnd));
            CancelInvoke(PresentReloadShellMethodName);

            reloadPhase = ReloadPhase.ShellEnd;
            OnReloadEnded?.Invoke();

            float duration = weaponRuntimeStats.ReloadEndSeconds;
            if (duration <= 0f)
            {
                FinishShellReloadEnd();
                return;
            }

            SetActionLock(duration);
            Invoke(nameof(FinishShellReloadEnd), duration);
        }

        private void FinishShellReloadEnd()
        {
            bool shouldFire = pendingShotAfterReloadEnd &&
                              canControl &&
                              inputSource != null &&
                              stats != null &&
                              weaponConfig != null &&
                              shootOrigin != null &&
                              currentAmmo > 0 &&
                              Time.time >= nextShotTime;

            isReloading = false;
            reloadPhase = ReloadPhase.None;
            pendingShotAfterReloadEnd = false;
            OnReloadFinished?.Invoke();

            if (shouldFire)
            {
                Shoot();
            }

            SaveWeaponAmmoState(weaponConfig);
        }

        private void CancelReloadSequence()
        {
            bool wasReloading = isReloading;
            isReloading = false;
            reloadPhase = ReloadPhase.None;
            pendingShotAfterReloadEnd = false;
            CancelInvoke(nameof(FinishMagazineReload));
            CancelInvoke(nameof(FinishShellReloadStart));
            CancelInvoke(nameof(FinishShellReloadInsert));
            CancelInvoke(nameof(FinishShellReloadEnd));
            CancelInvoke(PresentReloadShellMethodName);

            if (wasReloading)
            {
                OnReloadEnded?.Invoke();
            }
        }

        private void ScheduleReloadShellPresentation(float delay)
        {
            CancelInvoke(PresentReloadShellMethodName);

            if (delay <= 0f)
            {
                PresentReloadShell();
                return;
            }

            Invoke(PresentReloadShellMethodName, delay);
        }

        private void PresentReloadShell()
        {
            if (!isReloading)
            {
                return;
            }

            if (reloadPhase != ReloadPhase.ShellStart && reloadPhase != ReloadPhase.ShellInsert)
            {
                return;
            }

            OnReloadShellPresented?.Invoke();
        }

        private bool TryLoadSingleRound()
        {
            if (weaponRuntimeStats == null || currentAmmo >= weaponRuntimeStats.MagazineSize)
            {
                return false;
            }

            if (!hasInfiniteReserveAmmo)
            {
                if (currentReserveAmmo <= 0)
                {
                    return false;
                }

                currentReserveAmmo--;
            }

            currentAmmo = Mathf.Min(currentAmmo + 1, weaponRuntimeStats.MagazineSize);
            SaveWeaponAmmoState(weaponConfig);
            return true;
        }

        private void SaveWeaponAmmoState(WeaponConfig config)
        {
            if (config == null)
            {
                return;
            }

            ammoStatesByWeapon[config] = new WeaponAmmoState
            {
                CurrentAmmo = currentAmmo,
                CurrentReserveAmmo = currentReserveAmmo,
                HasInfiniteReserveAmmo = hasInfiniteReserveAmmo,
                MagazineSize = weaponRuntimeStats != null ? weaponRuntimeStats.MagazineSize : config.magazineSize
            };
        }

        private void LoadWeaponAmmoState(WeaponConfig config, bool refillAmmo)
        {
            if (config == null)
            {
                currentAmmo = 0;
                currentReserveAmmo = 0;
                hasInfiniteReserveAmmo = false;
                return;
            }

            WeaponAmmoState state;
            if (!ammoStatesByWeapon.TryGetValue(config, out state) || refillAmmo)
            {
                state = new WeaponAmmoState
                {
                    CurrentAmmo = weaponRuntimeStats != null ? weaponRuntimeStats.MagazineSize : config.magazineSize,
                    CurrentReserveAmmo = Mathf.Max(0, config.initialReserveAmmo),
                    HasInfiniteReserveAmmo = config.infiniteReserveAmmo
                };
            }

            int capacity = Mathf.Max(state.MagazineSize, weaponRuntimeStats != null ? weaponRuntimeStats.MagazineSize : config.magazineSize);
            currentAmmo = Mathf.Clamp(state.CurrentAmmo, 0, capacity);
            currentReserveAmmo = Mathf.Max(0, state.CurrentReserveAmmo);
            if (!config.infiniteReserveAmmo)
            {
                currentReserveAmmo = Mathf.Clamp(currentReserveAmmo, 0, ResolveReserveAmmoCapacity(config));
            }
            hasInfiniteReserveAmmo = state.HasInfiniteReserveAmmo;
            SaveWeaponAmmoState(config);
        }

        private static int ResolveReserveAmmoCapacity(WeaponConfig config)
        {
            if (config == null || config.infiniteReserveAmmo)
            {
                return 0;
            }

            return Mathf.Max(0, config.initialReserveAmmo);
        }
    }
}
