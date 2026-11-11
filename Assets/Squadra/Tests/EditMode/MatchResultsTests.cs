using System;
using NUnit.Framework;
using Squadra.Simulation;

namespace Squadra.Tests
{
    /// <summary>
    /// Exercises what the results screen reads from the match boundary: fleet orders and time in, the
    /// mission duration and each roster player's ships destroyed and ships lost out.
    /// </summary>
    public class MatchResultsTests
    {
        static Match Custom(params NodeConfig[] nodes)
        {
            var config = new MatchConfig { FleetSpeed = 2 };
            config.Nodes.AddRange(nodes);
            return new Match(config);
        }

        /// <summary>A planet that never produces, so battles can be set up with exact counts.</summary>
        static NodeConfig Idle(float x, Faction owner, int ships)
        {
            NodeConfig node = NodeConfig.Planet("Planet", new MapPoint(x, 0), owner, ships);
            node.ProductionInterval = 0;
            return node;
        }

        /// <summary>A moon that never produces. Moons do not count toward victory.</summary>
        static NodeConfig IdleMoon(float x, Faction owner = Faction.Neutral, int ships = 0)
        {
            NodeConfig node = NodeConfig.Moon("Moon", new MapPoint(x, 0), owner, ships);
            node.ProductionInterval = 0;
            return node;
        }

        static void AssertTotals(Match match, Faction faction, int destroyed, int lost)
        {
            Assert.AreEqual(destroyed, match.ShipsDestroyed(faction), faction + " ships destroyed");
            Assert.AreEqual(lost, match.ShipsLost(faction), faction + " ships lost");
        }

        static void AssertRow(PlayerResult row, Faction faction, int destroyed, int lost)
        {
            Assert.AreEqual(faction, row.Faction);
            Assert.AreEqual(destroyed, row.ShipsDestroyed, faction + " ships destroyed");
            Assert.AreEqual(lost, row.ShipsLost, faction + " ships lost");
        }

        // ------------------------------------------------------------ combat accounting

        [Test]
        public void A_fresh_attempt_starts_at_zero_time_and_zero_totals_with_no_results_yet()
        {
            Match match = new Match(MatchConfig.FirstPlayable(
                new MapPoint(0, 0), new MapPoint(10, 0), new MapPoint(4, 0)));

            Assert.AreEqual(0.0, match.Elapsed);
            AssertTotals(match, Faction.Blue, 0, 0);
            AssertTotals(match, Faction.Red, 0, 0);
            Assert.IsNull(match.Results);
        }

        [Test]
        public void A_winning_attack_counts_the_casualties_of_both_sides()
        {
            // The far Red planet only keeps the match going.
            Match match = Custom(Idle(0, Faction.Blue, 30), Idle(10, Faction.Red, 20), Idle(100, Faction.Red, 1));

            match.TryLaunch(Faction.Blue, 0, 1);
            match.Advance(5);

            Assert.AreEqual(10, match.Nodes[1].Ships);
            AssertTotals(match, Faction.Blue, 20, 20);
            AssertTotals(match, Faction.Red, 20, 20);
        }

        [Test]
        public void A_failed_attack_counts_for_the_attacker_and_the_defender()
        {
            Match match = Custom(Idle(0, Faction.Blue, 20), Idle(-4, Faction.Blue, 1), Idle(10, Faction.Red, 30));

            match.TryLaunch(Faction.Blue, 0, 2);
            match.Advance(5);

            Assert.AreEqual(10, match.Nodes[2].Ships);
            AssertTotals(match, Faction.Blue, 20, 20);
            AssertTotals(match, Faction.Red, 20, 20);
        }

        [Test]
        public void A_tie_counts_every_ship_of_both_fleets()
        {
            Match match = Custom(Idle(0, Faction.Blue, 20), Idle(10, Faction.Red, 20));

            match.TryLaunch(Faction.Blue, 0, 1);
            match.Advance(5);

            Assert.AreEqual(0, match.Nodes[1].Ships);
            AssertTotals(match, Faction.Blue, 20, 20);
            AssertTotals(match, Faction.Red, 20, 20);
        }

