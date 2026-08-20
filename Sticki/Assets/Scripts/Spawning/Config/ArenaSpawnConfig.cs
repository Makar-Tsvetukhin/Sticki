using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sticki.Spawning.Config
{
    [CreateAssetMenu(menuName = "Sticki/Spawning/Arena Spawn Config", fileName = "ArenaSpawnConfig")]
    public class ArenaSpawnConfig : ScriptableObject
    {
        [Header("Arena Totals")]
        [Min(0)] public int dynamicSpawnTotal = 50;
        [Min(1)] public int hardAliveCap = 80;
        [Min(0f)] public float startDelayAfterCombat = 0.2f;

        [Header("Spawn Rules")]
        [Min(0f)] public float minDistanceFromPlayer = 8f;
        [Min(0f)] public float fallbackMinDistance = 4f;
        [Min(0.05f)] public float pointBlockedRadius = 0.45f;
        public LayerMask spawnBlockedBy = ~0;
        public LayerMask lineOfSightMask = ~0;
        public bool preferHiddenSpawnPoints = true;

        [Header("Enemy Types")]
        public List<EnemyTypeConfig> enemyTypes = new();

        [Header("Waves")]
        public List<WaveConfig> waves = new();
    }

    [Serializable]
    public class EnemyTypeConfig
    {
        public string id = "white_melee";
        public GameObject prefab;
        [Min(0.01f)] public float weight = 1f;
        [Min(0)] public int prewarmCount = 16;
        [Min(1)] public int maxPoolSize = 96;
    }

    [Serializable]
    public class WaveConfig
    {
        [Min(1)] public int enemiesInWave = 8;
        [Min(1)] public int minPerTick = 1;
        [Min(1)] public int maxPerTick = 2;
        [Min(0.05f)] public float tickInterval = 0.4f;
        [Min(0f)] public float interWaveDelay = 2f;
        [Range(0f, 1f)] public float earlyNextWaveKillRatio = 0.5f;
    }
}
