using System;
using System.Collections.Generic;

namespace Squadra.Preparation
{
    /// <summary>
    /// Deals choices from a pool so that a mission's worlds vary: the pool is shuffled and used up before
    /// any choice repeats, and when more are needed than the pool holds, it is shuffled again for each
    /// further round. Any two choices are therefore used a number of times that differs by at most one.
    /// </summary>
    public static class AppearanceDealer
    {
        /// <summary>
        /// <paramref name="count"/> choices from <paramref name="pool"/>, which must not be empty when any
        /// are wanted. The same pool and the same random sequence always give the same deal.
        /// </summary>
        public static List<string> Deal(IReadOnlyList<string> pool, int count, Random random)
        {
            if (pool == null) throw new ArgumentNullException(nameof(pool));
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

            var dealt = new List<string>(count);
            if (count == 0) return dealt;
            if (pool.Count == 0) throw new ArgumentException("There is nothing to deal from.", nameof(pool));

            var round = new List<string>(pool);
            while (dealt.Count < count)
            {
                Shuffle(round, random);
                for (int i = 0; i < round.Count && dealt.Count < count; i++)
                    dealt.Add(round[i]);
            }

            return dealt;
        }

        // Fisher-Yates.
        static void Shuffle(List<string> items, Random random)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }
}
