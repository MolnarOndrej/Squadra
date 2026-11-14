namespace Squadra.Simulation
{
    /// <summary>A stationary map object: a planet or a moon. State is read-only outside the simulation.</summary>
    public sealed class Node
    {
        internal Node(int id, NodeConfig config, double captureDuration)
        {
            Id = id;
            Name = config.Name ?? "";
            Kind = config.Kind;
            Position = config.Position;
            Owner = config.Owner;
            Darts = config.Darts < 0 ? 0 : config.Darts;
            ProductionInterval = config.ProductionInterval;
            CaptureDuration = captureDuration;
        }

        public int Id { get; }
        public string Name { get; }
        public NodeKind Kind { get; }
        public MapPoint Position { get; }

        /// <summary>Seconds per produced Dart. Exposed so upgrades can change it mid-match.</summary>
        public double ProductionInterval { get; set; }

        /// <summary>Formal owner. Changes only when a capture completes.</summary>
        public Faction Owner { get; internal set; }

        /// <summary>
        /// Darts stationed here. They belong to <see cref="Occupant"/>. During a battle this is the
        /// stationed side's live count; the arriving side's Darts are in <see cref="Battle"/>.
        /// </summary>
        public int Darts { get; internal set; }

        /// <summary>The faction currently claiming this object, or Neutral when no capture is running.</summary>
        public Faction Captor { get; internal set; }

        public double CaptureDuration { get; }
        public double CaptureElapsed { get; internal set; }

        /// <summary>The battle being fought here, or null. Production and capture wait while it lasts.</summary>
        public Battle Battle { get; internal set; }

        internal double ProductionClock;

        public bool IsBeingCaptured => Captor != Faction.Neutral;

        /// <summary>True while a battle is being fought here.</summary>
        public bool IsContested => Battle != null;

        /// <summary>Faction the stationed fleet belongs to: the captor during a capture, otherwise the owner.</summary>
        public Faction Occupant => IsBeingCaptured ? Captor : Owner;

        /// <summary>Intervals shorter than this count as "no production".</summary>
        public const double MinProductionInterval = 0.001;

        /// <summary>True while the production clock runs: owned, outside capture and not fought over.</summary>
        public bool IsProducing =>
            Owner != Faction.Neutral && !IsBeingCaptured && !IsContested
            && ProductionInterval >= MinProductionInterval;

        /// <summary>
        /// Capture progress from 0 to 1. Zero when no capture is running. It stands still, and is kept,
        /// while the capturing force is in a battle.
        /// </summary>
        public float CaptureProgress
        {
            get
            {
                if (!IsBeingCaptured || CaptureDuration <= 0) return 0f;
                double p = CaptureElapsed / CaptureDuration;
                return p >= 1 ? 1f : (float)p;
            }
        }

        public double CaptureRemaining => IsBeingCaptured ? System.Math.Max(0, CaptureDuration - CaptureElapsed) : 0;

        /// <summary>Progress toward the next produced Dart, from 0 to 1.</summary>
        public float ProductionProgress
        {
            get
            {
                if (!IsProducing) return 0f;
                double p = ProductionClock / ProductionInterval;
                return p >= 1 ? 1f : (float)p;
            }
        }
    }

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
        public double Duration { get; }
        public double Elapsed { get; internal set; }

        public double Remaining => System.Math.Max(0, Duration - Elapsed);

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
