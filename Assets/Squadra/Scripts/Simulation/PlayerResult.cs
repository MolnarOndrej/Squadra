namespace Squadra.Simulation
{
    /// <summary>One player's combat totals for a single attempt at a mission.</summary>
    public readonly struct PlayerResult
    {
        public readonly Faction Faction;
        public readonly string Name;
        public readonly bool IsHuman;

        /// <summary>Individual enemy and neutral guard Darts this player's fleets destroyed.</summary>
        public readonly int DartsDestroyed;

        /// <summary>This player's Darts destroyed in combat, attacking or defending.</summary>
        public readonly int DartsLost;

        public PlayerResult(Faction faction, string name, bool isHuman, int dartsDestroyed, int dartsLost)
        {
            Faction = faction;
            Name = name;
            IsHuman = isHuman;
            DartsDestroyed = dartsDestroyed;
            DartsLost = dartsLost;
        }
    }
}
