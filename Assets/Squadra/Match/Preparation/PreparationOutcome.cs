using System.Collections.Generic;

namespace Squadra.Preparation
{
    /// <summary>
    /// What preparing one mission produced: the mission, or why it cannot be played. Warnings are the
    /// mission validator's, kept for diagnostics; they never stop a mission.
    /// </summary>
    public sealed class PreparationOutcome
    {
        internal PreparationOutcome(PreparedMission mission, IReadOnlyList<string> problems,
            IReadOnlyList<string> warnings)
        {
            Mission = mission;
            Problems = problems;
            Warnings = warnings;
        }

        /// <summary>The prepared mission, or null when it has any problem.</summary>
        public PreparedMission Mission { get; }

        /// <summary>Why the mission cannot be played, each naming the field or object it is about.</summary>
        public IReadOnlyList<string> Problems { get; }

        public IReadOnlyList<string> Warnings { get; }

        public bool Ok => Mission != null;

        /// <summary>Every problem, one per line.</summary>
        public string Explanation => string.Join("\n", Problems);
    }
}
