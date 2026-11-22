using Squadra.Pause;

namespace Squadra
{
    /// <summary>
    /// What the <see cref="PauseMenu"/> pauses and leaves: a mission's <see cref="GameController"/> or a
    /// <see cref="SandboxRun"/>. The menu decides where each press leads; the run freezes, says what
    /// leaving it costs and carries out a confirmed quit.
    /// </summary>
    public interface IPausableRun
    {
        /// <summary>False once the run is on its way out: there is nothing left to pause.</summary>
        bool CanPause { get; }

        /// <summary>
        /// Freezes the run under the pause menu, or releases it. Returns false when nothing changed:
        /// already in that state, or the run is on its way out.
        /// </summary>
        bool SetPaused(bool value);

        /// <summary>The text of the confirmation for a quit to <paramref name="destination"/>.</summary>
        QuitPrompt QuitPromptFor(QuitDestination destination);

        /// <summary>Leaves the run for the menu, paused or not.</summary>
        void BackToMenu();

        /// <summary>Closes the game from the run.</summary>
        void QuitToDesktop();
    }
}
