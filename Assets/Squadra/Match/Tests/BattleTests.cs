using System;
using System.Collections.Generic;
using NUnit.Framework;
using Squadra.Simulation;

namespace Squadra.Tests
{
    /// <summary>
    /// Exercises battles through the public match boundary: fleet orders and time advancement in; live
    /// Dart counts, battle state, ownership, capture and production, order validity and totals out.
    /// Fleet speed is 2, so a fleet covers two map units a second.
    /// </summary>
    public class BattleTests
    {
        const int Source = 0;
        const int Target = 1;

        static Match Custom(params NodeConfig[] nodes)
        {
            var config = new MatchConfig { FleetSpeed = 2 };
            config.Nodes.AddRange(nodes);
            return new Match(config);
        }

        static NodeConfig Planet(float x, Faction owner, int darts) =>
            NodeConfig.Planet("Planet", new MapPoint(x, 0), owner, darts);

        static NodeConfig MoonAt(float x, Faction owner = Faction.Neutral, int darts = 0) =>
            NodeConfig.Moon("Moon", new MapPoint(x, 0), owner, darts);

        /// <summary>A planet that never produces, so battles can be set up with exact counts.</summary>
        static NodeConfig Idle(float x, Faction owner, int darts)
        {
            NodeConfig node = Planet(x, owner, darts);
            node.ProductionInterval = 0;
            return node;
        }

        /// <summary>
        /// Blue attacks Red's planet one second away and the battle has just begun. A distant Red
        /// planet keeps the match going whatever happens.
        /// </summary>
        static Match Duel(int attackers, int defenders)
        {
            Match match = Custom(Idle(0, Faction.Blue, attackers), Idle(2, Faction.Red, defenders),
                Idle(500, Faction.Red, 1));
            Assert.IsTrue(match.TryLaunch(Faction.Blue, Source, Target));
            match.Advance(1);
            return match;
        }

        static double Seconds(int first, int second) => new BattleTiming().Duration(first, second);

        static List<BattleReport> Reports(Match match)
        {
            var reports = new List<BattleReport>();
            match.BattleEnded += reports.Add;
            return reports;
        }

        static void AssertSides(Match match, int node, int arriving, int stationed)
        {
            Battle battle = match.Nodes[node].Battle;
            Assert.IsNotNull(battle, "a battle is under way");
            Assert.AreEqual(arriving, battle.ArrivingDarts, "arriving side");
            Assert.AreEqual(stationed, battle.StationedDarts, "stationed side");
            Assert.AreEqual(stationed, match.Nodes[node].Darts, "the object's own count is its stationed side");
        }

        // ------------------------------------------------------------ duration

        [Test]
        public void Equal_forces_fight_for_five_seconds_whatever_their_size()
        {
            Assert.AreEqual(5.0, Seconds(1, 1), 1e-12);
            Assert.AreEqual(5.0, Seconds(10, 10), 1e-12);
            Assert.AreEqual(5.0, Seconds(100, 100), 1e-12);
        }

        [Test]
        public void Ten_to_one_or_worse_is_over_in_one_second()
        {
            Assert.AreEqual(1.0, Seconds(100, 10), 1e-12);
            Assert.AreEqual(1.0, Seconds(10, 1), 1e-12);
            Assert.AreEqual(1.0, Seconds(1000, 10), 1e-12, "beyond ten to one");
            Assert.AreEqual(1.0, Seconds(500, 1), 1e-12);
        }

        [Test]
        public void Duration_does_not_depend_on_which_side_is_larger()
        {
            foreach (int[] sides in new[] { new[] { 100, 10 }, new[] { 30, 20 }, new[] { 7, 3 }, new[] { 1000, 1 } })
                Assert.AreEqual(Seconds(sides[0], sides[1]), Seconds(sides[1], sides[0]), 1e-12);
        }

        [Test]
        public void Duration_rises_smoothly_from_decisive_to_balanced()
        {
            // The initial curve is a straight line over the smaller force as a fraction of the larger.
            Assert.AreEqual(1 + 4 * (0.5 - 0.1) / 0.9, Seconds(20, 10), 1e-9);
            Assert.AreEqual(Seconds(20, 10), Seconds(200, 100), 1e-9, "the ratio decides, not the size");

            double previous = Seconds(100, 10);
            for (int smaller = 11; smaller <= 100; smaller++)
            {
                double duration = Seconds(100, smaller);
                Assert.Greater(duration, previous, "100 against " + smaller);
                Assert.LessOrEqual(duration, 5.0);
                previous = duration;
            }
        }

