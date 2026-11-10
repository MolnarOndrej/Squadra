using System.Collections.Generic;
using NUnit.Framework;
using Squadra.Simulation;

namespace Squadra.Tests
{
    /// <summary>
    /// Exercises the public match boundary the scene uses: fleet orders and time advancement in,
    /// visible game outcomes out.
    /// </summary>
    public class MatchTests
    {
        const int Blue = 0; // Blue's planet
        const int Red = 1;  // Red's planet
        const int Moon = 2;

        // Fleet speed 2: Blue planet <-> moon takes 2 s, Blue planet <-> Red planet 5 s, moon <-> Red planet 3 s.
        static MatchConfig StandardConfig()
        {
            MatchConfig config = MatchConfig.FirstPlayable(
                new MapPoint(0, 0), new MapPoint(10, 0), new MapPoint(4, 0));
            config.FleetSpeed = 2;
            return config;
        }

        static Match Standard() => new Match(StandardConfig());

        static Match Custom(params NodeConfig[] nodes)
        {
            var config = new MatchConfig { FleetSpeed = 2 };
            config.Nodes.AddRange(nodes);
            return new Match(config);
        }

        static NodeConfig Planet(float x, Faction owner, int ships) =>
            NodeConfig.Planet("Planet", new MapPoint(x, 0), owner, ships);

        static NodeConfig MoonAt(float x, Faction owner = Faction.Neutral, int ships = 0) =>
            NodeConfig.Moon("Moon", new MapPoint(x, 0), owner, ships);

        /// <summary>A planet that never produces, so battles can be set up with exact counts.</summary>
        static NodeConfig Idle(float x, Faction owner, int ships)
        {
            NodeConfig node = Planet(x, owner, ships);
            node.ProductionInterval = 0;
            return node;
        }

        // ------------------------------------------------------------ initial state and production

        [Test]
        public void Initial_map_has_two_planets_with_30_ships_and_an_empty_neutral_moon()
        {
            Match match = Standard();

            Assert.AreEqual(3, match.Nodes.Count);
            Assert.AreEqual(NodeKind.Planet, match.Nodes[Blue].Kind);
            Assert.AreEqual(Faction.Blue, match.Nodes[Blue].Owner);
            Assert.AreEqual(30, match.Nodes[Blue].Ships);
            Assert.AreEqual(NodeKind.Planet, match.Nodes[Red].Kind);
            Assert.AreEqual(Faction.Red, match.Nodes[Red].Owner);
            Assert.AreEqual(30, match.Nodes[Red].Ships);
            Assert.AreEqual(NodeKind.Moon, match.Nodes[Moon].Kind);
            Assert.AreEqual(Faction.Neutral, match.Nodes[Moon].Owner);
            Assert.AreEqual(0, match.Nodes[Moon].Ships);
            Assert.AreEqual(0, match.Fleets.Count);
            Assert.AreEqual(MatchStatus.InProgress, match.Status);
        }

        [Test]
        public void Owned_planet_produces_one_whole_ship_each_second()
        {
            Match match = Standard();

            match.Advance(0.99);
            Assert.AreEqual(30, match.Nodes[Blue].Ships);

            match.Advance(0.01);
            Assert.AreEqual(31, match.Nodes[Blue].Ships);

            match.Advance(2.5);
            Assert.AreEqual(33, match.Nodes[Blue].Ships);
        }

        [Test]
        public void Production_is_the_same_in_small_frame_steps_as_in_one_large_step()
        {
            Match match = Standard();

            for (int i = 0; i < 600; i++) match.Advance(1f / 60f);

            Assert.AreEqual(40, match.Nodes[Blue].Ships);
            Assert.AreEqual(40, match.Nodes[Red].Ships);
        }

        [Test]
        public void Owned_moon_produces_one_ship_every_two_seconds()
        {
            Match match = Custom(Planet(0, Faction.Blue, 30), Planet(10, Faction.Red, 30), MoonAt(4, Faction.Blue));

            match.Advance(1.9);
            Assert.AreEqual(0, match.Nodes[Moon].Ships);

            match.Advance(0.1);
            Assert.AreEqual(1, match.Nodes[Moon].Ships);

            match.Advance(4);
            Assert.AreEqual(3, match.Nodes[Moon].Ships);
        }

