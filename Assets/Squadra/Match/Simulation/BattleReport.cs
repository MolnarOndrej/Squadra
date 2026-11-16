namespace Squadra.Simulation
{
    /// <summary>The visible facts of one finished battle, for presentation and for tests.</summary>
    public readonly struct BattleReport
    {
        public readonly int NodeId;
        public readonly Faction Arriving;
        public readonly Faction Stationed;
        public readonly BattleOutcome Outcome;

        /// <summary>Darts destroyed on each side over the whole battle.</summary>
        public readonly int LossesPerSide;

        /// <summary>Darts of the winning side left at the object. Zero after a tie.</summary>
        public readonly int Survivors;

        /// <summary>Seconds the battle lasted, reinforcements included.</summary>
        public readonly double Duration;

        public BattleReport(int nodeId, Faction arriving, Faction stationed, BattleOutcome outcome,
            int lossesPerSide, int survivors, double duration)
        {
            NodeId = nodeId;
            Arriving = arriving;
            Stationed = stationed;
            Outcome = outcome;
            LossesPerSide = lossesPerSide;
            Survivors = survivors;
            Duration = duration;
        }

        /// <summary>The side with Darts left, or Neutral after a tie.</summary>
        public Faction Winner
        {
            get
            {
                switch (Outcome)
                {
                    case BattleOutcome.AttackWon:
                    case BattleOutcome.ControlRestored:
                        return Arriving;
                    case BattleOutcome.Repelled:
                        return Stationed;
                    default:
                        return Faction.Neutral;
                }
            }
        }
    }
}
