namespace Squadra.Pause
{
    /// <summary>
    /// The paces the game can be played at, in the order the Settings screen lists them. The saved
    /// preference and the sandbox's temporary choice are always one of these; <see cref="GameClock"/>
    /// holds their multipliers and how they are stored.
    /// </summary>
    public enum GameSpeed
    {
        Slow,
        Normal,
        Fast,
        Turbo,
    }
}
