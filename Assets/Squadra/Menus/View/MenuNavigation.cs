using System;
using System.Collections.Generic;
using Squadra.Menus;
using UnityEngine;
using UnityEngine.UIElements;

namespace Squadra
{
    /// <summary>
    /// The pointer and keyboard behavior every menu screen shares, for everything under one document
    /// root. The style sheet highlights the focused control in either input mode; pointing at an enabled
    /// control gives it the focus, so the highlight stays on the last control pointed at. Any key or
    /// navigation move switches to keyboard mode, where a control that appears under a resting pointer
    /// neither takes the focus nor sounds; the first real pointer move switches back. The hover cue
    /// sounds when the player moves the highlight to another control, never when the program does.
    ///
    /// Screens declare their layout as lines of controls (<see cref="AddLine"/>); a move from a control
    /// in a line goes along it, and a move across it goes nowhere. A move from a control outside every
    /// line is left to UI Toolkit, which chooses by position.
    /// </summary>
    sealed class MenuNavigation
    {
        const string PointerModeClass = "menu-root--pointer";
        const string KeyboardModeClass = "menu-root--keys";

        sealed class Line
        {
            public MenuAxis Axis;
            public bool Wraps;
            public IReadOnlyList<VisualElement> Stops;
        }

        readonly MenuAudio sounds;
        readonly List<Line> lines = new List<Line>();
        readonly List<bool> open = new List<bool>();
        VisualElement root;
        VisualElement styledRoot;
        Vector3 lastPointer = new Vector3(float.NaN, float.NaN);

        /// <param name="sounds">The screen's interface cues; may be missing.</param>
        public MenuNavigation(MenuAudio sounds)
        {
            this.sounds = sounds;
        }

        /// <summary>True in keyboard mode.</summary>
        public bool Keyboard { get; private set; }

        /// <summary>
        /// While this returns false the screen takes no input: pointing at a control neither gives it
        /// the focus nor sounds, and a lost focus is not brought back. Unset, it always takes input.
        /// </summary>
        public Func<bool> Accepting { get; set; }

        /// <summary>
        /// Where a move takes the focus when no control of a declared line has it, so the first arrow
        /// press brings it back. Unset, such a move is left to UI Toolkit.
        /// </summary>
        public Func<Focusable> WhenLost { get; set; }

        /// <summary>
        /// Whether a press on an element may move the focus. Any other press leaves the focus where it
        /// is. Unset, a press on a control or inside one may.
        /// </summary>
        public Func<VisualElement, bool> PressMovesFocus { get; set; }

