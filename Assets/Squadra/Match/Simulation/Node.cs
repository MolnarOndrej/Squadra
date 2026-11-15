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
        public Faction Owner { get; private set; }

        /// <summary>
        /// Darts stationed here. They belong to <see cref="Occupant"/>. During a battle this is the
        /// stationed side's live count; the arriving side's Darts are in <see cref="Battle"/>.
        /// </summary>
        public int Darts { get; internal set; }

        /// <summary>The faction currently claiming this object, or Neutral when no capture is running.</summary>
        public Faction Captor { get; private set; }

        public double CaptureDuration { get; }
        public double CaptureElapsed { get; private set; }

        /// <summary>The battle being fought here, or null. Production and capture wait while it lasts.</summary>
        public Battle Battle { get; internal set; }

        double productionClock;

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
                double p = productionClock / ProductionInterval;
                return p >= 1 ? 1f : (float)p;
            }
        }

        // ------------------------------------------------------------------ clocks, driven by the match

        /// <summary>
        /// Seconds until this object's next event: a casualty of its battle, the end of its capture or
        /// its next Dart. Only one of the three clocks runs at a time.
        /// </summary>
        internal double TimeToNextEvent
        {
            get
            {
                if (IsContested) return Battle.TimeToNextPair;
                if (IsBeingCaptured) return CaptureDuration - CaptureElapsed;
                if (IsProducing) return ProductionInterval - productionClock;
                return double.PositiveInfinity;
            }
        }

        internal void Advance(double seconds)
        {
            // A contested object neither produces nor is captured: both clocks keep their place.
            if (IsContested) Battle.Advance(seconds);
            else if (IsBeingCaptured) CaptureElapsed += seconds;
            else if (IsProducing) productionClock += seconds;
        }

        internal bool IsProductionDue => IsProducing && ProductionInterval - productionClock <= Match.Epsilon;

        internal void ProduceDart()
        {
            productionClock = System.Math.Max(0, productionClock - ProductionInterval);
            Darts++;
        }

        internal bool IsCaptureDue =>
            !IsContested && IsBeingCaptured && CaptureDuration - CaptureElapsed <= Match.Epsilon;

        /// <summary>Starts a claimant's countdown from zero.</summary>
        internal void BeginCapture(Faction captor)
        {
            Captor = captor;
            CaptureElapsed = 0;
        }

        internal void ClearCapture()
        {
            Captor = Faction.Neutral;
            CaptureElapsed = 0;
        }

        /// <summary>Hands the object to its captor and returns the previous owner.</summary>
        internal Faction CompleteCapture()
        {
            Faction previousOwner = Owner;
            Owner = Captor;
            ClearCapture();
            productionClock = 0; // a newly captured object starts a fresh production interval
            return previousOwner;
        }
    }
}
