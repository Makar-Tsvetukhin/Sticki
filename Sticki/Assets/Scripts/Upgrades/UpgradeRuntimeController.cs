using Sticki.Player;
using Sticki.UI;
using UnityEngine;
using Sticki.Combat;
using Sticki.Combat.Config;
using System;
using System.Collections.Generic;

namespace Sticki.Upgrades
{
    public class UpgradeRuntimeController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UpgradeSelectionController selectionController;
        [SerializeField] private UpgradeCatalog catalog;
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private PlayerCombat playerCombat;
        [SerializeField] private PlayerWeaponSwitcher playerWeaponSwitcher;
        [SerializeField] private RunTabOverlayController runTabOverlayController;

        [Header("Flow")]
        [SerializeField] private bool randomizeSeedOnAwake = true;
        [SerializeField] private int sessionSeed;
        [SerializeField] private bool debugLogs;

        private readonly RunUpgradesState runState = new();
        private CharacterUpgradeApplier characterApplier;
        private WeaponUpgradeApplier weaponApplier;
        private UpgradeRollGenerator rollGenerator;
        private readonly Dictionary<UpgradeSelectionController.SelectionStation, List<UpgradeSelectionController.OptionData>> cachedRoomOptions = new();
        private string selectedPrimaryWeaponId;
        private PlayerHealth lastAppliedPlayerHealth;
        private PlayerCombat subscribedPlayerCombat;

        private static UpgradeRuntimeController instance;

        public RunUpgradesState RunState => runState;