        [Test]
        public void The_ends_and_the_curve_of_battle_duration_are_configuration()
        {
            var timing = new BattleTiming { ShortestDuration = 2, LongestDuration = 8, DecisiveFraction = 0.25 };
            Assert.AreEqual(2.0, timing.Duration(8, 2), 1e-12);
            Assert.AreEqual(2.0, timing.Duration(2, 80), 1e-12);
            Assert.AreEqual(8.0, timing.Duration(6, 6), 1e-12);
            Assert.AreEqual(5.0, timing.Duration(8, 5), 1e-12, "halfway between the ends");

            timing.Curve = balance => balance * balance;
            Assert.AreEqual(2.0, timing.Duration(8, 2), 1e-12);
            Assert.AreEqual(8.0, timing.Duration(6, 6), 1e-12);
            Assert.AreEqual(3.5, timing.Duration(8, 5), 1e-12);
            Assert.AreEqual(timing.Duration(8, 5), timing.Duration(5, 8), 1e-12);
        }

        [Test]
        public void A_match_fights_with_the_timing_it_was_configured_with()
        {
            var config = new MatchConfig { FleetSpeed = 2 };
            config.BattleTiming.ShortestDuration = 3;
            config.BattleTiming.LongestDuration = 3;
            config.Nodes.Add(Idle(0, Faction.Blue, 40));
            config.Nodes.Add(Idle(2, Faction.Red, 2));
            config.Nodes.Add(Idle(500, Faction.Red, 1));
            var match = new Match(config);

            match.TryLaunch(Faction.Blue, Source, Target);
            match.Advance(1);
            Assert.AreEqual(3.0, match.Nodes[Target].Battle.Remaining, 1e-9);

            match.Advance(2.9);
            Assert.IsTrue(match.Nodes[Target].IsContested);
            match.Advance(0.1);
            Assert.IsFalse(match.Nodes[Target].IsContested);
            Assert.AreEqual(38, match.Nodes[Target].Darts);
        }

        // ------------------------------------------------------------ starting a battle

        [Test]
        public void A_hostile_arrival_at_a_defended_object_starts_a_battle_and_nobody_has_lost_a_Dart()
        {
            Match match = Custom(Idle(0, Faction.Blue, 30), Idle(2, Faction.Red, 20), Idle(500, Faction.Red, 1));
            var started = new List<Battle>();
            var arrivals = new List<ArrivalReport>();
            var reports = Reports(match);
            match.BattleStarted += started.Add;
            match.FleetArrived += arrivals.Add;

            match.TryLaunch(Faction.Blue, Source, Target);
            match.Advance(1);

            Node target = match.Nodes[Target];
            Assert.IsTrue(target.IsContested);
            Assert.AreEqual(1, match.Battles.Count);
            Assert.AreSame(target.Battle, match.Battles[0]);
            Assert.AreEqual(1, started.Count);
            Assert.AreSame(target.Battle, started[0]);

            Battle battle = target.Battle;
            Assert.AreEqual(Target, battle.NodeId);
            Assert.AreEqual(Faction.Blue, battle.Arriving);
            Assert.AreEqual(Faction.Red, battle.Stationed);
            Assert.AreEqual(30, battle.ArrivingDarts);
            Assert.AreEqual(20, battle.StationedDarts);
            Assert.AreEqual(0, battle.LossesPerSide);
            Assert.IsFalse(battle.IsOver);
            Assert.AreEqual(Seconds(30, 20), battle.Remaining, 1e-9);

            Assert.AreEqual(20, target.Darts);
            Assert.AreEqual(Faction.Red, target.Owner);
            Assert.IsFalse(target.IsBeingCaptured, "nothing is decided at the start");
            Assert.AreEqual(0, match.Fleets.Count, "the fleet is no longer in transit");
            Assert.AreEqual(30, match.TotalDarts(Faction.Blue), "and none of it has gone missing");
            Assert.AreEqual(0, match.DartsLost(Faction.Blue));
            Assert.AreEqual(0, match.DartsLost(Faction.Red));
            Assert.AreEqual(0, reports.Count);

            Assert.AreEqual(1, arrivals.Count);
            Assert.AreEqual(ArrivalOutcome.BattleStarted, arrivals[0].Outcome);
            Assert.IsTrue(arrivals[0].EnteredBattle);
            Assert.AreEqual(30, arrivals[0].ArrivingDarts);
            Assert.AreEqual(20, arrivals[0].StationedDarts);
        }

        [Test]
        public void A_Dart_produced_at_the_moment_of_arrival_fights()
        {
            // Red's planet makes its tenth Dart at t=1, the moment Blue arrives.
            Match match = Custom(Idle(0, Faction.Blue, 30), Planet(2, Faction.Red, 9), Idle(500, Faction.Red, 1));

            match.TryLaunch(Faction.Blue, Source, Target);
            match.Advance(1);

            AssertSides(match, Target, 30, 10);
        }

        [Test]
        public void A_capture_that_completes_at_the_moment_of_arrival_completes_first()
        {
            // Blue lands on the moon at t=1 and owns it at t=6, the moment Red arrives.
            Match match = Custom(Idle(0, Faction.Blue, 8), MoonAt(2), Idle(14, Faction.Red, 3));

            match.TryLaunch(Faction.Blue, 0, 1);
            match.TryLaunch(Faction.Red, 2, 1); // 12 units: 6 s
            match.Advance(6);

            Node moon = match.Nodes[1];
            Assert.AreEqual(Faction.Blue, moon.Owner);
            Assert.IsFalse(moon.IsBeingCaptured);
            Assert.IsTrue(moon.IsContested);
            Assert.AreEqual(Faction.Blue, moon.Battle.Stationed);
            AssertSides(match, 1, 3, 8);
        }

