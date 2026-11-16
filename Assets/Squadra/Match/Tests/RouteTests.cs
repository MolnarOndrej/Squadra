using System;
using NUnit.Framework;
using Squadra.Simulation;

namespace Squadra.Tests
{
    /// <summary>
    /// Fleets travel only along the routes a mission lists, in either direction, and asteroids hold,
    /// receive and send fleets like any world without ever producing Darts.
    /// </summary>
    public class RouteTests
    {
        static NodeConfig World(NodeKind kind, float x, Faction owner, int darts)
        {
            return new NodeConfig
            {
                Name = kind.ToString(),
                Kind = kind,
                Position = new MapPoint(x, 0),
                Owner = owner,
                Darts = darts,
                ProductionInterval = 0,
            };
        }

        // A line of three worlds where only neighbours are connected: 0 - 1 - 2.
        static Match Sparse()
        {
            var config = new MatchConfig { FleetSpeed = 2 };
            config.Nodes.Add(World(NodeKind.Planet, 0, Faction.Blue, 10));
            config.Nodes.Add(World(NodeKind.Asteroid, 4, Faction.Neutral, 0));
            config.Nodes.Add(World(NodeKind.Moon, 8, Faction.Red, 10));
            config.Connect(1, 0);
            config.Connect(2, 1);
            return new Match(config);
        }

        // ------------------------------------------------------------ routes

        [Test]
        public void A_listed_route_runs_in_both_directions()
        {
            Match match = Sparse();

            Assert.IsTrue(match.AreConnected(0, 1));
            Assert.IsTrue(match.AreConnected(1, 0));
            Assert.IsTrue(match.CanLaunch(Faction.Blue, 0, 1), "listed as 1 - 0, flown from 0");
            Assert.IsTrue(match.CanLaunch(Faction.Red, 2, 1), "listed as 2 - 1, flown from 2");
            Assert.IsTrue(match.TryLaunch(Faction.Red, 2, 1));
        }

        [Test]
        public void An_order_to_an_unconnected_world_is_refused_and_changes_nothing()
        {
            Match match = Sparse();
            int launched = 0;
            match.FleetLaunched += _ => launched++;

            Assert.IsFalse(match.CanLaunch(Faction.Blue, 0, 2));
            Assert.IsFalse(match.TryLaunch(Faction.Blue, 0, 2));

            Assert.AreEqual(10, match.Nodes[0].Darts);
            Assert.AreEqual(0, match.Fleets.Count);
            Assert.AreEqual(0, launched);
        }

        [Test]
        public void A_world_connected_to_both_ends_does_not_make_a_direct_route()
        {
            Match match = Sparse();

            Assert.IsTrue(match.AreConnected(0, 1));
            Assert.IsTrue(match.AreConnected(1, 2));
            Assert.IsFalse(match.AreConnected(0, 2));
            Assert.IsFalse(match.CanLaunch(Faction.Red, 2, 0));
        }

        [Test]
        public void Each_route_is_kept_once_whichever_way_it_was_listed()
        {
            var config = new MatchConfig();
            config.Nodes.Add(World(NodeKind.Planet, 0, Faction.Blue, 1));
            config.Nodes.Add(World(NodeKind.Planet, 3, Faction.Red, 1));
            config.Connect(0, 1);
            config.Connect(1, 0);
            var match = new Match(config);

            Assert.AreEqual(1, match.Routes.Count);
            Assert.AreEqual(new Route(1, 0), match.Routes[0]);
            Assert.AreEqual(0, match.Routes[0].A);
            Assert.AreEqual(1, match.Routes[0].B);
        }

        [Test]
        public void A_route_must_join_two_worlds_of_the_map()
        {
            var config = new MatchConfig();
            config.Nodes.Add(World(NodeKind.Planet, 0, Faction.Blue, 1));
            config.Nodes.Add(World(NodeKind.Planet, 3, Faction.Red, 1));

            config.Connect(0, 0);
            Assert.Throws<ArgumentException>(() => new Match(config));

            config.Routes.Clear();
            config.Connect(1, 2);
            Assert.Throws<ArgumentException>(() => new Match(config));
        }

