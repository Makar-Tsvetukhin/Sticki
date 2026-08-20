using System.Collections.Generic;
using Sticki.Player;

namespace Sticki.Upgrades
{
    public sealed class UpgradeStatApplier
    {
        private readonly PlayerStats playerStats;
        private readonly List<string> appliedStatModifierSources = new();

        public UpgradeStatApplier(PlayerStats playerStats)
        {
            this.playerStats = playerStats;
        }

        public void Reapply(RunUpgradesState runState)
        {
            if (playerStats == null || runState == null)
            {
                return;
            }

            ClearAppliedStatModifiers();

            List<RunUpgradesState.UpgradeStack> upgrades = runState.GetAppliedUpgrades();
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
                        if (effect == null)
                        {
                            continue;
                        }

                        string sourceId = $"upgrade:{definition.Id}:{stackIndex}:{effectIndex}";
                        ModifierOperation operation = effect.EffectType == UpgradeEffectType.AddStat
                            ? ModifierOperation.Add
                            : ModifierOperation.Multiply;

                        playerStats.AddModifier(new StatModifier(effect.StatId, operation, effect.Value, sourceId));
                        appliedStatModifierSources.Add(sourceId);
                    }
                }
            }
        }

        public void Clear()
        {
            ClearAppliedStatModifiers();
        }

        private void ClearAppliedStatModifiers()
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
