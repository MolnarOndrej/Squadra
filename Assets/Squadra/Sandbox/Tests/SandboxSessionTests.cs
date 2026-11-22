using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Squadra.Sandbox;
using Squadra.Simulation;

namespace Squadra.Tests
{
    /// <summary>
    /// The sandbox through its session: a run that starts from the authored moment, takes orders for every
    /// player, never ends, edits worlds and tuning without counting anything, and resets, replenishes and
    /// prepares examples on demand, all by the match's own rules.
    /// </summary>
    public class SandboxSessionTests
    {
        const int Thalassa = 0; // Blue planet, 30 Darts
        const int Ember = 1;    // Red planet, 24 Darts
        const int Verdance = 2; // Green planet, 20 Darts
        const int Lune = 3;     // neutral moon, empty

        // The sandbox's triangle: three players' planets around a neutral moon, every pair of worlds
        // joined. Fleets fly one map unit a second, so Thalassa to Lune and Ember to Lune take 4 seconds.
        static SandboxSetup Triangle()
        {
            var setup = new SandboxSetup();
            setup.Players.AddRange(new[] { Faction.Blue, Faction.Red, Faction.Green });
            setup.Worlds.Add(NodeConfig.Planet("Thalassa", new MapPoint(0, 0), Faction.Blue, 30));
            setup.Worlds.Add(NodeConfig.Planet("Ember", new MapPoint(8, 0), Faction.Red, 24));
            setup.Worlds.Add(NodeConfig.Planet("Verdance", new MapPoint(4, 6), Faction.Green, 20));
            setup.Worlds.Add(NodeConfig.Moon("Lune", new MapPoint(4, 0)));
            setup.Routes.Add(new Route(Thalassa, Lune));
            setup.Routes.Add(new Route(Thalassa, Ember));
            setup.Routes.Add(new Route(Ember, Verdance));
            setup.Routes.Add(new Route(Verdance, Thalassa));
            setup.Routes.Add(new Route(Ember, Lune));
            setup.Routes.Add(new Route(Verdance, Lune));
            setup.Rules.FleetSpeed = 1;
            return setup;
        }

        // The triangle with no world producing, so that counts only change by what a test does.
        static SandboxSetup Quiet()
        {
            SandboxSetup setup = Triangle();
            foreach (NodeConfig world in setup.Worlds)
                world.ProductionInterval = 0;
            return setup;
        }

        static double BattleSeconds(int first, int second) => new BattleTiming().Duration(first, second);

        // ------------------------------------------------------------------ orders

        [Test]
        public void Every_players_worlds_take_orders_without_switching_players()
        {
            var session = new SandboxSession(Quiet());

            Assert.IsTrue(session.TryOrder(Thalassa, Lune, out _));
            Assert.IsTrue(session.TryOrder(Ember, Thalassa, out _));
            Assert.IsTrue(session.TryOrder(Verdance, Lune, out _));

            CollectionAssert.AreEqual(new[] { Faction.Blue, Faction.Red, Faction.Green },
                session.Match.Fleets.Select(fleet => fleet.Faction));
            CollectionAssert.AreEqual(new[] { 30, 24, 20 }, session.Match.Fleets.Select(fleet => fleet.Darts));
        }

        [Test]
        public void Neutral_disconnected_empty_and_committed_worlds_are_refused_with_a_reason()
        {
            SandboxSetup setup = Quiet();
            setup.Routes.Remove(new Route(Thalassa, Ember));
            setup.Battles.Add(new StartingBattle { World = Verdance, Attacker = Faction.Red, Darts = 5 });
            setup.Captures.Add(new StartingCapture { World = Ember, Captor = Faction.Blue, Progress = 0.2 });
            var session = new SandboxSession(setup);
            session.TrySetDarts(Lune, 5, out _);

            Assert.IsFalse(session.TryOrder(Lune, Thalassa, out string neutral));
            StringAssert.Contains("Neutral", neutral);
            Assert.IsFalse(session.TryOrder(Thalassa, Ember, out string disconnected));
            StringAssert.Contains("No route from Thalassa to Ember", disconnected);
            Assert.IsFalse(session.TryOrder(Verdance, Lune, out string fighting));
            StringAssert.Contains("fighting", fighting);
            Assert.IsFalse(session.TryOrder(Ember, Lune, out string capturing));
            StringAssert.Contains("capturing", capturing);

            session.TryOrder(Thalassa, Lune, out _);
            Assert.IsFalse(session.TryOrder(Thalassa, Lune, out string empty));
            StringAssert.Contains("No Darts", empty);

            Assert.AreEqual(1, session.Match.Fleets.Count);
            Assert.IsFalse(session.IsOrderSource(Lune));
            Assert.IsFalse(session.IsOrderSource(Verdance));
            Assert.IsTrue(session.IsOrderSource(Thalassa), "an owned world with no Darts can still be selected");
        }

