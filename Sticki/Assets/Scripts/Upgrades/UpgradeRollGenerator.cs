using System;
using System.Collections.Generic;
using Sticki.UI;

namespace Sticki.Upgrades
{
    public sealed class UpgradeRollGenerator
    {
        private readonly UpgradeCatalog catalog;

        public UpgradeRollGenerator(UpgradeCatalog catalog)
        {
            this.catalog = catalog;
        }

        public List<UpgradeSelectionController.OptionData> RollOptions(UpgradeCategory category, int count, RunUpgradesState runState, Random random)
        {
            List<UpgradeDefinition> pool = BuildAvailablePool(category, runState);
            List<UpgradeDefinition> rolled = RollWeighted(pool, count, random);
            return UpgradeOptionMapper.BuildOptions(rolled);
        }

        private List<UpgradeDefinition> BuildAvailablePool(UpgradeCategory category, RunUpgradesState runState)
        {
            IReadOnlyList<UpgradeDefinition> source = category == UpgradeCategory.Character
                ? catalog.CharacterUpgrades
                : catalog.WeaponUpgrades;

            List<UpgradeDefinition> available = new();
            if (source == null)
            {
                return available;
            }

            for (int i = 0; i < source.Count; i++)
            {
                UpgradeDefinition definition = source[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    continue;
                }

                int currentStacks = runState != null ? runState.GetStackCount(definition.Id) : 0;
                int maxStacks = definition.Stackable ? definition.MaxStacks : 1;
                if (maxStacks > 0 && currentStacks >= maxStacks)
                {
                    continue;
                }

                available.Add(definition);
            }

            return available;
        }

        private static List<UpgradeDefinition> RollWeighted(List<UpgradeDefinition> source, int count, Random random)
        {
            List<UpgradeDefinition> available = new(source);
            List<UpgradeDefinition> result = new();
            if (available.Count == 0 || count <= 0)
            {
                return result;
            }

            Random rng = random ?? new Random();
            int targetCount = Math.Min(count, available.Count);
            for (int i = 0; i < targetCount; i++)
            {
                float totalWeight = 0f;
                for (int candidateIndex = 0; candidateIndex < available.Count; candidateIndex++)
                {
                    totalWeight += Math.Max(0.0001f, available[candidateIndex].SelectionWeight);
                }

                double roll = rng.NextDouble() * totalWeight;
                float cumulative = 0f;
                int pickedIndex = available.Count - 1;
                for (int candidateIndex = 0; candidateIndex < available.Count; candidateIndex++)
                {
                    cumulative += Math.Max(0.0001f, available[candidateIndex].SelectionWeight);
                    if (roll <= cumulative)
                    {
                        pickedIndex = candidateIndex;
                        break;
                    }
                }

                result.Add(available[pickedIndex]);
                available.RemoveAt(pickedIndex);
            }

            return result;
        }
    }
}