        // ------------------------------------------------------------ casualties

        [Test]
        public void A_hundred_against_ten_loses_ten_pairs_over_one_second_and_ninety_attackers_begin_capture()
        {
            Match match = Duel(100, 10);
            var reports = Reports(match);

            match.Advance(0.05);
            AssertSides(match, Target, 100, 10); // no losses before the first pair

            match.Advance(0.05);
            AssertSides(match, Target, 99, 9);

            match.Advance(0.45); // 0.55 s in
            AssertSides(match, Target, 95, 5);
            Assert.AreEqual(5, match.Nodes[Target].Battle.LossesPerSide);
            Assert.AreEqual(0, reports.Count);

            match.Advance(0.45); // the tenth pair falls at one second
            Node target = match.Nodes[Target];
            Assert.IsFalse(target.IsContested);
            Assert.AreEqual(0, match.Battles.Count);
            Assert.AreEqual(90, target.Darts);
            Assert.AreEqual(Faction.Blue, target.Captor);
            Assert.AreEqual(Faction.Red, target.Owner);
            Assert.AreEqual(0.0, target.CaptureElapsed, 1e-9);

            Assert.AreEqual(1, reports.Count);
            Assert.AreEqual(BattleOutcome.AttackWon, reports[0].Outcome);
            Assert.AreEqual(Faction.Blue, reports[0].Winner);
            Assert.AreEqual(10, reports[0].LossesPerSide);
            Assert.AreEqual(90, reports[0].Survivors);
            Assert.AreEqual(1.0, reports[0].Duration, 1e-9);
        }

        [Test]
        public void Ten_against_a_hundred_leaves_ninety_defenders_in_control()
        {
            Match match = Duel(10, 100);
            var reports = Reports(match);

            match.Advance(0.5);
            AssertSides(match, Target, 5, 95);

            match.Advance(0.5);
            Node target = match.Nodes[Target];
            Assert.IsFalse(target.IsContested);
            Assert.AreEqual(90, target.Darts);
            Assert.AreEqual(Faction.Red, target.Owner);
            Assert.IsFalse(target.IsBeingCaptured);
            Assert.AreEqual(BattleOutcome.Repelled, reports[0].Outcome);
            Assert.AreEqual(Faction.Red, reports[0].Winner);
            Assert.AreEqual(0, match.TotalDarts(Faction.Blue));
        }

        [Test]
        public void Equal_forces_destroy_each_other_at_the_end_of_five_seconds([Values(10, 100)] int size)
        {
            Match match = Duel(size, size);
            var reports = Reports(match);

            match.Advance(2.5);
            AssertSides(match, Target, size / 2, size / 2);

            match.Advance(2.49);
            AssertSides(match, Target, 1, 1);

            match.Advance(0.01);
            Node target = match.Nodes[Target];
            Assert.IsFalse(target.IsContested);
            Assert.AreEqual(0, target.Darts);
            Assert.AreEqual(Faction.Red, target.Owner, "a tie leaves formal ownership alone");
            Assert.IsFalse(target.IsBeingCaptured);
            Assert.AreEqual(BattleOutcome.Tie, reports[0].Outcome);
            Assert.AreEqual(Faction.Neutral, reports[0].Winner);
            Assert.AreEqual(size, reports[0].LossesPerSide);
            Assert.AreEqual(5.0, reports[0].Duration, 1e-9);
        }

        [Test]
        public void One_against_one_lasts_five_seconds_with_its_only_pair_at_the_end()
        {
            Match match = Duel(1, 1);

            match.Advance(4.99);
            AssertSides(match, Target, 1, 1);
            Assert.AreEqual(0, match.DartsLost(Faction.Blue));

            match.Advance(0.01);
            Assert.IsFalse(match.Nodes[Target].IsContested);
            Assert.AreEqual(0, match.Nodes[Target].Darts);
            Assert.AreEqual(1, match.DartsLost(Faction.Blue));
            Assert.AreEqual(1, match.DartsLost(Faction.Red));
        }

        [Test]
        public void Pairs_are_spread_evenly_and_the_schedule_does_not_shift_as_the_ratio_changes()
        {
            // Thirty against twenty: twenty pairs over one fixed duration, although the ratio keeps
            // growing in the attackers' favor as the pairs fall.
            Match match = Duel(30, 20);
            double duration = Seconds(30, 20);
            double interval = duration / 20;
            Assert.AreEqual(duration, match.Nodes[Target].Battle.Remaining, 1e-9);

            match.Advance(interval * 0.5);
            AssertSides(match, Target, 30, 20);

            match.Advance(interval * 4); // 4.5 intervals in
            AssertSides(match, Target, 26, 16);

            match.Advance(interval * 10); // 14.5
            AssertSides(match, Target, 16, 6);
            Assert.AreEqual(interval * 5.5, match.Nodes[Target].Battle.Remaining, 1e-9);

            match.Advance(interval * 5); // 19.5: one pair to go, although it is now eleven against one
            AssertSides(match, Target, 11, 1);

            match.Advance(interval * 0.5);
            Assert.IsFalse(match.Nodes[Target].IsContested);
            Assert.AreEqual(10, match.Nodes[Target].Darts);
        }

