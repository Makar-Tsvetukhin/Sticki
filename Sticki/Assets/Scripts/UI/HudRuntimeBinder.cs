using Sticki.Combat;
using Sticki.Player;
using UnityEngine;

namespace Sticki.UI
{
    public class HudRuntimeBinder : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HudController hud;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private PlayerCombat playerCombat;
        [SerializeField] private Sticki.Spawning.ArenaSpawner arenaSpawner;

        private int lastHp = int.MinValue;
        private float lastHpNormalized = float.MinValue;
        private int lastAmmo = int.MinValue;
        private int lastReserveAmmo = int.MinValue;
        private bool lastInfiniteReserve;
        private int lastKills = int.MinValue;
        private int lastTarget = int.MinValue;
        private bool initialized;

        private void Awake()
        {
            TryResolveReferences();
        }

        private void Start()
        {
            RefreshAll(force: true);
            initialized = true;
        }

        private void Update()
        {
            TryResolveReferences();
            RefreshAll(force: !initialized);
        }

        private void OnEnable()
        {
            SubscribeToCombat();
        }

        private void OnDisable()
        {
            UnsubscribeFromCombat();
        }

        private void SubscribeToCombat()
        {
            if (playerCombat != null)
            {
                playerCombat.OnHit -= HandleHit;
                playerCombat.OnHit += HandleHit;
            }
        }

        private void UnsubscribeFromCombat()
        {
            if (playerCombat != null)
            {
                playerCombat.OnHit -= HandleHit;
            }
        }

        private void HandleHit(bool isKill)
        {
            if (hud != null)
            {
                hud.ShowHitMarker(isKill);
            }
        }

        private void TryResolveReferences()
        {
            if (hud == null)
            {
                hud = FindFirstObjectByType<HudController>();
            }

            if (playerHealth == null)
            {
                playerHealth = FindFirstObjectByType<PlayerHealth>();
            }

            if (playerCombat == null)
            {
                playerCombat = FindFirstObjectByType<PlayerCombat>();
                if (playerCombat != null)
                {
                    SubscribeToCombat();
                }
            }

            if (arenaSpawner == null)
            {
                arenaSpawner = FindFirstObjectByType<Sticki.Spawning.ArenaSpawner>();
            }
        }

        private void RefreshAll(bool force)
        {
            if (hud == null || playerHealth == null || playerCombat == null)
            {
                return;
            }

            float maxHp = Mathf.Max(1f, playerHealth.MaxHealth);
            int hp = Mathf.RoundToInt(playerHealth.CurrentHealth);
            float hpNormalized = Mathf.Clamp01(playerHealth.CurrentHealth / maxHp);

            if (force || hp != lastHp || Mathf.Abs(hpNormalized - lastHpNormalized) > 0.001f)
            {
                hud.UpdateHealth(hpNormalized, hp);
                lastHp = hp;
                lastHpNormalized = hpNormalized;
            }

            int ammo = Mathf.Max(0, playerCombat.CurrentAmmo);
            int reserveAmmo = Mathf.Max(0, playerCombat.CurrentReserveAmmo);
            bool infiniteReserve = playerCombat.HasInfiniteReserveAmmo;
            if (force || ammo != lastAmmo || reserveAmmo != lastReserveAmmo || infiniteReserve != lastInfiniteReserve)
            {
                hud.UpdateAmmo(ammo, reserveAmmo, infiniteReserve);
                lastAmmo = ammo;
                lastReserveAmmo = reserveAmmo;
                lastInfiniteReserve = infiniteReserve;
            }

            if (arenaSpawner != null)
            {
                int kills = Mathf.Max(0, arenaSpawner.TotalKilledCount);
                int target = Mathf.Max(0, arenaSpawner.TargetKillCount);
                if (force || kills != lastKills || target != lastTarget)
                {
                    hud.UpdateArenaProgress(kills, target);
                    lastKills = kills;
                    lastTarget = target;
                }
            }
        }
    }
}