        [Test]
        public void The_run_never_ends_and_keeps_producing_after_everyone_else_is_out()
        {
            var session = new SandboxSession(Triangle());
            session.TrySetOwner(Ember, Faction.Blue, out _);
            session.TrySetOwner(Verdance, Faction.Blue, out _);
            session.TrySetDarts(Ember, 0, out _);
            session.TrySetDarts(Verdance, 0, out _);
            Assert.IsFalse(session.Match.IsAlive(Faction.Red));
            Assert.IsFalse(session.Match.IsAlive(Faction.Green));

            session.Advance(10);

            Assert.AreEqual(MatchStatus.InProgress, session.Match.Status);
            Assert.IsNull(session.Match.Results);
            Assert.AreEqual(40, session.Match.Nodes[Thalassa].Darts);
            Assert.AreEqual(10, session.Match.Nodes[Ember].Darts);
            Assert.IsTrue(session.TryOrder(Ember, Lune, out _), "orders still work");
        }

        [Test]
        public void A_mission_still_ends_on_elimination()
        {
            var config = new MatchConfig { FleetSpeed = 1 };
            config.Nodes.Add(NodeConfig.Planet("Blue", new MapPoint(0, 0), Faction.Blue, 10));
            config.Nodes.Add(NodeConfig.Asteroid("Red", new MapPoint(2, 0), Faction.Red, 0));
            config.ConnectAll();
            var match = new Match(config);

            match.TryLaunch(Faction.Blue, 0, 1);
            match.Advance(8);

            Assert.AreEqual(MatchStatus.BlueVictory, match.Status);
        }

        // ------------------------------------------------------------------ the authored moment

        [Test]
        public void A_fleet_set_up_part_way_arrives_after_the_rest_of_its_flight()
        {
            SandboxSetup setup = Quiet();
            setup.Fleets.Add(new StartingFleet { Faction = Faction.Red, Darts = 9, From = Ember, To = Lune, Progress = 0.25 });
            var session = new SandboxSession(setup);

            Assert.AreEqual(24, session.Match.Nodes[Ember].Darts, "its Darts are not taken from where it set out");
            Assert.AreEqual(0.25f, session.Match.Fleets[0].Progress, 1e-6);

            session.Advance(2.99);
            Assert.AreEqual(1, session.Match.Fleets.Count);

            session.Advance(0.01);
            Assert.AreEqual(0, session.Match.Fleets.Count);
            Assert.AreEqual(Faction.Red, session.Match.Nodes[Lune].Captor);
            Assert.AreEqual(9, session.Match.Nodes[Lune].Darts);
        }

        [Test]
        public void A_capture_set_up_part_way_completes_after_the_rest_of_its_time()
        {
            SandboxSetup setup = Quiet();
            setup.Captures.Add(new StartingCapture { World = Ember, Captor = Faction.Blue, Progress = 0.4 });
            var session = new SandboxSession(setup);

            Node ember = session.Match.Nodes[Ember];
            Assert.AreEqual(Faction.Red, ember.Owner);
            Assert.AreEqual(Faction.Blue, ember.Occupant, "the Darts there are the captor's");
            Assert.AreEqual(0.4f, ember.CaptureProgress, 1e-6);

            session.Advance(2.99);
            Assert.AreEqual(Faction.Red, ember.Owner);

            session.Advance(0.01);
            Assert.AreEqual(Faction.Blue, ember.Owner);
            Assert.AreEqual(24, ember.Darts);
        }