        [Test]
        public void Neutral_moon_produces_nothing()
        {
            Match match = Standard();

            match.Advance(20);

            Assert.AreEqual(0, match.Nodes[Moon].Ships);
            Assert.AreEqual(Faction.Neutral, match.Nodes[Moon].Owner);
        }

        [Test]
        public void Production_rate_is_node_configuration()
        {
            NodeConfig fast = Planet(0, Faction.Blue, 0);
            fast.ProductionInterval = 0.5;
            Match match = Custom(fast, Planet(10, Faction.Red, 30));

            match.Advance(2);
            Assert.AreEqual(4, match.Nodes[0].Ships);

            match.Nodes[0].ProductionInterval = 0.25; // an upgrade mid-match
            match.Advance(1);
            Assert.AreEqual(8, match.Nodes[0].Ships);
        }

        [Test]
        public void Red_stays_at_its_planet_and_only_grows()
        {
            Match match = Standard();

            match.Advance(45);

            Assert.AreEqual(0, match.Fleets.Count);
            Assert.AreEqual(Faction.Red, match.Nodes[Red].Owner);
            Assert.AreEqual(75, match.Nodes[Red].Ships);
            Assert.AreEqual(Faction.Neutral, match.Nodes[Moon].Owner);
        }

        // ------------------------------------------------------------ orders and transit

        [Test]
        public void Launch_sends_every_stationed_ship_and_the_source_stays_owned()
        {
            Match match = Standard();
            match.Advance(3); // 33 ships

            Assert.IsTrue(match.TryLaunch(Faction.Blue, Blue, Moon, out Fleet fleet));

            Assert.AreEqual(33, fleet.Ships);
            Assert.AreEqual(Faction.Blue, fleet.Faction);
            Assert.AreEqual(Blue, fleet.SourceId);
            Assert.AreEqual(Moon, fleet.TargetId);
            Assert.AreEqual(0, match.Nodes[Blue].Ships);
            Assert.AreEqual(Faction.Blue, match.Nodes[Blue].Owner);
            Assert.AreEqual(1, match.Fleets.Count);
        }

        [Test]
        public void Empty_launch_point_keeps_producing()
        {
            Match match = Standard();
            match.TryLaunch(Faction.Blue, Blue, Moon);

            match.Advance(1.5);

            Assert.AreEqual(Faction.Blue, match.Nodes[Blue].Owner);
            Assert.AreEqual(1, match.Nodes[Blue].Ships);
        }

        [Test]
        public void Invalid_orders_are_rejected()
        {
            Match match = Standard();

            Assert.IsFalse(match.CanLaunch(Faction.Blue, Blue, Blue), "same source and target");
            Assert.IsFalse(match.CanLaunch(Faction.Blue, Red, Moon), "source owned by the rival");
            Assert.IsFalse(match.CanLaunch(Faction.Blue, Moon, Red), "neutral source");
            Assert.IsFalse(match.CanLaunch(Faction.Neutral, Blue, Red), "neutral issuer");
            Assert.IsFalse(match.CanLaunch(Faction.Blue, Blue, 7), "unknown target");
            Assert.IsFalse(match.CanLaunch(Faction.Blue, -1, Red), "unknown source");

            Assert.IsTrue(match.TryLaunch(Faction.Blue, Blue, Moon));
            Assert.IsFalse(match.TryLaunch(Faction.Blue, Blue, Red), "empty sources cannot launch");
            Assert.AreEqual(1, match.Fleets.Count);
        }

