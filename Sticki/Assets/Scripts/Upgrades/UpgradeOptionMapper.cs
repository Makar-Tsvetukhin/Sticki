using System;
using System.Collections.Generic;
using Sticki.UI;

namespace Sticki.Upgrades
{
    public static class UpgradeOptionMapper
    {
        public static List<UpgradeSelectionController.OptionData> BuildOptions(IReadOnlyList<UpgradeDefinition> upgrades)
        {
            List<UpgradeSelectionController.OptionData> options = new();
            if (upgrades == null)
            {
                return options;
            }

            for (int i = 0; i < upgrades.Count; i++)
            {
                UpgradeDefinition definition = upgrades[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    continue;
                }

                options.Add(new UpgradeSelectionController.OptionData
                {
                    id = definition.Id,
                    title = ResolveText(definition.TitleKey, definition.FallbackTitle, definition.Id),
                    description = ResolveText(definition.DescriptionKey, definition.FallbackDescription, string.Empty),
                    icon = definition.Icon,
                    iconText = definition.IconText,
                    stackable = definition.Stackable,
                    maxStacks = definition.MaxStacks
                });
            }

            return options;
        }

        private static string ResolveText(string key, string fallback, string finalFallback)
        {
            if (!string.IsNullOrWhiteSpace(fallback))
            {
                return fallback;
            }

            if (!string.IsNullOrWhiteSpace(key))
            {
                return key;
            }

            return finalFallback;
        }
    }
}