        [Test]
        public void A_map_without_routes_allows_no_orders()
        {
            var config = new MatchConfig();
            config.Nodes.Add(World(NodeKind.Planet, 0, Faction.Blue, 5));
            config.Nodes.Add(World(NodeKind.Planet, 3, Faction.Red, 5));
            var match = new Match(config);

            Assert.IsFalse(match.TryLaunch(Faction.Blue, 0, 1));
        }

        // ------------------------------------------------------------ asteroids

        [Test]
        public void An_owned_asteroid_never_produces_whatever_its_interval()
        {
            var config = new MatchConfig();
            NodeConfig asteroid = NodeConfig.Asteroid("Rock", new MapPoint(0, 0), Faction.Blue, 5);
            asteroid.ProductionInterval = 1; // even when told to: an asteroid never produces
            config.Nodes.Add(asteroid);
            config.Nodes.Add(World(NodeKind.Planet, 50, Faction.Red, 0));
            var match = new Match(config);
            int produced = 0;
            match.DartProduced += node => { if (node.Id == 0) produced++; };

            match.Advance(60);

            Assert.AreEqual(5, match.Nodes[0].Darts);
            Assert.AreEqual(0, produced);
            Assert.IsFalse(match.Nodes[0].CanProduce);
            Assert.IsFalse(match.Nodes[0].IsProducing);
            Assert.AreEqual(0f, match.Nodes[0].ProductionProgress);
        }

        [Test]
        public void Planets_and_moons_keep_their_production_rates()
        {
            var config = new MatchConfig();
            config.Nodes.Add(NodeConfig.Planet("Planet", new MapPoint(0, 0), Faction.Blue, 0));
            config.Nodes.Add(NodeConfig.Moon("Moon", new MapPoint(5, 0), Faction.Blue, 0));
            config.Nodes.Add(NodeConfig.Asteroid("Rock", new MapPoint(10, 0), Faction.Blue, 0));
            config.Nodes.Add(World(NodeKind.Planet, 50, Faction.Red, 0));
            var match = new Match(config);

            match.Advance(10);

            Assert.AreEqual(10, match.Nodes[0].Darts);
            Assert.AreEqual(5, match.Nodes[1].Darts);
            Assert.AreEqual(0, match.Nodes[2].Darts);
        }

        [Test]
        public void Asteroid_guards_fight_are_captured_and_the_asteroid_then_sends_fleets()
        {
            // Blue's 10 against 3 neutral guards on the asteroid, two seconds away.
            var config = new MatchConfig { FleetSpeed = 2 };
            config.Nodes.Add(World(NodeKind.Planet, 0, Faction.Blue, 10));
            config.Nodes.Add(World(NodeKind.Asteroid, 4, Faction.Neutral, 3));
            config.Nodes.Add(World(NodeKind.Planet, 8, Faction.Red, 20));
            config.Connect(0, 1);
            config.Connect(1, 2);
            var match = new Match(config);

            Assert.IsTrue(match.TryLaunch(Faction.Blue, 0, 1));
            match.Advance(2);
            Assert.IsTrue(match.Nodes[1].IsContested, "the guards fight");
            Assert.AreEqual(Faction.Neutral, match.Nodes[1].Battle.Stationed);

            match.Advance(new BattleTiming().Duration(10, 3));
            Assert.IsFalse(match.Nodes[1].IsContested);
            Assert.AreEqual(7, match.Nodes[1].Darts);
            Assert.AreEqual(Faction.Blue, match.Nodes[1].Captor);

            match.Advance(match.CaptureDuration);
            Assert.AreEqual(Faction.Blue, match.Nodes[1].Owner);
            Assert.IsFalse(match.Nodes[1].IsProducing);

            Assert.IsTrue(match.TryLaunch(Faction.Blue, 1, 2), "an owned asteroid sends its fleet like any world");
            Assert.AreEqual(0, match.Nodes[1].Darts);
        }
    }
}
