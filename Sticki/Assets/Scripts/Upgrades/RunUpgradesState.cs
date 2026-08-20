using System;
using System.Collections.Generic;

namespace Sticki.Upgrades
{
    [Serializable]
    public class RunUpgradesState
    {
        [Serializable]
        public readonly struct UpgradeStack
        {
            public UpgradeStack(UpgradeDefinition definition, int stacks)
            {
                Definition = definition;
                Stacks = stacks;
            }

            public UpgradeDefinition Definition { get; }
            public int Stacks { get; }
        }

        private readonly Dictionary<string, int> stacksById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, UpgradeDefinition> definitionsById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<UpgradeCategory, int> rollCounters = new();
        private readonly Dictionary<UpgradeCategory, List<string>> lastOfferedUpgradeIdsByCategory = new();

        public int SessionSeed { get; private set; }

        public void InitializeSeed(int seed)
        {
            SessionSeed = seed == 0 ? Environment.TickCount : seed;
            rollCounters.Clear();
        }

        public void Reset(int seed)
        {
            stacksById.Clear();
            definitionsById.Clear();
            lastOfferedUpgradeIdsByCategory.Clear();
            InitializeSeed(seed);
        }

        public bool TryAddUpgrade(UpgradeDefinition definition, out int newStackCount)
        {
            newStackCount = 0;
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
            {
                return false;
            }

            stacksById.TryGetValue(definition.Id, out int currentStacks);
            int maxStacks = definition.Stackable ? definition.MaxStacks : 1;
            if (maxStacks > 0 && currentStacks >= maxStacks)
            {
                newStackCount = currentStacks;
                return false;
            }

            newStackCount = currentStacks + 1;
            stacksById[definition.Id] = newStackCount;
            definitionsById[definition.Id] = definition;
            return true;
        }

        public int GetStackCount(string upgradeId)
        {
            if (string.IsNullOrWhiteSpace(upgradeId))
            {
                return 0;
            }

            return stacksById.TryGetValue(upgradeId, out int stacks) ? stacks : 0;
        }

        public List<UpgradeStack> GetAppliedUpgrades(UpgradeCategory? category = null)
        {
            List<UpgradeStack> result = new();
            foreach (KeyValuePair<string, int> pair in stacksById)
            {
                if (pair.Value <= 0 || !definitionsById.TryGetValue(pair.Key, out UpgradeDefinition definition) || definition == null)
                {
                    continue;
                }

                if (category.HasValue && definition.Category != category.Value)
                {
                    continue;
                }

                result.Add(new UpgradeStack(definition, pair.Value));
            }

            return result;
        }

        public IReadOnlyList<string> GetLastOfferedUpgradeIds(UpgradeCategory category)
        {
            return lastOfferedUpgradeIdsByCategory.TryGetValue(category, out List<string> ids)
                ? ids
                : Array.Empty<string>();
        }

        public void RememberOfferedUpgrades(UpgradeCategory category, IReadOnlyList<UpgradeDefinition> definitions)
        {
            List<string> ids = new();
            if (definitions != null)
            {
                for (int i = 0; i < definitions.Count; i++)
                {
                    UpgradeDefinition definition = definitions[i];
                    if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                    {
                        continue;
                    }

                    ids.Add(definition.Id);
                }
            }

            lastOfferedUpgradeIdsByCategory[category] = ids;
        }

        public System.Random CreateRollRandom(UpgradeCategory category)
        {
            rollCounters.TryGetValue(category, out int currentCounter);
            currentCounter++;
            rollCounters[category] = currentCounter;
            int seed = HashCode.Combine(SessionSeed, (int)category, currentCounter);
            return new System.Random(seed);
        }
    }
}
