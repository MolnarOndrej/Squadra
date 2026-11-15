namespace Squadra.Simulation
{
    /// <summary>The visible facts of one fleet arrival, for presentation and for tests.</summary>
    public readonly struct ArrivalReport
    {
        public readonly int NodeId;
        public readonly int FleetId;
        public readonly Faction Arriving;
        public readonly int ArrivingDarts;

        /// <summary>Faction of the fleet stationed at the object before the arrival.</summary>
        public readonly Faction Stationed;

        /// <summary>Darts stationed at the object before the arrival.</summary>
        public readonly int StationedDarts;

        public readonly ArrivalOutcome Outcome;

        public ArrivalReport(int nodeId, int fleetId, Faction arriving, int arrivingDarts, Faction stationed,
            int stationedDarts, ArrivalOutcome outcome)
        {
            NodeId = nodeId;
            FleetId = fleetId;
            Arriving = arriving;
            ArrivingDarts = arrivingDarts;
            Stationed = stationed;
            StationedDarts = stationedDarts;
            Outcome = outcome;
        }

        /// <summary>True when the fleet went into a battle, a new one or one already under way.</summary>
        public bool EnteredBattle =>
            Outcome == ArrivalOutcome.BattleStarted || Outcome == ArrivalOutcome.BattleJoined;
    }
}
