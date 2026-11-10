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
            Ships = config.Ships < 0 ? 0 : config.Ships;
            ProductionInterval = config.ProductionInterval;
            CaptureDuration = captureDuration;
        }

        public int Id { get; }
        public string Name { get; }
        public NodeKind Kind { get; }
        public MapPoint Position { get; }

        /// <summary>Seconds per produced ship. Exposed so upgrades can change it mid-match.</summary>
        public double ProductionInterval { get; set; }

        /// <summary>Formal owner. Changes only when a capture completes.</summary>
        public Faction Owner { get; internal set; }

        /// <summary>Ships stationed here. They belong to <see cref="Occupant"/>.</summary>
        public int Ships { get; internal set; }

        /// <summary>The faction currently claiming this object, or Neutral when no capture is running.</summary>
        public Faction Captor { get; internal set; }

        public double CaptureDuration { get; }
        public double CaptureElapsed { get; internal set; }

        internal double ProductionClock;

        public bool IsBeingCaptured => Captor != Faction.Neutral;

        /// <summary>Faction the stationed fleet belongs to: the captor during a capture, otherwise the owner.</summary>
        public Faction Occupant => IsBeingCaptured ? Captor : Owner;

        /// <summary>Intervals shorter than this count as "no production".</summary>
        public const double MinProductionInterval = 0.001;

        public bool IsProducing =>
            Owner != Faction.Neutral && !IsBeingCaptured && ProductionInterval >= MinProductionInterval;

        /// <summary>Capture progress from 0 to 1. Zero when no capture is running.</summary>
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

        /// <summary>Progress toward the next produced ship, from 0 to 1.</summary>
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

    /// <summary>A fleet travelling between two objects.</summary>
    public sealed class Fleet
    {
        internal Fleet(int id, Faction faction, int ships, int sourceId, int targetId, double duration)
        {
            Id = id;
            Faction = faction;
            Ships = ships;
            SourceId = sourceId;
            TargetId = targetId;
            Duration = duration;
        }

        public int Id { get; }
        public Faction Faction { get; }
        public int Ships { get; }
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
