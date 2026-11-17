using System;
using System.Collections.Generic;

namespace Squadra.Menus
{
    /// <summary>
    /// Where one keyboard move goes along a line of menu controls. A line lists its stops in order; a
    /// stop that cannot take the focus right now (disabled, hidden, a mission that cannot be played) is
    /// passed over. A line that wraps carries on from one end to the other; one that does not stops at
    /// its ends.
    /// </summary>
    public static class MenuStep
    {
        /// <summary>
        /// +1 for a move towards the end of a line (Down in a column, Right in a row), -1 towards its
        /// start, and 0 for a move across it.
        /// </summary>
        public static int Along(MenuAxis axis, MenuDirection direction)
        {
            switch (direction)
            {
                case MenuDirection.Up: return axis == MenuAxis.Column ? -1 : 0;
                case MenuDirection.Down: return axis == MenuAxis.Column ? 1 : 0;
                case MenuDirection.Left: return axis == MenuAxis.Row ? -1 : 0;
                default: return axis == MenuAxis.Row ? 1 : 0;
            }
        }

        /// <summary>
        /// Where a move along the line takes the focus, as a place in the line. Starting at
        /// <paramref name="from"/>, it goes stop by stop in the direction of <paramref name="step"/>
        /// (+1 towards the end, -1 towards the start) and lands on the first stop that can take the focus,
        /// carrying on from the other end when the line wraps. Returns -1 when the focus should stay where
        /// it is: when <paramref name="step"/> is 0 (the move runs across the line, such as Left in a
        /// column), when a line that does not wrap ends first, or when no other stop can take the focus.
        /// </summary>
        /// <param name="open">For each stop of the line, in order, whether it can take the focus now.</param>
        public static int Next(IReadOnlyList<bool> open, int from, int step, bool wraps)
        {
            if (open == null) throw new ArgumentNullException(nameof(open));
            if (from < 0 || from >= open.Count) throw new ArgumentOutOfRangeException(nameof(from));
            if (step == 0) return -1;

            int place = from;
            for (int tried = 1; tried < open.Count; tried++)
            {
                place += Math.Sign(step);
                if (place < 0 || place >= open.Count)
                {
                    if (!wraps) return -1;
                    place = (place + open.Count) % open.Count;
                }

                if (open[place]) return place;
            }

            return -1;
        }
    }
}