        [Test]
        public void Every_casualty_step_is_announced_with_the_counts_after_it()
        {
            Match match = Custom(Idle(0, Faction.Blue, 6), Idle(2, Faction.Red, 4), Idle(500, Faction.Red, 1));
            var steps = new List<string>();
            match.BattleCasualties += (battle, pairs) =>
                steps.Add(pairs + ":" + battle.ArrivingDarts + "v" + battle.StationedDarts);
            match.BattleEnded += report => steps.Add("end:" + report.Outcome);

            match.TryLaunch(Faction.Blue, Source, Target);
            match.Advance(20);

            CollectionAssert.AreEqual(new[] { "1:5v3", "1:4v2", "1:3v1", "1:2v0", "end:AttackWon" }, steps);
        }

        // ------------------------------------------------------------ frame steps

        struct Snapshot
        {
            public string Text;
            public double Elapsed;
            public double[] Clocks;
        }

        static Snapshot Capture(Match match)
        {
            var text = new System.Text.StringBuilder();
            var clocks = new List<double>();
            foreach (Node node in match.Nodes)
            {
                text.Append(node.Id).Append(':').Append(node.Owner).Append('/').Append(node.Captor).Append('/')
                    .Append(node.Darts).Append(node.IsContested ? "/fighting " : "/quiet ");
                clocks.Add(node.CaptureElapsed);
                clocks.Add(node.ProductionProgress);
                if (node.IsContested)
                {
                    text.Append(node.Battle.Arriving).Append(node.Battle.ArrivingDarts).Append('v')
                        .Append(node.Battle.Stationed).Append(node.Battle.StationedDarts).Append(' ');
                    clocks.Add(node.Battle.Remaining);
                }
            }

            foreach (MatchPlayer player in match.Players)
            {
                text.Append(player.Faction).Append(' ').Append(match.DartsDestroyed(player.Faction)).Append('/')
                    .Append(match.DartsLost(player.Faction)).Append('/').Append(match.TotalDarts(player.Faction))
                    .Append(' ');
            }

            text.Append(match.Fleets.Count).Append(" fleets ").Append(match.Status);
            return new Snapshot { Text = text.ToString(), Elapsed = match.Elapsed, Clocks = clocks.ToArray() };
        }

        // Production, a battle, a reinforcement on each side, the capture that follows and a second
        // battle at the far planet, all inside a few seconds.
        static Match Busy()
        {
            Match match = Custom(
                Planet(0, Faction.Blue, 12),   // 0: keeps producing
                Planet(2, Faction.Red, 9),     // 1: attacked at t=1
                Idle(-3, Faction.Blue, 6),     // 2: reinforces the attack at t=2.5
                Idle(7, Faction.Red, 4),       // 3: reinforces the defense at t=2.5
                Idle(40, Faction.Red, 30),     // 4: keeps the match going
                MoonAt(1, Faction.Neutral, 3)); // 5: guarded
            match.TryLaunch(Faction.Blue, 0, 1);
            match.TryLaunch(Faction.Blue, 2, 1);
            match.TryLaunch(Faction.Red, 3, 1);
            return match;
        }

        [Test]
        public void One_long_advance_and_many_frame_sized_ones_give_the_same_match(
            [Values(0.9, 2.5, 3.7, 6.0, 14.0)] double seconds)
        {
            Match whole = Busy();
            Match framed = Busy();
            Match uneven = Busy();

            whole.Advance(seconds);

            int frames = (int)Math.Round(seconds * 60);
            for (int i = 0; i < frames; i++) framed.Advance(seconds / frames);

            double left = seconds;
            for (int i = 0; left > 0; i++)
            {
                double step = Math.Min(left, 0.013 + 0.11 * (i % 7));
                uneven.Advance(step);
                left -= step;
            }

            Snapshot expected = Capture(whole);
            foreach (Match other in new[] { framed, uneven })
            {
                Snapshot actual = Capture(other);
                Assert.AreEqual(expected.Text, actual.Text);
                Assert.AreEqual(expected.Elapsed, actual.Elapsed, 1e-6);
                Assert.AreEqual(expected.Clocks.Length, actual.Clocks.Length);
                for (int i = 0; i < expected.Clocks.Length; i++)
                    Assert.AreEqual(expected.Clocks[i], actual.Clocks[i], 1e-5, "clock " + i);
            }
        }