        private void Awake()
        {
            LogDebug($"UpgradeRuntimeController: Awake on {gameObject.name}. Instance already exists: {instance != null}");
            if (instance != null && instance != this)
            {
                LogDebug("UpgradeRuntimeController: Duplicate instance found, destroying.");
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            ResolveReferences();

            int seed = sessionSeed;
            if (randomizeSeedOnAwake || seed == 0)
            {
                seed = Environment.TickCount;
            }

            sessionSeed = seed;
            runState.InitializeSeed(seed);
            characterApplier = new CharacterUpgradeApplier(playerStats);
            weaponApplier = new WeaponUpgradeApplier(playerCombat);
            rollGenerator = catalog != null ? new UpgradeRollGenerator(catalog) : null;
            
            if (catalog == null) Debug.LogWarning("UpgradeRuntimeController: Catalog is NULL in Awake!", this);
            else LogDebug($"UpgradeRuntimeController: Initialized with catalog '{catalog.name}'");
        }

        private void OnEnable()
        {
            LogDebug("UpgradeRuntimeController: OnEnable");
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            ResolveReferences();
            BindToSelectionController();
        }

        private void OnDisable()
        {
            LogDebug("UpgradeRuntimeController: OnDisable");
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnbindFromSelectionController();
            SyncCombatSubscription(null);
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            LogDebug($"UpgradeRuntimeController: OnSceneLoaded '{scene.name}'");
            ResolveReferences();
            BindToSelectionController();
            ApplyAllUpgrades(); // Re-apply to the new player instance
            ApplySelectedPrimaryWeapon(false);
        }

        private void ResolveReferences()
        {
            if (selectionController == null || !selectionController.gameObject.activeInHierarchy)
            {
                selectionController = FindFirstObjectByType<UpgradeSelectionController>();
            }

            if (playerStats == null || !playerStats.gameObject.activeInHierarchy)
            {
                playerStats = FindFirstObjectByType<PlayerStats>();
                characterApplier = new CharacterUpgradeApplier(playerStats);
            }

            if (playerHealth == null || !playerHealth.gameObject.activeInHierarchy)
            {
                playerHealth = FindFirstObjectByType<PlayerHealth>();
            }

            if (playerCombat == null || !playerCombat.gameObject.activeInHierarchy)
            {
                playerCombat = FindFirstObjectByType<PlayerCombat>();
                weaponApplier = new WeaponUpgradeApplier(playerCombat);
            }

            SyncCombatSubscription(playerCombat);

            if (playerWeaponSwitcher == null || !playerWeaponSwitcher.gameObject.activeInHierarchy)
            {
                playerWeaponSwitcher = FindFirstObjectByType<PlayerWeaponSwitcher>();
            }

            if (runTabOverlayController == null || !runTabOverlayController.gameObject.activeInHierarchy)
            {
                runTabOverlayController = FindFirstObjectByType<RunTabOverlayController>();
            }
        }

        private void BindToSelectionController()
        {
            if (selectionController == null)
            {
                return;
            }

            selectionController.OptionsProvider = ProvideOptionsForStation;
            selectionController.SelectionValidator = ValidateAndApplySelection;
        }

        private void SyncCombatSubscription(PlayerCombat combatToSubscribe)
        {
            if (subscribedPlayerCombat == combatToSubscribe)
            {
                return;
            }

            if (subscribedPlayerCombat != null)
            {
                subscribedPlayerCombat.OnWeaponChanged -= HandleWeaponChanged;
            }

            subscribedPlayerCombat = combatToSubscribe;

            if (subscribedPlayerCombat != null)
            {
                subscribedPlayerCombat.OnWeaponChanged += HandleWeaponChanged;
            }
        }

        private void UnbindFromSelectionController()
        {
            if (selectionController == null)
            {
                return;
            }

            if (selectionController.OptionsProvider == ProvideOptionsForStation)
            {
                selectionController.OptionsProvider = null;
            }
            if (selectionController.SelectionValidator == ValidateAndApplySelection)
            {
                selectionController.SelectionValidator = null;
            }
        }

        [ContextMenu("Reapply Upgrades")]
        public void ReapplyUpgrades()
        {
            ApplyAllUpgrades();
        }

        [ContextMenu("Reset Room Option Cache")]
        public void ResetRoomOptionCache()
        {
            cachedRoomOptions.Clear();
            selectionController?.ResetRoomSelections();
        }

        public void ResetRunState()
        {
            int seed = sessionSeed;
            if (randomizeSeedOnAwake || seed == 0)
            {
                seed = Environment.TickCount;
            }

            sessionSeed = seed;
            runState.Reset(seed);
            cachedRoomOptions.Clear();
            selectedPrimaryWeaponId = null;
            selectionController?.ResetRoomSelections();
            playerWeaponSwitcher?.ResetLoadout();
            ApplyAllUpgrades();
        }

        private bool ValidateAndApplySelection(UpgradeSelectionController.SelectionStation station, UpgradeSelectionController.OptionData option)
        {
            if (station == UpgradeSelectionController.SelectionStation.PrimaryWeapon)
            {
                selectedPrimaryWeaponId = option != null ? option.id : null;
                ApplySelectedPrimaryWeapon(true);
                return true;
            }

            if (option == null || string.IsNullOrWhiteSpace(option.id))
            {
                return false;
            }

            if (catalog == null)
            {
                Debug.LogWarning("UpgradeRuntimeController: catalog is not assigned.", this);
                return false;
            }

            if (!catalog.TryGetUpgradeById(option.id, out UpgradeDefinition definition) || definition == null)
            {
                Debug.LogWarning($"UpgradeRuntimeController: upgrade '{option.id}' was not found in catalog.", this);
                return false;
            }

            UpgradeCategory expectedCategory = station == UpgradeSelectionController.SelectionStation.Character
                ? UpgradeCategory.Character
                : UpgradeCategory.Weapon;
            if (definition.Category != expectedCategory)
            {
                Debug.LogWarning(
                    $"UpgradeRuntimeController: upgrade '{definition.Id}' has category '{definition.Category}', expected '{expectedCategory}'.",
                    this);
                return false;
            }

            if (!runState.TryAddUpgrade(definition, out int newStackCount))
            {
                if (debugLogs)
                {
                    Debug.Log($"UpgradeRuntimeController: upgrade '{definition.Id}' skipped because max stacks were reached.", this);
                }
                return false;
            }

            cachedRoomOptions.Remove(definition.Category == UpgradeCategory.Character
                ? UpgradeSelectionController.SelectionStation.Character
                : UpgradeSelectionController.SelectionStation.WeaponUpgrade);
            ApplyAllUpgrades();

            if (debugLogs)
            {
                Debug.Log($"UpgradeRuntimeController: applied '{definition.Id}', stacks = {newStackCount}.", this);
            }

            return true;
        }

        private void HandleWeaponChanged(WeaponConfig _)
        {
            ApplyAllUpgrades();
        }

        private List<UpgradeSelectionController.OptionData> ProvideOptionsForStation(UpgradeSelectionController.SelectionStation station, int count)
        {
            if (station == UpgradeSelectionController.SelectionStation.PrimaryWeapon)
            {
                return null;
            }

            if (cachedRoomOptions.TryGetValue(station, out List<UpgradeSelectionController.OptionData> cached))
            {
                return CloneOptions(cached);
            }

            if (catalog == null)
            {
                Debug.LogWarning("UpgradeRuntimeController: Catalog is null when requesting options.", this);
                return null;
            }

            if (rollGenerator == null)
            {
                rollGenerator = new UpgradeRollGenerator(catalog);
            }

            UpgradeCategory category = station == UpgradeSelectionController.SelectionStation.Character
                ? UpgradeCategory.Character
                : UpgradeCategory.Weapon;

            List<UpgradeSelectionController.OptionData> rolled = rollGenerator.RollOptions(category, count, runState, runState.CreateRollRandom(category));
            
            if (debugLogs)
            {
                Debug.Log($"UpgradeRuntimeController: Rolled {rolled.Count} options for {category}.", this);
            }

            cachedRoomOptions[station] = CloneOptions(rolled);
            return rolled;
        }

        private void ApplyAllUpgrades()
        {
            if (playerStats == null && playerCombat == null)
            {
                Debug.LogWarning("UpgradeRuntimeController: upgrade targets are missing.", this);
                return;
            }

            if (characterApplier == null && playerStats != null)
            {
                characterApplier = new CharacterUpgradeApplier(playerStats);
            }

            if (weaponApplier == null && playerCombat != null)
            {
                weaponApplier = new WeaponUpgradeApplier(playerCombat);
            }

            characterApplier?.Reapply(runState);
            weaponApplier?.Reapply(runState);
            RestoreSpawnedPlayerHealthIfNeeded();
            RefreshRunTab();
        }

        private void RestoreSpawnedPlayerHealthIfNeeded()
        {
            if (playerHealth == null)
            {
                return;
            }

            bool isNewPlayerInstance = lastAppliedPlayerHealth != playerHealth;
            if (!isNewPlayerInstance)
            {
                return;
            }

            playerHealth.RestoreToFull();
            lastAppliedPlayerHealth = playerHealth;
        }

        private void RefreshRunTab()
        {
            if (runTabOverlayController == null)
            {
                return;
            }

            List<RunUpgradesState.UpgradeStack> appliedUpgrades = runState.GetAppliedUpgrades();
            runTabOverlayController.SetAppliedUpgrades(appliedUpgrades);
        }

        private void ApplySelectedPrimaryWeapon(bool equipImmediately)
        {
            if (string.IsNullOrWhiteSpace(selectedPrimaryWeaponId))
            {
                return;
            }

            if (playerWeaponSwitcher == null)
            {
                playerWeaponSwitcher = FindFirstObjectByType<PlayerWeaponSwitcher>();
            }

            playerWeaponSwitcher?.SetPrimaryWeaponById(selectedPrimaryWeaponId, equipImmediately);
        }

        private static List<UpgradeSelectionController.OptionData> CloneOptions(List<UpgradeSelectionController.OptionData> source)
        {
            List<UpgradeSelectionController.OptionData> clone = new();
            if (source == null)
            {
                return clone;
            }

            for (int i = 0; i < source.Count; i++)
            {
                UpgradeSelectionController.OptionData item = source[i];
                if (item == null)
                {
                    continue;
                }

                clone.Add(new UpgradeSelectionController.OptionData
                {
                    id = item.id,
                    title = item.title,
                    description = item.description,
                    icon = item.icon,
                    iconText = item.iconText,
                    stackable = item.stackable,
                    maxStacks = item.maxStacks
                });
            }

            return clone;
        }

        private void LogDebug(string message)
        {
            if (debugLogs)
            {
                Debug.Log(message, this);
            }
        }
    }
}