        [Test]
        public void A_battle_set_up_takes_casualties_on_the_normal_schedule()
        {
            SandboxSetup setup = Quiet();
            setup.Battles.Add(new StartingBattle { World = Verdance, Attacker = Faction.Red, Darts = 14 });
            var session = new SandboxSession(setup);

            Battle battle = session.Match.Nodes[Verdance].Battle;
            Assert.IsNotNull(battle);
            Assert.AreEqual(Faction.Green, battle.Stationed);
            Assert.AreEqual(20, battle.StationedDarts);
            Assert.AreEqual(14, battle.ArrivingDarts);
            Assert.AreEqual(24, session.Match.Nodes[Ember].Darts, "the attackers are not taken from Red's planet");

            double pair = BattleSeconds(14, 20) / 14;
            session.Advance(pair - 1e-4);
            Assert.AreEqual(0, battle.LossesPerSide);

            session.Advance(2e-4);
            Assert.AreEqual(1, battle.LossesPerSide);
            Assert.AreEqual(1, session.Match.DartsLost(Faction.Red));

            session.Advance(BattleSeconds(14, 20));
            Assert.IsNull(session.Match.Nodes[Verdance].Battle);
            Assert.AreEqual(6, session.Match.Nodes[Verdance].Darts);
            Assert.AreEqual(Faction.Green, session.Match.Nodes[Verdance].Owner);
        }

        [Test]
        public void A_battle_set_up_during_a_capture_holds_the_capture()
        {
            SandboxSetup setup = Quiet();
            setup.Captures.Add(new StartingCapture { World = Ember, Captor = Faction.Blue, Progress = 0.4 });
            setup.Battles.Add(new StartingBattle { World = Ember, Attacker = Faction.Red, Darts = 10 });
            var session = new SandboxSession(setup);

            Node ember = session.Match.Nodes[Ember];
            Assert.AreEqual(Faction.Blue, ember.Battle.Stationed, "the captor's Darts defend");

            session.Advance(1);
            Assert.AreEqual(0.4f, ember.CaptureProgress, 1e-6);
            Assert.AreEqual(Faction.Blue, ember.Captor);
        }

        [Test]
        public void A_capture_with_no_Darts_is_cleared_and_the_owner_keeps_the_world()
        {
            SandboxSetup setup = Quiet();
            setup.Captures.Add(new StartingCapture { World = Lune, Captor = Faction.Blue, Progress = 0.5 });
            var session = new SandboxSession(setup);

            Node lune = session.Match.Nodes[Lune];
            Assert.IsFalse(lune.IsBeingCaptured);
            Assert.AreEqual(Faction.Neutral, lune.Owner);
            CollectionAssert.IsEmpty(session.Problems);
        }

        [Test]
        public void A_battle_with_no_attackers_is_left_out_and_the_stationed_side_carries_on()
        {
            SandboxSetup setup = Quiet();
            setup.Battles.Add(new StartingBattle { World = Verdance, Attacker = Faction.Red, Darts = 0 });
            var session = new SandboxSession(setup);

            Assert.IsFalse(session.Match.Nodes[Verdance].IsContested);
            Assert.AreEqual(20, session.Match.Nodes[Verdance].Darts);
            CollectionAssert.IsEmpty(session.Match.Battles);
        }

        [Test]
        public void Attackers_at_a_world_with_nobody_stationed_land_as_any_fleet_would()
        {
            SandboxSetup setup = Quiet();
            setup.Worlds[Ember].Darts = 0;
            setup.Battles.Add(new StartingBattle { World = Lune, Attacker = Faction.Red, Darts = 8 });
            setup.Battles.Add(new StartingBattle { World = Ember, Attacker = Faction.Red, Darts = 5 });
            setup.Captures.Add(new StartingCapture { World = Ember, Captor = Faction.Green, Progress = 0.5 });
            var session = new SandboxSession(setup);

            Node lune = session.Match.Nodes[Lune];
            Assert.IsFalse(lune.IsContested);
            Assert.AreEqual(8, lune.Darts);
            Assert.AreEqual(Faction.Red, lune.Captor);
            Assert.AreEqual(0f, lune.CaptureProgress, "a fresh capture");

            Node ember = session.Match.Nodes[Ember];
            Assert.IsFalse(ember.IsBeingCaptured, "the owner coming home holds its world at once");
            Assert.AreEqual(Faction.Red, ember.Owner);
            Assert.AreEqual(5, ember.Darts);
        }

