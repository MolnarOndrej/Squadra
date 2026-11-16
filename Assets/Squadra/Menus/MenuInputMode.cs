using UnityEngine;
using UnityEngine.UIElements;

namespace Squadra
{
    /// <summary>
    /// Whether a menu is being used with the keyboard or with the pointer. The style sheet highlights
    /// the focused control in either mode; pointing at a control gives it the focus, so the highlight
    /// stays on the last control pointed at. The hover cue sounds when the pointer or a keyboard move
    /// reaches a control; in keyboard mode a control that appears under a resting pointer stays silent
    /// and does not take the focus. The main menu, the
    /// pause menu and the results screen each keep one. The screen decides which events switch the
    /// mode; this keeps the mode and shows it.
    /// </summary>
    sealed class MenuInputMode
    {
        const string PointerModeClass = "menu-root--pointer";
        const string KeyboardModeClass = "menu-root--keys";

        readonly MenuAudio sounds;
        VisualElement styledRoot;
        Vector3 lastPointer = new Vector3(float.NaN, float.NaN);

        /// <param name="sounds">The screen's interface cues; may be missing.</param>
        public MenuInputMode(MenuAudio sounds)
        {
            this.sounds = sounds;
        }

        /// <summary>True in keyboard mode.</summary>
        public bool Keyboard { get; private set; }

        /// <summary>
        /// Names the element that carries the mode's style classes. The classes are only written by
        /// the next <see cref="Set"/>, so a tree that was built anew starts out as its layout has it.
        /// </summary>
        public void Attach(VisualElement root)
        {
            styledRoot = root;
        }

        public void Set(bool keys)
        {
            Keyboard = keys;
            if (sounds != null) sounds.PointerActive = !keys;
            styledRoot.EnableInClassList(KeyboardModeClass, keys);
            styledRoot.EnableInClassList(PointerModeClass, !keys);
        }

        /// <summary>
        /// Makes a control without a hover cue take the focus when the pointer enters it, as buttons do
        /// through <see cref="MenuAudio.AttachHover"/>, so its highlight follows the pointer too.
        /// </summary>
        public void FollowPointer(VisualElement control)
        {
            control.RegisterCallback<PointerEnterEvent>(OnPointerEntered);
        }

        /// <summary>
        /// A pointer move event from the panel, trickling down. The panel also reports the pointer when
        /// the layout changes under it; only real movement counts, and that ends keyboard mode. The
        /// control under the pointer was entered while the pointer did not count, so it is entered
        /// again: it takes the focus without the pointer having to leave it first.
        /// </summary>
        public void OnPointerMoved(PointerMoveEvent moved)
        {
            if (moved.position == lastPointer) return;

            lastPointer = moved.position;
            if (!Keyboard) return;

            Set(false);
            // Enter events go to each element under the pointer, as the panel sends them, so the
            // controls' own enter handlers and their conditions decide what happens.
            for (var element = moved.target as VisualElement; element != null; element = element.parent)
            {
                using (PointerEnterEvent entered = PointerEnterEvent.GetPooled(moved))
                {
                    entered.target = element;
                    element.SendEvent(entered);
                }
            }
        }

        /// <summary>
        /// A navigation move event from the panel, bubbling up, so a move that a screen takes over for
        /// itself (the map turns pages with Left and Right) never arrives. When the move takes the focus
        /// to another control, the hover cue plays, as when the pointer reaches one. The focus only
        /// moves after the event has been handled, so it is compared a moment later.
        /// </summary>
        public void OnNavigated(NavigationMoveEvent move)
        {
            var root = (VisualElement)move.currentTarget;
            Focusable before = root.focusController?.focusedElement;
            root.schedule.Execute(() =>
            {
                Focusable after = root.focusController?.focusedElement;
                if (after != null && after != before && sounds != null) sounds.Hover();
            });
        }

        /// <summary>
        /// A pointer press event from the panel, trickling down. It ends keyboard mode. A press beside
        /// the controls would clear the focus and with it the highlight, so the focus stays where it is.
        /// </summary>
        public void OnPointerPressed(PointerDownEvent pressed)
        {
            Set(false);
            if (!PressesControl(pressed.target as VisualElement))
                ((VisualElement)pressed.currentTarget).focusController?.IgnoreEvent(pressed);
        }

        void OnPointerEntered(PointerEnterEvent entered)
        {
            var control = (VisualElement)entered.currentTarget;
            if (!Keyboard && control.enabledInHierarchy) control.Focus();
        }

        static bool PressesControl(VisualElement target)
        {
            for (VisualElement element = target; element != null; element = element.parent)
                if (element.canGrabFocus) return true;
            return false;
        }
    }
}
