using System;

namespace Squadra.Simulation
{
    public enum Faction
    {
        Neutral = 0,
        Blue = 1,
        Red = 2,
    }

    public enum NodeKind
    {
        Planet,
        Moon,
    }

    public enum MatchStatus
    {
        InProgress,
        BlueVictory,
        BlueDefeat,
    }

    /// <summary>What happened when a fleet reached its destination.</summary>
    public enum ArrivalOutcome
    {
        /// <summary>Allied arrival at an owned object: the ships joined the stationed fleet.</summary>
        Merged,

        /// <summary>Arrival allied with the fleet that is capturing the object: joined it, timer untouched.</summary>
        ReinforcedCapture,

        /// <summary>Nobody was stationed there: the whole fleet landed and began capturing.</summary>
        Landed,

        /// <summary>The arriving fleet won the battle and its survivors began capturing.</summary>
        AttackWon,

        /// <summary>The arriving fleet was destroyed; the stationed fleet keeps its survivors.</summary>
        Repelled,

        /// <summary>Both fleets were destroyed. Any capture in progress is cleared.</summary>
        Tie,

        /// <summary>The incumbent owner's fleet destroyed the capturing fleet and took control back at once.</summary>
        ControlRestored,
    }

    /// <summary>A position on the map, in map units.</summary>
    public readonly struct MapPoint : IEquatable<MapPoint>
    {
        public readonly float X;
        public readonly float Y;

        public MapPoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static double Distance(MapPoint a, MapPoint b)
        {
            double dx = (double)a.X - b.X;
            double dy = (double)a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public bool Equals(MapPoint other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is MapPoint other && Equals(other);
        public override int GetHashCode() => unchecked((X.GetHashCode() * 397) ^ Y.GetHashCode());
        public override string ToString() => $"({X}, {Y})";
    }

    /// <summary>The visible facts of one fleet arrival, for presentation and for tests.</summary>
    public readonly struct ArrivalReport
    {
        public readonly int NodeId;
        public readonly int FleetId;
        public readonly Faction Arriving;
        public readonly int ArrivingShips;

        /// <summary>Faction of the fleet stationed at the object before the arrival.</summary>
        public readonly Faction Stationed;
        public readonly int StationedShips;

        public readonly ArrivalOutcome Outcome;

        /// <summary>Ships destroyed on each side. Zero for peaceful arrivals.</summary>
        public readonly int LossesPerSide;

        /// <summary>Ships stationed at the object once the arrival was resolved.</summary>
        public readonly int ShipsAfter;

        public ArrivalReport(int nodeId, int fleetId, Faction arriving, int arrivingShips, Faction stationed,
            int stationedShips, ArrivalOutcome outcome, int lossesPerSide, int shipsAfter)
        {
            NodeId = nodeId;
            FleetId = fleetId;
            Arriving = arriving;
            ArrivingShips = arrivingShips;
            Stationed = stationed;
            StationedShips = stationedShips;
            Outcome = outcome;
            LossesPerSide = lossesPerSide;
            ShipsAfter = shipsAfter;
        }

        public bool WasBattle => LossesPerSide > 0;
    }
}