        [Test]
        public void An_invalid_fleet_is_named_and_left_out_while_the_rest_starts()
        {
            SandboxSetup setup = Quiet();
            setup.Routes.Remove(new Route(Thalassa, Ember));
            setup.Fleets.Add(new StartingFleet { Faction = Faction.Red, Darts = 9, From = Ember, To = Lune });
            setup.Fleets.Add(new StartingFleet { Faction = Faction.Blue, Darts = 5, From = Thalassa, To = -1 });
            setup.Fleets.Add(new StartingFleet { Faction = Faction.Blue, Darts = 6, From = Thalassa, To = Ember });
            setup.Captures.Add(new StartingCapture { World = Ember, Captor = Faction.Green, Progress = 0.5 });
            var session = new SandboxSession(setup);

            Assert.AreEqual(1, session.Match.Fleets.Count);
            Assert.AreEqual(Faction.Red, session.Match.Fleets[0].Faction);
            Assert.AreEqual(Faction.Green, session.Match.Nodes[Ember].Captor);

            Assert.AreEqual(2, session.Problems.Count);
            StringAssert.StartsWith("Fleet 2 (Blue, 5 Darts)", session.Problems[0]);
            StringAssert.Contains("world to set out from and one to fly to", session.Problems[0]);
            StringAssert.StartsWith("Fleet 3 (Blue, 6 Darts)", session.Problems[1]);
            StringAssert.Contains("No route from Thalassa to Ember", session.Problems[1]);
        }

        [Test]
        public void Reset_restores_the_authored_moment_after_the_run_has_changed_it()
        {
            SandboxSetup setup = Quiet();
            setup.Captures.Add(new StartingCapture { World = Ember, Captor = Faction.Blue, Progress = 0.4 });
            setup.Battles.Add(new StartingBattle { World = Verdance, Attacker = Faction.Red, Darts = 14 });
            setup.Fleets.Add(new StartingFleet { Faction = Faction.Green, Darts = 7, From = Verdance, To = Lune, Progress = 0.5 });
            var session = new SandboxSession(setup);
            Match first = session.Match;

            session.TryOrder(Thalassa, Lune, out _);
            session.TrySetKind(Lune, NodeKind.Asteroid, out _);
            session.TryStageBattleExample(Lune, out _);
            session.Advance(8);

            session.Reset();

            Match match = session.Match;
            Assert.AreNotSame(first, match);
            Assert.AreEqual(0.0, match.Elapsed);
            Assert.AreEqual(30, match.Nodes[Thalassa].Darts);
            Assert.AreEqual(NodeKind.Moon, match.Nodes[Lune].Kind);
            Assert.AreEqual(Faction.Neutral, match.Nodes[Lune].Owner);
            Assert.AreEqual(0.4f, match.Nodes[Ember].CaptureProgress, 1e-6);
            Assert.AreEqual(1, match.Battles.Count);
            Assert.AreEqual(14, match.Battles[0].ArrivingDarts);
            Assert.AreEqual(20, match.Battles[0].StationedDarts);
            Assert.AreEqual(1, match.Fleets.Count, "no fleet of the run is kept and none is doubled");
            Assert.AreEqual(0.5f, match.Fleets[0].Progress, 1e-6);
            Assert.AreEqual(0, match.DartsLost(Faction.Red));
        }

        [Test]
        public void After_resets_each_order_is_reported_once_by_the_match_in_play()
        {
            var session = new SandboxSession(Quiet());
            int launches = 0;
            void Listen() => session.Match.FleetLaunched += fleet => launches++;
            Listen();
            session.Started += Listen;

            session.Reset();
            session.Reset();
            session.TryOrder(Thalassa, Lune, out _);

            Assert.AreEqual(1, launches);
        }

        // ------------------------------------------------------------------ editing worlds

        [Test]
        public void Handing_over_a_world_being_captured_calls_the_capture_off()
        {
            SandboxSetup setup = Quiet();
            setup.Captures.Add(new StartingCapture { World = Ember, Captor = Faction.Blue, Progress = 0.4 });
            var session = new SandboxSession(setup);
            var captures = new List<Node>();
            session.Match.CaptureCompleted += (node, previous) => captures.Add(node);

            Assert.IsTrue(session.TrySetOwner(Ember, Faction.Green, out _));

            Node ember = session.Match.Nodes[Ember];
            Assert.AreEqual(Faction.Green, ember.Owner);
            Assert.IsFalse(ember.IsBeingCaptured);
            Assert.AreEqual(24, ember.Darts, "the stationed Darts are the new owner's");
            session.Advance(10);
            Assert.AreEqual(Faction.Green, ember.Owner);
            CollectionAssert.IsEmpty(captures, "an edit is not a capture");
        }