        [Test]
        public void An_advance_across_the_end_of_a_battle_carries_on_into_the_capture_that_follows()
        {
            // Ten to one: the battle takes one second, and the capture starts the moment it ends.
            Match match = Duel(20, 2);

            match.Advance(3.5);

            Node target = match.Nodes[Target];
            Assert.IsFalse(target.IsContested);
            Assert.AreEqual(18, target.Darts);
            Assert.AreEqual(Faction.Blue, target.Captor);
            Assert.AreEqual(2.5, target.CaptureElapsed, 1e-6, "no extra delay after the battle");

            match.Advance(2.5);
            Assert.AreEqual(Faction.Blue, target.Owner);
        }

        // ------------------------------------------------------------ reinforcements

        // Ten against ten from t=1: a pair every half second, at 1.5, 2.0, 2.5 and so on.
        static Match Reinforceable(float blueReserveAt, int blueReserve, float redReserveAt, int redReserve)
        {
            Match match = Custom(Idle(0, Faction.Blue, 10), Idle(2, Faction.Red, 10),
                Idle(blueReserveAt, Faction.Blue, blueReserve), Idle(redReserveAt, Faction.Red, redReserve),
                Idle(500, Faction.Red, 1));
            match.TryLaunch(Faction.Blue, 0, 1);
            return match;
        }

        [Test]
        public void Reinforcing_the_attackers_adds_to_their_count_at_once_and_keeps_every_loss()
        {
            Match match = Reinforceable(-4.5f, 5, 100, 1);
            var arrivals = new List<ArrivalReport>();
            var reports = Reports(match);
            match.FleetArrived += arrivals.Add;

            match.TryLaunch(Faction.Blue, 2, 1); // 6.5 units: arrives at t=3.25, between two pairs
            match.Advance(3.2);
            AssertSides(match, 1, 6, 6);

            match.Advance(0.05);
            AssertSides(match, 1, 11, 6);
            Battle battle = match.Nodes[1].Battle;
            Assert.AreEqual(4, battle.LossesPerSide, "the losses so far stay lost");
            Assert.AreEqual(4, match.DartsLost(Faction.Blue));
            Assert.AreEqual(ArrivalOutcome.BattleJoined, arrivals[1].Outcome);
            Assert.AreEqual(1, match.Battles.Count, "the same battle carries on");
            Assert.AreEqual(Faction.Red, match.Nodes[1].Owner);
            Assert.IsFalse(match.Nodes[1].IsBeingCaptured, "joining a battle grants nothing");

            // A fresh duration for eleven against six, counted from the reinforcement.
            double rest = Seconds(11, 6);
            Assert.AreEqual(rest, battle.Remaining, 1e-9);
            Assert.Greater(2.25 + rest, 5.0, "the battle as a whole now lasts longer than five seconds");

            match.Advance(rest - 0.01);
            AssertSides(match, 1, 6, 1);

            match.Advance(0.01);
            Assert.IsFalse(match.Nodes[1].IsContested);
            Assert.AreEqual(5, match.Nodes[1].Darts);
            Assert.AreEqual(Faction.Blue, match.Nodes[1].Captor);
            Assert.AreEqual(1, reports.Count);
            Assert.AreEqual(BattleOutcome.AttackWon, reports[0].Outcome);
            Assert.AreEqual(10, reports[0].LossesPerSide);
            Assert.AreEqual(2.25 + rest, reports[0].Duration, 1e-6);
        }

        [Test]
        public void Reinforcing_the_defenders_adds_to_their_count_at_once_and_keeps_every_loss()
        {
            Match match = Reinforceable(-100, 1, 8.5f, 5);
            var reports = Reports(match);

            match.TryLaunch(Faction.Red, 3, 1); // 6.5 units: arrives at t=3.25
            match.Advance(3.25);

            AssertSides(match, 1, 6, 11);
            Battle battle = match.Nodes[1].Battle;
            Assert.AreEqual(4, battle.LossesPerSide);
            Assert.AreEqual(Seconds(6, 11), battle.Remaining, 1e-9);

            match.Advance(battle.Remaining);
            Assert.IsFalse(match.Nodes[1].IsContested);
            Assert.AreEqual(5, match.Nodes[1].Darts);
            Assert.AreEqual(Faction.Red, match.Nodes[1].Owner);
            Assert.IsFalse(match.Nodes[1].IsBeingCaptured);
            Assert.AreEqual(BattleOutcome.Repelled, reports[0].Outcome);
            Assert.AreEqual(10, match.DartsLost(Faction.Blue));
            Assert.AreEqual(15, match.DartsDestroyed(Faction.Red) + match.Nodes[1].Darts);
        }

        [Test]
        public void Reinforcements_for_both_sides_at_the_same_moment_are_counted_together()
        {
            Match match = Reinforceable(-4.5f, 5, 8.5f, 9);

            match.TryLaunch(Faction.Blue, 2, 1); // both arrive at t=3.25
            match.TryLaunch(Faction.Red, 3, 1);
            match.Advance(3.25);

            AssertSides(match, 1, 11, 15);
            Battle battle = match.Nodes[1].Battle;
            Assert.AreEqual(4, battle.LossesPerSide);

            // One schedule for the forces after both arrivals, not one per arrival.
            Assert.AreEqual(Seconds(11, 15), battle.Remaining, 1e-9);

            match.Advance(battle.Remaining);
            Assert.IsFalse(match.Nodes[1].IsContested);
            Assert.AreEqual(4, match.Nodes[1].Darts);
            Assert.AreEqual(Faction.Red, match.Nodes[1].Owner);
        }