        [Test]
        public void Repeated_battles_add_up_and_each_is_counted_once()
        {
            // Blue attacks twice from two planets; the second fleet arrives a second after the first.
            Match match = Custom(Idle(0, Faction.Blue, 4), Idle(-2, Faction.Blue, 7), Idle(10, Faction.Red, 30));

            match.TryLaunch(Faction.Blue, 0, 2); // 5 s
            match.TryLaunch(Faction.Blue, 1, 2); // 6 s
            match.Advance(5);
            AssertTotals(match, Faction.Blue, 4, 4);
            AssertTotals(match, Faction.Red, 4, 4);

            match.Advance(0.5); // nothing happens between the battles
            AssertTotals(match, Faction.Blue, 4, 4);

            match.Advance(0.5);
            Assert.AreEqual(19, match.Nodes[2].Ships);
            AssertTotals(match, Faction.Blue, 11, 11);
            AssertTotals(match, Faction.Red, 11, 11);
        }

        [Test]
        public void Each_player_only_counts_the_battles_it_fought()
        {
            // Blue meets Red (5 ships) and then Green (3 ships); Red and Green never meet each other.
            Match match = Custom(Idle(0, Faction.Blue, 20), Idle(4, Faction.Red, 5), Idle(-10, Faction.Blue, 9),
                IdleMoon(-14, Faction.Green, 3));

            match.TryLaunch(Faction.Blue, 0, 1); // 2 s: 20 against Red's 5
            match.TryLaunch(Faction.Blue, 2, 3); // 2 s: 9 against Green's 3
            match.Advance(2);

            AssertTotals(match, Faction.Blue, 8, 8);
            AssertTotals(match, Faction.Red, 5, 5);
            AssertTotals(match, Faction.Green, 3, 3);
        }

        [Test]
        public void A_battle_during_capture_counts_for_the_capturing_fleet_not_the_owner()
        {
            // Blue is capturing Green's empty planet when Red attacks it: Blue and Red fight, Green does not.
            Match match = Custom(Idle(0, Faction.Blue, 10), Idle(2, Faction.Green, 0), Idle(8, Faction.Red, 4),
                IdleMoon(40, Faction.Green, 1));

            match.TryLaunch(Faction.Blue, 0, 1); // lands unopposed at t=1 and starts capturing
            match.TryLaunch(Faction.Red, 2, 1);  // arrives at t=3
            match.Advance(3);

            Assert.AreEqual(Faction.Green, match.Nodes[1].Owner, "the capture is still running");
            Assert.AreEqual(Faction.Blue, match.Nodes[1].Captor);
            Assert.AreEqual(6, match.Nodes[1].Ships);
            AssertTotals(match, Faction.Blue, 4, 4);
            AssertTotals(match, Faction.Red, 4, 4);
            AssertTotals(match, Faction.Green, 0, 0);
        }

        [Test]
        public void An_owner_that_interrupts_a_capture_counts_its_battle_with_the_captor()
        {
            // Blue's 4 survivors are capturing Red's planet when Red's reserve of 9 takes it back.
            Match match = Custom(Idle(0, Faction.Blue, 5), Idle(2, Faction.Red, 1), Idle(8, Faction.Red, 9));

            match.TryLaunch(Faction.Blue, 0, 1); // t=1: 5 against 1
            match.TryLaunch(Faction.Red, 2, 1);  // t=3: 9 against the 4 captors
            match.Advance(3);

            Assert.AreEqual(Faction.Red, match.Nodes[1].Owner);
            Assert.IsFalse(match.Nodes[1].IsBeingCaptured);
            AssertTotals(match, Faction.Blue, 5, 5);
            AssertTotals(match, Faction.Red, 5, 5);
        }

