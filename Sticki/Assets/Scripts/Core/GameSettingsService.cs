using UnityEngine;

namespace Sticki.Core
{
    public struct GameSettingsData
    {
        public float MouseSensitivity;
        public float FieldOfView;
        public float MasterVolume;
        public float SfxVolume;
        public float MusicVolume;
    }

    public static class GameSettingsService
    {
        private const string MouseSensitivityKey = "sticki.settings.mouse_sensitivity";
        private const string FieldOfViewKey = "sticki.settings.fov";
        private const string MasterVolumeKey = "sticki.settings.master_volume";
        private const string SfxVolumeKey = "sticki.settings.sfx_volume";
        private const string MusicVolumeKey = "sticki.settings.music_volume";

        private static bool loaded;
        private static GameSettingsData current;

        public static GameSettingsData Current
        {
            get
            {
                EnsureLoaded();
                return current;
            }
        }

        public static void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }

            current = new GameSettingsData
            {
                MouseSensitivity = PlayerPrefs.GetFloat(MouseSensitivityKey, 1f),
                FieldOfView = PlayerPrefs.GetFloat(FieldOfViewKey, 90f),
                MasterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 100f),
                SfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 100f),
                MusicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 100f)
            };

            loaded = true;
            ApplyAll();
        }

        public static void SetMouseSensitivity(float value)
        {
            EnsureLoaded();
            current.MouseSensitivity = Mathf.Clamp(value, 0.1f, 5f);
            PlayerPrefs.SetFloat(MouseSensitivityKey, current.MouseSensitivity);
            PlayerPrefs.Save();
        }

        public static void SetFieldOfView(float value)
        {
            EnsureLoaded();
            current.FieldOfView = Mathf.Clamp(value, 60f, 110f);
            PlayerPrefs.SetFloat(FieldOfViewKey, current.FieldOfView);
            PlayerPrefs.Save();
            ApplySceneSettings();
        }

        public static void SetMasterVolume(float value)
        {
            EnsureLoaded();
            current.MasterVolume = Mathf.Clamp(value, 0f, 100f);
            PlayerPrefs.SetFloat(MasterVolumeKey, current.MasterVolume);
            PlayerPrefs.Save();
            ApplyAudioSettings();
        }

        public static void SetSfxVolume(float value)
        {
            EnsureLoaded();
            current.SfxVolume = Mathf.Clamp(value, 0f, 100f);
            PlayerPrefs.SetFloat(SfxVolumeKey, current.SfxVolume);
            PlayerPrefs.Save();
        }

        public static void SetMusicVolume(float value)
        {
            EnsureLoaded();
            current.MusicVolume = Mathf.Clamp(value, 0f, 100f);
            PlayerPrefs.SetFloat(MusicVolumeKey, current.MusicVolume);
            PlayerPrefs.Save();
        }

        public static void ApplyAll()
        {
            EnsureLoaded();
            ApplyAudioSettings();
            ApplySceneSettings();
        }

        public static void ApplySceneSettings()
        {
            EnsureLoaded();

            Camera mainCamera = Camera.main;
            if (mainCamera != null && mainCamera.enabled)
            {
                mainCamera.fieldOfView = current.FieldOfView;
            }
        }

        public static float GetMouseSensitivityMultiplier()
        {
            EnsureLoaded();
            return current.MouseSensitivity;
        }

        private static void ApplyAudioSettings()
        {
            AudioListener.volume = Mathf.Clamp01(current.MasterVolume / 100f);
        }
    }
}
