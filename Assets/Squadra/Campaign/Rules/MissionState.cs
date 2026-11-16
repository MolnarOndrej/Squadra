namespace Squadra.Campaign
{
    /// <summary>How one mission appears on the map.</summary>
    public enum MissionState
    {
        /// <summary>No mission definition assigned yet: shown locked, as "coming soon".</summary>
        Preview,

        /// <summary>Playable, but the mission before it is not completed.</summary>
        Locked,

        /// <summary>Playable now and not won yet.</summary>
        Available,

        /// <summary>Won this session. Still playable.</summary>
        Completed,

        /// <summary>A definition is assigned but could not be prepared: never playable, with the reason.</summary>
        Unavailable,
    }
}
