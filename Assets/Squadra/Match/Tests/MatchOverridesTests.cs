using System;
using NUnit.Framework;
using Squadra.Preparation;
using Squadra.Simulation;

namespace Squadra.Tests
{
    /// <summary>
    /// The rules a sandbox may play by instead of the game's own: an omitted override is the default as
    /// the code defines it, a given one is played by, and neither touches what a mission lays down or
    /// any other attempt at a normal mission.
    /// </summary>
    public class MatchOverridesTests
    {
        const int BluePlanet = 0;
        const int BlueMoon = 1;
        const int Asteroid = 3;

        static WorldPools Pools => new WorldPools(
            new WorldPool(new[] { "planet-a" }, new[] { "Aster", "Boreas" }),
            new WorldPool(new[] { "moon-a" }, new[] { "Lune" }),
            new WorldPool(new[] { "rock-a" }, new[] { "Cairn" }),
            new[] { "nebula" });

        // Blue holds a planet and, 4 units away, a moon; Red holds a planet; a neutral asteroid lies aside.
        const string MissionText = "{ \"schemaVersion\": 1, \"id\": \"m-overrides\", \"name\": \"Overrides\", "
            + "\"map\": { \"distanceScale\": 1 }, "
            + "\"players\": [{ \"id\": \"player1\", \"color\": \"blue\" }, { \"id\": \"player2\", \"color\": \"red\" }], "
            + "\"nodes\": ["
            + "{ \"id\": \"P1\", \"type\": \"planet\", \"size\": 1, \"position\": { \"x\": 0, \"y\": 0 }, \"owner\": \"player1\", \"darts\": 10 }, "
            + "{ \"id\": \"M1\", \"type\": \"moon\", \"size\": 0.5, \"position\": { \"x\": 4, \"y\": 0 }, \"owner\": \"player1\", \"darts\": 5 }, "
            + "{ \"id\": \"P2\", \"type\": \"planet\", \"size\": 1.2, \"position\": { \"x\": 12, \"y\": 0 }, \"owner\": \"player2\", \"darts\": 20 }, "
            + "{ \"id\": \"A1\", \"type\": \"asteroid\", \"size\": 0.3, \"position\": { \"x\": 0, \"y\": 3 }, \"owner\": null, \"darts\": 2 }], "
            + "\"edges\": [{ \"source\": \"P1\", \"target\": \"M1\" }, { \"source\": \"M1\", \"target\": \"P2\" }, "
            + "{ \"source\": \"P1\", \"target\": \"A1\" }] }";

        static PreparedMission Prepared()
        {
            PreparationOutcome outcome = MissionPreparation.Prepare("m-overrides", "m-overrides", MissionText, Pools,
                new Random(1));
            Assert.IsTrue(outcome.Ok, outcome.Explanation);
            return outcome.Mission;
        }

        static MatchOverrides EveryRule() => new MatchOverrides
        {
            FleetSpeed = 4,
            PlanetProductionInterval = 0.5,
            MoonProductionInterval = 0.25,
        };

        [Test]
        public void Omitted_overrides_keep_the_rules_the_code_defines()
        {
            MatchConfig config = Prepared().CreateConfig();
            new MatchOverrides().ApplyTo(config);
            var match = new Match(config);

            Assert.AreEqual(4 / new MatchConfig().FleetSpeed, match.TravelTime(BluePlanet, BlueMoon), 1e-9);
            Assert.AreEqual(NodeConfig.PlanetProductionInterval, match.Nodes[BluePlanet].ProductionInterval);
            Assert.AreEqual(NodeConfig.MoonProductionInterval, match.Nodes[BlueMoon].ProductionInterval);
            Assert.IsFalse(match.Nodes[Asteroid].CanProduce);
        }

        [Test]
        public void Given_overrides_are_played_by()
        {
            MatchConfig config = Prepared().CreateConfig();
            EveryRule().ApplyTo(config);
            var match = new Match(config);

            Assert.AreEqual(1.0, match.TravelTime(BluePlanet, BlueMoon), 1e-9);

            match.Advance(1.0);
            Assert.AreEqual(10 + 2, match.Nodes[BluePlanet].Darts);
            Assert.AreEqual(5 + 4, match.Nodes[BlueMoon].Darts);
        }

        [Test]
        public void Overriding_one_rule_keeps_the_others_at_their_defaults()
        {
            MatchConfig config = Prepared().CreateConfig();
            new MatchOverrides { MoonProductionInterval = 0.25 }.ApplyTo(config);

            Assert.AreEqual(new MatchConfig().FleetSpeed, config.FleetSpeed);
            Assert.AreEqual(NodeConfig.PlanetProductionInterval, config.Nodes[BluePlanet].ProductionInterval);
            Assert.AreEqual(0.25, config.Nodes[BlueMoon].ProductionInterval);
        }

        [Test]
        public void Overrides_leave_what_the_mission_lays_down()
        {
            MatchConfig plain = Prepared().CreateConfig();
            MatchConfig overridden = Prepared().CreateConfig();
            EveryRule().ApplyTo(overridden);

            Assert.AreEqual(plain.Nodes.Count, overridden.Nodes.Count);
            for (int i = 0; i < plain.Nodes.Count; i++)
            {
                Assert.AreEqual(plain.Nodes[i].Name, overridden.Nodes[i].Name, "node " + i);
                Assert.AreEqual(plain.Nodes[i].Kind, overridden.Nodes[i].Kind, "node " + i);
                Assert.AreEqual(plain.Nodes[i].Owner, overridden.Nodes[i].Owner, "node " + i);
                Assert.AreEqual(plain.Nodes[i].Darts, overridden.Nodes[i].Darts, "node " + i);
                Assert.AreEqual(plain.Nodes[i].Position, overridden.Nodes[i].Position, "node " + i);
            }

            CollectionAssert.AreEqual(plain.Routes, overridden.Routes);
            Assert.AreEqual(0, overridden.Nodes[Asteroid].ProductionInterval, "an asteroid never produces");
        }

        [Test]
        public void Overrides_never_reach_another_attempt_at_the_mission()
        {
            PreparedMission mission = Prepared();
            EveryRule().ApplyTo(mission.CreateConfig());

            MatchConfig next = mission.CreateConfig();
            Assert.AreEqual(new MatchConfig().FleetSpeed, next.FleetSpeed);
            Assert.AreEqual(NodeConfig.PlanetProductionInterval, next.Nodes[BluePlanet].ProductionInterval);
            Assert.AreEqual(NodeConfig.MoonProductionInterval, next.Nodes[BlueMoon].ProductionInterval);
        }
    }
}
