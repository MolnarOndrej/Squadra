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

        public int StationedDarts { get; private set; }
        public int ArrivingDarts { get; private set; }

        /// <summary>Darts each side has lost in this battle so far. Reinforcements never give them back.</summary>
        public int LossesPerSide { get; private set; }

        /// <summary>Seconds of fighting so far.</summary>
        public double Elapsed { get; private set; }

        /// <summary>True once a side has run out and the outcome has been applied to the object.</summary>
        public bool IsOver { get; private set; }

        /// <summary>
        /// Seconds until the battle ends if nobody reinforces it. A reinforcement replaces it with a
        /// fresh duration for the forces then present, so the battle as a whole can last longer.
        /// </summary>
        public double Remaining => IsOver ? 0 : Math.Max(0, scheduleDuration - scheduleClock);

        public bool Involves(Faction faction) => faction == Stationed || faction == Arriving;

        /// <summary>Darts this side still has in the battle. Zero for anyone not fighting in it.</summary>
        public int DartsOf(Faction faction)
        {
            if (faction == Arriving) return ArrivingDarts;
            return faction == Stationed ? StationedDarts : 0;
        }

        // The casualty schedule: schedulePairs pairs spread evenly over scheduleDuration seconds, the last
        // one at the end. It is fixed until a reinforcement replaces it. Pairs are timed from the start
        // of the schedule rather than from the previous pair, so rounding does not add up.
        double scheduleClock;
        double scheduleDuration;
        int schedulePairs = 1;
        int pairsDone;

        // A reinforcement has joined at this instant and the schedule has not been replaced yet.
        bool reinforced;

        /// <summary>True once a side has run out. The match then applies the outcome to the object.</summary>
        internal bool IsDecided => ArrivingDarts == 0 || StationedDarts == 0;

        internal double TimeToNextPair => scheduleDuration * (pairsDone + 1) / schedulePairs - scheduleClock;

        /// <summary>Spreads the pairs still to be lost evenly over a fresh duration for the forces now present.</summary>
        internal void Schedule(BattleTiming timing)
        {
            scheduleClock = 0;
            scheduleDuration = timing.Duration(ArrivingDarts, StationedDarts);
            schedulePairs = Math.Max(1, Math.Min(ArrivingDarts, StationedDarts));
            pairsDone = 0;
        }

        internal void Advance(double seconds)
        {
            scheduleClock += seconds;
            Elapsed += seconds;
        }

        /// <summary>
        /// Adds a fleet to the side it belongs to. The schedule is replaced by the next
        /// <see cref="TakeDueCasualties"/>, once every arrival of this instant has been counted.
        /// </summary>
        internal void Reinforce(Faction faction, int darts)
        {
            if (faction == Stationed) StationedDarts += darts;
            else ArrivingDarts += darts;

            reinforced = true;
        }

        /// <summary>
        /// Removes the pairs that are due now and returns how many fell. After a reinforcement, a pair
        /// that was already due still falls, against the reinforced forces, and whatever fighting is
        /// left gets a fresh schedule.
        /// </summary>
        internal int TakeDueCasualties(BattleTiming timing)
        {
            int pairs = 0;

            if (reinforced)
            {
                reinforced = false;
                if (TimeToNextPair <= Match.Epsilon) pairs += LosePair();
                if (!IsDecided) Schedule(timing);
            }

            while (!IsDecided && TimeToNextPair <= Match.Epsilon)
                pairs += LosePair();

            return pairs;
        }

        internal void End()
        {
            IsOver = true;
        }

        // One Dart from each side, at the same moment.
        int LosePair()
        {
            ArrivingDarts--;
            StationedDarts--;
            LossesPerSide++;
            pairsDone++;
            return 1;
        }
    }
}