        [Test]
        public void Travel_time_is_distance_divided_by_fleet_speed()
        {
            Match match = Standard();

            Assert.AreEqual(2.0, match.TravelTime(Blue, Moon), 1e-9);
            Assert.AreEqual(5.0, match.TravelTime(Blue, Red), 1e-9);
            Assert.AreEqual(3.0, match.TravelTime(Moon, Red), 1e-9);

            match.TryLaunch(Faction.Blue, Blue, Moon, out Fleet fleet);
            Assert.AreEqual(2.0, fleet.Duration, 1e-9);

            match.Advance(1);
            Assert.AreEqual(0.5f, fleet.Progress, 1e-5f);
            Assert.AreEqual(1, match.Fleets.Count);

            match.Advance(1);
            Assert.AreEqual(0, match.Fleets.Count);
        }

        [Test]
        public void Friendly_arrival_merges_with_the_stationed_fleet()
        {
            Match match = Custom(Idle(0, Faction.Blue, 12), Idle(4, Faction.Blue, 7), Idle(10, Faction.Red, 30));
            var reports = Capture(match);

            match.TryLaunch(Faction.Blue, 0, 1);
            match.Advance(2);

            Assert.AreEqual(19, match.Nodes[1].Ships);
            Assert.AreEqual(Faction.Blue, match.Nodes[1].Owner);
            Assert.IsFalse(match.Nodes[1].IsBeingCaptured);
            Assert.AreEqual(ArrivalOutcome.Merged, reports[0].Outcome);
            Assert.IsFalse(reports[0].WasBattle);
        }

        [Test]
        public void Crossing_fleets_pass_each_other_in_open_space()
        {
            Match match = Custom(Idle(0, Faction.Blue, 10), Idle(10, Faction.Red, 10));

            match.TryLaunch(Faction.Blue, 0, 1);
            match.TryLaunch(Faction.Red, 1, 0);
            match.Advance(4.9);

            Assert.AreEqual(2, match.Fleets.Count);
            Assert.AreEqual(10, match.Fleets[0].Ships);
            Assert.AreEqual(10, match.Fleets[1].Ships);
        }

        // ------------------------------------------------------------ combat

        [Test]
        public void Thirty_attacking_twenty_leaves_ten_attackers_who_begin_capture()
        {
            Match match = Custom(Idle(0, Faction.Blue, 30), Idle(10, Faction.Red, 20));
            var reports = Capture(match);

            match.TryLaunch(Faction.Blue, 0, 1);
            match.Advance(5);

            Node target = match.Nodes[1];
            Assert.AreEqual(10, target.Ships);
            Assert.AreEqual(Faction.Blue, target.Captor);
            Assert.AreEqual(Faction.Blue, target.Occupant);
            Assert.AreEqual(Faction.Red, target.Owner, "ownership waits for the capture to finish");
            Assert.AreEqual(ArrivalOutcome.AttackWon, reports[0].Outcome);
            Assert.AreEqual(20, reports[0].LossesPerSide);
            Assert.AreEqual(10, reports[0].ShipsAfter);
        }

        [Test]
        public void Twenty_attacking_thirty_leaves_ten_defenders_in_control()
        {
            Match match = Custom(Idle(0, Faction.Blue, 20), Idle(10, Faction.Red, 30));
            var reports = Capture(match);

            match.TryLaunch(Faction.Blue, 0, 1);
            match.Advance(5);

            Node target = match.Nodes[1];
            Assert.AreEqual(10, target.Ships);
            Assert.AreEqual(Faction.Red, target.Owner);
            Assert.IsFalse(target.IsBeingCaptured);
            Assert.AreEqual(ArrivalOutcome.Repelled, reports[0].Outcome);
        }

        [Test]
        public void A_tie_leaves_the_incumbent_owner_with_zero_ships()
        {
            Match match = Custom(Idle(0, Faction.Blue, 20), Idle(10, Faction.Red, 20));
            var reports = Capture(match);

            match.TryLaunch(Faction.Blue, 0, 1);
            match.Advance(5);

            Node target = match.Nodes[1];
            Assert.AreEqual(0, target.Ships);
            Assert.AreEqual(Faction.Red, target.Owner);
            Assert.IsFalse(target.IsBeingCaptured);
            Assert.AreEqual(ArrivalOutcome.Tie, reports[0].Outcome);
        }

