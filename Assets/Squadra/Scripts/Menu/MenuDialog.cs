using System;
using UnityEngine;
using UnityEngine.UIElements;

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

    /// <summary>
    /// The confirmation dialog shared by the main menu and the pause menu: a title, a message, Cancel
    /// and Confirm. It keeps its own deadline on the real clock, so a countdown runs on while the game
    /// is paused or another application is in front. The host decides what happens to the screen
    /// underneath and where the focus goes afterwards.
    /// </summary>
    public sealed class MenuDialog
    {
        const string HiddenClass = "hidden";

        readonly VisualElement element;
        readonly MenuAudio sounds;
        readonly Label title;
        readonly Label message;
        readonly Button cancelButton;
        readonly Button confirmButton;

        Action confirmed;
        Action cancelled;
        Func<int, string> text;
        double deadline;
        int secondsShown;
        Focusable opener;

        /// <summary>
        /// Binds to the dialog element of a menu document. Its Cancel and Confirm buttons are connected
        /// here; <paramref name="onOpened"/> runs when the dialog appears and <paramref name="onClosed"/>
        /// when it goes, with the control that had the focus before it opened.
        /// </summary>
        public MenuDialog(VisualElement element, MenuAudio sounds, Action onOpened, Action<Focusable> onClosed)
        {
            this.element = element;
            this.sounds = sounds;
            Opened = onOpened;
            Closed = onClosed;

            title = element.Q<Label>("dialogTitle");
            message = element.Q<Label>("dialogMessage");
            cancelButton = element.Q<Button>("dialogCancelButton");
            confirmButton = element.Q<Button>("dialogConfirmButton");
            cancelButton.clicked += () => Cancel(true);
            confirmButton.clicked += Confirm;
        }

        Action Opened { get; }
        Action<Focusable> Closed { get; }

        public bool IsOpen { get; private set; }
        public string Title => title.text;
        public string Message => message.text;

        /// <summary>The button that takes the focus when the dialog opens.</summary>
        public Button CancelButton => cancelButton;
        public Button ConfirmButton => confirmButton;

        /// <summary>Opens the dialog, cancelling one that is already open. See <see cref="IDialogHost"/>.</summary>
        public void Show(string heading, Func<int, string> body, Action onConfirm, Action onCancel, float timeout)
        {
            if (IsOpen) Cancel(false);

            IsOpen = true;
            confirmed = onConfirm;
            cancelled = onCancel;
            text = body;
            deadline = timeout > 0f ? Time.realtimeSinceStartupAsDouble + timeout : 0d;
            secondsShown = Mathf.CeilToInt(timeout);
            opener = element.focusController?.focusedElement;

            title.text = heading;
            message.text = body(secondsShown);
            element.RemoveFromClassList(HiddenClass);
            Opened?.Invoke();
        }

        /// <summary>What the Confirm button does: closes the dialog, then carries out what was confirmed.</summary>
        public void Confirm()
        {
            if (!IsOpen) return;

            Action action = confirmed;
            sounds.Select();
            Close();
            action?.Invoke();
        }

        /// <summary>
        /// What Cancel and Escape do, and what happens when the time runs out: closes the dialog, then
        /// undoes whatever was waiting for confirmation. The back cue is the only sound it makes.
        /// </summary>
        public void Cancel(bool playCue)
        {
            if (!IsOpen) return;

            Action action = cancelled;
            if (playCue) sounds.Back();
            Close();
            action?.Invoke();
        }

        /// <summary>Call every frame: keeps the countdown current and cancels when it runs out.</summary>
        public void Tick()
        {
            if (!IsOpen || deadline <= 0d) return;

            int seconds = Mathf.CeilToInt((float)(deadline - Time.realtimeSinceStartupAsDouble));
            if (seconds <= 0)
            {
                Cancel(false);
            }
            else if (seconds != secondsShown)
            {
                secondsShown = seconds;
                message.text = text(seconds);
            }
        }

        void Close()
        {
            Focusable returnTo = opener;
            IsOpen = false;
            confirmed = null;
            cancelled = null;
            text = null;
            opener = null;
            deadline = 0d;
            element.AddToClassList(HiddenClass);
            Closed?.Invoke(returnTo);
        }
    }
}
