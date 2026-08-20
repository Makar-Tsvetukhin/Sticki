using System.Collections.Generic;
using Sticki.Player;

namespace Sticki.Upgrades
{
    public sealed class CharacterUpgradeApplier
    {
        private readonly PlayerStats playerStats;
        private readonly List<string> appliedStatModifierSources = new();

        public CharacterUpgradeApplier(PlayerStats playerStats)
        {
            this.playerStats = playerStats;
        }

        public void Reapply(RunUpgradesState runState)
        {
            if (playerStats == null || runState == null)
            {
                return;
            }

            Clear();

            List<RunUpgradesState.UpgradeStack> upgrades = runState.GetAppliedUpgrades(UpgradeCategory.Character);
            for (int i = 0; i < upgrades.Count; i++)
            {
                UpgradeDefinition definition = upgrades[i].Definition;
                int stacks = upgrades[i].Stacks;
                if (definition == null || stacks <= 0)
                {
                    continue;
                }

                IReadOnlyList<UpgradeEffectDefinition> effects = definition.Effects;
                for (int stackIndex = 0; stackIndex < stacks; stackIndex++)
                {
                    for (int effectIndex = 0; effectIndex < effects.Count; effectIndex++)
                    {
                        UpgradeEffectDefinition effect = effects[effectIndex];
                        if (effect == null || !effect.TryResolveCharacterStat(definition.Category, out PlayerStatId statId))
                        {
                            continue;
                        }

                        string sourceId = $"upgrade:{definition.Id}:{stackIndex}:{effectIndex}";
                        ModifierOperation operation = effect.EffectType == UpgradeEffectType.AddStat
                            ? ModifierOperation.Add
                            : ModifierOperation.Multiply;

                        playerStats.AddModifier(new StatModifier(statId, operation, effect.Value, sourceId));
                        appliedStatModifierSources.Add(sourceId);
                    }
                }
            }
        }

        public void Clear()
        {
            if (playerStats == null)
            {
                return;
            }

            for (int i = 0; i < appliedStatModifierSources.Count; i++)
            {
                playerStats.RemoveModifiersBySource(appliedStatModifierSources[i]);
            }

            appliedStatModifierSources.Clear();
        }
    }
}