        [Test]
        public void A_reinforcement_at_the_moment_a_pair_is_due_neither_delays_nor_cancels_that_pair()
        {
            Match match = Reinforceable(-4, 5, 100, 1);

            match.TryLaunch(Faction.Blue, 2, 1); // 6 units: arrives at t=3, when the fourth pair falls
            match.Advance(2.99);
            AssertSides(match, 1, 7, 7);

            match.Advance(0.01);

            // The arrival is counted first, then the pair that was due: 7 + 5 - 1 against 7 - 1.
            AssertSides(match, 1, 11, 6);
            Battle battle = match.Nodes[1].Battle;
            Assert.AreEqual(4, battle.LossesPerSide);
            Assert.AreEqual(Seconds(11, 6), battle.Remaining, 1e-9, "the rest is scheduled from the surviving counts");

            match.Advance(battle.Remaining);
            Assert.AreEqual(5, match.Nodes[1].Darts);
            Assert.AreEqual(Faction.Blue, match.Nodes[1].Captor);
        }

        [Test]
        public void A_reinforcement_at_the_last_pair_turns_a_tie_into_a_win_for_its_side()
        {
            // Without help the tenth pair would end the battle in a tie at t=6.
            Match match = Reinforceable(-10, 4, 100, 1);
            var reports = Reports(match);

            match.TryLaunch(Faction.Blue, 2, 1); // 12 units: arrives at t=6
            match.Advance(5.99);
            AssertSides(match, 1, 1, 1);
            Assert.AreEqual(0, reports.Count);

            match.Advance(0.01);

            Node target = match.Nodes[1];
            Assert.IsFalse(target.IsContested, "the pair that was due ended the battle");
            Assert.AreEqual(4, target.Darts);
            Assert.AreEqual(Faction.Blue, target.Captor);
            Assert.AreEqual(1, reports.Count, "one outcome, and not a tie first");
            Assert.AreEqual(BattleOutcome.AttackWon, reports[0].Outcome);
            Assert.AreEqual(10, reports[0].LossesPerSide);
            Assert.AreEqual(4, reports[0].Survivors);
        }

        [Test]
        public void Reinforcements_for_both_sides_at_the_last_pair_keep_the_battle_going()
        {
            Match match = Reinforceable(-10, 4, 14, 2);
            var reports = Reports(match);

            match.TryLaunch(Faction.Blue, 2, 1); // both arrive at t=6
            match.TryLaunch(Faction.Red, 3, 1);
            match.Advance(6);

            // 1 + 4 against 1 + 2, then the pair that was due.
            AssertSides(match, 1, 4, 2);
            Assert.AreEqual(0, reports.Count, "nobody has run out, so there is no outcome yet");
            Battle battle = match.Nodes[1].Battle;
            Assert.AreEqual(10, battle.LossesPerSide);
            Assert.AreEqual(Seconds(4, 2), battle.Remaining, 1e-9);

            match.Advance(battle.Remaining);
            Assert.AreEqual(2, match.Nodes[1].Darts);
            Assert.AreEqual(Faction.Blue, match.Nodes[1].Captor);
            Assert.AreEqual(BattleOutcome.AttackWon, reports[0].Outcome);
            Assert.AreEqual(12, reports[0].LossesPerSide);
        }

        [Test]
        public void A_second_fleet_arriving_with_the_first_joins_the_battle_it_starts()
        {
            // Two Blue fleets reach Red's planet at the same moment. The one launched first starts the
            // battle and the other joins it; the schedule is for both together.
            Match match = Custom(Idle(0, Faction.Blue, 6), Idle(4, Faction.Blue, 4), Idle(2, Faction.Red, 10),
                Idle(500, Faction.Red, 1));
            var arrivals = new List<ArrivalReport>();
            match.FleetArrived += arrivals.Add;

            match.TryLaunch(Faction.Blue, 0, 2);
            match.TryLaunch(Faction.Blue, 1, 2);
            match.Advance(1);

            Assert.AreEqual(ArrivalOutcome.BattleStarted, arrivals[0].Outcome);
            Assert.AreEqual(ArrivalOutcome.BattleJoined, arrivals[1].Outcome);
            AssertSides(match, 2, 10, 10);
            Assert.AreEqual(5.0, match.Nodes[2].Battle.Remaining, 1e-9);
        }

        // ------------------------------------------------------------ production and capture

