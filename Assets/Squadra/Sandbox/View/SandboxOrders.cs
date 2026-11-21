using System.Collections.Generic;
using Squadra.Sandbox;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Squadra
{
    /// <summary>
    /// The sandbox user's side of the battlefield: which world the panel edits, which one is selected to
    /// send a fleet, which one the pointer is over and what a click means. In Inspect mode a click on any
    /// world, neutral or fought over, makes it the one the panel edits, and never sends a fleet. In Order
    /// mode clicks work as in a mission, for whichever player owns the world: select a world, then click a
    /// destination to send every Dart stationed there; a click on a world that is not a destination selects
    /// it for the panel too. A click on empty space never takes the panel's world away, and a click that
    /// sends a fleet never changes it. A click that cannot be carried out does nothing.
    /// The session decides whether an order is allowed; nothing here decides a rule.
    /// </summary>
    sealed class SandboxOrders
    {
        readonly IReadOnlyList<WorldView> worlds;
        SandboxSession session;

        public SandboxOrders(SandboxSession session, IReadOnlyList<WorldView> worlds)
        {
            this.session = session;
            this.worlds = worlds;
        }

        /// <summary>True in Order mode, where clicks send fleets; false in Inspect mode, where they only select.</summary>
        public bool OrderMode { get; private set; }

        /// <summary>In Order mode, the index of the world selected to send a fleet, or -1.</summary>
        public int Selected { get; private set; } = -1;

        /// <summary>The index of the world the panel edits, or -1 until a world is clicked.</summary>
        public int Inspected { get; private set; } = -1;

        /// <summary>The world drawn as selected: the inspected one in Inspect mode, the order's in Order mode.</summary>
        public int Outlined => OrderMode ? Selected : Inspected;

        /// <summary>The index of the world under the pointer, or -1.</summary>
        public int Hovered { get; private set; } = -1;

        /// <summary>Switches between Inspect and Order mode. An order's selection is put away; the inspected world stays.</summary>
        public void SetOrderMode(bool value)
        {
            OrderMode = value;
            Selected = -1;
        }

        /// <summary>
        /// Reads the pointer for this frame: what it is over, a cancelling right-click and a click. A pointer
        /// over the panel is over no world, and its clicks are the panel's.
        /// </summary>
        public void ReadPointer(Camera cam, bool overPanel, bool canSend)
        {
            Pointer pointer = Pointer.current;
            if (pointer == null || overPanel)
            {
                Hovered = -1;
                return;
            }

            Vector2 screen = pointer.position.ReadValue();
            Vector3 point = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            Hovered = PlayerOrders.WorldAt(worlds, point);

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame) Selected = -1;

            if (pointer.press.wasPressedThisFrame) Click(Hovered, canSend);
        }

        /// <summary>True for a world a click selects in the current mode.</summary>
        public bool IsSelectable(int index)
        {
            return !OrderMode || session.IsOrderSource(index);
        }

        /// <summary>
        /// Applies one click on a world, or on empty space when <paramref name="index"/> is negative.
        /// Without <paramref name="canSend"/>, as while paused, a click that would send a fleet does nothing.
        /// </summary>
        public void Click(int index, bool canSend)
        {
            bool world = index >= 0 && index < worlds.Count;
            if (!OrderMode)
            {
                if (world) Inspected = index;
                return;
            }

            if (!world || index == Selected)
            {
                Selected = -1;
                return;
            }

            // Nothing to send from yet, or the first world could not send there: the click selects.
            if (Selected < 0 || (session.IsOrderSource(index) && !session.CanOrder(Selected, index)))
            {
                Inspected = index;
                Selected = session.IsOrderSource(index) ? index : -1;
                return;
            }

            if (canSend && session.TryOrder(Selected, index, out _)) Selected = -1;
        }

        /// <summary>In Order mode, puts the selection away once its world can no longer give orders.</summary>
        public void DropUnusableSelection()
        {
            if (OrderMode && Selected >= 0 && !session.IsOrderSource(Selected)) Selected = -1;
        }

        /// <summary>
        /// Starts again with <paramref name="restarted"/>, as after a reset: no order selected and nothing
        /// hovered; the mode and the inspected world stay as they were.
        /// </summary>
        public void Restart(SandboxSession restarted)
        {
            session = restarted;
            Selected = -1;
            Hovered = -1;
        }
    }
}
