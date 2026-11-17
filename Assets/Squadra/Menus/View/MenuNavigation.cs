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
    /// Screens declare their layout as lines of controls (<see cref="AddLine"/>). A move from a control
    /// in a line goes along it. A move across it goes to a line the control's container is a stop of
    /// (<see cref="AddGroup"/>), or nowhere. A move from a control outside every line is left to UI
    /// Toolkit, which chooses by position.
    /// </summary>
    public sealed class MenuNavigation
    {
        const string PointerModeClass = "menu-root--pointer";
        const string KeyboardModeClass = "menu-root--keys";

        sealed class Line
        {
            public MenuAxis Axis;
            public bool Wraps;
            public Func<IReadOnlyList<VisualElement>> Stops;
        }

        readonly MenuAudio sounds;
        readonly List<Line> lines = new List<Line>();
        readonly Dictionary<VisualElement, Func<VisualElement>> groups = new Dictionary<VisualElement, Func<VisualElement>>();
        readonly HashSet<VisualElement> keepAcross = new HashSet<VisualElement>();
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
            groups.Clear();
            keepAcross.Clear();
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
            AddLine(axis, wraps, () => stops);
        }

        /// <summary>
        /// Declares a line whose controls change while the screen is used (pages, tabs). The list is
        /// asked for at each move. Controls that cannot take the focus are passed over.
        /// </summary>
        public void AddLine(MenuAxis axis, bool wraps, Func<IReadOnlyList<VisualElement>> stops)
        {
            lines.Add(new Line { Axis = axis, Wraps = wraps, Stops = stops });
        }

        /// <summary>
        /// Makes <paramref name="place"/>, a container of controls, a stop of the lines it is listed in.
        /// A move from a control inside it across the control's own line goes along those lines, and a
        /// move onto it gives the focus to <paramref name="entry"/> (none: it is passed over).
        /// </summary>
        public void AddGroup(VisualElement place, Func<VisualElement> entry)
        {
            groups[place] = entry;
        }

        /// <summary>A move across the control's line is left to the control, as a slider uses Left and Right.</summary>
        public void KeepAcross(VisualElement control)
        {
            keepAcross.Add(control);
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

            // The focused control and its containers, innermost first: the first line among them that
            // runs the way of the move takes it.
            bool inLine = false;
            bool keeps = false;
            for (var element = root.focusController?.focusedElement as VisualElement; element != null; element = element.parent)
            {
                keeps |= keepAcross.Contains(element);
                foreach (Line line in lines)
                {
                    IReadOnlyList<VisualElement> stops = line.Stops();
                    int place = IndexOf(stops, element);
                    if (place < 0) continue;

                    inLine = true;
                    int step = MenuStep.Along(line.Axis, direction);
                    if (step == 0) continue;

                    Claim(move);
                    Step(stops, place, step, line.Wraps);
                    return;
                }
            }

            if (inLine)
            {
                // Across every line of the control: nowhere, unless the control uses the move itself.
                if (!keeps) Claim(move);
                return;
            }

            if (WhenLost == null || !TakesInput) return;

            Claim(move);
            WhenLost()?.Focus();
        }

        void Step(IReadOnlyList<VisualElement> stops, int place, int step, bool wraps)
        {
            open.Clear();
            foreach (VisualElement stop in stops)
                open.Add(Target(stop)?.canGrabFocus == true);
            int next = MenuStep.Next(open, place, step, wraps);
            if (next < 0) return;

            Target(stops[next]).Focus();
            if (sounds != null) sounds.Hover();
        }

        // The control a stop gives the focus to: a group's entry, or the stop itself.
        VisualElement Target(VisualElement stop)
        {
            return groups.TryGetValue(stop, out Func<VisualElement> entry) ? entry() : stop;
        }

        static int IndexOf(IReadOnlyList<VisualElement> stops, VisualElement element)
        {
            for (int i = 0; i < stops.Count; i++)
                if (stops[i] == element) return i;
            return -1;
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
