using System;
using System.Collections.Generic;
using System.Text;

namespace Squadra.MissionEditing
{
    /// <summary>
    /// The fields that hold text the mission could not take, each until its author corrects it or
    /// gives it up. It only keeps the list; when the author must be asked about it is the session's
    /// decision.
    /// </summary>
    sealed class InvalidInputs
    {
        sealed class Entry
        {
            public string Label;
            public bool OfSelection;
        }

        readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

        public bool Any => entries.Count > 0;

        /// <param name="ofSelection">True for a field of the selected object.</param>
        public void Report(string field, string label, bool ofSelection)
        {
            entries[field] = new Entry { Label = label, OfSelection = ofSelection };
        }

        public void Clear(string field)
        {
            entries.Remove(field);
        }

        public void ClearAll()
        {
            entries.Clear();
        }

        public bool Contains(string field)
        {
            return entries.ContainsKey(field);
        }

        /// <summary>The labels of the fields, for a question to the author; empty when there are none.</summary>
        public string Labels(bool selectionOnly)
        {
            var text = new StringBuilder();
            foreach (Entry entry in entries.Values)
            {
                if (selectionOnly && !entry.OfSelection) continue;
                if (text.Length > 0) text.Append(", ");
                text.Append(entry.Label);
            }

            return text.ToString();
        }

        /// <summary>Forgets the fields, or only those of the selected object. False when there were none.</summary>
        public bool Discard(bool selectionOnly)
        {
            var doomed = new List<string>();
            foreach (KeyValuePair<string, Entry> entry in entries)
                if (!selectionOnly || entry.Value.OfSelection) doomed.Add(entry.Key);
            if (doomed.Count == 0) return false;

            foreach (string field in doomed) entries.Remove(field);
            return true;
        }
    }
}
