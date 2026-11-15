namespace Squadra.Simulation
{
    /// <summary>What happened when a fleet reached its destination.</summary>
    public enum ArrivalOutcome
    {
        /// <summary>Allied arrival at an owned object: the Darts joined the stationed fleet.</summary>
        Merged,

        /// <summary>Arrival allied with the fleet that is capturing the object: joined it, timer untouched.</summary>
        ReinforcedCapture,

        /// <summary>Nobody was stationed there: the whole fleet landed and began capturing.</summary>
        Landed,

        /// <summary>A hostile force was stationed there: the fleet began a battle with it.</summary>
        BattleStarted,

        /// <summary>A battle was already being fought there: the fleet joined its own side of it.</summary>
        BattleJoined,
    }
}
