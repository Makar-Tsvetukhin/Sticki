using System.Collections.Generic;
using Sticki.AI;
using Sticki.Player;
using Sticki.Spawning;
using Sticki.Spawning.Config;
using Sticki.UI;
using Sticki.Upgrades;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sticki.Core
{
    public class RunFlowController : MonoBehaviour
    {
        private static RunFlowController instance;
        private const string RuntimeConfigProviderResourcePath = "Configs/RuntimeConfigProvider";
        private static RuntimeConfigProvider cachedRuntimeConfigProvider;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureInstanceOnStartup()
        {
            _ = Instance;
        }

        public static RunFlowController Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("RunFlowController");
                    instance = go.AddComponent<RunFlowController>();
                    DontDestroyOnLoad(go);
                }

                return instance;
            }
        }

        [Header("Run State")]
        [SerializeField] private int currentRoomNumber;
        [SerializeField] private string nextArenaSceneName = "Ar1";

        [Header("Pool")]
        [SerializeField] private List<string> availableArenaPool = new() { "Ar2", "Ar3", "Ar4" };
        [SerializeField] private List<string> usedArenasBeforeRepeat = new();
        [SerializeField] private int lastPreparedAfterRoomNumber = -1;
        [SerializeField] private string lastLoadedSceneName;

        public int CurrentRoomNumber => currentRoomNumber;
        public string NextArenaSceneName => nextArenaSceneName;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            bool isCombatArena = IsCombatArenaScene(scene.name);
            bool wasCombatArena = IsCombatArenaScene(lastLoadedSceneName);
            bool isReloadOfSameArena = wasCombatArena && string.Equals(lastLoadedSceneName, scene.name, System.StringComparison.Ordinal);

            if (isCombatArena)
            {
                if (!RunSessionController.Instance.IsRunActive)
                {
                    RunSessionController.Instance.BeginRun();
                }

                if (!isReloadOfSameArena)
                {
                    currentRoomNumber++;
                }

                EnemyMeleeAI.GlobalCombatActive = false;
                EnsureArenaSpawnerExists();
                RunSessionController.Instance.SetCurrentRoomNumber(currentRoomNumber);
            }

            if (scene.name == "UpgradeRoom")
            {
                PrepareNextRandomArena();
            }

            UpdateUI();
            GameSettingsService.ApplyAll();
            lastLoadedSceneName = scene.name;
        }

        public void StartRun()
        {
            currentRoomNumber = 0;
            usedArenasBeforeRepeat.Clear();
            availableArenaPool = new List<string> { "Ar2", "Ar3", "Ar4" };
            nextArenaSceneName = "Ar1";
            lastPreparedAfterRoomNumber = -1;
            lastLoadedSceneName = null;
            UpgradeRuntimeController upgradeRuntimeController = FindFirstObjectByType<UpgradeRuntimeController>();
            upgradeRuntimeController?.ResetRunState();
            RunSessionController.Instance.BeginRun();

            SceneManager.LoadScene("Ar1");
        }

        public void PrepareNextRandomArena()
        {
            if (lastPreparedAfterRoomNumber == currentRoomNumber)
            {
                return;
            }

            if (availableArenaPool.Count == 0)
            {
                availableArenaPool.AddRange(usedArenasBeforeRepeat);
                usedArenasBeforeRepeat.Clear();
            }

            int randomIndex = Random.Range(0, availableArenaPool.Count);
            nextArenaSceneName = availableArenaPool[randomIndex];

            availableArenaPool.RemoveAt(randomIndex);
            usedArenasBeforeRepeat.Add(nextArenaSceneName);
            lastPreparedAfterRoomNumber = currentRoomNumber;
        }

        public void LoadNextArena()
        {
            if (string.IsNullOrEmpty(nextArenaSceneName))
            {
                PrepareNextRandomArena();
            }

            SceneManager.LoadScene(nextArenaSceneName);
        }

        public void UpdateUI()
        {
            RunTabOverlayController runTab = FindFirstObjectByType<RunTabOverlayController>();
            if (runTab != null)
            {
                runTab.SetRoomInfo(currentRoomNumber, string.Empty);
            }
        }

        private static void EnsureArenaSpawnerExists()
        {
            ArenaSpawner spawner = FindFirstObjectByType<ArenaSpawner>();
            Transform playerTransform = ResolvePlayerTransform();
            Transform initialEnemiesRoot = GameObject.Find("EnemyParent")?.transform;
            Transform spawnPointsRoot = GameObject.Find("SpawnPoints")?.transform;
            RuntimeConfigProvider runtimeConfigProvider = LoadRuntimeConfigProvider();
            ArenaSpawnConfig config = runtimeConfigProvider != null ? runtimeConfigProvider.DefaultArenaSpawnConfig : null;

            if (spawner != null)
            {
                spawner.Configure(null, initialEnemiesRoot, playerTransform, spawnPointsRoot);
                return;
            }

            if (config == null)
            {
                Debug.LogError($"RunFlowController: failed to resolve default arena config via Resources/{RuntimeConfigProviderResourcePath}.", instance);
                return;
            }

            GameObject go = new GameObject("ArenaSpawnerSystem");
            go.SetActive(false);
            spawner = go.AddComponent<ArenaSpawner>();
            spawner.Configure(config, initialEnemiesRoot, playerTransform, spawnPointsRoot);
            go.SetActive(true);
        }

        private static Transform ResolvePlayerTransform()
        {
            if (PlayerHealth.Instance != null)
            {
                return PlayerHealth.Instance.transform;
            }

            GameObject player = GameObject.Find("Player");
            return player != null ? player.transform : null;
        }

        public static RuntimeConfigProvider LoadRuntimeConfigProvider()
        {
            if (cachedRuntimeConfigProvider == null)
            {
                cachedRuntimeConfigProvider = Resources.Load<RuntimeConfigProvider>(RuntimeConfigProviderResourcePath);
            }

            return cachedRuntimeConfigProvider;
        }

        private static bool IsCombatArenaScene(string sceneName)
        {
            return !string.IsNullOrWhiteSpace(sceneName)
                   && sceneName.StartsWith("Ar")
                   && sceneName != "Ar1_old"
                   && sceneName != "Ar2_old";
        }
    }
}
