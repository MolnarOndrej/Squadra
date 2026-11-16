namespace Squadra.Pause
{
    /// <summary>Which screen of the pause menu is showing.</summary>
    public enum PauseScreen
    {
        /// <summary>The pause menu is closed and the mission, or its result, is in front.</summary>
        Closed,

        /// <summary>The root pause menu: Paused above its four actions.</summary>
        Menu,

        /// <summary>The shared Settings screen, opened from the pause menu.</summary>
        Settings,
    }
}
