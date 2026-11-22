namespace Squadra.Simulation
{
    /// <summary>A fleet, a group of Darts, travelling between two objects.</summary>
    public sealed class Fleet
    {
        internal Fleet(int id, Faction faction, int darts, int sourceId, int targetId, double duration)
        {
            Id = id;
            Faction = faction;
            Darts = darts;
            SourceId = sourceId;
            TargetId = targetId;
            Duration = duration;
        }

        public int Id { get; }
        public Faction Faction { get; }
        public int Darts { get; }
        public int SourceId { get; }
        public int TargetId { get; }

        /// <summary>Total travel time in seconds: route distance divided by the common fleet speed.</summary>
        public double Duration { get; private set; }
        public double Elapsed { get; internal set; }

        public double Remaining => System.Math.Max(0, Duration - Elapsed);

        internal bool HasArrived => Duration - Elapsed <= Match.Epsilon;

        /// <summary>
        /// Gives the flight a new total time, as when the fleet speed changes on the way. The fleet keeps
        /// how far along it is and covers the rest at the new pace.
        /// </summary>
        internal void Retime(double duration)
        {
            double progress = Duration > 0 ? System.Math.Min(1, Elapsed / Duration) : 1;
            Duration = duration;
            Elapsed = progress * duration;
        }

        public float Progress
        {
            get
            {
                if (Duration <= 0) return 1f;
                double p = Elapsed / Duration;
                return p >= 1 ? 1f : (float)p;
            }
        }
    }
}
