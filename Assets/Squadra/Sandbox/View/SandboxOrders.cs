using System.Collections.Generic;
using Squadra.Sandbox;
using Squadra.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Squadra
{
    /// <summary>
    /// The sandbox user's side of the battlefield: which world is selected, which one the pointer is over
    /// and what a click means. In Inspect mode a click selects any world, neutral or fought over, for the
    /// panel to edit, and never sends a fleet. In Order mode clicks work as in a mission, for whichever
    /// player owns the world: select a world, then click a destination to send every Dart stationed there.
    /// The session decides whether an order is allowed; nothing here decides a rule.
    /// </summary>
    sealed class SandboxOrders
    {
        const float NoticeSeconds = 1.8f;

        readonly IReadOnlyList<WorldView> worlds;
        SandboxSession session;
        float noticeLeft;
        string notice;

        public SandboxOrders(SandboxSession session, IReadOnlyList<WorldView> worlds)
        {
            this.session = session;
            this.worlds = worlds;
        }

        /// <summary>True in Order mode, where clicks send fleets; false in Inspect mode, where they only select.</summary>
        public bool OrderMode { get; private set; }

        /// <summary>The selected world's index, or -1.</summary>
        public int Selected { get; private set; } = -1;

        /// <summary>The index of the world under the pointer, or -1.</summary>
        public int Hovered { get; private set; } = -1;

        /// <summary>Switches between Inspect and Order mode. A selection that cannot give orders is put away.</summary>
        public void SetOrderMode(bool value)
        {
            OrderMode = value;
            DropUnusableSelection();
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
        /// Without <paramref name="canSend"/>, as while paused, a click that would send a fleet only says so.
        /// </summary>
        public void Click(int index, bool canSend)
        {
            if (index < 0 || index >= worlds.Count || index == Selected)
            {
                Selected = -1;
                return;
            }

            if (!OrderMode)
            {
                Selected = index;
                return;
            }

            if (Selected < 0)
            {
                if (session.IsOrderSource(index)) Selected = index;
                else Notify(WhyNotASource(index));
                return;
            }

            if (session.IsOrderSource(index) && !session.CanOrder(Selected, index))
            {
                Selected = index; // the first world could not send there: the click is a new selection
                return;
            }

            if (!canSend)
            {
                Notify("Paused  -  resume to send fleets");
            }
            else if (session.TryOrder(Selected, index, out string refusal))
            {
                Selected = -1;
            }
            else
            {
                Notify(refusal);
            }
        }

        string WhyNotASource(int index)
        {
            Node node = session.Match.Nodes[index];
            if (node.Owner == Faction.Neutral) return "Neutral worlds take no orders  -  select a player's world";
            if (node.IsContested) return "The Darts at " + node.Name + " are fighting and cannot leave";
            return "The Darts at " + node.Name + " are capturing it and cannot leave";
        }

        /// <summary>In Order mode, puts the selection away once its world can no longer give orders.</summary>
        public void DropUnusableSelection()
        {
            if (OrderMode && Selected >= 0 && !session.IsOrderSource(Selected)) Selected = -1;
        }

        /// <summary>
        /// Starts again with <paramref name="restarted"/>, as after a reset: nothing selected or hovered,
        /// no notice, the mode as it was.
        /// </summary>
        public void Restart(SandboxSession restarted)
        {
            session = restarted;
            Selected = -1;
            Hovered = -1;
            noticeLeft = 0f;
        }

        /// <summary>What the hint line says now: a recent notice, or what a click would do.</summary>
        public string Hint
        {
            get
            {
                if (notice != null && noticeLeft > 0f) return notice;

                if (!OrderMode)
                {
                    return Selected < 0
                        ? "Inspect mode  -  click any world to edit it"
                        : "Editing " + worlds[Selected].DisplayName + "  -  click another world, or empty space to close";
                }

                if (Selected < 0) return "Order mode  -  click any player's world to select its fleet";

                Node node = session.Match.Nodes[Selected];
                if (node.Darts == 0) return "No Darts stationed here yet  -  click again to cancel";
                return "Click a destination to send all " + node.Darts + (node.Darts == 1 ? " Dart" : " Darts")
                    + " of " + node.Owner + "  -  click " + worlds[Selected].DisplayName + " again to cancel";
            }
        }

        /// <summary>Lets a notice run out, in real seconds, so it lasts as long at every game speed.</summary>
        public void CountDownNotice(float seconds)
        {
            noticeLeft -= seconds;
        }

        /// <summary>Shows a short notice on the hint line, such as why a command was refused.</summary>
        public void Notify(string text)
        {
            notice = text;
            noticeLeft = NoticeSeconds;
        }
    }
}
