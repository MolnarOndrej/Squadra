namespace Squadra.Pause
{
    /// <summary>How the mission stands when the player asks to leave it.</summary>
    public enum MissionStanding
    {
        /// <summary>The match is still being played: leaving abandons it.</summary>
        Unfinished,

        /// <summary>The match was won and the victory is already recorded.</summary>
        Won,

        /// <summary>The match was lost. Leaving changes nothing.</summary>
        Lost,
    }
}
