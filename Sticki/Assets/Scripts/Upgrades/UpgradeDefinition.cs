using System.Collections.Generic;
using UnityEngine;

namespace Sticki.Upgrades
{
    [CreateAssetMenu(fileName = "UpgradeDefinition", menuName = "Sticki/Upgrades/Upgrade Definition")]
    public class UpgradeDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id;
        [SerializeField] private UpgradeCategory category = UpgradeCategory.Character;

        [Header("Localization")]
        [SerializeField] private string titleKey;
        [SerializeField] private string descriptionKey;
        [SerializeField] private string fallbackTitle;
        [SerializeField] [TextArea(2, 4)] private string fallbackDescription;

        [Header("Presentation")]
        [SerializeField] private Sprite icon;
        [SerializeField] private string iconText;

        [Header("Stacking")]
        [SerializeField] private bool stackable = true;
        [SerializeField] [Min(0)] private int maxStacks;
        [SerializeField] private UpgradeRarity rarity = UpgradeRarity.Common;
        [SerializeField] [Min(0.0001f)] private float selectionWeight = 1f;

        [Header("Effects")]
        [SerializeField] private List<UpgradeEffectDefinition> effects = new();

        public string Id => id;
        public UpgradeCategory Category => category;
        public string TitleKey => titleKey;
        public string DescriptionKey => descriptionKey;
        public string FallbackTitle => fallbackTitle;
        public string FallbackDescription => fallbackDescription;
        public Sprite Icon => icon;
        public string IconText => iconText;
        public bool Stackable => stackable;
        public int MaxStacks => maxStacks;
        public UpgradeRarity Rarity => rarity;
        public float SelectionWeight => selectionWeight;
        public IReadOnlyList<UpgradeEffectDefinition> Effects => effects;
    }
}