        [Test]
        public void Attack_on_an_empty_owned_object_wins_with_the_full_fleet()
        {
            Match match = Custom(Idle(0, Faction.Blue, 9), Idle(10, Faction.Red, 0));
            var reports = Capture(match);

            match.TryLaunch(Faction.Blue, 0, 1);
            match.Advance(5);

            Node target = match.Nodes[1];
            Assert.AreEqual(9, target.Ships);
            Assert.AreEqual(Faction.Blue, target.Captor);
            Assert.AreEqual(Faction.Red, target.Owner);
            Assert.AreEqual(0, reports[0].LossesPerSide);
        }

        [Test]
        public void Defenders_produced_during_travel_count_in_the_battle()
        {
            Match match = Standard();

            // 30 ships leave at t=0 and arrive at t=5, when Red has grown to 35.
            match.TryLaunch(Faction.Blue, Blue, Red);
            match.Advance(5);

            Assert.AreEqual(Faction.Red, match.Nodes[Red].Owner);
            Assert.IsFalse(match.Nodes[Red].IsBeingCaptured);
            Assert.AreEqual(5, match.Nodes[Red].Ships);
        }

        [Test]
        public void A_direct_opening_attack_can_never_outnumber_red()
        {
            Match match = Standard();

            match.Advance(60);
            match.TryLaunch(Faction.Blue, Blue, Red);
            match.Advance(5);

            Assert.AreEqual(Faction.Red, match.Nodes[Red].Owner);
            Assert.IsFalse(match.Nodes[Red].IsBeingCaptured);
        }

        // ------------------------------------------------------------ capture

        [Test]
        public void Neutral_landing_costs_zero_ships_and_starts_a_five_second_capture()
        {
            Match match = Standard();
            var reports = Capture(match);

            match.TryLaunch(Faction.Blue, Blue, Moon);
            match.Advance(2);

            Node moon = match.Nodes[Moon];
            Assert.AreEqual(30, moon.Ships);
            Assert.AreEqual(Faction.Blue, moon.Captor);
            Assert.AreEqual(Faction.Neutral, moon.Owner);
            Assert.AreEqual(0f, moon.CaptureProgress, 1e-5f);
            Assert.AreEqual(5.0, moon.CaptureRemaining, 1e-6);
            Assert.AreEqual(ArrivalOutcome.Landed, reports[0].Outcome);

            match.Advance(2.5);
            Assert.AreEqual(0.5f, moon.CaptureProgress, 1e-5f);
            Assert.AreEqual(Faction.Neutral, moon.Owner);

            match.Advance(2.4);
            Assert.AreEqual(Faction.Neutral, moon.Owner);

            match.Advance(0.1);
            Assert.AreEqual(Faction.Blue, moon.Owner);
            Assert.IsFalse(moon.IsBeingCaptured);
            Assert.AreEqual(30, moon.Ships);
        }

        [Test]
        public void Production_waits_for_capture_and_then_starts_a_fresh_interval()
        {
            Match match = Standard();

            match.TryLaunch(Faction.Blue, Blue, Moon);
            match.Advance(2 + 5); // landed at 2 s, captured at 7 s
            Assert.AreEqual(30, match.Nodes[Moon].Ships, "nothing is produced during the countdown");

            match.Advance(1.9);
            Assert.AreEqual(30, match.Nodes[Moon].Ships);

            match.Advance(0.1);
            Assert.AreEqual(31, match.Nodes[Moon].Ships);
        }

        [Test]
        public void Captured_planet_stops_producing_for_its_old_owner_during_the_countdown()
        {
            // The far Red planet only keeps the match going after the near one falls.
            Match match = Custom(Idle(0, Faction.Blue, 40), Planet(10, Faction.Red, 0), Idle(100, Faction.Red, 1));

            match.TryLaunch(Faction.Blue, 0, 1);
            match.Advance(5); // Red made 5 ships; 35 Blue survivors begin capture
            Assert.AreEqual(35, match.Nodes[1].Ships);

            match.Advance(4.9);
            Assert.AreEqual(35, match.Nodes[1].Ships);
            Assert.AreEqual(Faction.Red, match.Nodes[1].Owner);

            match.Advance(0.1);
            Assert.AreEqual(Faction.Blue, match.Nodes[1].Owner);

            match.Advance(1);
            Assert.AreEqual(36, match.Nodes[1].Ships);
        }

