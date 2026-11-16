using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Squadra.Preparation;

namespace Squadra.Tests
{
    /// <summary>
    /// How world surfaces and names are dealt from a pool. The tests hold the invariants over many random
    /// sequences rather than one particular shuffle.
    /// </summary>
    public class AppearanceDealerTests
    {
        static readonly string[] Five = { "a", "b", "c", "d", "e" };

        static IEnumerable<int> Seeds => Enumerable.Range(0, 200);

        [Test]
        public void A_pool_larger_than_the_demand_never_repeats()
        {
            foreach (int seed in Seeds)
            {
                List<string> dealt = AppearanceDealer.Deal(Five, 3, new Random(seed));

                Assert.AreEqual(3, dealt.Count);
                Assert.AreEqual(3, dealt.Distinct().Count(), "seed " + seed);
                Assert.IsTrue(dealt.All(Five.Contains));
            }
        }

        [Test]
        public void A_pool_as_large_as_the_demand_uses_every_choice_once()
        {
            foreach (int seed in Seeds)
                CollectionAssert.AreEquivalent(Five, AppearanceDealer.Deal(Five, 5, new Random(seed)), "seed " + seed);
        }

        [Test]
        public void A_pool_smaller_than_the_demand_is_used_up_before_each_repeat_and_stays_balanced()
        {
            foreach (int seed in Seeds)
            {
                List<string> dealt = AppearanceDealer.Deal(Five, 13, new Random(seed));

                Assert.AreEqual(13, dealt.Count);
                for (int round = 0; round * 5 < dealt.Count; round++)
                {
                    List<string> part = dealt.Skip(round * 5).Take(5).ToList();
                    Assert.AreEqual(part.Count, part.Distinct().Count(), "seed " + seed + ", round " + round);
                }

                int[] uses = Five.Select(choice => dealt.Count(d => d == choice)).ToArray();
                Assert.LessOrEqual(uses.Max() - uses.Min(), 1, "seed " + seed);
            }
        }

        [Test]
        public void A_single_choice_pool_gives_that_choice_every_time()
        {
            CollectionAssert.AreEqual(new[] { "only", "only", "only" },
                AppearanceDealer.Deal(new[] { "only" }, 3, new Random(1)));
        }

        [Test]
        public void Nothing_wanted_needs_no_pool_but_something_wanted_does()
        {
            CollectionAssert.IsEmpty(AppearanceDealer.Deal(Array.Empty<string>(), 0, new Random(1)));
            Assert.Throws<ArgumentException>(() => AppearanceDealer.Deal(Array.Empty<string>(), 1, new Random(1)));
        }

        [Test]
        public void The_same_random_sequence_gives_the_same_deal()
        {
            CollectionAssert.AreEqual(AppearanceDealer.Deal(Five, 12, new Random(42)),
                AppearanceDealer.Deal(Five, 12, new Random(42)));
        }

        [Test]
        public void A_pool_does_not_depend_on_the_order_its_content_was_found_in()
        {
            var found = new WorldPool(new[] { "ember", "thalassa", "ember", " " }, new[] { "Vesta", "Ares" });
            var foundOtherwise = new WorldPool(new[] { "thalassa", "ember" }, new[] { "Ares", "Vesta", "" });

            CollectionAssert.AreEqual(new[] { "ember", "thalassa" }, found.VariantIds);
            CollectionAssert.AreEqual(found.VariantIds, foundOtherwise.VariantIds);
            CollectionAssert.AreEqual(found.Names, foundOtherwise.Names);
            CollectionAssert.AreEqual(AppearanceDealer.Deal(found.VariantIds, 7, new Random(3)),
                AppearanceDealer.Deal(foundOtherwise.VariantIds, 7, new Random(3)));
        }

        [Test]
        public void A_name_dictionary_has_one_name_per_line()
        {
            CollectionAssert.AreEqual(new[] { "Thalassa", "New Ember", "Lune" },
                WorldPool.ReadNames("Thalassa\r\n  New Ember \n\n\r\nLune"));
            CollectionAssert.IsEmpty(WorldPool.ReadNames(""));
        }
    }
}
