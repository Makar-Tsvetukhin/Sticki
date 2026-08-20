using Sticki.Player;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Sticki.UI.Core;

namespace Sticki.UI
{
    public class PauseController : UIScreenController
    {
        [Header("Flow")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private bool autoPauseOnFocusLost = true;
        [SerializeField] private bool pauseAudioListener = false;
        [SerializeField] private bool forceGameplayCursorOnResume = true;
        [SerializeField] private CursorLockMode gameplayCursorLockMode = CursorLockMode.Locked;
        [SerializeField] private bool gameplayCursorVisible = false;

        [Header("References")]
        [SerializeField] private PlayerControlCoordinator playerControlCoordinator;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private HudController hudController;
        [SerializeField] private GameplayUIRootController gameplayUIRootController;

        private Button resumeButton;
        private Button settingsButton;
        private Button mainMenuButton;
        private Button quitGameButton;

        private bool isPaused;
        private bool hadCursorVisibleBeforePause;
        private CursorLockMode cursorLockBeforePause;
        private Coroutine cursorRestoreRoutine;
        private float initializationTime;

        public bool IsPaused => isPaused;

        private void Awake()
        {
            TryResolveReferences();
        }

        protected override void OnInitialize()
        {
            initializationTime = Time.unscaledTime;
            
            resumeButton = root.Q<Button>("btn-resume");
            settingsButton = root.Q<Button>("btn-settings");
            mainMenuButton = root.Q<Button>("btn-main-menu");
            quitGameButton = root.Q<Button>("btn-quit-game");

            if (resumeButton != null) resumeButton.clicked += Resume;
            if (settingsButton != null) settingsButton.clicked += OpenSettingsPlaceholder;
            if (mainMenuButton != null) mainMenuButton.clicked += ReturnToMainMenu;
            if (quitGameButton != null) quitGameButton.clicked += QuitGame;

            // Start in resumed state
            isPaused = false;
            Hide();
        }

        private void OnDestroy()
        {
            if (resumeButton != null) resumeButton.clicked -= Resume;
            if (settingsButton != null) settingsButton.clicked -= OpenSettingsPlaceholder;
            if (mainMenuButton != null) mainMenuButton.clicked -= ReturnToMainMenu;
            if (quitGameButton != null) quitGameButton.clicked -= QuitGame;
        }

        private void Update()
        {
            TryResolveReferences();

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                TogglePause();
                return;
            }

            if (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame)
            {
                TogglePause();
            }
        }

        private void TryResolveReferences()
        {
            if (playerControlCoordinator == null)
                playerControlCoordinator = FindFirstObjectByType<PlayerControlCoordinator>();

            if (playerHealth == null)
                playerHealth = FindFirstObjectByType<PlayerHealth>();

            if (hudController == null)
                hudController = FindFirstObjectByType<HudController>();

            if (gameplayUIRootController == null)
                gameplayUIRootController = FindFirstObjectByType<GameplayUIRootController>();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // Don't pause if just started, or focusing, or already paused
            if (!autoPauseOnFocusLost || hasFocus || isPaused || (Time.unscaledTime - initializationTime < 1f))
                return;

            Pause();
        }

        public void TogglePause()
        {
            if (playerHealth != null && playerHealth.IsDead)
                return;

            if (isPaused) Resume();
            else Pause();
        }

        public void Pause()
        {
            if (isPaused) return;

            isPaused = true;
            Time.timeScale = 0f;

            if (pauseAudioListener) AudioListener.pause = true;

            if (playerControlCoordinator != null)
                playerControlCoordinator.SetControlEnabled(false);

            if (hudController != null)
                hudController.SetCrosshairVisible(false);

            if (cursorRestoreRoutine != null)
            {
                StopCoroutine(cursorRestoreRoutine);
                cursorRestoreRoutine = null;
            }

            cursorLockBeforePause = UnityEngine.Cursor.lockState;
            hadCursorVisibleBeforePause = UnityEngine.Cursor.visible;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            Show();
        }

        public void Resume()
        {
            if (!isPaused) return;

            isPaused = false;
            Time.timeScale = 1f;

            if (pauseAudioListener) AudioListener.pause = false;

            if (playerControlCoordinator != null)
                playerControlCoordinator.SetControlEnabled(true);

            if (hudController != null)
                hudController.SetCrosshairVisible(true);

            if (forceGameplayCursorOnResume)
            {
                UnityEngine.Cursor.lockState = gameplayCursorLockMode;
                UnityEngine.Cursor.visible = gameplayCursorVisible;
                if (isActiveAndEnabled)
                {
                    cursorRestoreRoutine = StartCoroutine(ApplyCursorStateForFrames(gameplayCursorLockMode, gameplayCursorVisible, 4));
                }
            }
            else
            {
                UnityEngine.Cursor.lockState = cursorLockBeforePause;
                UnityEngine.Cursor.visible = hadCursorVisibleBeforePause;
                if (isActiveAndEnabled)
                {
                    cursorRestoreRoutine = StartCoroutine(ApplyCursorStateForFrames(cursorLockBeforePause, hadCursorVisibleBeforePause, 4));
                }
            }

            Hide();
        }

        private IEnumerator ApplyCursorStateForFrames(CursorLockMode lockMode, bool visible, int frameCount)
        {
            for (int i = 0; i < frameCount; i++)
            {
                UnityEngine.Cursor.lockState = lockMode;
                UnityEngine.Cursor.visible = visible;
                yield return null;
            }
            cursorRestoreRoutine = null;
        }

        private void OpenSettingsPlaceholder()
        {
            if (gameplayUIRootController == null)
            {
                gameplayUIRootController = FindFirstObjectByType<GameplayUIRootController>();
            }

            gameplayUIRootController?.OpenSettingsFromPause();
        }

        private void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            if (pauseAudioListener) AudioListener.pause = false;
            SceneManager.LoadScene(mainMenuSceneName);
        }

        private void QuitGame()
        {
            Time.timeScale = 1f;
            if (pauseAudioListener) AudioListener.pause = false;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
