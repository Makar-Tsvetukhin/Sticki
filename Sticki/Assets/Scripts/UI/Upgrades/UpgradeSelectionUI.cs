using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Sticki.UI.Core;

namespace Sticki.UI
{
    public class UpgradeSelectionUI : UIScreenController
    {
        private Label titleLabel;
        private Label subtitleLabel;
        private VisualElement optionsContainer;
        private Button closeButton;

        public event Action OnCloseRequested;
        public event Action<UpgradeSelectionController.OptionData> OnOptionSelected;

        protected override void OnInitialize()
        {
            titleLabel = root.Q<Label>("upgrade-selection-title");
            subtitleLabel = root.Q<Label>("upgrade-selection-subtitle");
            optionsContainer = root.Q<VisualElement>("upgrade-selection-options");
            closeButton = root.Q<Button>("btn-upgrade-selection-close");

            if (closeButton != null)
            {
                closeButton.clicked += () => OnCloseRequested?.Invoke();
            }

            Hide();
        }

        public void Setup(string title, string subtitle, List<UpgradeSelectionController.OptionData> choices)
        {
            if (titleLabel != null) titleLabel.text = title;
            if (subtitleLabel != null) subtitleLabel.text = subtitle;

            if (optionsContainer == null) return;
            optionsContainer.Clear();

            if (choices == null || choices.Count == 0)
            {
                Label empty = new Label("Пока нет доступных вариантов.");
                empty.AddToClassList("upgrade-empty");
                optionsContainer.Add(empty);
                return;
            }

            foreach (var option in choices)
            {
                Button button = new Button(() => OnOptionSelected?.Invoke(option));
                button.AddToClassList("upgrade-option-card");

                VisualElement row = new VisualElement();
                row.AddToClassList("upgrade-option-row");

                VisualElement icon = new VisualElement();
                icon.AddToClassList("upgrade-option-icon");
                if (option.icon != null)
                {
                    icon.style.backgroundImage = new StyleBackground(option.icon);
                }
                else
                {
                    Label iconLabel = new Label(GetFallbackIconText(option));
                    iconLabel.AddToClassList("upgrade-option-icon-text");
                    icon.Add(iconLabel);
                }
                row.Add(icon);

                VisualElement textWrap = new VisualElement();
                textWrap.AddToClassList("upgrade-option-text-wrap");

                Label optionTitle = new Label(option.title);
                optionTitle.AddToClassList("upgrade-option-title");
                textWrap.Add(optionTitle);

                Label optionDescription = new Label(option.description);
                optionDescription.AddToClassList("upgrade-option-description");
                textWrap.Add(optionDescription);

                row.Add(textWrap);
                button.Add(row);
                optionsContainer.Add(button);
            }
        }

        private string GetFallbackIconText(UpgradeSelectionController.OptionData option)
        {
            if (option == null) return "?";
            if (!string.IsNullOrWhiteSpace(option.iconText)) return option.iconText.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(option.id)) return "UP";

            string id = option.id.ToLowerInvariant();
            if (id.Contains("weapon_ar")) return "AR";
            if (id.Contains("weapon_lmg")) return "LMG";
            if (id.Contains("weapon_sniper")) return "SNR";
            if (id.Contains("weapon_shotgun")) return "SHG";
            if (id.Contains("hp")) return "HP";
            if (id.Contains("speed")) return "SPD";
            if (id.Contains("resist")) return "DEF";
            if (id.Contains("regen")) return "RGN";
            if (id.Contains("damage")) return "DMG";
            if (id.Contains("crit")) return "CRT";
            if (id.Contains("rof")) return "ROF";
            if (id.Contains("reload")) return "RLD";
            if (id.Contains("spread")) return "ACC";

            return "UP";
        }
    }
}