        [Test]
        public void Reinforcement_production_and_peaceful_capture_add_no_casualties()
        {
            MatchConfig config = MatchConfig.FirstPlayable(
                new MapPoint(0, 0), new MapPoint(10, 0), new MapPoint(4, 0));
            config.FleetSpeed = 2;
            var match = new Match(config);

            match.TryLaunch(Faction.Blue, 0, 2); // lands on the empty moon and captures it
            match.Advance(10);
            Assert.AreEqual(Faction.Blue, match.Nodes[2].Owner);

            match.TryLaunch(Faction.Blue, 0, 2); // the planet's new ships join the moon's fleet
            match.Advance(2);
            Assert.Greater(match.Nodes[2].Ships, 30);

            AssertTotals(match, Faction.Blue, 0, 0);
            AssertTotals(match, Faction.Red, 0, 0);
        }

        // ------------------------------------------------------------ neutral guards

        [Test]
        public void Destroying_neutral_guards_counts_for_the_attacker_and_nobody_else()
        {
            Match match = Custom(Idle(0, Faction.Blue, 8), IdleMoon(4, Faction.Neutral, 5), Idle(100, Faction.Red, 1));

            match.TryLaunch(Faction.Blue, 0, 1);
            match.Advance(2);

            Assert.AreEqual(3, match.Nodes[1].Ships);
            Assert.AreEqual(Faction.Blue, match.Nodes[1].Captor);
            AssertTotals(match, Faction.Blue, 5, 5);
            AssertTotals(match, Faction.Red, 0, 0);
            AssertTotals(match, Faction.Neutral, 0, 0);
        }

        [Test]
        public void Ships_lost_to_neutral_guards_are_credited_to_no_player()
        {
            Match match = Custom(Idle(0, Faction.Blue, 3), Idle(-4, Faction.Blue, 1), IdleMoon(4, Faction.Neutral, 5),
                Idle(100, Faction.Red, 1));

            match.TryLaunch(Faction.Blue, 0, 2);
            match.Advance(2);

            Assert.AreEqual(2, match.Nodes[2].Ships, "two guards are left");
            Assert.AreEqual(Faction.Neutral, match.Nodes[2].Owner);
            AssertTotals(match, Faction.Blue, 3, 3);
            AssertTotals(match, Faction.Red, 0, 0);
        }

        [Test]
        public void Neutral_is_never_a_player_and_has_no_results_row()
        {
            // Blue clears the guarded moon, then takes Red's planet and wins.
            Match match = Custom(Idle(0, Faction.Blue, 8), IdleMoon(4, Faction.Neutral, 5), Idle(-10, Faction.Blue, 9),
                Idle(-14, Faction.Red, 2));

            match.TryLaunch(Faction.Blue, 0, 1);
            match.TryLaunch(Faction.Blue, 2, 3);
            match.Advance(20);

            Assert.AreEqual(MatchStatus.BlueVictory, match.Status);
            Assert.IsFalse(match.IsPlayer(Faction.Neutral));
            Assert.AreEqual(2, match.Results.Players.Count);
            Assert.IsFalse(match.Results.TryGet(Faction.Neutral, out _));
            AssertRow(match.Results.Players[0], Faction.Blue, 7, 7);
            AssertRow(match.Results.Players[1], Faction.Red, 2, 2);
        }

        // ------------------------------------------------------------ roster

        [Test]
        public void The_first_playable_roster_is_Blue_the_human_player_and_Red()
        {
            Match match = new Match(MatchConfig.FirstPlayable(
                new MapPoint(0, 0), new MapPoint(10, 0), new MapPoint(4, 0)));

            Assert.AreEqual(Faction.Blue, match.Human);
            Assert.AreEqual(2, match.Players.Count);
            Assert.AreEqual(Faction.Blue, match.Players[0].Faction);
            Assert.AreEqual("Blue", match.Players[0].Name);
            Assert.AreEqual(Faction.Red, match.Players[1].Faction);
            Assert.AreEqual("Red", match.Players[1].Name);
        }

