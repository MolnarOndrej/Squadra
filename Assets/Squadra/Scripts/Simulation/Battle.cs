using System;

namespace Squadra.Simulation
{
    /// <summary>
    /// A fight under way at one map object between the force stationed there and a force that arrived.
    /// Each casualty step removes one Dart from each side. The counts here are the live ones; nothing is
    /// decided before a side has actually run out. State is read-only outside the simulation.
    /// </summary>
    public sealed class Battle
    {
        internal Battle(int nodeId, Faction stationed, int stationedDarts, Faction arriving, int arrivingDarts)
        {
            NodeId = nodeId;
            Stationed = stationed;
            StationedDarts = stationedDarts;
            Arriving = arriving;
            ArrivingDarts = arrivingDarts;
        }

        public int NodeId { get; }

        /// <summary>
        /// The side that held the object when the battle began: whoever's Darts were actually there, a
        /// capturing force or neutral guards included, rather than the formal owner.
        /// </summary>
        public Faction Stationed { get; }

        /// <summary>The side that arrived and started the battle.</summary>
        public Faction Arriving { get; }

        public int StationedDarts { get; internal set; }
        public int ArrivingDarts { get; internal set; }

        /// <summary>Darts each side has lost in this battle so far. Reinforcements never give them back.</summary>
        public int LossesPerSide { get; internal set; }

        /// <summary>Seconds of fighting so far.</summary>
        public double Elapsed { get; internal set; }

        /// <summary>True once a side has run out and the outcome has been applied to the object.</summary>
        public bool IsOver { get; internal set; }

        /// <summary>
        /// Seconds until the battle ends if nobody reinforces it. A reinforcement replaces it with a
        /// fresh duration for the forces then present, so the battle as a whole can last longer.
        /// </summary>
        public double Remaining => IsOver ? 0 : Math.Max(0, ScheduleDuration - ScheduleClock);

        public bool Involves(Faction faction) => faction == Stationed || faction == Arriving;

        /// <summary>Darts this side still has in the battle. Zero for anyone not fighting in it.</summary>
        public int DartsOf(Faction faction)
        {
            if (faction == Arriving) return ArrivingDarts;
            return faction == Stationed ? StationedDarts : 0;
        }

        // The casualty schedule: SchedulePairs pairs spread evenly over ScheduleDuration seconds, the last
        // one at the end. It is fixed until a reinforcement replaces it. Pairs are timed from the start
        // of the schedule rather than from the previous pair, so rounding does not add up.
        internal double ScheduleClock;
        internal double ScheduleDuration;
        internal int SchedulePairs = 1;
        internal int PairsDone;

        /// <summary>A reinforcement has joined at this instant and the schedule has not been replaced yet.</summary>
        internal bool Reinforced;

        internal double TimeToNextPair => ScheduleDuration * (PairsDone + 1) / SchedulePairs - ScheduleClock;
    }
}