        [Test]
        public void Handing_over_a_world_in_battle_calls_the_battle_off_without_casualties()
        {
            SandboxSetup setup = Quiet();
            setup.Battles.Add(new StartingBattle { World = Verdance, Attacker = Faction.Red, Darts = 14 });
            setup.Fleets.Add(new StartingFleet { Faction = Faction.Red, Darts = 3, From = Ember, To = Verdance, Progress = 0.5 });
            var session = new SandboxSession(setup);
            int battleEvents = 0;
            session.Match.BattleEnded += report => battleEvents++;
            session.Match.BattleCasualties += (battle, pairs) => battleEvents++;

            Assert.IsTrue(session.TrySetOwner(Verdance, Faction.Blue, out _));

            Node verdance = session.Match.Nodes[Verdance];
            Assert.IsFalse(verdance.IsContested);
            CollectionAssert.IsEmpty(session.Match.Battles);
            Assert.AreEqual(Faction.Blue, verdance.Owner);
            Assert.AreEqual(20, verdance.Darts);
            Assert.AreEqual(24, session.Match.TotalDarts(Faction.Red) - 3, "the attackers are gone");
            Assert.AreEqual(0, session.Match.DartsLost(Faction.Red));
            Assert.AreEqual(0, session.Match.DartsDestroyed(Faction.Green));
            Assert.AreEqual(0, battleEvents);
            Assert.AreEqual(1, session.Match.Fleets.Count, "a fleet on its way is left alone");
        }

        [Test]
        public void Setting_Darts_replaces_the_count_and_refuses_what_is_out_of_range()
        {
            var session = new SandboxSession(Quiet());

            Assert.IsFalse(session.TrySetDarts(Thalassa, -1, out string negative));
            StringAssert.Contains("0 to " + SandboxSession.MostDarts, negative);
            Assert.IsFalse(session.TrySetDarts(Thalassa, SandboxSession.MostDarts + 1, out _));
            Assert.AreEqual(30, session.Match.Nodes[Thalassa].Darts);

            Assert.IsTrue(session.TrySetDarts(Thalassa, 7, out _));
            Assert.AreEqual(7, session.Match.Nodes[Thalassa].Darts);
            Assert.IsTrue(session.TrySetDarts(Lune, 4, out _));
            Assert.AreEqual(Faction.Neutral, session.Match.Nodes[Lune].Occupant, "neutral guards");
        }

        [Test]
        public void Setting_Darts_during_a_capture_sets_the_captors_and_none_clears_it()
        {
            SandboxSetup setup = Quiet();
            setup.Captures.Add(new StartingCapture { World = Ember, Captor = Faction.Blue, Progress = 0.4 });
            var session = new SandboxSession(setup);
            Node ember = session.Match.Nodes[Ember];

            session.TrySetDarts(Ember, 3, out _);
            Assert.AreEqual(3, ember.Darts);
            Assert.AreEqual(Faction.Blue, ember.Captor);
            Assert.AreEqual(0.4f, ember.CaptureProgress, 1e-6);

            session.TrySetDarts(Ember, 0, out _);
            Assert.IsFalse(ember.IsBeingCaptured);
            Assert.AreEqual(Faction.Red, ember.Owner);
        }

        [Test]
        public void Setting_the_defenders_during_a_battle_times_it_afresh_and_none_lets_the_attackers_land()
        {
            SandboxSetup setup = Quiet();
            setup.Battles.Add(new StartingBattle { World = Verdance, Attacker = Faction.Red, Darts = 14 });
            var session = new SandboxSession(setup);
            Node verdance = session.Match.Nodes[Verdance];
            session.Advance(0.1);

            session.TrySetDarts(Verdance, 40, out _);
            Assert.AreEqual(40, verdance.Battle.StationedDarts);
            Assert.AreEqual(40, verdance.Darts);
            Assert.AreEqual(BattleSeconds(14, 40), verdance.Battle.Remaining, 1e-9);

            session.TrySetDarts(Verdance, 0, out _);
            Assert.IsFalse(verdance.IsContested);
            Assert.AreEqual(14, verdance.Darts);
            Assert.AreEqual(Faction.Red, verdance.Captor);
            Assert.AreEqual(Faction.Green, verdance.Owner);
            Assert.AreEqual(0, session.Match.DartsLost(Faction.Green));
        }

