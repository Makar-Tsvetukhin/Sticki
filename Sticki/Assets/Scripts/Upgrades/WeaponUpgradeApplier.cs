using System.Collections.Generic;
using Sticki.Combat;
using Sticki.Combat.Config;
using Sticki.Player;

namespace Sticki.Upgrades
{
    public sealed class WeaponUpgradeApplier
    {
        private readonly PlayerCombat playerCombat;

        public WeaponUpgradeApplier(PlayerCombat playerCombat)
        {
            this.playerCombat = playerCombat;
        }

        public void Reapply(RunUpgradesState runState)
        {
            if (playerCombat == null)
            {
                return;
            }

            WeaponConfig baseConfig = playerCombat.CurrentWeaponConfig;
            if (baseConfig == null)
            {
                playerCombat.ApplyWeaponRuntimeStats(null);
                return;
            }

            WeaponRuntimeStats runtimeStats = new(baseConfig);
            if (runState != null)
            {
                List<RunUpgradesState.UpgradeStack> upgrades = runState.GetAppliedUpgrades(UpgradeCategory.Weapon);
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
                            if (effect == null || !effect.TryResolveWeaponStat(definition.Category, out WeaponStatId statId))
                            {
                                continue;
                            }

                            ModifierOperation operation = effect.EffectType == UpgradeEffectType.AddStat
                                ? ModifierOperation.Add
                                : ModifierOperation.Multiply;

                            string sourceId = $"upgrade:{definition.Id}:{stackIndex}:{effectIndex}";
                            runtimeStats.AddModifier(new WeaponStatModifier(statId, operation, effect.Value, sourceId));
                        }
                    }
                }
            }

            playerCombat.ApplyWeaponRuntimeStats(runtimeStats);
        }
    }
}