        [Test]
        public void Without_a_configured_roster_the_starting_owners_are_the_players()
        {
            Match match = Custom(Idle(0, Faction.Red, 3), IdleMoon(2), Idle(4, Faction.Green, 3), Idle(8, Faction.Blue, 3),
                Idle(12, Faction.Red, 3));

            // The human player first, then the others in map order, each once.
            Assert.AreEqual(3, match.Players.Count);
            Assert.AreEqual(Faction.Blue, match.Players[0].Faction);
            Assert.AreEqual(Faction.Red, match.Players[1].Faction);
            Assert.AreEqual(Faction.Green, match.Players[2].Faction);
        }

        [Test]
        public void A_starting_owner_missing_from_the_roster_still_gets_a_row()
        {
            var config = new MatchConfig { FleetSpeed = 2 };
            config.Players.Add(new MatchPlayer(Faction.Blue, "Commander"));
            config.Nodes.Add(Idle(0, Faction.Blue, 9));
            config.Nodes.Add(Idle(4, Faction.Red, 2));
            var match = new Match(config);

            match.TryLaunch(Faction.Blue, 0, 1);
            match.Advance(10);

            Assert.AreEqual(MatchStatus.BlueVictory, match.Status);
            Assert.AreEqual("Commander", match.Results.Players[0].Name);
            AssertRow(match.Results.Players[1], Faction.Red, 2, 2);
            Assert.AreEqual("Red", match.Results.Players[1].Name);
        }

        [Test]
        public void A_roster_cannot_list_Neutral_or_the_same_player_twice()
        {
            var neutral = new MatchConfig();
            neutral.Players.Add(new MatchPlayer(Faction.Neutral));
            Assert.Throws<ArgumentException>(() => new Match(neutral));

            var twice = new MatchConfig();
            twice.Players.Add(new MatchPlayer(Faction.Blue));
            twice.Players.Add(new MatchPlayer(Faction.Blue));
            Assert.Throws<ArgumentException>(() => new Match(twice));
        }

        [Test]
        public void A_two_player_result_lists_the_human_first_with_both_totals()
        {
            Match match = Custom(Idle(0, Faction.Blue, 50), Idle(10, Faction.Red, 20));

            match.TryLaunch(Faction.Blue, 0, 1);
            match.Advance(10);

            MatchResults results = match.Results;
            Assert.AreEqual(MatchStatus.BlueVictory, results.Outcome);
            Assert.IsTrue(results.IsVictory);
            Assert.AreEqual(2, results.Players.Count);
            AssertRow(results.Players[0], Faction.Blue, 20, 20);
            Assert.IsTrue(results.Players[0].IsHuman);
            AssertRow(results.Players[1], Faction.Red, 20, 20);
            Assert.IsFalse(results.Players[1].IsHuman);
        }