        [Test]
        public void Changing_type_keeps_owner_Darts_and_conflict_and_applies_the_new_production()
        {
            SandboxSetup setup = Triangle();
            setup.Battles.Add(new StartingBattle { World = Verdance, Attacker = Faction.Red, Darts = 14 });
            var session = new SandboxSession(setup);
            session.TrySetOwner(Lune, Faction.Blue, out _);
            session.TrySetDarts(Lune, 5, out _);

            Assert.IsTrue(session.TrySetKind(Lune, NodeKind.Planet, out _));
            Node lune = session.Match.Nodes[Lune];
            Assert.AreEqual(Faction.Blue, lune.Owner);
            Assert.AreEqual(5, lune.Darts);
            Assert.AreEqual(1.0, lune.ProductionInterval);
            session.Advance(3);
            Assert.AreEqual(8, lune.Darts);

            Assert.IsTrue(session.TrySetKind(Lune, NodeKind.Asteroid, out _));
            Assert.IsFalse(lune.CanProduce);
            Assert.IsFalse(session.TrySetProducing(Lune, true, out string asteroid));
            StringAssert.Contains("Asteroids", asteroid);

            session.TrySetKind(Verdance, NodeKind.Moon, out _);
            Assert.IsTrue(session.Match.Nodes[Verdance].IsContested, "the battle goes on");
            Assert.AreEqual(6, session.Match.Routes.Count);
        }

        [Test]
        public void Production_switched_off_waits_and_carries_on_when_switched_on()
        {
            var session = new SandboxSession(Triangle());
            Node thalassa = session.Match.Nodes[Thalassa];

            session.Advance(0.5);
            Assert.IsTrue(session.TrySetProducing(Thalassa, false, out _));
            Assert.IsFalse(thalassa.CanProduce);
            session.Advance(5);
            Assert.AreEqual(30, thalassa.Darts);

            session.TrySetProducing(Thalassa, true, out _);
            session.Advance(0.49);
            Assert.AreEqual(30, thalassa.Darts);
            session.Advance(0.01);
            Assert.AreEqual(31, thalassa.Darts);

            session.TrySetProducing(Thalassa, false, out _);
            session.TrySetOwner(Thalassa, Faction.Red, out _);
            Assert.IsFalse(thalassa.CanProduce, "a new owner leaves production as it was");
            session.TrySetKind(Thalassa, NodeKind.Planet, out _);
            Assert.IsTrue(thalassa.CanProduce, "a new type starts with production on");
        }

        // ------------------------------------------------------------------ tuning

        [Test]
        public void Tuning_follows_the_game_unless_overridden_and_never_changes_the_setup()
        {
            SandboxSetup setup = Quiet();
            setup.Rules.FleetSpeed = null;
            var plain = new SandboxSession(setup);
            Assert.IsNull(plain.FleetSpeedOverride);
            Assert.AreEqual(SandboxSession.DefaultFleetSpeed, plain.FleetSpeed);
            Assert.AreEqual(new MatchConfig().FleetSpeed, SandboxSession.DefaultFleetSpeed);
            Assert.AreEqual(NodeConfig.MoonProductionInterval, plain.ProductionInterval(NodeKind.Moon));

            setup.Rules.FleetSpeed = 1;
            var session = new SandboxSession(setup);
            Assert.AreEqual(1.0, session.FleetSpeed);

            Assert.IsTrue(session.TrySetFleetSpeed(2, out _));
            Assert.IsTrue(session.TrySetProductionInterval(NodeKind.Moon, 0.5, out _));
            Assert.AreEqual(2.0, session.FleetSpeed);
            Assert.AreEqual(0.5, session.Match.Nodes[Lune].ProductionInterval);
            Assert.AreEqual(1.0, setup.Rules.FleetSpeed);
            Assert.IsNull(setup.Rules.MoonProductionInterval);
            Assert.AreEqual(0.0, setup.Worlds[Lune].ProductionInterval, "as the quiet setup has it");

            Assert.IsTrue(session.TrySetFleetSpeed(null, out _));
            Assert.AreEqual(SandboxSession.DefaultFleetSpeed, session.FleetSpeed);
            Assert.IsNull(session.FleetSpeedOverride);

            Assert.IsFalse(session.TrySetFleetSpeed(0, out _));
            Assert.IsFalse(session.TrySetProductionInterval(NodeKind.Planet, 0.001, out _));
            Assert.IsFalse(session.TrySetProductionInterval(NodeKind.Asteroid, 1, out _));
            Assert.AreEqual(SandboxSession.DefaultFleetSpeed, session.FleetSpeed);
        }

        [Test]
        public void A_new_fleet_speed_flies_the_rest_of_each_flight_at_the_new_pace()
        {
            var session = new SandboxSession(Quiet());
            session.TryOrder(Thalassa, Lune, out _); // 4 seconds at speed 1
            session.Advance(1);

            session.TrySetFleetSpeed(2, out _);
            Fleet fleet = session.Match.Fleets[0];
            Assert.AreEqual(0.25f, fleet.Progress, 1e-6);
            Assert.AreEqual(1.5, fleet.Remaining, 1e-9);

            session.Advance(1.49);
            Assert.AreEqual(1, session.Match.Fleets.Count);
            session.Advance(0.01);
            Assert.AreEqual(0, session.Match.Fleets.Count);
            Assert.AreEqual(Faction.Blue, session.Match.Nodes[Lune].Captor);
        }

