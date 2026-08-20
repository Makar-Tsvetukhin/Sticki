using System;
using UnityEngine;

namespace Sticki.Core
{
    public class RunSessionController : MonoBehaviour
    {
        private static RunSessionController instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureInstanceOnStartup()
        {
            _ = Instance;
        }

        public static RunSessionController Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("RunSessionController");
                    instance = go.AddComponent<RunSessionController>();
                    DontDestroyOnLoad(go);
                }

                return instance;
            }
        }

        [SerializeField] private bool runActive;
        [SerializeField] private int currentRoomNumber;
        [SerializeField] private int totalKills;
        [SerializeField] private float runStartRealtime;
        [SerializeField] private float finalDurationSeconds;
        [SerializeField] private bool resultSaved;

        public bool IsRunActive => runActive;
        public int CurrentRoomNumber => Mathf.Max(0, currentRoomNumber);
        public int TotalKills => Mathf.Max(0, totalKills);
        public float ElapsedSeconds => runActive
            ? Mathf.Max(0f, Time.realtimeSinceStartup - runStartRealtime)
            : Mathf.Max(0f, finalDurationSeconds);

        public event Action OnRunStatsChanged;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void BeginRun()
        {
            runActive = true;
            currentRoomNumber = 0;
            totalKills = 0;
            runStartRealtime = Time.realtimeSinceStartup;
            finalDurationSeconds = 0f;
            resultSaved = false;
            NotifyChanged();
        }

        public void AbandonRun()
        {
            if (!runActive)
            {
                return;
            }

            finalDurationSeconds = ElapsedSeconds;
            runActive = false;
            NotifyChanged();
        }

        public void SetCurrentRoomNumber(int roomNumber)
        {
            int safeRoom = Mathf.Max(0, roomNumber);
            if (currentRoomNumber == safeRoom)
            {
                return;
            }

            currentRoomNumber = safeRoom;
            NotifyChanged();
        }

        public void RegisterKill(int killCount = 1)
        {
            if (!runActive || killCount <= 0)
            {
                return;
            }

            totalKills += killCount;
            NotifyChanged();
        }

        public RunRecordEntry FinalizeAndSaveRun(string endReason, string endSceneName)
        {
            if (!runActive && resultSaved)
            {
                return null;
            }

            finalDurationSeconds = ElapsedSeconds;
            runActive = false;

            if (resultSaved)
            {
                NotifyChanged();
                return null;
            }

            RunRecordEntry entry = new RunRecordEntry
            {
                finishedAtUtc = DateTime.UtcNow.ToString("o"),
                roomsCompleted = Mathf.Max(0, currentRoomNumber),
                totalKills = Mathf.Max(0, totalKills),
                durationSeconds = Mathf.Max(0f, finalDurationSeconds),
                endReason = endReason ?? string.Empty,
                endSceneName = endSceneName ?? string.Empty
            };

            RunRecordsStorage.SaveRecord(entry);
            resultSaved = true;
            NotifyChanged();
            return entry;
        }

        private void NotifyChanged()
        {
            OnRunStatsChanged?.Invoke();
        }
    }
}
