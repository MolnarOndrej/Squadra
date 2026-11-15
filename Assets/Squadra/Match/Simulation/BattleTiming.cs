using System;

namespace Squadra.Simulation
{
    /// <summary>
    /// How long a battle lasts. The duration comes from the numerical balance of the two sides and not
    /// from their size: equal forces fight for the longest time, a side outnumbered by the decisive
    /// ratio or worse falls in the shortest, and swapping the sides changes nothing.
    /// </summary>
    public sealed class BattleTiming
    {
        /// <summary>Seconds a battle lasts when one side outnumbers the other by the decisive ratio or more.</summary>
        public double ShortestDuration = 1.0;

        /// <summary>Seconds a battle between equal forces lasts.</summary>
        public double LongestDuration = 5.0;

        /// <summary>
        /// The smaller force as a fraction of the larger at which a battle is already as short as it
        /// gets. 0.1 is ten to one.
        /// </summary>
        public double DecisiveFraction = 0.1;

        /// <summary>
        /// Shapes the durations between the two ends. It maps the balance, from 0 (decisive) to 1 (equal
        /// forces), to a value in the same range. Null is a straight line.
        /// </summary>
        public Func<double, double> Curve;

        /// <summary>Seconds a battle between forces of these two sizes lasts when nobody reinforces it.</summary>
        public double Duration(int first, int second)
        {
            int smaller = Math.Min(first, second);
            int larger = Math.Max(first, second);
            if (smaller <= 0) return 0;

            double decisive = Math.Min(1, Math.Max(0, DecisiveFraction));
            double fraction = (double)smaller / larger;
            double balance = decisive >= 1 ? 1 : (fraction - decisive) / (1 - decisive);
            balance = Math.Min(1, Math.Max(0, balance));
            if (Curve != null) balance = Math.Min(1, Math.Max(0, Curve(balance)));

            double shortest = Math.Max(0, ShortestDuration);
            double longest = Math.Max(shortest, LongestDuration);
            return shortest + (longest - shortest) * balance;
        }
    }
}
