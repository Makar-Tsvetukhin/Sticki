using System;
using System.Globalization;
using Sticki.Core;
using Sticki.UI.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sticki.UI
{
    public class SettingsController : UIScreenController
    {
        public event Action OnBackRequested;
        public event Action OnApplied;

        private VisualElement panelGeneral;
        private VisualElement panelAudio;
        private VisualElement panelControls;
        private VisualElement panelVideo;
        private Button navGeneral;
        private Button navAudio;
        private Button navControls;
        private Button navVideo;
        private Button applyButton;
        private Button backButton;

        private Slider mouseSensitivity;
        private Label mouseSensitivityValue;
        private Slider fovSlider;
        private Label fovValue;
        private Slider masterVolume;
        private Label masterVolumeValue;
        private Slider sfxVolume;
        private Label sfxVolumeValue;
        private Slider musicVolume;
        private Label musicVolumeValue;

        private GameSettingsData pendingSettings;

        private const string ActiveClass = "is-active";
        private const string HiddenClass = "is-hidden";
        private bool suppressCallbacks;

        public override void Initialize(UIDocument doc)
        {
            document = doc;
            root = document.rootVisualElement.Q<VisualElement>(rootName);

            if (root == null)
            {
                root = document.rootVisualElement.Q<VisualElement>("settings-root");
            }

            if (root == null)
            {
                Debug.LogWarning($"SettingsController on {gameObject.name}: settings root was not found.");
                return;
            }

            OnInitialize();
        }

        protected override void OnInitialize()
        {
            GameSettingsService.EnsureLoaded();

            navGeneral = root.Q<Button>("nav-general");
            navAudio = root.Q<Button>("nav-audio");
            navControls = root.Q<Button>("nav-controls");
            navVideo = root.Q<Button>("nav-video");
            applyButton = root.Q<Button>("btn-apply");
            backButton = root.Q<Button>("btn-back");

            panelGeneral = root.Q<VisualElement>("panel-general");
            panelAudio = root.Q<VisualElement>("panel-audio");
            panelControls = root.Q<VisualElement>("panel-controls");
            panelVideo = root.Q<VisualElement>("panel-video");

            mouseSensitivity = root.Q<Slider>("mouse-sensitivity");
            mouseSensitivityValue = root.Q<Label>("mouse-sensitivity-value");
            fovSlider = root.Q<Slider>("fov-slider");
            fovValue = root.Q<Label>("fov-value");
            masterVolume = root.Q<Slider>("master-volume");
            masterVolumeValue = root.Q<Label>("master-volume-value");
            sfxVolume = root.Q<Slider>("sfx-volume");
            sfxVolumeValue = root.Q<Label>("sfx-volume-value");
            musicVolume = root.Q<Slider>("music-volume");
            musicVolumeValue = root.Q<Label>("music-volume-value");

            if (navGeneral != null) navGeneral.clicked += () => ShowPanel(panelGeneral, navGeneral);
            if (navAudio != null) navAudio.clicked += () => ShowPanel(panelAudio, navAudio);
            if (navControls != null) navControls.clicked += () => ShowPanel(panelControls, navControls);
            if (navVideo != null) navVideo.clicked += () => ShowPanel(panelVideo, navVideo);
            if (applyButton != null) applyButton.clicked += ApplyPendingSettings;
            if (backButton != null) backButton.clicked += HandleBackPressed;

            SetupMouseSensitivity();
            SetupFieldOfView();
            SetupPercentSlider(masterVolume, masterVolumeValue, value => pendingSettings.MasterVolume = value);
            SetupPercentSlider(sfxVolume, sfxVolumeValue, value => pendingSettings.SfxVolume = value);
            SetupPercentSlider(musicVolume, musicVolumeValue, value => pendingSettings.MusicVolume = value);

            ShowPanel(panelGeneral, navGeneral);
            ReloadPendingFromSavedSettings();
            Hide();
        }

        protected override void OnShow()
        {
            ReloadPendingFromSavedSettings();
        }

        private void SetupMouseSensitivity()
        {
            if (mouseSensitivity == null)
            {
                return;
            }

            mouseSensitivity.lowValue = 0.1f;
            mouseSensitivity.highValue = 5f;
            mouseSensitivity.RegisterValueChangedCallback(evt =>
            {
                if (suppressCallbacks)
                {
                    return;
                }

                pendingSettings.MouseSensitivity = evt.newValue;
                UpdateValue(mouseSensitivityValue, evt.newValue, 2);
            });
            SetupEditableValue(mouseSensitivity, mouseSensitivityValue, 2);
        }

        private void SetupFieldOfView()
        {
            if (fovSlider == null)
            {
                return;
            }

            fovSlider.lowValue = 60f;
            fovSlider.highValue = 110f;
            fovSlider.RegisterValueChangedCallback(evt =>
            {
                if (suppressCallbacks)
                {
                    return;
                }

                pendingSettings.FieldOfView = evt.newValue;
                UpdateValue(fovValue, evt.newValue, 0);
            });
            SetupEditableValue(fovSlider, fovValue, 0);
        }

        private void SetupPercentSlider(Slider slider, Label label, Action<float> assignValue)
        {
            if (slider == null)
            {
                return;
            }

            slider.lowValue = 0f;
            slider.highValue = 100f;
            slider.RegisterValueChangedCallback(evt =>
            {
                if (suppressCallbacks)
                {
                    return;
                }

                assignValue?.Invoke(evt.newValue);
                UpdateValue(label, evt.newValue, 0);
            });
            SetupEditableValue(slider, label, 0);
        }

        private void ReloadPendingFromSavedSettings()
        {
            pendingSettings = GameSettingsService.Current;
            suppressCallbacks = true;

            SetSliderValue(mouseSensitivity, mouseSensitivityValue, pendingSettings.MouseSensitivity, 2);
            SetSliderValue(fovSlider, fovValue, pendingSettings.FieldOfView, 0);
            SetSliderValue(masterVolume, masterVolumeValue, pendingSettings.MasterVolume, 0);
            SetSliderValue(sfxVolume, sfxVolumeValue, pendingSettings.SfxVolume, 0);
            SetSliderValue(musicVolume, musicVolumeValue, pendingSettings.MusicVolume, 0);

            suppressCallbacks = false;
        }

        private void SetSliderValue(Slider slider, Label label, float value, int decimals)
        {
            if (slider != null)
            {
                slider.value = value;
            }

            UpdateValue(label, value, decimals);
        }

        private void ApplyPendingSettings()
        {
            GameSettingsService.SetMouseSensitivity(pendingSettings.MouseSensitivity);
            GameSettingsService.SetFieldOfView(pendingSettings.FieldOfView);
            GameSettingsService.SetMasterVolume(pendingSettings.MasterVolume);
            GameSettingsService.SetSfxVolume(pendingSettings.SfxVolume);
            GameSettingsService.SetMusicVolume(pendingSettings.MusicVolume);
            OnApplied?.Invoke();
        }

        private void HandleBackPressed()
        {
            ReloadPendingFromSavedSettings();
            OnBackRequested?.Invoke();
        }

        private void UpdateValue(Label label, float value, int decimals)
        {
            if (label == null)
            {
                return;
            }

            label.text = value.ToString(decimals > 0 ? $"F{decimals}" : "F0");
        }

        private void SetupEditableValue(Slider slider, Label label, int decimals)
        {
            if (slider == null || label == null)
            {
                return;
            }

            label.AddToClassList("panel-value--editable");
            label.tooltip = "Click to edit";
            label.RegisterCallback<PointerDownEvent>(_ => BeginEditValue(slider, label, decimals));
        }

        private void BeginEditValue(Slider slider, Label label, int decimals)
        {
            if (label.parent == null || label.userData is TextField)
            {
                return;
            }

            VisualElement parent = label.parent;
            int labelIndex = parent.IndexOf(label);
            TextField input = new TextField
            {
                isDelayed = false,
                value = label.text
            };
            input.AddToClassList("panel-value-input");

            label.userData = input;
            label.style.display = DisplayStyle.None;
            parent.Insert(labelIndex, input);

            bool closed = false;

            void Close(bool apply)
            {
                if (closed)
                {
                    return;
                }

                closed = true;

                if (apply && TryParseSliderValue(input.value, out float parsed))
                {
                    slider.value = Mathf.Clamp(parsed, slider.lowValue, slider.highValue);
                }
                else
                {
                    UpdateValue(label, slider.value, decimals);
                }

                input.RemoveFromHierarchy();
                label.style.display = DisplayStyle.Flex;
                label.userData = null;
            }

            input.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    evt.StopImmediatePropagation();
                    Close(true);
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    evt.StopImmediatePropagation();
                    Close(false);
                }
            });
            input.RegisterCallback<FocusOutEvent>(_ => Close(true));

            input.schedule.Execute(() =>
            {
                input.Focus();
                input.SelectAll();
            });
        }

        private static bool TryParseSliderValue(string rawValue, out float value)
        {
            string normalized = rawValue.Trim().Replace(',', '.');
            return float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private void ShowPanel(VisualElement panel, Button nav)
        {
            SetPanelVisible(panelGeneral, panel == panelGeneral);
            SetPanelVisible(panelAudio, panel == panelAudio);
            SetPanelVisible(panelControls, panel == panelControls);
            SetPanelVisible(panelVideo, panel == panelVideo);

            SetNavActive(navGeneral, nav == navGeneral);
            SetNavActive(navAudio, nav == navAudio);
            SetNavActive(navControls, nav == navControls);
            SetNavActive(navVideo, nav == navVideo);
        }

        private static void SetPanelVisible(VisualElement panel, bool visible)
        {
            if (panel == null)
            {
                return;
            }

            if (visible)
            {
                panel.RemoveFromClassList(HiddenClass);
            }
            else
            {
                panel.AddToClassList(HiddenClass);
            }
        }

        private static void SetNavActive(Button button, bool active)
        {
            if (button == null)
            {
                return;
            }

            if (active)
            {
                button.AddToClassList(ActiveClass);
            }
            else
            {
                button.RemoveFromClassList(ActiveClass);
            }
        }
    }
}
