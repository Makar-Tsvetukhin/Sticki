using System;
using Sticki.Player;
using UnityEngine;

namespace Sticki.Upgrades
{
    public enum UpgradeEffectTarget
    {
        CharacterStat,
        WeaponStat
    }

    public enum UpgradeEffectType
    {
        AddStat,
        MultiplyStat
    }

    [Serializable]
    public class UpgradeEffectDefinition
    {
        [SerializeField] private UpgradeEffectTarget target = UpgradeEffectTarget.CharacterStat;
        [SerializeField] private UpgradeEffectType effectType = UpgradeEffectType.AddStat;
        [SerializeField] private PlayerStatId statId = PlayerStatId.MoveSpeed;
        [SerializeField] private WeaponStatId weaponStatId = WeaponStatId.Damage;
        [SerializeField] private float value = 1f;

        public UpgradeEffectTarget Target => target;
        public UpgradeEffectType EffectType => effectType;
        public PlayerStatId StatId => statId;
        public WeaponStatId WeaponStatId => weaponStatId;
        public float Value => value;

        public bool TryResolveCharacterStat(UpgradeCategory category, out PlayerStatId resolvedStatId)
        {
            resolvedStatId = statId;
            return target == UpgradeEffectTarget.CharacterStat && category == UpgradeCategory.Character;
        }

        public bool TryResolveWeaponStat(UpgradeCategory category, out WeaponStatId resolvedStatId)
        {
            if (target == UpgradeEffectTarget.WeaponStat && category == UpgradeCategory.Weapon)
            {
                resolvedStatId = weaponStatId;
                return true;
            }

            // Backward-compatible path for older assets where weapon upgrades used PlayerStatId.
            if (category == UpgradeCategory.Weapon && TryMapLegacyWeaponStat(statId, out resolvedStatId))
            {
                return true;
            }

            resolvedStatId = default;
            return false;
        }

        private static bool TryMapLegacyWeaponStat(PlayerStatId legacyStatId, out WeaponStatId resolvedStatId)
        {
            switch (legacyStatId)
            {
                case PlayerStatId.DamageMultiplier:
                    resolvedStatId = WeaponStatId.Damage;
                    return true;
                case PlayerStatId.FireRateMultiplier:
                    resolvedStatId = WeaponStatId.FireIntervalSeconds;
                    return true;
                default:
                    resolvedStatId = default;
                    return false;
            }
        }
    }
}
