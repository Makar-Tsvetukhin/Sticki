using System;
using System.Collections.Generic;
using Sticki.Combat;
using Sticki.Core;
using Sticki.Player;
using Sticki.Spawning;
using Sticki.UI.Core;
using Sticki.Upgrades;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Sticki.UI
{
    public class RunTabOverlayController : UIScreenController
    {
        [Serializable]
        public class UpgradeEntry
        {
            public string title = "Upgrade";
            public string description = "Upgrade description.";
            [Min(1)] public int stacks = 1;
            public Sprite icon;
        }

        [Header("References")]
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private PlayerCombat playerCombat;
        [SerializeField] private ArenaSpawner arenaSpawner;
        [SerializeField] private PauseController pauseController;
        [SerializeField] private UpgradeRuntimeController upgradeRuntimeController;

        [Header("Room Info")]
        [SerializeField] [Min(1)] private int currentRoomNumber = 1;

        [Header("Upgrades (runtime/demo)")]
        [SerializeField] private List<UpgradeEntry> activeUpgrades = new();

        private Label roomValueLabel;
        private Label arenaValueLabel;
        private Label timerValueLabel;
        private ScrollView upgradesList;

        public int CurrentRoomNumber => Mathf.Max(1, currentRoomNumber);

        private void Awake()
        {
            TryResolveReferences();
        }

        protected override void OnInitialize()
        {
            roomValueLabel = document.rootVisualElement.Q<Label>("room-value");
            arenaValueLabel = document.rootVisualElement.Q<Label>("arena-value");
            timerValueLabel = document.rootVisualElement.Q<Label>("timer-value");

            upgradesList = document.rootVisualElement.Q<ScrollView>("upgrades-list");
            if (upgradesList != null)
            {
                upgradesList.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                upgradesList.verticalScrollerVisibility = ScrollerVisibility.Auto;
            }

            if (arenaValueLabel != null)
            {
                arenaValueLabel.style.display = DisplayStyle.None;
            }

            Hide();
            RefreshHeader();
            TryPopulateUpgradesFromRuntime();
            RebuildUpgradesList();
        }

        private void Update()
        {
            TryResolveReferences();

            bool isPauseOpen = pauseController != null && pauseController.IsPaused;
            bool showByKey = IsOverlayHotkeyHeld();
            bool shouldShow = showByKey && !isPauseOpen;

            if (shouldShow)
            {
                Show();
            }
            else
            {
                Hide();
            }

            if (!IsVisible())
            {
                return;
            }

            RefreshHeader();
        }

        public void SetRoomInfo(int roomNumber, string arenaLabel)
        {
            currentRoomNumber = Mathf.Max(1, roomNumber);
            RefreshHeader();
        }

        public void SetActiveUpgrades(List<UpgradeEntry> upgrades)
        {
            activeUpgrades = upgrades ?? new List<UpgradeEntry>();
            RebuildUpgradesList();
        }

        public void SetAppliedUpgrades(List<RunUpgradesState.UpgradeStack> upgrades)
        {
            activeUpgrades = new List<UpgradeEntry>();
            if (upgrades != null)
            {
                for (int i = 0; i < upgrades.Count; i++)
                {
                    RunUpgradesState.UpgradeStack stack = upgrades[i];
                    UpgradeDefinition definition = stack.Definition;
                    if (definition == null || stack.Stacks <= 0)
                    {
                        continue;
                    }

                    string title = !string.IsNullOrWhiteSpace(definition.FallbackTitle)
                        ? definition.FallbackTitle
                        : definition.Id;
                    string description = definition.FallbackDescription ?? string.Empty;

                    activeUpgrades.Add(new UpgradeEntry
                    {
                        title = title,
                        description = description,
                        stacks = stack.Stacks,
                        icon = definition.Icon
                    });
                }
            }

            activeUpgrades.Sort((left, right) => string.Compare(left.title, right.title, StringComparison.OrdinalIgnoreCase));
            RebuildUpgradesList();
        }

        public void AddOrUpdateUpgrade(string title, string description, int stacks)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return;
            }

            int clampedStacks = Mathf.Max(1, stacks);
            for (int i = 0; i < activeUpgrades.Count; i++)
            {
                UpgradeEntry entry = activeUpgrades[i];
                if (string.Equals(entry.title, title, StringComparison.OrdinalIgnoreCase))
                {
                    entry.description = description;
                    entry.stacks = clampedStacks;
                    RebuildUpgradesList();
                    return;
                }
            }

            activeUpgrades.Add(new UpgradeEntry
            {
                title = title,
                description = description,
                stacks = clampedStacks
            });
            RebuildUpgradesList();
        }

        private bool IsOverlayHotkeyHeld()
        {
            bool keyboardHeld = Keyboard.current != null && Keyboard.current.tabKey.isPressed;
            bool gamepadHeld = Gamepad.current != null && Gamepad.current.selectButton.isPressed;
            return keyboardHeld || gamepadHeld;
        }

        private void RefreshHeader()
        {
            RunSessionController runSession = RunSessionController.Instance;
            int roomNumber = runSession.IsRunActive ? Mathf.Max(1, runSession.CurrentRoomNumber) : Mathf.Max(1, currentRoomNumber);

            if (roomValueLabel != null)
            {
                roomValueLabel.text = roomNumber.ToString();
            }

            if (timerValueLabel != null)
            {
                TimeSpan span = TimeSpan.FromSeconds(runSession.ElapsedSeconds);
                timerValueLabel.text = $"{(int)span.TotalMinutes:00}:{span.Seconds:00}";
            }
        }

        private void RebuildUpgradesList()
        {
            if (upgradesList == null)
            {
                return;
            }

            upgradesList.Clear();
            if (activeUpgrades == null || activeUpgrades.Count == 0)
            {
                Label empty = new Label("Пока нет активных улучшений.");
                empty.AddToClassList("upgrade-empty");
                upgradesList.Add(empty);
                return;
            }

            for (int i = 0; i < activeUpgrades.Count; i++)
            {
                UpgradeEntry entry = activeUpgrades[i];
                VisualElement row = new VisualElement();
                row.AddToClassList("upgrade-row");

                VisualElement top = new VisualElement();
                top.AddToClassList("upgrade-row-top");

                VisualElement icon = new VisualElement();
                icon.AddToClassList("upgrade-icon");
                if (entry.icon != null)
                {
                    icon.style.backgroundImage = new StyleBackground(entry.icon);
                }

                VisualElement head = new VisualElement();
                head.AddToClassList("upgrade-row-head");

                Label name = new Label(string.IsNullOrWhiteSpace(entry.title) ? "Улучшение" : entry.title);
                name.AddToClassList("upgrade-name");
                head.Add(name);

                Label stack = new Label($"УРОВЕНЬ x{Mathf.Max(1, entry.stacks)}");
                stack.AddToClassList("upgrade-stack");
                head.Add(stack);

                top.Add(icon);
                top.Add(head);
                row.Add(top);

                if (!string.IsNullOrWhiteSpace(entry.description))
                {
                    Label description = new Label(entry.description);
                    description.AddToClassList("upgrade-description");
                    row.Add(description);
                }

                upgradesList.Add(row);
            }
        }

        private void TryResolveReferences()
        {
            if (playerHealth == null)
            {
                playerHealth = FindFirstObjectByType<PlayerHealth>();
            }

            if (playerCombat == null)
            {
                playerCombat = FindFirstObjectByType<PlayerCombat>();
            }

            if (arenaSpawner == null)
            {
                arenaSpawner = FindFirstObjectByType<ArenaSpawner>();
            }

            if (pauseController == null)
            {
                pauseController = FindFirstObjectByType<PauseController>();
            }

            if (upgradeRuntimeController == null)
            {
                upgradeRuntimeController = FindFirstObjectByType<UpgradeRuntimeController>();
            }

            TryPopulateUpgradesFromRuntime();
        }

        private void TryPopulateUpgradesFromRuntime()
        {
            if (upgradeRuntimeController == null)
            {
                return;
            }

            List<RunUpgradesState.UpgradeStack> appliedUpgrades = upgradeRuntimeController.RunState.GetAppliedUpgrades();
            if (appliedUpgrades.Count == activeUpgrades.Count && activeUpgrades.Count > 0)
            {
                return;
            }

            SetAppliedUpgrades(appliedUpgrades);
        }
    }
}
