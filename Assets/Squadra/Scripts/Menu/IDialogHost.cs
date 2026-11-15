using System;

namespace Squadra
{
    /// <summary>
    /// Something that can put a confirmation dialog in front of the player. The shared Settings screen
    /// asks its host, the main menu or the pause menu, to confirm a display change this way.
    /// </summary>
    public interface IDialogHost
    {
        /// <summary>
        /// Opens the confirmation dialog over the current screen. Only the dialog can be used until it
        /// closes, and Cancel has the focus. <paramref name="message"/> gets the seconds left when
        /// <paramref name="timeout"/> is set; running out of time cancels.
        /// </summary>
        void ShowDialog(string title, Func<int, string> message, Action onConfirm, Action onCancel,
            float timeout = 0f);
    }
}
