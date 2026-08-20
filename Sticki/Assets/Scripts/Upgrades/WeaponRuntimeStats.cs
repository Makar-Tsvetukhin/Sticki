using System.Collections.Generic;
using Sticki.Combat.Config;
using Sticki.Player;
using UnityEngine;

namespace Sticki.Upgrades
{
    public sealed class WeaponRuntimeStats
    {
        private readonly WeaponConfig baseConfig;
        private readonly List<WeaponStatModifier> modifiers = new();

        public WeaponRuntimeStats(WeaponConfig baseConfig)
        {
            this.baseConfig = baseConfig;
            Recalculate();
        }

        public float Damage { get; private set; }
        public float FireIntervalSeconds { get; private set; }
        public float Range { get; private set; }
        public int PelletsPerShot { get; private set; }
        public float SpreadAngle { get; private set; }
        public int MagazineSize { get; private set; }
        public float ReloadSeconds { get; private set; }
        public WeaponReloadType ReloadType { get; private set; }
        public float ReloadStartSeconds { get; private set; }
        public float ReloadInsertSeconds { get; private set; }
        public float ReloadEndSeconds { get; private set; }
        public float ReloadShellAppearStartDelay { get; private set; }
        public float ReloadShellAppearInsertDelay { get; private set; }

        public void AddModifier(WeaponStatModifier modifier)
        {
            modifiers.Add(modifier);
            Recalculate();
        }

        private void Recalculate()
        {
            if (baseConfig == null)
            {
                Damage = 0f;
                FireIntervalSeconds = 0.1f;
                Range = 0f;
                PelletsPerShot = 1;
                SpreadAngle = 0f;
                MagazineSize = 1;
                ReloadSeconds = 0.1f;
                ReloadType = WeaponReloadType.Magazine;
                ReloadStartSeconds = 0.1f;
                ReloadInsertSeconds = 0.1f;
                ReloadEndSeconds = 0.1f;
                ReloadShellAppearStartDelay = 0f;
                ReloadShellAppearInsertDelay = 0f;
                return;
            }

            Damage = EvaluateFloat(WeaponStatId.Damage, baseConfig.damage, 0.01f);
            FireIntervalSeconds = EvaluateFloat(WeaponStatId.FireIntervalSeconds, baseConfig.fireIntervalSeconds, 0.01f);
            Range = EvaluateFloat(WeaponStatId.Range, baseConfig.range, 0.1f);
            PelletsPerShot = baseConfig.pelletsPerShot;
            SpreadAngle = baseConfig.spreadAngle;
            MagazineSize = EvaluateInt(WeaponStatId.MagazineSize, baseConfig.magazineSize, 1);
            ReloadSeconds = EvaluateFloat(WeaponStatId.ReloadSeconds, baseConfig.reloadSeconds, 0.01f);
            ReloadType = baseConfig.reloadType;
            ReloadStartSeconds = Mathf.Max(0f, baseConfig.reloadStartSeconds);
            ReloadInsertSeconds = Mathf.Max(0.01f, baseConfig.reloadInsertSeconds);
            ReloadEndSeconds = Mathf.Max(0f, baseConfig.reloadEndSeconds);
            ReloadShellAppearStartDelay = Mathf.Max(0f, baseConfig.reloadShellAppearStartDelay);
            ReloadShellAppearInsertDelay = Mathf.Max(0f, baseConfig.reloadShellAppearInsertDelay);
        }

        private float EvaluateFloat(WeaponStatId statId, float baseValue, float minValue)
        {
            float result = baseValue;
            for (int i = 0; i < modifiers.Count; i++)
            {
                WeaponStatModifier modifier = modifiers[i];
                if (modifier.StatId != statId)
                {
                    continue;
                }

                if (modifier.Operation == ModifierOperation.Add)
                {
                    result += modifier.Value;
                }
                else
                {
                    result *= modifier.Value;
                }
            }

            return Mathf.Max(minValue, result);
        }

        private int EvaluateInt(WeaponStatId statId, int baseValue, int minValue)
        {
            float result = baseValue;
            for (int i = 0; i < modifiers.Count; i++)
            {
                WeaponStatModifier modifier = modifiers[i];
                if (modifier.StatId != statId)
                {
                    continue;
                }

                if (modifier.Operation == ModifierOperation.Add)
                {
                    result += modifier.Value;
                }
                else
                {
                    result *= modifier.Value;
                }
            }

            return Mathf.Max(minValue, Mathf.RoundToInt(result));
        }
    }
}