        [Test]
        public void Allied_arrival_reinforces_a_capture_without_restarting_the_clock()
        {
            Match match = Custom(Idle(0, Faction.Blue, 10), Idle(2, Faction.Blue, 6), MoonAt(4), Idle(20, Faction.Red, 5));
            var reports = Capture(match);

            match.TryLaunch(Faction.Blue, 1, 2); // 1 s away, lands at t=1
            match.TryLaunch(Faction.Blue, 0, 2); // 2 s away, lands at t=2
            match.Advance(2);

            Node moon = match.Nodes[2];
            Assert.AreEqual(16, moon.Ships);
            Assert.AreEqual(Faction.Blue, moon.Captor);
            Assert.AreEqual(1.0, moon.CaptureElapsed, 1e-6);
            Assert.AreEqual(ArrivalOutcome.ReinforcedCapture, reports[1].Outcome);

            match.Advance(4);
            Assert.AreEqual(Faction.Blue, moon.Owner);
        }

        [Test]
        public void Rival_arrival_during_capture_fights_at_once_and_a_winning_captor_keeps_its_clock()
        {
            Match match = Custom(Idle(0, Faction.Blue, 10), MoonAt(2), Idle(8, Faction.Red, 4));

            match.TryLaunch(Faction.Blue, 0, 1); // lands at t=1
            match.TryLaunch(Faction.Red, 2, 1);  // arrives at t=3
            match.Advance(3);

            Node moon = match.Nodes[1];
            Assert.AreEqual(6, moon.Ships);
            Assert.AreEqual(Faction.Blue, moon.Captor);
            Assert.AreEqual(2.0, moon.CaptureElapsed, 1e-6);

            match.Advance(3);
            Assert.AreEqual(Faction.Blue, moon.Owner);
        }

        [Test]
        public void Rival_that_beats_the_captor_starts_its_own_five_second_capture()
        {
            Match match = Custom(Idle(0, Faction.Blue, 4), MoonAt(2), Idle(8, Faction.Red, 10));
            var reports = Capture(match);

            match.TryLaunch(Faction.Blue, 0, 1); // lands at t=1
            match.TryLaunch(Faction.Red, 2, 1);  // arrives at t=3
            match.Advance(3);

            Node moon = match.Nodes[1];
            Assert.AreEqual(6, moon.Ships);
            Assert.AreEqual(Faction.Red, moon.Captor);
            Assert.AreEqual(Faction.Neutral, moon.Owner);
            Assert.AreEqual(0.0, moon.CaptureElapsed, 1e-6);
            Assert.AreEqual(ArrivalOutcome.AttackWon, reports[1].Outcome);

            match.Advance(4.9);
            Assert.AreEqual(Faction.Neutral, moon.Owner);
            match.Advance(0.1);
            Assert.AreEqual(Faction.Red, moon.Owner);
        }

        [Test]
        public void Tie_during_a_neutral_capture_leaves_the_object_neutral()
        {
            Match match = Custom(Idle(0, Faction.Blue, 7), MoonAt(2), Idle(8, Faction.Red, 7));
            var reports = Capture(match);

            match.TryLaunch(Faction.Blue, 0, 1);
            match.TryLaunch(Faction.Red, 2, 1);
            match.Advance(3);

            Node moon = match.Nodes[1];
            Assert.AreEqual(0, moon.Ships);
            Assert.AreEqual(Faction.Neutral, moon.Owner);
            Assert.IsFalse(moon.IsBeingCaptured);
            Assert.AreEqual(ArrivalOutcome.Tie, reports[1].Outcome);

            match.Advance(10);
            Assert.AreEqual(Faction.Neutral, moon.Owner);
            Assert.AreEqual(0, moon.Ships);
        }

