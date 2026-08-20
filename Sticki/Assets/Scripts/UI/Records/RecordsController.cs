using System;
using System.Collections.Generic;
using System.Globalization;
using Sticki.Core;
using Sticki.UI.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sticki.UI
{
    public class RecordsController : UIScreenController
    {
        private ScrollView recordsList;
        private readonly List<RecordView> records = new();

        private sealed class RecordView
        {
            public string Date;
            public string Rooms;
            public string Time;
            public string Kills;
        }

        protected override void OnInitialize()
        {
            recordsList = root.Q<ScrollView>("records-list");
            if (recordsList != null)
            {
                recordsList.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                recordsList.verticalScrollerVisibility = ScrollerVisibility.Auto;
            }

            Hide();
        }

        protected override void OnShow()
        {
            LoadRecords();
            RebuildList();
        }

        private void RebuildList()
        {
            if (recordsList == null)
            {
                return;
            }

            recordsList.Clear();

            if (records.Count == 0)
            {
                Label empty = new Label("No run records yet.");
                empty.AddToClassList("records-empty");
                recordsList.Add(empty);
                return;
            }

            for (int i = 0; i < records.Count; i++)
            {
                RecordView record = records[i];
                VisualElement row = new VisualElement();
                row.AddToClassList("records-row");
                row.Add(CreateCell((i + 1).ToString(), "records-col--index", false));
                row.Add(CreateCell(record.Date, "records-col--date", true));
                row.Add(CreateCell(record.Rooms, "records-col--rooms", false));
                row.Add(CreateCell(record.Time, "records-col--time", false));
                row.Add(CreateCell(record.Kills, "records-col--kills", false));
                recordsList.Add(row);
            }
        }

        private void LoadRecords()
        {
            records.Clear();

            IReadOnlyList<RunRecordEntry> storedRecords = RunRecordsStorage.LoadRecords();
            for (int i = 0; i < storedRecords.Count; i++)
            {
                RunRecordEntry record = storedRecords[i];
                if (record == null)
                {
                    continue;
                }

                records.Add(new RecordView
                {
                    Date = FormatDate(record.finishedAtUtc),
                    Rooms = Mathf.Max(0, record.roomsCompleted).ToString(),
                    Time = FormatTime(record.durationSeconds),
                    Kills = Mathf.Max(0, record.totalKills).ToString()
                });
            }
        }

        private static Label CreateCell(string text, string widthClass, bool muted)
        {
            Label label = new Label(text);
            label.AddToClassList("records-cell");
            label.AddToClassList(widthClass);
            if (muted)
            {
                label.AddToClassList("records-cell--muted");
            }

            return label;
        }

        private static string FormatDate(string utcTimestamp)
        {
            if (DateTime.TryParse(utcTimestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsed))
            {
                return parsed.ToLocalTime().ToString("dd.MM.yyyy");
            }

            return "--.--.----";
        }

        private static string FormatTime(float seconds)
        {
            TimeSpan span = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
            return $"{(int)span.TotalMinutes:00}:{span.Seconds:00}";
        }
    }
}
