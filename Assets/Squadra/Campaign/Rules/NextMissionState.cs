namespace Squadra.Campaign
{
    /// <summary>What the results screen does with its Next Mission action.</summary>
    public enum NextMissionState
    {
        /// <summary>No mission follows this one in the catalog: the action is not shown at all.</summary>
        Hidden,

        /// <summary>The following mission can be launched.</summary>
        Enabled,

        /// <summary>This mission was won, but the following one has no scenario or cannot be launched yet.</summary>
        ComingSoon,

        /// <summary>This attempt was lost. Shown disabled whether or not the following mission is open.</summary>
        WinRequired,
    }
}
