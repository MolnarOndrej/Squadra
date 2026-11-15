using System.Collections.Generic;

namespace Squadra.Missions
{
    /// <summary>What reading a mission file produced: the mission, or the reasons there is none.</summary>
    public sealed class MissionReadResult
    {
        public MissionReadResult(MissionDefinition mission, IReadOnlyList<MissionIssue> issues)
        {
            Mission = mission;
            Issues = issues;
        }

        /// <summary>The mission, or null when the file has any error.</summary>
        public MissionDefinition Mission { get; }

        /// <summary>Errors that rejected the file, or the warnings of a mission that was read.</summary>
        public IReadOnlyList<MissionIssue> Issues { get; }

        public bool Ok => Mission != null;
    }
}
