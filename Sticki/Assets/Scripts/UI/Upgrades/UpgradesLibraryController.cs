using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Sticki.UI.Core;

namespace Sticki.UI
{
    public class UpgradesLibraryController : UIScreenController
    {
        [SerializeField] private int discoveredCount = 0;

        private Label subtitleLabel;
        private ScrollView upgradesList;

        private readonly List<UpgradeDefinition> allUpgrades = new()
        {
            new UpgradeDefinition("Калибр +", "Оружие", "Обычное", "Увеличивает урон оружия."),
            new UpgradeDefinition("Критический шанс", "Оружие", "Редкое", "Повышает шанс критического попадания."),
            new UpgradeDefinition("Критический урон", "Оружие", "Редкое", "Усиливает критический множитель."),
            new UpgradeDefinition("Пробитие", "Оружие", "Редкое", "Пуля пробивает дополнительную цель."),
            new UpgradeDefinition("Разрывные патроны", "Оружие", "Эпическое", "Выстрелы наносят урон по области."),
            new UpgradeDefinition("Зажигательные патроны", "Оружие", "Эпическое", "Попадания накладывают горение."),
            new UpgradeDefinition("Скорость перезарядки", "Оружие", "Обычное", "Сокращает время перезарядки."),
            new UpgradeDefinition("Скорострельность", "Оружие", "Обычное", "Увеличивает темп стрельбы."),
            new UpgradeDefinition("Размер магазина", "Оружие", "Обычное", "Добавляет патроны в магазин."),
            new UpgradeDefinition("Точность", "Оружие", "Обычное", "Уменьшает разброс."),
            new UpgradeDefinition("Макс. здоровье", "Персонаж", "Обычное", "Увеличивает запас здоровья."),
            new UpgradeDefinition("Регенерация", "Персонаж", "Редкое", "Медленно восстанавливает здоровье."),
            new UpgradeDefinition("Скорость движения", "Персонаж", "Обычное", "Ускоряет перемещение."),
            new UpgradeDefinition("Сопротивление урону", "Персонаж", "Редкое", "Снижает входящий урон."),
            new UpgradeDefinition("Боевой импульс", "Персонаж", "Эпическое", "Серия убийств повышает урон."),
            new UpgradeDefinition("Охотник", "Персонаж", "Редкое", "Убийства дают краткий бонус скорости."),
            new UpgradeDefinition("Сборщик", "Персонаж", "Обычное", "Повышает шанс дропа ресурсов."),
            new UpgradeDefinition("Тяжелый шаг", "Персонаж", "Редкое", "Сбивает врагов при рывке."),
            new UpgradeDefinition("Читер?", "Персонаж", "Легендарное", "Однократно отменяет смертельный урон."),
            new UpgradeDefinition("Стабилизатор", "Оружие", "Редкое", "Сильнее гасит отдачу при зажиме.")
        };

        private sealed class UpgradeDefinition
        {
            public readonly string Name;
            public readonly string Category;
            public readonly string Rarity;
            public readonly string Description;

            public UpgradeDefinition(string name, string category, string rarity, string description)
            {
                Name = name;
                Category = category;
                Rarity = rarity;
                Description = description;
            }
        }

        protected override void OnInitialize()
        {
            subtitleLabel = root.Q<Label>("upgrades-subtitle");
            upgradesList = root.Q<ScrollView>("upgrades-list");

            if (upgradesList != null)
            {
                upgradesList.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                upgradesList.verticalScrollerVisibility = ScrollerVisibility.Auto;
            }

            Rebuild();
            Hide();
        }

        public void SetDiscoveredCount(int value)
        {
            discoveredCount = Mathf.Clamp(value, 0, allUpgrades.Count);
            Rebuild();
        }

        private void Rebuild()
        {
            if (upgradesList == null) return;

            int unlocked = Mathf.Clamp(discoveredCount, 0, allUpgrades.Count);
            if (subtitleLabel != null)
            {
                subtitleLabel.text = $"ОТКРЫТО {unlocked} / {allUpgrades.Count}";
            }

            upgradesList.Clear();
            for (int i = 0; i < allUpgrades.Count; i++)
            {
                bool isUnlocked = i < unlocked;
                upgradesList.Add(CreateCard(allUpgrades[i], isUnlocked));
            }
        }

        private static VisualElement CreateCard(UpgradeDefinition data, bool isUnlocked)
        {
            VisualElement card = new VisualElement();
            card.AddToClassList("upgrade-card");
            if (!isUnlocked) card.AddToClassList("upgrade-card--locked");

            VisualElement icon = new VisualElement();
            icon.AddToClassList("upgrade-icon");
            if (!isUnlocked) icon.AddToClassList("upgrade-icon--locked");

            VisualElement info = new VisualElement();
            info.AddToClassList("upgrade-info");

            Label name = new Label(isUnlocked ? data.Name : "???");
            name.AddToClassList("upgrade-name");
            if (!isUnlocked) name.AddToClassList("upgrade-name--locked");

            Label meta = new Label(isUnlocked ? $"{data.Category}  |  {data.Rarity}" : "СЕКРЕТНО");
            meta.AddToClassList("upgrade-meta");

            Label description = new Label(isUnlocked ? data.Description : "Описание скрыто до открытия.");
            description.AddToClassList("upgrade-description");
            if (!isUnlocked) description.AddToClassList("upgrade-description--locked");

            info.Add(name);
            info.Add(meta);
            info.Add(description);

            card.Add(icon);
            card.Add(info);
            return card;
        }
    }
}
