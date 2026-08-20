using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sticki.Core
{
    [Serializable]
    public class RunRecordEntry
    {
        public string finishedAtUtc;
        public int roomsCompleted;
        public int totalKills;
        public float durationSeconds;
        public string endReason;
        public string endSceneName;
    }

    [Serializable]
    internal class RunRecordCollection
    {
        public List<RunRecordEntry> records = new();
    }

    public static class RunRecordsStorage
    {
        private const string PlayerPrefsKey = "sticki.run.records";
        private const int MaxStoredRecords = 50;

        public static IReadOnlyList<RunRecordEntry> LoadRecords()
        {
            return LoadCollection().records;
        }

        public static void SaveRecord(RunRecordEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            RunRecordCollection collection = LoadCollection();
            collection.records.Add(entry);
            collection.records.Sort(CompareRecords);

            if (collection.records.Count > MaxStoredRecords)
            {
                collection.records.RemoveRange(MaxStoredRecords, collection.records.Count - MaxStoredRecords);
            }

            string json = JsonUtility.ToJson(collection);
            PlayerPrefs.SetString(PlayerPrefsKey, json);
            PlayerPrefs.Save();
        }

        private static RunRecordCollection LoadCollection()
        {
            string json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new RunRecordCollection();
            }

            RunRecordCollection collection = JsonUtility.FromJson<RunRecordCollection>(json);
            return collection ?? new RunRecordCollection();
        }

        private static int CompareRecords(RunRecordEntry left, RunRecordEntry right)
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            int roomsCompare = right.roomsCompleted.CompareTo(left.roomsCompleted);
            if (roomsCompare != 0)
            {
                return roomsCompare;
            }

            int killsCompare = right.totalKills.CompareTo(left.totalKills);
            if (killsCompare != 0)
            {
                return killsCompare;
            }

            int durationCompare = right.durationSeconds.CompareTo(left.durationSeconds);
            if (durationCompare != 0)
            {
                return durationCompare;
            }

            return string.CompareOrdinal(right.finishedAtUtc, left.finishedAtUtc);
        }
    }
}
