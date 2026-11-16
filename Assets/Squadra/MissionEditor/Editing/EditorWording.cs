using System.Collections.Generic;
using System.Text;
using Squadra.Missions;

namespace Squadra.MissionEditing
{
    /// <summary>Small pieces of wording the editor's status lines and dialogs share.</summary>
    public static class EditorWording
    {
        /// <summary>A count with its noun, in the plural unless there is exactly one: "1 route", "3 routes".</summary>
        public static string Count(int count, string noun)
        {
            return count + " " + noun + (count == 1 ? "" : "s");
        }

        /// <summary>The issues as a list for a dialog, one to a line, cut short after the first few.</summary>
        internal static string List(IReadOnlyList<MissionIssue> issues)
        {
            const int shown = 8;
            var text = new StringBuilder();
            for (int i = 0; i < issues.Count && i < shown; i++)
            {
                if (i > 0) text.Append('\n');
                text.Append("- ").Append(issues[i]);
            }

            if (issues.Count > shown) text.Append("\n- and ").Append(issues.Count - shown).Append(" more");
            return text.ToString();
        }
    }
}
