namespace Squadra.Simulation
{
    /// <summary>
    /// Who Darts and map objects belong to. Neutral is nobody; every other value is the stable identity
    /// of one player, and a mission's roster names the ones that take part. Only Blue and Red are
    /// playable so far: the other identities exist so rosters and results can hold up to six players.
    /// </summary>
    public enum Faction
    {
        Neutral = 0,
        Blue = 1,
        Red = 2,
        Green = 3,
        Yellow = 4,
        Purple = 5,
        Orange = 6,
    }
}
