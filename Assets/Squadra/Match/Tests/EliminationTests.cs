using System.Collections.Generic;
using NUnit.Framework;
using Squadra.Simulation;

namespace Squadra.Tests
{
    /// <summary>
    /// The one objective every mission shares: Blue wins once Red owns no world of any kind and has no
    /// Darts left anywhere, and loses once the same is true of Blue. Neutral worlds and guards are nobody's.
    /// </summary>
    public class EliminationTests
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

        static Match Connected(params NodeConfig[] nodes)
        {
            var config = new MatchConfig { FleetSpeed = 2 };
            config.Players.Add(new MatchPlayer(Faction.Blue));
            config.Players.Add(new MatchPlayer(Faction.Red));
            config.Nodes.AddRange(nodes);
            config.ConnectAll();
            return new Match(config);
        }

        static double BattleSeconds(int first, int second) => new BattleTiming().Duration(first, second);

        static List<MatchStatus> Statuses(Match match)
        {
            var statuses = new List<MatchStatus>();
            match.StatusChanged += statuses.Add;
            return statuses;
        }

        [TestCase(NodeKind.Moon)]
        [TestCase(NodeKind.Asteroid)]
        public void An_empty_enemy_world_of_any_kind_holds_off_victory_until_it_is_captured(NodeKind kind)
        {
            Match match = Connected(World(NodeKind.Planet, 0, Faction.Blue, 10), World(kind, 4, Faction.Red, 0));
            List<MatchStatus> statuses = Statuses(match);

            match.Advance(30);
            Assert.AreEqual(MatchStatus.InProgress, match.Status, "an empty world still counts");

            match.TryLaunch(Faction.Blue, 0, 1); // lands at t=32, captured at t=37
            match.Advance(6.9);
            Assert.AreEqual(MatchStatus.InProgress, match.Status);
            Assert.AreEqual(Faction.Blue, match.Nodes[1].Captor);

            match.Advance(0.1);
            Assert.AreEqual(MatchStatus.BlueVictory, match.Status);
            Assert.AreEqual(37, match.Results.Duration, 1e-9);
            CollectionAssert.AreEqual(new[] { MatchStatus.BlueVictory }, statuses);
        }

        [Test]
        public void Neutral_worlds_and_guards_never_stand_in_the_way()
        {
            Match match = Connected(World(NodeKind.Planet, 0, Faction.Blue, 10), World(NodeKind.Planet, 4, Faction.Red, 0),
                World(NodeKind.Planet, 20, Faction.Neutral, 50), World(NodeKind.Moon, 30, Faction.Neutral, 0));

            match.TryLaunch(Faction.Blue, 0, 1);
            match.Advance(7);

            Assert.AreEqual(MatchStatus.BlueVictory, match.Status);
        }

        [Test]
        public void Red_Darts_fighting_as_attackers_keep_Red_in_until_the_battle_is_lost()
        {
            // Red sends its garrison against heavily guarded neutral rocks; Blue takes the emptied planet.
            Match match = Connected(World(NodeKind.Planet, 0, Faction.Blue, 10), World(NodeKind.Planet, 4, Faction.Red, 6),
                World(NodeKind.Asteroid, -20, Faction.Neutral, 20));

            match.TryLaunch(Faction.Red, 1, 2);  // 24 units: arrives at t=12
            match.TryLaunch(Faction.Blue, 0, 1); // lands at t=2, owns the planet at t=7
            match.Advance(12.5);

            Assert.AreEqual(Faction.Blue, match.Nodes[1].Owner);
            Assert.IsTrue(match.Nodes[2].IsContested);
            Assert.AreEqual(0, match.Fleets.Count, "nothing is in transit any more");
            Assert.AreEqual(MatchStatus.InProgress, match.Status, "Red's attackers are still fighting");

            double end = 12 + BattleSeconds(6, 20);
            match.Advance(end - match.Elapsed - 1e-3);
            Assert.AreEqual(MatchStatus.InProgress, match.Status);

            match.Advance(1);
            Assert.AreEqual(MatchStatus.BlueVictory, match.Status);
            Assert.AreEqual(end, match.Results.Duration, 1e-9, "the deciding casualties end the match");
            Assert.IsTrue(match.Results.TryGet(Faction.Red, out PlayerResult red));
            Assert.AreEqual(6, red.DartsLost);
        }