        [Test]
        public void Incumbent_owner_that_beats_the_captor_restores_control_immediately_and_resumes_production()
        {
            // Red owns the middle planet (empty) and has a reserve further out.
            Match match = Custom(Idle(0, Faction.Blue, 5), Planet(2, Faction.Red, 0), Idle(8, Faction.Red, 9));
            var reports = Capture(match);

            match.TryLaunch(Faction.Blue, 0, 1); // arrives t=1: Red made 1 ship, 4 Blue survivors capture
            match.TryLaunch(Faction.Red, 2, 1);  // arrives t=3: 9 vs 4
            match.Advance(3);

            Node planet = match.Nodes[1];
            Assert.AreEqual(Faction.Red, planet.Owner);
            Assert.IsFalse(planet.IsBeingCaptured);
            Assert.AreEqual(5, planet.Ships);
            Assert.AreEqual(ArrivalOutcome.ControlRestored, reports[1].Outcome);

            match.Advance(1);
            Assert.AreEqual(6, planet.Ships, "production resumes for the incumbent");
        }

        [Test]
        public void Tie_during_capture_of_an_owned_object_leaves_it_with_its_prior_owner()
        {
            Match match = Custom(Idle(0, Faction.Blue, 5), Idle(2, Faction.Red, 0), Idle(8, Faction.Red, 5));

            match.TryLaunch(Faction.Blue, 0, 1);
            match.TryLaunch(Faction.Red, 2, 1);
            match.Advance(3);

            Node planet = match.Nodes[1];
            Assert.AreEqual(Faction.Red, planet.Owner);
            Assert.IsFalse(planet.IsBeingCaptured);
            Assert.AreEqual(0, planet.Ships);
        }

        [Test]
        public void A_fleet_that_is_capturing_cannot_be_ordered_away()
        {
            Match match = Standard();

            match.TryLaunch(Faction.Blue, Blue, Moon);
            match.Advance(3);

            Assert.IsFalse(match.CanLaunch(Faction.Blue, Moon, Red));
        }

        // ------------------------------------------------------------ match result

        [Test]
        public void Blue_wins_when_both_planets_are_blue_even_with_a_neutral_moon()
        {
            Match match = Custom(Idle(0, Faction.Blue, 50), Idle(10, Faction.Red, 20), MoonAt(4));
            var statuses = new List<MatchStatus>();
            match.StatusChanged += statuses.Add;

            match.TryLaunch(Faction.Blue, 0, 1);
            match.Advance(5);
            Assert.AreEqual(MatchStatus.InProgress, match.Status, "capturing is not yet owning");

            match.Advance(4.9);
            Assert.AreEqual(MatchStatus.InProgress, match.Status);

            match.Advance(0.1);
            Assert.AreEqual(MatchStatus.BlueVictory, match.Status);
            Assert.AreEqual(Faction.Neutral, match.Nodes[2].Owner);
            CollectionAssert.AreEqual(new[] { MatchStatus.BlueVictory }, statuses);
        }

        [Test]
        public void Red_fleet_in_transit_delays_victory_until_it_is_gone()
        {
            // Red sends its garrison on a long trip to the far moon; Blue takes the emptied planet meanwhile.
            Match match = Custom(Idle(0, Faction.Blue, 10), Idle(4, Faction.Red, 6), MoonAt(34));

            match.TryLaunch(Faction.Red, 1, 2);  // 15 s to the moon
            match.TryLaunch(Faction.Blue, 0, 1); // 2 s, lands unopposed, owns the planet at t=7
            match.Advance(8);

            Assert.AreEqual(Faction.Blue, match.Nodes[1].Owner);
            Assert.AreEqual(MatchStatus.InProgress, match.Status, "a Red fleet is still in flight");

            match.Advance(6.9);
            Assert.AreEqual(MatchStatus.InProgress, match.Status);

            match.Advance(0.1); // the Red fleet lands on the moon and starts capturing: no Red fleet in transit
            Assert.AreEqual(MatchStatus.BlueVictory, match.Status);
        }

