using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Sticki.Core;
using Sticki.UI.Core;

namespace Sticki.UI
{
    public class FrontendUIRootController : UIRootController
    {
        private void Start()
{
            GameSettingsService.ApplyAll();
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            var mainMenu = GetScreen<MainMenuUIController>();
            if (mainMenu != null)
            {
                mainMenu.OnStartClicked += StartRun;
                mainMenu.OnSettingsClicked += () => ShowScreen<SettingsController>(true);
                mainMenu.OnRecordsClicked += () => ShowScreen<RecordsController>(true);
                mainMenu.OnUpgradesClicked += () => ShowScreen<UpgradesLibraryController>(true);
                mainMenu.OnHowToClicked += () => ShowScreen<SimpleScreenController>(true);
                mainMenu.OnQuitClicked += QuitGame;
            }

            WireBack<SettingsController>("btn-back");
            WireBack<RecordsController>("btn-records-back");
            WireBack<UpgradesLibraryController>("btn-upgrades-back");
            WireBack<SimpleScreenController>("btn-howto-back");

            ShowScreen<MainMenuUIController>(true);
        }

        private void WireBack<T>(string buttonName) where T : UIScreenController
        {
            var screen = GetScreen<T>();
            if (screen == null || screen.RootVisualElement == null) return;
            var btn = screen.RootVisualElement.Q<Button>(buttonName);
            if (btn != null) btn.clicked += () => ShowScreen<MainMenuUIController>(true);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                var menu = GetScreen<MainMenuUIController>();
                if (menu != null && !menu.IsVisible())
                {
                    ShowScreen<MainMenuUIController>(true);
                }
            }
        }

        private void StartRun()
        {
            Time.timeScale = 1f;
            Sticki.Core.RunFlowController.Instance.StartRun();
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
