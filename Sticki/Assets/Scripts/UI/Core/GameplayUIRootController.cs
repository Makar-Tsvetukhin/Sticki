using UnityEngine;
using Sticki.UI.Core;

namespace Sticki.UI
{
    public class GameplayUIRootController : UIRootController
    {
        private void Start()
        {
            SettingsController settings = GetScreen<SettingsController>();
            if (settings != null)
            {
                settings.OnBackRequested += CloseSettingsToPause;
                settings.OnApplied += CloseSettingsToPause;
            }

            ShowScreen<HudController>(false);
            HideScreen<PauseController>();
            HideScreen<DeathScreenController>();
            HideScreen<RunTabOverlayController>();
            HideScreen<SettingsController>();
            HideScreen<UpgradeSelectionUI>();
        }

        public void OpenPause() => ShowScreen<PauseController>(false);
        public void ClosePause() => HideScreen<PauseController>();
        public void OpenDeathScreen() => ShowScreen<DeathScreenController>(false);

        public void OpenSettingsFromPause()
        {
            HideScreen<PauseController>();
            ShowScreen<SettingsController>(false);
        }

        private void CloseSettingsToPause()
        {
            HideScreen<SettingsController>();
            ShowScreen<PauseController>(false);
        }
    }
}