        [Test]
        public void Red_Darts_defending_a_capture_keep_Red_in_until_they_fall()
        {
            // Red leaves its planet to capture an asteroid; Blue takes the planet, then attacks the captors.
            Match match = Connected(
                World(NodeKind.Planet, 0, Faction.Blue, 10),     // 0
                World(NodeKind.Planet, 2, Faction.Red, 6),       // 1
                World(NodeKind.Asteroid, 10, Faction.Neutral, 0), // 2
                World(NodeKind.Planet, 14, Faction.Blue, 20));   // 3

            match.TryLaunch(Faction.Red, 1, 2);  // lands at t=4, would own the asteroid at t=9
            match.TryLaunch(Faction.Blue, 0, 1); // lands at t=1, owns Red's planet at t=6
            match.Advance(6.5);
            Assert.AreEqual(Faction.Blue, match.Nodes[1].Owner);
            Assert.AreEqual(Faction.Red, match.Nodes[2].Captor);
            Assert.AreEqual(MatchStatus.InProgress, match.Status, "capturing Darts keep Red in");

            match.TryLaunch(Faction.Blue, 3, 2); // arrives at t=8.5, before the capture completes
            match.Advance(2);
            Assert.IsTrue(match.Nodes[2].IsContested);
            Assert.AreEqual(Faction.Neutral, match.Nodes[2].Owner);
            Assert.AreEqual(Faction.Red, match.Nodes[2].Battle.Stationed);
            Assert.AreEqual(MatchStatus.InProgress, match.Status, "the stationed side is still fighting");

            match.Advance(BattleSeconds(20, 6));
            Assert.AreEqual(MatchStatus.BlueVictory, match.Status);
            Assert.AreEqual(8.5 + BattleSeconds(20, 6), match.Results.Duration, 1e-9);
        }

        [Test]
        public void Blue_holding_only_an_empty_asteroid_stays_in_until_it_is_taken()
        {
            Match match = Connected(World(NodeKind.Asteroid, 0, Faction.Blue, 0), World(NodeKind.Planet, 4, Faction.Red, 10));

            match.Advance(50);
            Assert.AreEqual(MatchStatus.InProgress, match.Status);

            match.TryLaunch(Faction.Red, 1, 0); // lands at t=52, captured at t=57
            match.Advance(6.9);
            Assert.AreEqual(MatchStatus.InProgress, match.Status);

            match.Advance(0.1);
            Assert.AreEqual(MatchStatus.BlueDefeat, match.Status);
            Assert.AreEqual(57, match.Results.Duration, 1e-9);
        }

        [Test]
        public void Blue_Darts_in_transit_keep_Blue_in_after_its_last_world_falls()
        {
            Match match = Connected(World(NodeKind.Planet, 0, Faction.Blue, 5), World(NodeKind.Planet, 4, Faction.Red, 10),
                World(NodeKind.Moon, -40, Faction.Neutral, 0));

            match.TryLaunch(Faction.Blue, 0, 2); // 20 s to the far moon
            match.TryLaunch(Faction.Red, 1, 0);  // lands at t=2, owns Blue's planet at t=7
            match.Advance(8);

            Assert.AreEqual(Faction.Red, match.Nodes[0].Owner);
            Assert.AreEqual(MatchStatus.InProgress, match.Status, "Blue's fleet is still flying");
            Assert.IsTrue(match.IsAlive(Faction.Blue));
        }

        [Test]
        public void One_large_step_reaches_the_same_outcome_at_the_same_time_as_many_small_ones()
        {
            Match large = Connected(World(NodeKind.Planet, 0, Faction.Blue, 10), World(NodeKind.Moon, 4, Faction.Red, 3));
            Match small = Connected(World(NodeKind.Planet, 0, Faction.Blue, 10), World(NodeKind.Moon, 4, Faction.Red, 3));
            large.TryLaunch(Faction.Blue, 0, 1);
            small.TryLaunch(Faction.Blue, 0, 1);

            large.Advance(60);
            for (int i = 0; i < 6000; i++) small.Advance(0.01);

            Assert.AreEqual(MatchStatus.BlueVictory, large.Status);
            Assert.AreEqual(large.Status, small.Status);
            Assert.AreEqual(large.Results.Duration, small.Results.Duration, 1e-6);
        }

        [Test]
        public void Both_sides_out_at_the_same_instant_is_a_defeat()
        {
            // Not reachable from a prepared mission, where both sides start with a world and a world only
            // ever passes from one player to another, but the rule needs an answer: the human has not
            // survived to win.
            Match match = Connected(World(NodeKind.Planet, 0, Faction.Neutral, 0));

            match.Advance(1);

            Assert.AreEqual(MatchStatus.BlueDefeat, match.Status);
        }
    }
}
