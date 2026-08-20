using Sticki.Spawning;
using Sticki.Spawning.Config;
using UnityEngine;

namespace Sticki.Core
{
    [CreateAssetMenu(menuName = "Sticki/Core/Runtime Config Provider", fileName = "RuntimeConfigProvider")]
    public class RuntimeConfigProvider : ScriptableObject
    {
        [Header("Spawning")]
        [SerializeField] private ArenaSpawnConfig defaultArenaSpawnConfig;
        [SerializeField] private ArenaDifficultyConfig arenaDifficultyConfig;

        public ArenaSpawnConfig DefaultArenaSpawnConfig => defaultArenaSpawnConfig;
        public ArenaDifficultyConfig ArenaDifficultyConfig => arenaDifficultyConfig;
    }
}
