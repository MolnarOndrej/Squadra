using System.Collections.Generic;

namespace Squadra.Pause
{
    /// <summary>Which screen of the pause menu is showing.</summary>
    public enum PauseScreen
    {
        /// <summary>The pause menu is closed and the mission, or its result, is in front.</summary>
        Closed,

        /// <summary>The root pause menu: Paused above its four actions.</summary>
        Menu,

        /// <summary>The shared Settings screen, opened from the pause menu.</summary>
        Settings,
    }

    /// <summary>The actions of the root pause menu, in the order they are shown.</summary>
    public enum PauseAction
    {
        Resume,
        Settings,
        QuitToMenu,
        QuitToDesktop,
    }

    /// <summary>Where a confirmed quit leads.</summary>
    public enum QuitDestination
    {
        /// <summary>The menu's mission map.</summary>
        Menu,

        /// <summary>Out of the game.</summary>
        Desktop,
    }

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

    /// <summary>
    /// The navigation rules of the pause menu: which screen is showing, what Escape does next and
    /// whether a quit has been confirmed. The mission stays paused for as long as the menu is open,
    /// whatever screen or dialog is in front. It decides nothing about the match itself.
    /// </summary>
    public sealed class PauseFlow
    {
        static readonly PauseAction[] Order =
        {
            PauseAction.Resume, PauseAction.Settings, PauseAction.QuitToMenu, PauseAction.QuitToDesktop,
        };

        /// <summary>The root menu's actions from top to bottom.</summary>
        public static IReadOnlyList<PauseAction> Actions => Order;

        /// <summary>The action that has the keyboard focus when the pause menu opens.</summary>
        public const PauseAction InitialFocus = PauseAction.Resume;

        public PauseScreen Screen { get; private set; }

        /// <summary>True while the mission is paused: from opening the menu until it is resumed.</summary>
        public bool IsOpen => Screen != PauseScreen.Closed;

        /// <summary>True once a quit has been confirmed. Nothing else is accepted after that.</summary>
        public bool Leaving { get; private set; }

        /// <summary>Where the confirmed quit leads. Only meaningful while <see cref="Leaving"/>.</summary>
        public QuitDestination Destination { get; private set; }

        /// <summary>Opens the root pause menu. False when it is already open or the mission is being left.</summary>
        public bool Open()
        {
            if (Leaving || IsOpen) return false;

            Screen = PauseScreen.Menu;
            return true;
        }

        /// <summary>Closes the pause menu from its root screen. False from anywhere else.</summary>
        public bool Resume()
        {
            if (Leaving || Screen != PauseScreen.Menu) return false;

            Screen = PauseScreen.Closed;
            return true;
        }

        /// <summary>Goes from the root pause menu to Settings. False from anywhere else.</summary>
        public bool OpenSettings()
        {
            if (Leaving || Screen != PauseScreen.Menu) return false;

            Screen = PauseScreen.Settings;
            return true;
        }

        /// <summary>Goes from Settings back to the root pause menu. The mission stays paused.</summary>
        public bool LeaveSettings()
        {
            if (Leaving || Screen != PauseScreen.Settings) return false;

            Screen = PauseScreen.Menu;
            return true;
        }

        /// <summary>
        /// Commits to leaving the mission. True exactly once, and only while the pause menu is open:
        /// a second confirmation during the scene change or the application exit does nothing.
        /// </summary>
        public bool ConfirmQuit(QuitDestination destination)
        {
            if (Leaving || !IsOpen) return false;

            Leaving = true;
            Destination = destination;
            return true;
        }

        /// <summary>
        /// One press of Escape. A control that used the press itself, such as an open dropdown list
        /// closing, takes it first and nothing else happens. Then an open dialog is cancelled, then
        /// Settings is left, then the root menu resumes; with the menu closed the press opens it.
        /// Each press does exactly one of these, so the press that opens the menu can never close it.
        /// </summary>
        public EscapeStep Escape(bool usedByControl, bool dialogOpen)
        {
            if (Leaving || usedByControl) return EscapeStep.None;
            if (IsOpen && dialogOpen) return EscapeStep.CancelDialog;

            switch (Screen)
            {
                case PauseScreen.Closed:
                    return Open() ? EscapeStep.Open : EscapeStep.None;
                case PauseScreen.Settings:
                    return LeaveSettings() ? EscapeStep.LeaveSettings : EscapeStep.None;
                default:
                    return Resume() ? EscapeStep.Resume : EscapeStep.None;
            }
        }
    }
}
