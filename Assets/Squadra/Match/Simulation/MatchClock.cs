using System;

namespace Squadra.Simulation
{
    /// <summary>Formats match time for display.</summary>
    public static class MatchClock
    {
        /// <summary>
        /// Whole elapsed seconds as minutes:seconds. Minutes are not wrapped into hours, so a long
        /// match reads "75:09".
        /// </summary>
        public static string Format(double seconds)
        {
            long whole = WholeSeconds(seconds);
            return (whole / 60).ToString("00") + ":" + (whole % 60).ToString("00");
        }

        /// <summary>
        /// Completed seconds. A moment that is a rounding error short of a whole second counts as that
        /// second, because time arrives in small float steps.
        /// </summary>
        public static long WholeSeconds(double seconds)
        {
            return seconds > 0 ? (long)Math.Floor(seconds + 1e-6) : 0;
        }
    }
}
