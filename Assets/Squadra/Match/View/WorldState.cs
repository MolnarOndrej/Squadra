using Squadra.Simulation;

namespace Squadra
{
    /// <summary>
    /// What there is to show of one world at one moment: its owner and who holds it, its Darts, a
    /// capture and a battle there. In a mission it is read from the match's node; a sandbox sample makes
    /// up one that no match has reached.
    /// </summary>
    public struct WorldState
    {
        public Faction Owner;
        public Faction Occupant;
        public Faction Captor;
        public int Darts;
        public float CaptureProgress;
        public float CaptureRemaining;

        /// <summary>Seconds per produced Dart; zero for a world that never produces.</summary>
        public float Interval;

        /// <summary>True while a battle is fought here. <see cref="Darts"/> is then the stationed side's count.</summary>
        public bool Fighting;
        public Faction Arriving;
        public int ArrivingDarts;

        /// <summary>The state of a match's node as it is now.</summary>
        public static WorldState Of(Node node)
        {
            Battle battle = node.Battle;
            return new WorldState
            {
                Owner = node.Owner,
                Occupant = node.Occupant,
                Captor = node.Captor,
                Darts = node.Darts,
                CaptureProgress = node.CaptureProgress,
                CaptureRemaining = (float)node.CaptureRemaining,
                Interval = node.CanProduce ? (float)node.ProductionInterval : 0f,
                Fighting = battle != null,
                Arriving = battle != null ? battle.Arriving : Faction.Neutral,
                ArrivingDarts = battle != null ? battle.ArrivingDarts : 0,
            };
        }
    }
}
