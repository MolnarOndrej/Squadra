namespace Squadra.Pause
{
    /// <summary>What one press of Escape does in a mission scene.</summary>
    public enum EscapeStep
    {
        /// <summary>Nothing: something else used the press, or the mission is being left.</summary>
        None,

        /// <summary>The pause menu opens and the mission freezes.</summary>
        Open,

        /// <summary>The pause menu closes and the mission, or its result, carries on.</summary>
        Resume,

        /// <summary>Settings closes and the root pause menu comes back.</summary>
        LeaveSettings,

        /// <summary>The open dialog is cancelled; the screen it was opened from comes back.</summary>
        CancelDialog,
    }
}
