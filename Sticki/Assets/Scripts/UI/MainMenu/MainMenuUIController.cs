using System;
using UnityEngine;
using UnityEngine.UIElements;
using Sticki.UI.Core;

namespace Sticki.UI
{
    public class MainMenuUIController : UIScreenController
    {
        public event Action OnStartClicked;
        public event Action OnRecordsClicked;
        public event Action OnSettingsClicked;
        public event Action OnUpgradesClicked;
        public event Action OnHowToClicked;
        public event Action OnQuitClicked;

        private Button startButton;
        private Button recordsButton;
        private Button settingsButton;
        private Button upgradesButton;
        private Button howToButton;
        private Button quitButton;

        protected override void OnInitialize()
        {
            startButton = root.Q<Button>("btn-start");
            recordsButton = root.Q<Button>("btn-records");
            settingsButton = root.Q<Button>("btn-settings");
            upgradesButton = root.Q<Button>("btn-upgrades");
            howToButton = root.Q<Button>("btn-howto");
            quitButton = root.Q<Button>("btn-quit");

            if (startButton != null) startButton.clicked += () => OnStartClicked?.Invoke();
            if (recordsButton != null) recordsButton.clicked += () => OnRecordsClicked?.Invoke();
            if (settingsButton != null) settingsButton.clicked += () => OnSettingsClicked?.Invoke();
            if (upgradesButton != null) upgradesButton.clicked += () => OnUpgradesClicked?.Invoke();
            if (howToButton != null) howToButton.clicked += () => OnHowToClicked?.Invoke();
            if (quitButton != null) quitButton.clicked += () => OnQuitClicked?.Invoke();
        }
    }
}