        [Test]
        public void A_new_production_rate_finishes_the_Dart_in_progress_at_the_new_pace()
        {
            var session = new SandboxSession(Triangle());
            Node thalassa = session.Match.Nodes[Thalassa];
            session.Advance(0.5);

            session.TrySetProductionInterval(NodeKind.Planet, 4, out _);
            Assert.AreEqual(0.5f, thalassa.ProductionProgress, 1e-6);

            session.Advance(1.99);
            Assert.AreEqual(30, thalassa.Darts);
            session.Advance(0.01);
            Assert.AreEqual(31, thalassa.Darts);
        }

        [Test]
        public void Reset_keeps_the_runs_tuning_and_a_new_run_drops_it()
        {
            SandboxSetup setup = Quiet();
            var session = new SandboxSession(setup);
            session.TrySetFleetSpeed(3, out _);
            session.TrySetProductionInterval(NodeKind.Planet, 0.25, out _);

            session.Reset();
            Assert.AreEqual(3.0, session.FleetSpeed);
            Assert.AreEqual(0.25, session.Match.Nodes[Thalassa].ProductionInterval);

            var next = new SandboxSession(setup);
            Assert.AreEqual(1.0, next.FleetSpeed);
            Assert.AreEqual(0.0, next.Match.Nodes[Thalassa].ProductionInterval);
        }

        // ------------------------------------------------------------------ three sides

        [Test]
        public void A_third_side_waits_outside_a_battle_and_arrives_the_moment_it_ends()
        {
            SandboxSetup setup = Quiet();
            setup.Battles.Add(new StartingBattle { World = Verdance, Attacker = Faction.Red, Darts = 14 });
            setup.Fleets.Add(new StartingFleet { Faction = Faction.Blue, Darts = 5, From = Thalassa, To = Verdance, Progress = 0.99 });
            var session = new SandboxSession(setup);
            var battles = new List<Faction>();
            session.Match.BattleStarted += battle => battles.Add(battle.Arriving);

            double end = BattleSeconds(14, 20);
            session.Advance(end - 0.01);
            Assert.AreEqual(1, session.Match.Fleets.Count, "waiting, not lost");
            Assert.AreEqual(0.0, session.Match.Fleets[0].Remaining);
            Assert.AreEqual(Faction.Red, session.Match.Nodes[Verdance].Battle.Arriving);

            session.Advance(0.01);
            Assert.AreEqual(0, session.Match.Fleets.Count);
            Battle next = session.Match.Nodes[Verdance].Battle;
            Assert.IsNotNull(next, "Green's six survivors meet Blue's five");
            Assert.AreEqual(Faction.Blue, next.Arriving);
            Assert.AreEqual(6, next.StationedDarts);
            CollectionAssert.AreEqual(new[] { Faction.Blue }, battles);
        }

        [Test]
        public void A_waiting_third_side_gives_the_same_result_in_one_step_as_in_many()
        {
            SandboxSetup setup = Quiet();
            setup.Battles.Add(new StartingBattle { World = Verdance, Attacker = Faction.Red, Darts = 14 });
            setup.Fleets.Add(new StartingFleet { Faction = Faction.Blue, Darts = 9, From = Thalassa, To = Verdance, Progress = 0.9 });
            setup.Fleets.Add(new StartingFleet { Faction = Faction.Red, Darts = 2, From = Ember, To = Verdance, Progress = 0.95 });

            var large = new SandboxSession(setup);
            large.Advance(12);

            var small = new SandboxSession(setup);
            for (int i = 0; i < 1200; i++)
                small.Advance(0.01);

            foreach (Faction faction in new[] { Faction.Blue, Faction.Red, Faction.Green })
            {
                Assert.AreEqual(large.Match.TotalDarts(faction), small.Match.TotalDarts(faction), faction.ToString());
                Assert.AreEqual(large.Match.DartsLost(faction), small.Match.DartsLost(faction), faction.ToString());
            }

            Node bigStep = large.Match.Nodes[Verdance];
            Node smallSteps = small.Match.Nodes[Verdance];
            Assert.AreEqual(bigStep.Owner, smallSteps.Owner);
            Assert.AreEqual(bigStep.Occupant, smallSteps.Occupant);
            Assert.AreEqual(bigStep.Darts, smallSteps.Darts);
        }