        [Test]
        public void A_six_player_result_keeps_every_player_in_roster_order_with_its_own_totals()
        {
            // Blue (human) takes Red's planet and wins. Meanwhile Yellow wipes out Green, whose moon it
            // captures; Purple and Orange never fight. Moons do not count toward Blue's victory.
            var config = new MatchConfig { FleetSpeed = 2, Human = Faction.Blue };
            config.Players.Add(new MatchPlayer(Faction.Red, "Crimson Pact"));
            config.Players.Add(new MatchPlayer(Faction.Green));
            config.Players.Add(new MatchPlayer(Faction.Blue, "You"));
            config.Players.Add(new MatchPlayer(Faction.Yellow));
            config.Players.Add(new MatchPlayer(Faction.Purple));
            config.Players.Add(new MatchPlayer(Faction.Orange));
            config.Nodes.Add(Idle(0, Faction.Blue, 20));       // 0
            config.Nodes.Add(Idle(10, Faction.Red, 4));        // 1
            config.Nodes.Add(IdleMoon(20, Faction.Green, 2));  // 2
            config.Nodes.Add(IdleMoon(24, Faction.Yellow, 6)); // 3
            config.Nodes.Add(IdleMoon(40, Faction.Purple, 9)); // 4
            config.Nodes.Add(IdleMoon(50, Faction.Orange, 0)); // 5
            var match = new Match(config);

            match.TryLaunch(Faction.Yellow, 3, 2); // t=2: 6 against Green's 2, captured at t=7
            match.TryLaunch(Faction.Blue, 0, 1);   // t=5: 20 against Red's 4, captured at t=10
            match.Advance(9.9);
            Assert.AreEqual(MatchStatus.InProgress, match.Status);
            Assert.IsFalse(match.IsAlive(Faction.Green), "Green has lost everything");

            match.Advance(0.1);
            Assert.AreEqual(MatchStatus.BlueVictory, match.Status);

            MatchResults results = match.Results;
            Assert.AreEqual(6, results.Players.Count);
            AssertRow(results.Players[0], Faction.Blue, 4, 4);
            AssertRow(results.Players[1], Faction.Red, 4, 4);
            AssertRow(results.Players[2], Faction.Green, 2, 2);
            AssertRow(results.Players[3], Faction.Yellow, 2, 2);
            AssertRow(results.Players[4], Faction.Purple, 0, 0);
            AssertRow(results.Players[5], Faction.Orange, 0, 0);

            Assert.AreEqual("You", results.Players[0].Name);
            Assert.AreEqual("Crimson Pact", results.Players[1].Name);
            Assert.AreEqual("Orange", results.Players[5].Name);
            for (int i = 0; i < results.Players.Count; i++)
                Assert.AreEqual(i == 0, results.Players[i].IsHuman, results.Players[i].Name);

            Assert.IsTrue(results.TryGet(Faction.Green, out PlayerResult green), "an eliminated player keeps its row");
            Assert.AreEqual(2, green.ShipsLost);
        }

        // ------------------------------------------------------------ the final snapshot

        [Test]
        public void A_battle_that_wins_the_match_is_in_the_results_exactly_once()
        {
            // Blue holds every planet from t=7, but a Red fleet is still flying. It dies at Blue's far
            // planet at t=17, and that battle ends the match.
            Match match = Custom(Idle(0, Faction.Blue, 10), Idle(4, Faction.Red, 6), Idle(-30, Faction.Blue, 8));

            match.TryLaunch(Faction.Red, 1, 2);  // 17 s
            match.TryLaunch(Faction.Blue, 0, 1); // 2 s, unopposed, captured at t=7
            match.Advance(16.9);
            Assert.AreEqual(MatchStatus.InProgress, match.Status);
            AssertTotals(match, Faction.Blue, 0, 0);
            Assert.IsNull(match.Results);

            match.Advance(0.1);
            Assert.AreEqual(MatchStatus.BlueVictory, match.Status);
            AssertRow(match.Results.Players[0], Faction.Blue, 6, 6);
            AssertRow(match.Results.Players[1], Faction.Red, 6, 6);
            Assert.AreEqual(17.0, match.Results.Duration, 1e-6);
        }

        [Test]
        public void A_battle_that_loses_the_match_is_in_the_results_exactly_once()
        {
            // Blue's planet falls while its only fleet is flying; the fleet then dies at Red's far planet.
            Match match = Custom(Idle(0, Faction.Blue, 5), Idle(4, Faction.Red, 3), Idle(30, Faction.Red, 9));

            match.TryLaunch(Faction.Blue, 0, 2); // 15 s
            match.TryLaunch(Faction.Red, 1, 0);  // 2 s, captures the empty planet at t=7
            match.Advance(14.9);
            Assert.AreEqual(MatchStatus.InProgress, match.Status);

            match.Advance(0.1);
            Assert.AreEqual(MatchStatus.BlueDefeat, match.Status);
            Assert.IsFalse(match.Results.IsVictory);
            AssertRow(match.Results.Players[0], Faction.Blue, 5, 5);
            AssertRow(match.Results.Players[1], Faction.Red, 5, 5);
            Assert.AreEqual(15.0, match.Results.Duration, 1e-6);
        }