        [Test]
        public void Blue_survives_losing_its_last_base_while_a_fleet_can_still_capture()
        {
            // Blue empties its only planet toward the far moon; Red walks into the empty planet.
            Match match = Custom(Idle(0, Faction.Blue, 8), Idle(4, Faction.Red, 3), MoonAt(-26));

            match.TryLaunch(Faction.Blue, 0, 2); // 13 s to the moon
            match.TryLaunch(Faction.Red, 1, 0);  // 2 s, captures Blue's planet at t=7
            match.Advance(8);

            Assert.AreEqual(Faction.Red, match.Nodes[0].Owner);
            Assert.AreEqual(MatchStatus.InProgress, match.Status, "Blue's fleet is still in transit");

            match.Advance(6); // t=14: Blue's fleet has landed and is capturing
            Assert.AreEqual(Faction.Blue, match.Nodes[2].Captor);
            Assert.AreEqual(MatchStatus.InProgress, match.Status, "actively capturing keeps Blue alive");

            match.Advance(5);
            Assert.AreEqual(Faction.Blue, match.Nodes[2].Owner);
            Assert.AreEqual(MatchStatus.InProgress, match.Status);
            Assert.IsTrue(match.IsAlive(Faction.Blue));
        }

        [Test]
        public void Blue_loses_once_it_owns_nothing_and_has_no_surviving_fleet()
        {
            // Blue's whole fleet dies attacking; Red then takes Blue's empty planet.
            Match match = Custom(Idle(0, Faction.Blue, 5), Idle(4, Faction.Red, 9), Idle(8, Faction.Red, 3));

            match.TryLaunch(Faction.Blue, 0, 1); // destroyed at t=2
            match.TryLaunch(Faction.Red, 2, 0);  // 4 s, captures the empty planet at t=9
            match.Advance(8.9);
            Assert.AreEqual(MatchStatus.InProgress, match.Status);

            match.Advance(0.2);
            Assert.AreEqual(MatchStatus.BlueDefeat, match.Status);
        }

        [Test]
        public void Time_and_orders_stop_once_the_match_has_a_result()
        {
            Match match = Custom(Planet(0, Faction.Blue, 50), Idle(10, Faction.Red, 20));
            match.TryLaunch(Faction.Blue, 0, 1);
            match.Advance(10);
            Assert.AreEqual(MatchStatus.BlueVictory, match.Status);

            int ships = match.Nodes[0].Ships;
            double elapsed = match.Elapsed;
            match.Advance(10);

            Assert.AreEqual(ships, match.Nodes[0].Ships);
            Assert.AreEqual(elapsed, match.Elapsed, 1e-9);
            Assert.IsFalse(match.TryLaunch(Faction.Blue, 0, 1));
        }

        [Test]
        public void The_intended_opening_wins_take_the_moon_merge_then_attack()
        {
            Match match = Standard();

            match.TryLaunch(Faction.Blue, Blue, Moon); // lands t=2, moon is Blue at t=7
            match.Advance(30);
            Assert.AreEqual(Faction.Blue, match.Nodes[Moon].Owner);

            match.TryLaunch(Faction.Blue, Blue, Moon); // merge the planet's 30 new ships at the moon
            match.Advance(2);
            // Moon: 30 landed + 12 produced (one every 2 s from t=9 to t=31) + 30 merged.
            Assert.AreEqual(72, match.Nodes[Moon].Ships);

            match.TryLaunch(Faction.Blue, Moon, Red); // 3 s; Red has 30 + 35 = 65 on arrival
            match.Advance(3);
            Assert.AreEqual(Faction.Blue, match.Nodes[Red].Captor);
            Assert.AreEqual(7, match.Nodes[Red].Ships);

            match.Advance(5);
            Assert.AreEqual(Faction.Blue, match.Nodes[Red].Owner);
            Assert.AreEqual(MatchStatus.BlueVictory, match.Status);
        }

        static List<ArrivalReport> Capture(Match match)
        {
            var reports = new List<ArrivalReport>();
            match.FleetArrived += reports.Add;
            return reports;
        }
    }
}