        [Test]
        public void A_contested_object_produces_nothing_and_keeps_its_partial_progress()
        {
            // Red's planet makes its twentieth Dart at t=1. Blue arrives at t=1.5, half way to the next.
            Match match = Custom(Idle(-1, Faction.Blue, 5), Planet(2, Faction.Red, 19), Idle(500, Faction.Red, 1));

            match.TryLaunch(Faction.Blue, Source, Target);
            match.Advance(1.5);
            AssertSides(match, Target, 5, 20);
            Assert.IsFalse(match.Nodes[Target].IsProducing);

            double battle = Seconds(5, 20); // longer than a production interval
            Assert.Greater(battle, 1.0);
            match.Advance(battle);

            Node target = match.Nodes[Target];
            Assert.IsFalse(target.IsContested);
            Assert.AreEqual(15, target.Darts, "nothing was produced during the battle");
            Assert.IsTrue(target.IsProducing);
            Assert.AreEqual(0.5f, target.ProductionProgress, 1e-4f);

            match.Advance(0.49);
            Assert.AreEqual(15, target.Darts);
            match.Advance(0.01);
            Assert.AreEqual(16, target.Darts, "the half interval that was left, and no catching up");
            match.Advance(1);
            Assert.AreEqual(17, target.Darts);
        }

        [Test]
        public void An_object_taken_in_battle_starts_a_fresh_production_interval_once_captured()
        {
            Match match = Custom(Idle(-1, Faction.Blue, 40), Planet(2, Faction.Red, 3), Idle(500, Faction.Red, 1));

            match.TryLaunch(Faction.Blue, Source, Target);
            match.Advance(1.5); // Red has 4; ten to one
            match.Advance(1);   // the battle
            Node target = match.Nodes[Target];
            Assert.AreEqual(36, target.Darts);
            Assert.AreEqual(Faction.Blue, target.Captor);

            match.Advance(5);
            Assert.AreEqual(Faction.Blue, target.Owner);
            Assert.AreEqual(36, target.Darts);

            match.Advance(0.99);
            Assert.AreEqual(36, target.Darts);
            match.Advance(0.01);
            Assert.AreEqual(37, target.Darts);
        }

        [Test]
        public void Neutral_guards_are_the_stationed_side_and_stay_neutral_after_a_tie()
        {
            Match match = Custom(Idle(0, Faction.Blue, 4), MoonAt(2, Faction.Neutral, 4), Idle(500, Faction.Red, 1));
            var reports = Reports(match);

            match.TryLaunch(Faction.Blue, 0, 1);
            match.Advance(1);
            Assert.AreEqual(Faction.Neutral, match.Nodes[1].Battle.Stationed);

            match.Advance(5);
            Assert.AreEqual(BattleOutcome.Tie, reports[0].Outcome);
            Assert.AreEqual(Faction.Neutral, match.Nodes[1].Owner);
            Assert.IsFalse(match.Nodes[1].IsBeingCaptured);
            Assert.AreEqual(0, match.Nodes[1].Darts);
            Assert.AreEqual(4, match.DartsDestroyed(Faction.Blue));
            Assert.AreEqual(4, match.DartsLost(Faction.Blue));
        }

        // ------------------------------------------------------------ orders

        [Test]
        public void No_fleet_can_be_launched_from_a_contested_object()
        {
            Match match = Custom(Idle(0, Faction.Blue, 10), Idle(2, Faction.Red, 30), Idle(8, Faction.Red, 5),
                Idle(-6, Faction.Blue, 3));

            match.TryLaunch(Faction.Blue, 0, 1);
            match.Advance(1.5);
            Assert.IsTrue(match.Nodes[1].IsContested);
            int defenders = match.Nodes[1].Darts;
            int attackers = match.Nodes[1].Battle.ArrivingDarts;

            Assert.IsFalse(match.CanLaunch(Faction.Red, 1, 0), "the defenders are committed");
            Assert.IsFalse(match.TryLaunch(Faction.Red, 1, 2));
            Assert.IsFalse(match.CanLaunch(Faction.Blue, 1, 0), "and so are the attackers");
            Assert.IsFalse(match.TryLaunch(Faction.Blue, 1, 3));
            Assert.AreEqual(0, match.Fleets.Count);
            AssertSides(match, 1, attackers, defenders); // the refused orders moved nobody

            // Orders from anywhere else are as good as ever, and a fleet may be sent to the battle.
            Assert.IsTrue(match.CanLaunch(Faction.Red, 2, 0));
            Assert.IsTrue(match.TryLaunch(Faction.Blue, 3, 1));
            Assert.AreEqual(1, match.Fleets.Count);
        }

        [Test]
        public void The_defender_can_launch_again_once_the_attack_is_beaten_off()
        {
            Match match = Duel(2, 20);
            Assert.IsFalse(match.CanLaunch(Faction.Red, Target, Source));

            match.Advance(1);
            Assert.IsFalse(match.Nodes[Target].IsContested);
            Assert.IsTrue(match.CanLaunch(Faction.Red, Target, Source));
        }

        [Test]
        public void Survivors_of_a_won_attack_are_capturing_and_still_cannot_be_ordered_away()
        {
            Match match = Duel(20, 2);

            match.Advance(1);
            Assert.AreEqual(Faction.Blue, match.Nodes[Target].Captor);
            Assert.IsFalse(match.CanLaunch(Faction.Blue, Target, Source));
        }