        [Test]
        public void Results_stay_the_same_however_long_they_are_looked_at()
        {
            Match match = Custom(NodeConfig.Planet("Planet", new MapPoint(0, 0), Faction.Blue, 50),
                Idle(10, Faction.Red, 20));
            match.TryLaunch(Faction.Blue, 0, 1);
            match.Advance(10);
            Assert.AreEqual(MatchStatus.BlueVictory, match.Status);

            MatchResults results = match.Results;
            double duration = results.Duration;

            for (int i = 0; i < 600; i++) match.Advance(1f / 60f);
            match.TryLaunch(Faction.Blue, 0, 1);
            match.Advance(100);

            Assert.AreSame(results, match.Results);
            Assert.AreEqual(duration, match.Results.Duration);
            Assert.AreEqual(duration, match.Elapsed);
            AssertRow(match.Results.Players[0], Faction.Blue, 20, 20);
            AssertRow(match.Results.Players[1], Faction.Red, 20, 20);
            AssertTotals(match, Faction.Blue, 20, 20);
            AssertTotals(match, Faction.Red, 20, 20);
        }

        [Test]
        public void Results_are_ready_when_the_outcome_is_announced()
        {
            Match match = Custom(Idle(0, Faction.Blue, 50), Idle(10, Faction.Red, 20));
            MatchResults announced = null;
            match.StatusChanged += _ => announced = match.Results;

            match.TryLaunch(Faction.Blue, 0, 1);
            match.Advance(10);

            Assert.IsNotNull(announced);
            Assert.AreEqual(20, announced.Players[0].ShipsDestroyed);
            Assert.AreEqual(10.0, announced.Duration, 1e-6);
        }

        // ------------------------------------------------------------ duration

        [Test]
        public void Duration_counts_only_the_time_the_match_was_advanced()
        {
            Match match = Custom(Idle(0, Faction.Blue, 50), Idle(10, Faction.Red, 20));
            match.TryLaunch(Faction.Blue, 0, 1);

            match.Advance(3);
            // A pause: the scene simply stops passing time in, however long the pause lasts.
            match.Advance(0);
            match.Advance(-5);
            Assert.AreEqual(3.0, match.Elapsed, 1e-9);

            match.Advance(7); // arrival at t=5, captured at t=10
            Assert.AreEqual(MatchStatus.BlueVictory, match.Status);
            Assert.AreEqual(10.0, match.Results.Duration, 1e-6);
        }

        [Test]
        public void Duration_stops_at_the_outcome_even_inside_one_large_step()
        {
            Match match = Custom(Idle(0, Faction.Blue, 50), Idle(10, Faction.Red, 20));
            match.TryLaunch(Faction.Blue, 0, 1);

            match.Advance(500);

            Assert.AreEqual(10.0, match.Results.Duration, 1e-6);
            Assert.AreEqual("00:10", MatchClock.Format(match.Results.Duration));
        }

        [Test]
        public void Mission_time_reads_as_minutes_and_seconds_beyond_one_hour()
        {
            Assert.AreEqual("00:00", MatchClock.Format(0));
            Assert.AreEqual("00:00", MatchClock.Format(-3));
            Assert.AreEqual("00:59", MatchClock.Format(59.94));
            Assert.AreEqual("01:00", MatchClock.Format(60));
            Assert.AreEqual("03:27", MatchClock.Format(207.5));
            Assert.AreEqual("59:59", MatchClock.Format(3599.999));
            Assert.AreEqual("60:00", MatchClock.Format(3600));
            Assert.AreEqual("75:09", MatchClock.Format(4509));
            Assert.AreEqual("100:00", MatchClock.Format(6000));
        }

        [Test]
        public void Mission_time_shows_whole_seconds_after_many_small_frame_steps()
        {
            Match match = Custom(Idle(0, Faction.Blue, 50), Idle(10, Faction.Red, 20));
            match.TryLaunch(Faction.Blue, 0, 1);

            for (int i = 0; i < 1200; i++) match.Advance(1f / 60f);

            Assert.AreEqual(MatchStatus.BlueVictory, match.Status);
            Assert.AreEqual("00:10", MatchClock.Format(match.Results.Duration));
        }
    }
}