        /// <summary>
        /// Listens to the pointer and navigation events of <paramref name="eventRoot"/> and everything in
        /// it. <paramref name="modeRoot"/> carries the mode's style classes; they are only written by the
        /// next <see cref="SetKeyboard"/>, so a tree that was built anew starts out as its layout has it.
        /// Attaching to a new tree forgets the lines of the old one; the mode carries over.
        /// </summary>
        public void Attach(VisualElement eventRoot, VisualElement modeRoot)
        {
            lines.Clear();
            root = eventRoot;
            styledRoot = modeRoot;
            root.RegisterCallback<PointerMoveEvent>(OnPointerMoved, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerEnterEvent>(OnPointerEntered, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerDownEvent>(OnPointerPressed, TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationMoveEvent>(OnNavigated);
        }

        /// <summary>
        /// Declares a column or a row of controls, in order. A move along it goes to the next control
        /// that can take the focus, wrapping around at the ends when <paramref name="wraps"/> is set;
        /// a move across it goes nowhere.
        /// </summary>
        public void AddLine(MenuAxis axis, bool wraps, params VisualElement[] stops)
        {
            lines.Add(new Line { Axis = axis, Wraps = wraps, Stops = stops });
        }

        public void SetKeyboard(bool keys)
        {
            Keyboard = keys;
            styledRoot.EnableInClassList(KeyboardModeClass, keys);
            styledRoot.EnableInClassList(PointerModeClass, !keys);
        }

        /// <summary>Call every frame while the screen takes input: any key press switches to keyboard mode.</summary>
        public void NoticeKeys()
        {
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (!Keyboard && keyboard != null && keyboard.anyKey.wasPressedThisFrame) SetKeyboard(true);
        }

        // ------------------------------------------------------------------ pointer

        /// <summary>
        /// A pointer move event from the panel, trickling down. The panel also reports the pointer when
        /// the layout changes under it; only real movement counts, and that ends keyboard mode. The
        /// control under the pointer was entered while the pointer did not count, so it is pointed at
        /// now: it takes the focus without the pointer having to leave it first.
        /// </summary>
        void OnPointerMoved(PointerMoveEvent moved)
        {
            if (moved.position == lastPointer) return;

            lastPointer = moved.position;
            if (!Keyboard) return;

            SetKeyboard(false);
            Point(ControlAt(moved.target as VisualElement));
        }

        // Enter events trickle down from the root, so one listener here sees the pointer reach every
        // control.
        void OnPointerEntered(PointerEnterEvent entered)
        {
            if (!Keyboard) Point(ControlAt(entered.target as VisualElement));
        }

        /// <summary>
        /// A pointer press event from the panel, trickling down. It ends keyboard mode. A press beside
        /// the controls would clear the focus and with it the highlight, so the focus stays where it is.
        /// </summary>
        void OnPointerPressed(PointerDownEvent pressed)
        {
            SetKeyboard(false);
            var target = pressed.target as VisualElement;
            bool movesFocus = PressMovesFocus != null ? PressMovesFocus(target) : PressesControl(target);
            if (!movesFocus) root.focusController?.IgnoreEvent(pressed);
        }

        // Gives the control the focus. The cue marks the highlight moving, so a control that already
        // has it stays silent. Only buttons sound when pointed at; fields take the focus silently.
        void Point(VisualElement control)
        {
            if (control == null || !control.enabledInHierarchy || !TakesInput) return;
            if (Holds(control, root.focusController?.focusedElement as VisualElement)) return;

            if (control is Button && sounds != null) sounds.Hover();
            control.Focus();
        }

        // The control an element belongs to: the nearest one that can be focused, itself included.
        static VisualElement ControlAt(VisualElement element)
        {
            for (; element != null; element = element.parent)
                if (element.focusable) return element;
            return null;
        }

        // A field passes the focus on to its input, so the focus may sit inside the control.
        static bool Holds(VisualElement control, VisualElement focused)
        {
            return focused != null && (focused == control || control.Contains(focused));
        }

        static bool PressesControl(VisualElement target)
        {
            for (VisualElement element = target; element != null; element = element.parent)
                if (element.canGrabFocus) return true;
            return false;
        }

        // ------------------------------------------------------------------ keyboard

        // A navigation move event from the panel, trickling down, so it arrives before the screens' own
        // handlers. A move from a control in a declared line is taken over here: the panel's own choice
        // goes by position. Any other move is left alone.
        void OnNavigationMove(NavigationMoveEvent move)
        {
            SetKeyboard(true);
            if (!DirectionOf(move.direction, out MenuDirection direction)) return;
            if (!Find(root.focusController?.focusedElement as VisualElement, out Line line, out int place))
            {
                if (WhenLost == null || !TakesInput) return;

                Claim(move);
                WhenLost()?.Focus();
                return;
            }

            Claim(move);

            open.Clear();
            foreach (VisualElement stop in line.Stops)
                open.Add(stop.canGrabFocus);
            int next = MenuStep.Next(open, place, MenuStep.Along(line.Axis, direction), line.Wraps);
            if (next < 0) return;

            line.Stops[next].Focus();
            if (sounds != null) sounds.Hover();
        }

        void Claim(NavigationMoveEvent move)
        {
            move.StopPropagation();
            root.focusController?.IgnoreEvent(move);
        }

        bool TakesInput => Accepting == null || Accepting();

        /// <summary>
        /// A navigation move event from the panel, bubbling up, so a move that a line or a screen takes
        /// over for itself never arrives. When the move takes the focus to another control, the hover
        /// cue plays, as when the pointer reaches one. The focus only moves after the event has been
        /// handled, so it is compared a moment later.
        /// </summary>
        void OnNavigated(NavigationMoveEvent move)
        {
            Focusable before = root.focusController?.focusedElement;
            root.schedule.Execute(() =>
            {
                Focusable after = root.focusController?.focusedElement;
                if (after != null && after != before && sounds != null) sounds.Hover();
            });
        }

        // The line and place of the focused control: the first of its ancestors, itself included, that
        // is a stop of a declared line.
        bool Find(VisualElement focused, out Line found, out int place)
        {
            for (VisualElement element = focused; element != null; element = element.parent)
            {
                foreach (Line line in lines)
                {
                    for (int i = 0; i < line.Stops.Count; i++)
                    {
                        if (line.Stops[i] != element) continue;

                        found = line;
                        place = i;
                        return true;
                    }
                }
            }

            found = null;
            place = -1;
            return false;
        }

        static bool DirectionOf(NavigationMoveEvent.Direction move, out MenuDirection direction)
        {
            switch (move)
            {
                case NavigationMoveEvent.Direction.Up:
                    direction = MenuDirection.Up;
                    return true;
                case NavigationMoveEvent.Direction.Down:
                    direction = MenuDirection.Down;
                    return true;
                case NavigationMoveEvent.Direction.Left:
                    direction = MenuDirection.Left;
                    return true;
                case NavigationMoveEvent.Direction.Right:
                    direction = MenuDirection.Right;
                    return true;
                default:
                    direction = MenuDirection.Up;
                    return false;
            }
        }
    }
}
