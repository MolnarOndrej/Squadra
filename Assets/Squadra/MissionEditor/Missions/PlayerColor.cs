namespace Squadra.Missions
{
    /// <summary>
    /// The colors a mission player can be drawn in. A color is presentation only: a blue player is not
    /// the game's Blue faction, a human or any particular AI. Neutral is not a color; a node without an
    /// owner has no player at all.
    /// </summary>
    public enum PlayerColor
    {
        Blue,
        Red,
        Green,
        Yellow,
        Purple,
        Orange,
    }
}
