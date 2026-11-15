namespace Squadra.Campaign
{
    /// <summary>
    /// What the results screen offers after one attempt at a mission: its heading, what becomes of the
    /// Next Mission action and which action starts with the keyboard focus.
    /// </summary>
    public readonly struct ResultsPlan
    {
        public const string ComingSoonReason = "Next mission coming soon";
        public const string WinRequiredReason = "Win this mission to continue";

        public readonly ResultsHeading Heading;
        public readonly NextMissionState NextMission;

        /// <summary>Catalog index of the mission right after this one, or -1 when there is none.</summary>
        public readonly int NextMissionIndex;

        public readonly ResultsAction InitialFocus;

        public ResultsPlan(ResultsHeading heading, NextMissionState nextMission, int nextMissionIndex,
            ResultsAction initialFocus)
        {
            Heading = heading;
            NextMission = nextMission;
            NextMissionIndex = nextMissionIndex;
            InitialFocus = initialFocus;
        }

        public bool ShowsNextMission => NextMission != NextMissionState.Hidden;

        public bool CanStartNextMission => NextMission == NextMissionState.Enabled;

        /// <summary>The celebration and the thank-you message belong to a completed campaign only.</summary>
        public bool Celebrates => Heading == ResultsHeading.CampaignComplete;

        /// <summary>Why Next Mission is shown disabled, for its tooltip. Null when it is enabled or hidden.</summary>
        public string NextMissionReason
        {
            get
            {
                switch (NextMission)
                {
                    case NextMissionState.ComingSoon: return ComingSoonReason;
                    case NextMissionState.WinRequired: return WinRequiredReason;
                    default: return null;
                }
            }
        }
    }
}
