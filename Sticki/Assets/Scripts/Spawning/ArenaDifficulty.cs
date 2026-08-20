using Sticki.Core;

namespace Sticki.Spawning
{
    public static class ArenaDifficulty
    {
        private static bool missingConfigLogged;

        public static ArenaDifficultySnapshot Evaluate(int roomNumber)
        {
            RuntimeConfigProvider runtimeConfigProvider = RunFlowController.LoadRuntimeConfigProvider();
            ArenaDifficultyConfig config = runtimeConfigProvider != null ? runtimeConfigProvider.ArenaDifficultyConfig : null;
            if (config == null)
            {
                if (!missingConfigLogged)
                {
                    UnityEngine.Debug.LogWarning("ArenaDifficulty: ArenaDifficultyConfig was not found. Falling back to neutral difficulty multipliers.");
                    missingConfigLogged = true;
                }

                return new ArenaDifficultySnapshot(roomNumber, System.Math.Max(0, roomNumber - 1), 1f, 1f, 1f, 1f, 1f, 1f, 1f);
            }

            missingConfigLogged = false;
            return config.Evaluate(roomNumber);
        }
    }
}
