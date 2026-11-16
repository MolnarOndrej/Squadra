using System.Collections.Generic;

namespace Squadra.Missions
{
    /// <summary>Everything the validator found in one mission.</summary>
    public sealed class MissionValidation
    {
        public MissionValidation(IReadOnlyList<MissionIssue> issues)
        {
            Issues = issues;
            foreach (MissionIssue issue in issues)
            {
                if (issue.IsError) ErrorCount++;
                else WarningCount++;
            }
        }

        /// <summary>Errors first, then warnings, each in document order.</summary>
        public IReadOnlyList<MissionIssue> Issues { get; }
        public int ErrorCount { get; }
        public int WarningCount { get; }
        public bool HasErrors => ErrorCount > 0;

        public bool Has(IssueCode code)
        {
            foreach (MissionIssue issue in Issues)
                if (issue.Code == code) return true;
            return false;
        }
    }
}
