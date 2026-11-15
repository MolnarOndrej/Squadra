using UnityEngine;
using UnityEngine.UIElements;

namespace Squadra
{
    /// <summary>
    /// Whether a menu is being used with the keyboard or with the pointer. The style sheet highlights
    /// hovered controls in pointer mode and the focused one in keyboard mode, and the hover cue only
    /// sounds in pointer mode. The main menu, the pause menu and the results screen each keep one.
    /// The screen decides which events switch the mode; this keeps the mode and shows it.
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
        /// A pointer move event from the panel. The panel also reports the pointer when the layout
        /// changes under it; only real movement counts, and that ends keyboard mode.
        /// </summary>
        public void OnPointerMoved(PointerMoveEvent moved)
        {
            if (moved.position == lastPointer) return;

            lastPointer = moved.position;
            if (Keyboard) Set(false);
        }
    }
}
