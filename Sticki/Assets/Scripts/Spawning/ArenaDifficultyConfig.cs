using UnityEngine;

namespace Sticki.Spawning
{
    public readonly struct ArenaDifficultySnapshot
    {
        public ArenaDifficultySnapshot(
            int roomNumber,
            int difficultyTier,
            float enemyHealthMultiplier,
            float meleeDamageMultiplier,
            float dynamicSpawnMultiplier,
            float aliveCapMultiplier,
            float waveSizeMultiplier,
            float tickIntervalMultiplier,
            float interWaveDelayMultiplier)
        {
            RoomNumber = roomNumber;
            DifficultyTier = difficultyTier;
            EnemyHealthMultiplier = enemyHealthMultiplier;
            MeleeDamageMultiplier = meleeDamageMultiplier;
            DynamicSpawnMultiplier = dynamicSpawnMultiplier;
            AliveCapMultiplier = aliveCapMultiplier;
            WaveSizeMultiplier = waveSizeMultiplier;
            TickIntervalMultiplier = tickIntervalMultiplier;
            InterWaveDelayMultiplier = interWaveDelayMultiplier;
        }

        public int RoomNumber { get; }
        public int DifficultyTier { get; }
        public float EnemyHealthMultiplier { get; }
        public float MeleeDamageMultiplier { get; }
        public float DynamicSpawnMultiplier { get; }
        public float AliveCapMultiplier { get; }
        public float WaveSizeMultiplier { get; }
        public float TickIntervalMultiplier { get; }
        public float InterWaveDelayMultiplier { get; }
    }

    [CreateAssetMenu(menuName = "Sticki/Spawning/Arena Difficulty Config", fileName = "ArenaDifficultyConfig")]
    public class ArenaDifficultyConfig : ScriptableObject
    {
        [Header("Room Mapping")]
        [SerializeField] [Min(1)] private int firstCombatRoom = 1;

        [Header("Multipliers")]
        [SerializeField] private AnimationCurve enemyHealthMultiplierByTier = DefaultIncreasingCurve(1f, 0.18f, 12f);
        [SerializeField] private float enemyHealthMultiplierCap = 3.2f;

        [SerializeField] private AnimationCurve meleeDamageMultiplierByTier = DefaultIncreasingCurve(1f, 0.10f, 12f);
        [SerializeField] private float meleeDamageMultiplierCap = 2.25f;

        [SerializeField] private AnimationCurve dynamicSpawnMultiplierByTier = DefaultIncreasingCurve(1f, 0.15f, 12f);
        [SerializeField] private float dynamicSpawnMultiplierCap = 2.8f;

        [SerializeField] private AnimationCurve aliveCapMultiplierByTier = DefaultIncreasingCurve(1f, 0.10f, 12f);
        [SerializeField] private float aliveCapMultiplierCap = 2.25f;

        [SerializeField] private AnimationCurve waveSizeMultiplierByTier = DefaultIncreasingCurve(1f, 0.12f, 12f);
        [SerializeField] private float waveSizeMultiplierCap = 2.5f;

        [Header("Interval Multipliers")]
        [SerializeField] private AnimationCurve tickIntervalMultiplierByTier = DefaultDecreasingCurve(1f, 0.05f, 12f);
        [SerializeField] [Min(0.05f)] private float tickIntervalMultiplierMin = 0.45f;

        [SerializeField] private AnimationCurve interWaveDelayMultiplierByTier = DefaultDecreasingCurve(1f, 0.12f, 12f);
        [SerializeField] [Min(0.01f)] private float interWaveDelayMultiplierMin = 0.20f;

        public ArenaDifficultySnapshot Evaluate(int roomNumber)
        {
            int safeRoom = Mathf.Max(firstCombatRoom, roomNumber);
            int tier = Mathf.Max(0, safeRoom - firstCombatRoom);

            return new ArenaDifficultySnapshot(
                safeRoom,
                tier,
                EvaluateIncreasing(enemyHealthMultiplierByTier, enemyHealthMultiplierCap, tier),
                EvaluateIncreasing(meleeDamageMultiplierByTier, meleeDamageMultiplierCap, tier),
                EvaluateIncreasing(dynamicSpawnMultiplierByTier, dynamicSpawnMultiplierCap, tier),
                EvaluateIncreasing(aliveCapMultiplierByTier, aliveCapMultiplierCap, tier),
                EvaluateIncreasing(waveSizeMultiplierByTier, waveSizeMultiplierCap, tier),
                EvaluateDecreasing(tickIntervalMultiplierByTier, tickIntervalMultiplierMin, tier),
                EvaluateDecreasing(interWaveDelayMultiplierByTier, interWaveDelayMultiplierMin, tier));
        }

        private static float EvaluateIncreasing(AnimationCurve curve, float cap, int tier)
        {
            float value = curve != null ? curve.Evaluate(tier) : 1f;
            return Mathf.Clamp(value, 0.05f, Mathf.Max(0.05f, cap));
        }

        private static float EvaluateDecreasing(AnimationCurve curve, float min, int tier)
        {
            float value = curve != null ? curve.Evaluate(tier) : 1f;
            return Mathf.Max(min, value);
        }

        private static AnimationCurve DefaultIncreasingCurve(float start, float step, float maxTier)
        {
            return new AnimationCurve(
                new Keyframe(0f, start),
                new Keyframe(maxTier, start + step * maxTier));
        }

        private static AnimationCurve DefaultDecreasingCurve(float start, float step, float maxTier)
        {
            return new AnimationCurve(
                new Keyframe(0f, start),
                new Keyframe(maxTier, Mathf.Max(0.05f, start - step * maxTier)));
        }
    }
}
