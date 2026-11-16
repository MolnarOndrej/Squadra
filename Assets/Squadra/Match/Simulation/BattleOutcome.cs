namespace Squadra.Simulation
{
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
}
