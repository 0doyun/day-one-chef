// Growable list of EventLogEntry, plus helpers for Day 11 evaluator
// serialisation. Intentionally a plain POCO — no Unity dependency —
// so tests + Flutter-side JSON work without scene plumbing.

using System.Collections.Generic;
using UnityEngine;
using DayOneChef.Gameplay.Data;

namespace DayOneChef.Gameplay
{
    public class EventLog
    {
        private readonly List<EventLogEntry> _entries = new();

        public IReadOnlyList<EventLogEntry> Entries => _entries;
        public int Count => _entries.Count;

        public void Append(EventLogEntry entry)
        {
            if (entry == null) return;
            _entries.Add(entry);
        }

        public void Clear() => _entries.Clear();

        /// <summary>
        /// Serialise to the shape Day 11's evaluator prompt expects:
        /// a single JSON object with `entries: [...]`.
        /// </summary>
        public string ToJson()
        {
            var wrapper = new EventLogWrapper { entries = _entries.ToArray() };
            return JsonUtility.ToJson(wrapper);
        }

        [System.Serializable]
        private class EventLogWrapper
        {
            public EventLogEntry[] entries;
        }
    }
}
