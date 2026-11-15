using UnityEngine.UIElements;

namespace Squadra
{
    /// <summary>Moves the keyboard focus between the screens of a menu.</summary>
    static class MenuFocus
    {
        /// <summary>
        /// Gives <paramref name="target"/> the focus a moment from now. Focus only takes once the
        /// element is displayed, which is a frame after its class changes. By then the target may have
        /// been disabled or hidden again; asking such an element to take the focus would clear it
        /// altogether, so it is left where it is.
        /// </summary>
        /// <param name="scheduler">An element of the same panel, whose scheduler runs the delay.</param>
        public static void Soon(VisualElement scheduler, Focusable target)
        {
            if (target == null) return;
            scheduler.schedule.Execute(() =>
            {
                if (target.canGrabFocus) target.Focus();
            }).StartingIn(20);
        }
    }
}
