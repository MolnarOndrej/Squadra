using System;

namespace Squadra.Simulation
{
    /// <summary>
    /// How long a battle lasts. The duration comes from the numerical balance of the two sides and not
    /// from their size: equal forces fight for the longest time, a side outnumbered by the decisive
    /// ratio or worse falls in the shortest, and swapping the sides changes nothing.
    /// </summary>
    public sealed class BattleTiming
    {
        /// <summary>Seconds a battle lasts when one side outnumbers the other by the decisive ratio or more.</summary>
        public double ShortestDuration = 1.0;

        /// <summary>Seconds a battle between equal forces lasts.</summary>
        public double LongestDuration = 5.0;

        /// <summary>
        /// The smaller force as a fraction of the larger at which a battle is already as short as it
        /// gets. 0.1 is ten to one.
        /// </summary>
        public double DecisiveFraction = 0.1;

        /// <summary>
        /// Shapes the durations between the two ends. It maps the balance, from 0 (decisive) to 1 (equal
        /// forces), to a value in the same range. Null is a straight line.
        /// </summary>
        public Func<double, double> Curve;

        /// <summary>Seconds a battle between forces of these two sizes lasts when nobody reinforces it.</summary>
        public double Duration(int first, int second)
        {
            int smaller = Math.Min(first, second);
            int larger = Math.Max(first, second);
            if (smaller <= 0) return 0;

            double decisive = Math.Min(1, Math.Max(0, DecisiveFraction));
            double fraction = (double)smaller / larger;
            double balance = decisive >= 1 ? 1 : (fraction - decisive) / (1 - decisive);
            balance = Math.Min(1, Math.Max(0, balance));
            if (Curve != null) balance = Math.Min(1, Math.Max(0, Curve(balance)));

            double shortest = Math.Max(0, ShortestDuration);
            double longest = Math.Max(shortest, LongestDuration);
            return shortest + (longest - shortest) * balance;
        }
    }

    /// <summary>How a battle ended.</summary>
    public enum BattleOutcome
    {
        /// <summary>The arriving side won and its survivors began capturing the object.</summary>
        AttackWon,

        /// <summary>The arriving side was destroyed; the stationed side keeps its survivors.</summary>
        Repelled,

        /// <summary>Both sides were destroyed. Any capture in progress is cleared.</summary>
        Tie,

        /// <summary>The object's owner destroyed the force capturing it and took control back at once.</summary>
        ControlRestored,
    }

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