        // ------------------------------------------------------------------ replenishing and examples

        [Test]
        public void Replenishing_tops_player_worlds_up_to_their_starting_Darts()
        {
            SandboxSetup setup = Quiet();
            setup.Captures.Add(new StartingCapture { World = Verdance, Captor = Faction.Red, Progress = 0.1 });
            var session = new SandboxSession(setup);
            session.TrySetDarts(Thalassa, 3, out _);
            session.TrySetOwner(Ember, Faction.Blue, out _);
            session.TrySetDarts(Ember, 1, out _);
            session.TrySetDarts(Verdance, 2, out _);

            Assert.AreEqual(2, session.Replenish());

            Assert.AreEqual(30, session.Match.Nodes[Thalassa].Darts);
            Assert.AreEqual(24, session.Match.Nodes[Ember].Darts);
            Assert.AreEqual(2, session.Match.Nodes[Verdance].Darts, "a capture under way is left alone");
            Assert.AreEqual(0, session.Match.Nodes[Lune].Darts);
            Assert.AreEqual(0, session.Match.DartsLost(Faction.Blue));
        }

        [Test]
        public void The_capture_example_lands_and_captures_by_the_rules_again_and_again()
        {
            var session = new SandboxSession(Quiet());
            Assert.AreEqual(Lune, session.DefaultExampleWorld);
            int launches = 0;
            session.Match.FleetLaunched += fleet => launches++;

            for (int attempt = 0; attempt < 2; attempt++)
            {
                Assert.IsTrue(session.TryStageCaptureExample(Lune, out _));
                Node lune = session.Match.Nodes[Lune];
                Assert.AreEqual(Faction.Neutral, lune.Owner);
                Assert.AreEqual(0, lune.Darts);
                Assert.AreEqual(1, session.Match.Fleets.Count);
                Assert.AreEqual(Faction.Blue, session.Match.Fleets[0].Faction, "Thalassa is the moon's first neighbor");
                Assert.AreEqual(1.5, session.Match.Fleets[0].Remaining, 1e-9);

                session.Advance(1.5);
                Assert.AreEqual(Faction.Blue, lune.Captor);
                session.Advance(5);
                Assert.AreEqual(Faction.Blue, lune.Owner);
            }

            Assert.AreEqual(0, launches, "nothing left a world");
            Assert.AreEqual(30, session.Match.Nodes[Thalassa].Darts);
        }

        [Test]
        public void The_battle_example_fights_by_the_rules_again_and_again()
        {
            var session = new SandboxSession(Quiet());

            for (int attempt = 0; attempt < 2; attempt++)
            {
                Assert.IsTrue(session.TryStageBattleExample(Lune, out _));
                Assert.AreEqual(Faction.Red, session.Match.Nodes[Lune].Owner);
                Assert.AreEqual(15, session.Match.Nodes[Lune].Darts);

                session.Advance(1.5);
                Battle battle = session.Match.Nodes[Lune].Battle;
                Assert.IsNotNull(battle);
                Assert.AreEqual(Faction.Blue, battle.Arriving);
                Assert.AreEqual(25, battle.ArrivingDarts);

                session.Advance(BattleSeconds(25, 15));
                Assert.IsFalse(session.Match.Nodes[Lune].IsContested);
                Assert.AreEqual(Faction.Blue, session.Match.Nodes[Lune].Captor);
                Assert.AreEqual(10, session.Match.Nodes[Lune].Darts);
            }

            Assert.AreEqual(30, session.Match.DartsLost(Faction.Blue), "the real battles count, the preparation does not");
        }

        [Test]
        public void An_example_needs_a_world_with_a_route_and_refuses_without_changing_anything()
        {
            SandboxSetup setup = Quiet();
            setup.Routes.RemoveAll(route => route.A == Lune || route.B == Lune);
            var session = new SandboxSession(setup);
            session.TrySetDarts(Lune, 4, out _);

            Assert.IsFalse(session.TryStageCaptureExample(Lune, out string problem));
            StringAssert.Contains("No route leads to Lune", problem);
            Assert.IsFalse(session.TryStageBattleExample(-1, out _));
            Assert.AreEqual(4, session.Match.Nodes[Lune].Darts);
            CollectionAssert.IsEmpty(session.Match.Fleets);
        }
    }
}