        // ------------------------------------------------------------ survival, pause and lifecycle

        [Test]
        public void Darts_in_a_battle_count_in_their_faction_total()
        {
            Match match = Duel(10, 10);
            match.Advance(2.25);

            Assert.AreEqual(6, match.TotalDarts(Faction.Blue));
            Assert.AreEqual(6 + 1, match.TotalDarts(Faction.Red), "the six fighting and the one far away");
            Assert.IsTrue(match.IsEngaged(Faction.Blue));
            Assert.IsTrue(match.IsEngaged(Faction.Red));
            Assert.IsFalse(match.IsEngaged(Faction.Green));
        }

        [Test]
        public void Time_that_is_not_passed_in_costs_no_Darts_and_is_never_made_up()
        {
            Match paused = Reinforceable(-4.5f, 5, 100, 1);
            Match straight = Reinforceable(-4.5f, 5, 100, 1);
            paused.TryLaunch(Faction.Blue, 2, 1);
            straight.TryLaunch(Faction.Blue, 2, 1);

            // Paused during the approach, in the battle and after the reinforcement: a pause is simply
            // no time coming in, however long it lasts.
            foreach (double moment in new[] { 0.6, 2.3, 3.6 })
            {
                paused.Advance(moment - paused.Elapsed);
                string before = Capture(paused).Text;
                double elapsed = paused.Elapsed;
                for (int i = 0; i < 600; i++) paused.Advance(0);
                paused.Advance(-30);
                Assert.AreEqual(before, Capture(paused).Text);
                Assert.AreEqual(elapsed, paused.Elapsed);
            }

            paused.Advance(9 - paused.Elapsed);
            straight.Advance(9);
            Assert.AreEqual(Capture(straight).Text, Capture(paused).Text);
            Assert.AreEqual(straight.Elapsed, paused.Elapsed, 1e-9);
        }

        [Test]
        public void A_battle_left_unresolved_when_the_match_ends_stays_as_it_was()
        {
            // Blue takes Red's only planet while a second Blue fleet is still fighting Red's moon guards.
            NodeConfig moon = MoonAt(-8, Faction.Red, 10);
            moon.ProductionInterval = 0;
            Match match = Custom(Idle(0, Faction.Blue, 40), Idle(2, Faction.Red, 4), Idle(-20, Faction.Blue, 10), moon);

            match.TryLaunch(Faction.Blue, 0, 1); // t=1, one second of battle, captured at t=7
            match.TryLaunch(Faction.Blue, 2, 3); // 12 units: arrives at t=6, five seconds of battle
            match.Advance(7);

            Assert.AreEqual(MatchStatus.BlueVictory, match.Status);
            Assert.IsTrue(match.Nodes[3].IsContested);
            AssertSides(match, 3, 8, 8);
            int lost = match.Results.Players[0].DartsLost;
            Assert.AreEqual(4 + 2, lost);

            match.Advance(30);
            AssertSides(match, 3, 8, 8);
            Assert.AreEqual(lost, match.DartsLost(Faction.Blue));
            Assert.AreEqual(lost, match.Results.Players[0].DartsLost);
        }

        [Test]
        public void A_new_attempt_starts_without_the_battles_and_totals_of_the_last_one()
        {
            var config = new MatchConfig { FleetSpeed = 2 };
            config.Nodes.Add(Idle(0, Faction.Blue, 10));
            config.Nodes.Add(Idle(2, Faction.Red, 10));

            var first = new Match(config);
            first.TryLaunch(Faction.Blue, 0, 1);
            first.Advance(3);
            Assert.AreEqual(1, first.Battles.Count);
            Assert.Greater(first.DartsLost(Faction.Blue), 0);

            var second = new Match(config); // what a restart, or a return through the menu, builds
            Assert.AreEqual(0, second.Battles.Count);
            Assert.AreEqual(0, second.Fleets.Count);
            Assert.IsFalse(second.Nodes[1].IsContested);
            Assert.AreEqual(10, second.Nodes[0].Darts);
            Assert.AreEqual(10, second.Nodes[1].Darts);
            Assert.AreEqual(0, second.DartsLost(Faction.Blue));
            Assert.AreEqual(0, second.DartsDestroyed(Faction.Red));
            Assert.AreEqual(0.0, second.Elapsed);
        }

        [Test]
        public void A_third_side_arriving_at_a_battle_is_refused_rather_than_given_a_rule()
        {
            // What a third faction does at a battle is undecided. Until it is specified the match says so
            // instead of quietly picking a side, a queue or a winner.
            Match match = Custom(Idle(0, Faction.Blue, 10), Idle(2, Faction.Red, 10), Idle(8, Faction.Green, 5));

            match.TryLaunch(Faction.Blue, 0, 1);  // t=1
            match.TryLaunch(Faction.Green, 2, 1); // t=3
            match.Advance(2.9);

            Assert.Throws<NotSupportedException>(() => match.Advance(0.2));
        }
    }
}
