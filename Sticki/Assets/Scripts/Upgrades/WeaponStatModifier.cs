using Sticki.Player;

namespace Sticki.Upgrades
{
    public readonly struct WeaponStatModifier
    {
        public WeaponStatModifier(WeaponStatId statId, ModifierOperation operation, float value, string sourceId)
        {
            StatId = statId;
            Operation = operation;
            Value = value;
            SourceId = sourceId;
        }

        public WeaponStatId StatId { get; }
        public ModifierOperation Operation { get; }
        public float Value { get; }
        public string SourceId { get; }
    }
}
