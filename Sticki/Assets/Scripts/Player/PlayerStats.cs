using System;
using System.Collections.Generic;
using Sticki.Player.Config;
using UnityEngine;

namespace Sticki.Player
{
    public enum PlayerStatId
    {
        MoveSpeed,
        SprintMultiplier,
        JumpHeight,
        Gravity,
        LookSensitivity,
        MaxPitchAngle,
        MaxHealth,
        DamageMultiplier,
        FireRateMultiplier,
        DamageTakenMultiplier,
        HealthRegenPerSecond
    }

    public enum ModifierOperation
    {
        Add,
        Multiply
    }

    public readonly struct StatModifier
    {
        public readonly PlayerStatId StatId;
        public readonly ModifierOperation Operation;
        public readonly float Value;
        public readonly string SourceId;

        public StatModifier(PlayerStatId statId, ModifierOperation operation, float value, string sourceId)
        {
            StatId = statId;
            Operation = operation;
            Value = value;
            SourceId = sourceId;
        }
    }

    public class PlayerStats : MonoBehaviour
    {
        [SerializeField] private PlayerBaseConfig baseConfig;

        public event Action OnStatsChanged;

        public float MoveSpeed { get; private set; }
        public float SprintMultiplier { get; private set; }
        public float JumpHeight { get; private set; }
        public float Gravity { get; private set; }
        public float LookSensitivity { get; private set; }
        public float MaxPitchAngle { get; private set; }
        public float MaxHealth { get; private set; }
        public float DamageMultiplier { get; private set; } = 1f;
        public float FireRateMultiplier { get; private set; } = 1f;
        public float DamageTakenMultiplier { get; private set; } = 1f;
        public float HealthRegenPerSecond { get; private set; }

        public bool IsReady => baseConfig != null;

        private readonly List<StatModifier> modifiers = new();

        private void Awake()
        {
            Recalculate();
        }

        public void AddModifier(StatModifier modifier)
        {
            modifiers.Add(modifier);
            Recalculate();
        }

        public void RemoveModifiersBySource(string sourceId)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                return;
            }

            for (int i = modifiers.Count - 1; i >= 0; i--)
            {
                if (modifiers[i].SourceId == sourceId)
                {
                    modifiers.RemoveAt(i);
                }
            }

            Recalculate();
        }

        public void Recalculate()
        {
            if (baseConfig == null)
            {
                Debug.LogError("PlayerStats requires PlayerBaseConfig reference.", this);
                return;
            }

            MoveSpeed = Evaluate(PlayerStatId.MoveSpeed, baseConfig.moveSpeed);
            SprintMultiplier = Evaluate(PlayerStatId.SprintMultiplier, baseConfig.sprintMultiplier);
            JumpHeight = Evaluate(PlayerStatId.JumpHeight, baseConfig.jumpHeight);
            Gravity = Evaluate(PlayerStatId.Gravity, baseConfig.gravity);
            LookSensitivity = Evaluate(PlayerStatId.LookSensitivity, baseConfig.lookSensitivity);
            MaxPitchAngle = Evaluate(PlayerStatId.MaxPitchAngle, baseConfig.maxPitchAngle);
            MaxHealth = Evaluate(PlayerStatId.MaxHealth, baseConfig.maxHealth);
            DamageMultiplier = Evaluate(PlayerStatId.DamageMultiplier, 1f);
            FireRateMultiplier = Evaluate(PlayerStatId.FireRateMultiplier, 1f);
            DamageTakenMultiplier = Evaluate(PlayerStatId.DamageTakenMultiplier, 1f);
            HealthRegenPerSecond = Evaluate(PlayerStatId.HealthRegenPerSecond, 0f);

            OnStatsChanged?.Invoke();
        }

        private float Evaluate(PlayerStatId statId, float baseValue)
        {
            float result = baseValue;

            for (int i = 0; i < modifiers.Count; i++)
            {
                StatModifier modifier = modifiers[i];
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

            return result;
        }
    }
}
