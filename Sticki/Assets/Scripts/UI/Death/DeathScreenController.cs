using System;
using Sticki.Core;
using Sticki.Player;
using Sticki.Spawning;
using Sticki.UI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Sticki.UI
{
    public class DeathScreenController : UIScreenController
    {
        [Header("Flow")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private bool freezeTimeOnDeath;
        [SerializeField] private bool pauseAudioListener;

        [Header("References")]
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private PlayerControlCoordinator playerControlCoordinator;
        [SerializeField] private HudController hudController;
        [SerializeField] private ArenaSpawner arenaSpawner;
        [SerializeField] private RunTabOverlayController runTabOverlayController;

        private Button restartButton;
        private Button mainMenuButton;
        private Label roomsLabel;
        private Label killsLabel;
        private Label timeLabel;
        private bool shown;
        private PlayerHealth subscribedPlayerHealth;

        private void Awake()
        {
            TryResolveReferences();
        }

        protected override void OnInitialize()
        {
            restartButton = root.Q<Button>("btn-death-restart");
            mainMenuButton = root.Q<Button>("btn-death-main-menu");
            roomsLabel = root.Q<Label>("death-rooms");
            killsLabel = root.Q<Label>("death-kills");
            timeLabel = root.Q<Label>("death-time");

            if (restartButton != null)
            {
                restartButton.clicked += RestartRun;
            }

            if (mainMenuButton != null)
            {
                mainMenuButton.clicked += ReturnToMainMenu;
            }

            Hide();
            SyncPlayerDeathSubscription();
        }

        private void OnDestroy()
        {
            if (restartButton != null)
            {
                restartButton.clicked -= RestartRun;
            }

            if (mainMenuButton != null)
            {
                mainMenuButton.clicked -= ReturnToMainMenu;
            }

            UnsubscribeFromPlayerDeath();
        }

        private void Update()
        {
            TryResolveReferences();
            SyncPlayerDeathSubscription();
        }

        private void HandlePlayerDied()
        {
            if (shown)
            {
                return;
            }

            shown = true;

            if (playerControlCoordinator != null)
            {
                playerControlCoordinator.SetControlEnabled(false);
            }

            if (hudController != null)
            {
                hudController.SetCrosshairVisible(false);
                hudController.SetInteractionPrompt(string.Empty, string.Empty, false);
            }

            if (freezeTimeOnDeath)
            {
                Time.timeScale = 0f;
            }

            if (pauseAudioListener)
            {
                AudioListener.pause = true;
            }

            RunSessionController.Instance.FinalizeAndSaveRun("death", SceneManager.GetActiveScene().name);
            FillStats();
            Show();

            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        private void FillStats()
        {
            if (roomsLabel != null)
            {
                roomsLabel.text = Mathf.Max(1, RunSessionController.Instance.CurrentRoomNumber).ToString();
            }

            if (killsLabel != null)
            {
                killsLabel.text = Mathf.Max(0, RunSessionController.Instance.TotalKills).ToString();
            }

            if (timeLabel != null)
            {
                TimeSpan span = TimeSpan.FromSeconds(RunSessionController.Instance.ElapsedSeconds);
                timeLabel.text = $"{(int)span.TotalMinutes:00}:{span.Seconds:00}";
            }
        }

        private void RestartRun()
        {
            RestoreGlobalStateBeforeTransition();
            RunFlowController.Instance.StartRun();
        }

        private void ReturnToMainMenu()
        {
            RestoreGlobalStateBeforeTransition();
            RunSessionController.Instance.AbandonRun();
            SceneManager.LoadScene(mainMenuSceneName);
        }

        private void RestoreGlobalStateBeforeTransition()
        {
            Time.timeScale = 1f;
            if (pauseAudioListener)
            {
                AudioListener.pause = false;
            }
        }

        private void TryResolveReferences()
        {
            if (playerHealth == null)
            {
                playerHealth = FindFirstObjectByType<PlayerHealth>();
            }

            if (playerControlCoordinator == null)
            {
                playerControlCoordinator = FindFirstObjectByType<PlayerControlCoordinator>();
            }

            if (hudController == null)
            {
                hudController = FindFirstObjectByType<HudController>();
            }

            if (arenaSpawner == null)
            {
                arenaSpawner = FindFirstObjectByType<ArenaSpawner>();
            }

            if (runTabOverlayController == null)
            {
                runTabOverlayController = FindFirstObjectByType<RunTabOverlayController>();
            }
        }

        private void SyncPlayerDeathSubscription()
        {
            if (subscribedPlayerHealth == playerHealth)
            {
                return;
            }

            UnsubscribeFromPlayerDeath();
            if (playerHealth == null)
            {
                return;
            }

            playerHealth.OnDied += HandlePlayerDied;
            subscribedPlayerHealth = playerHealth;
        }

        private void UnsubscribeFromPlayerDeath()
        {
            if (subscribedPlayerHealth == null)
            {
                return;
            }

            subscribedPlayerHealth.OnDied -= HandlePlayerDied;
            subscribedPlayerHealth = null;
        }
    }
}
