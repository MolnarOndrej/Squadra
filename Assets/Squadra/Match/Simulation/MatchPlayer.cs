namespace Squadra.Simulation
{
    /// <summary>One participating player of a match, as its roster lists them.</summary>
    public readonly struct MatchPlayer
    {
        /// <summary>Stable identity. Results are keyed by it.</summary>
        public readonly Faction Faction;

        /// <summary>Display name.</summary>
        public readonly string Name;

        public MatchPlayer(Faction faction, string name = null)
        {
            Faction = faction;
            Name = string.IsNullOrEmpty(name) ? faction.ToString() : name;
        }
    }
}
