using System.Collections.Generic;
using Squadra.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Squadra
{
    /// <summary>
    /// The human player's side of giving orders: which world is selected, which one the pointer is
    /// over, what a click means and what the hint line says about it. It asks the match whether an
    /// order is allowed and gives it; it decides no rule itself.
    /// </summary>
    sealed class PlayerOrders
    {
        readonly Match match;
        readonly IReadOnlyList<WorldView> worlds;
        readonly Faction player;

        float noticeLeft;
        string notice;

        public PlayerOrders(Match match, IReadOnlyList<WorldView> worlds, Faction player)
        {
            this.match = match;
            this.worlds = worlds;
            this.player = player;
        }

        /// <summary>The selected world's index, or -1.</summary>
        public int Selected { get; private set; } = -1;

        /// <summary>The index of the world under the pointer, or -1.</summary>
        public int Hovered { get; private set; } = -1;

        /// <summary>Reads the pointer for this frame: what it is over, a cancelling right-click and a click.</summary>
        public void ReadPointer(Camera cam)
        {
            Pointer pointer = Pointer.current;
            if (pointer == null)
            {
                Hovered = -1;
                return;
            }

            Vector2 screen = pointer.position.ReadValue();
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            Hovered = WorldAt(world);

            // Escape belongs to the pause menu; a right-click, or a click on the selected world, cancels.
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame) Selected = -1;

            if (pointer.press.wasPressedThisFrame) Click(Hovered);
        }

        int WorldAt(Vector2 point)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < worlds.Count; i++)
            {
                float distance = Vector2.Distance(point, worlds[i].Position);
                if (distance <= worlds[i].PickRadius && distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>True for a world whose fleet can be ordered. Darts that are capturing or fighting are committed where they are.</summary>
        public bool IsSelectable(int index)
        {
            Node node = match.Nodes[index];
            return node.Owner == player && !node.IsBeingCaptured && !node.IsContested;
        }

        /// <summary>
        /// Applies one click on a world (or on empty space when <paramref name="index"/> is negative):
        /// select an owned world, cancel by clicking it again, or send its whole fleet to another world.
        /// </summary>
        public void Click(int index)
        {
            if (match.Status != MatchStatus.InProgress) return;

            if (index < 0 || index >= worlds.Count)
            {
                Selected = -1;
                return;
            }

            if (Selected < 0)
            {
                if (IsSelectable(index)) Selected = index;
                else if (match.Nodes[index].Owner == player && match.Nodes[index].IsContested)
                    Notify("The Darts at " + worlds[index].DisplayName + " are fighting and cannot leave");
                else Notify("Select one of your own worlds first");
                return;
            }

            if (index == Selected)
            {
                Selected = -1;
                return;
            }

            if (match.TryLaunch(player, Selected, index))
            {
                Selected = -1;
            }
            else if (IsSelectable(index))
            {
                Selected = index; // the first world could not send there: treat the click as a new selection
            }
            else if (!match.AreConnected(Selected, index))
            {
                Notify("No route from " + worlds[Selected].DisplayName + " to " + worlds[index].DisplayName);
            }
            else
            {
                Notify("No Darts stationed at " + worlds[Selected].DisplayName + " yet");
            }
        }

        /// <summary>Puts the selection away once its world can no longer be ordered, as when it comes under attack.</summary>
        public void DropUnusableSelection()
        {
            if (Selected >= 0 && !IsSelectable(Selected)) Selected = -1;
        }

        /// <summary>Forgets the selection and the hovered world, as when the match has ended.</summary>
        public void Clear()
        {
            Selected = -1;
            Hovered = -1;
        }

        /// <summary>What the hint line says now: a recent notice, or what a click would do.</summary>
        public string Hint
        {
            get
            {
                if (notice != null && noticeLeft > 0f) return notice;

                if (Selected < 0) return "Click one of your worlds to select its fleet";

                int darts = match.Nodes[Selected].Darts;
                if (darts == 0) return "No Darts stationed here yet  -  click again to cancel";
                return "Click a destination to send all " + darts + (darts == 1 ? " Dart" : " Darts") + "  -  click "
                    + worlds[Selected].DisplayName + " again to cancel";
            }
        }

        /// <summary>
        /// Lets a notice run out. Called with real seconds, and only while the match is played: a notice
        /// stays as long at every game speed and does not run out under the pause menu.
        /// </summary>
        public void CountDownNotice(float seconds)
        {
            noticeLeft -= seconds;
        }

        void Notify(string text)
        {
            notice = text;
            noticeLeft = 1.8f;
        }
    }
}
