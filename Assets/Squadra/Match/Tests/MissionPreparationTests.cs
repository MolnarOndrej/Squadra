using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Squadra.Preparation;
using Squadra.Simulation;
using Regex = System.Text.RegularExpressions.Regex;

namespace Squadra.Tests
{
    /// <summary>
    /// From a mission file to a match: what preparing a mission keeps from the file, what it refuses,
    /// and the looks it gives the worlds. The match built from a prepared mission is checked through
    /// its public boundary, as the scene uses it.
    /// </summary>
    public class MissionPreparationTests
    {
        const string FirstContactPath = "Assets/Squadra/Match/Scenes/mission-01.json";

        static WorldPools Pools => new WorldPools(
            new WorldPool(new[] { "planet-a", "planet-b", "planet-c" }, new[] { "Aster", "Boreas", "Cygna", "Draco" }),
            new WorldPool(new[] { "moon-a" }, new[] { "Lune", "Selene" }),
            new WorldPool(new[] { "rock-a", "rock-b" }, new[] { "Cairn", "Flint", "Shard" }),
            new[] { "nebula", "void" });

        const string NamesFolder = "Assets/Squadra/Visuals/WorldNames/";

        static string FirstContact() => Bundled(FirstContactPath);

        static string Bundled(string projectPath)
        {
            // Tests run from the project folder; look upward too, so they also run from a build folder.
            string folder = Directory.GetCurrentDirectory();
            while (folder != null)
            {
                string path = Path.Combine(folder, projectPath);
                if (File.Exists(path)) return File.ReadAllText(path);
                folder = Path.GetDirectoryName(folder);
            }

            Assert.Fail("Could not find " + projectPath);
            return null;
        }

        static List<string> BundledNames(string type) => WorldPool.ReadNames(Bundled(NamesFolder + type + ".txt")).ToList();

        static string Mission(string nodes, string edges, string players = null, double scale = 1, string id = "m-test",
            string name = "Test")
        {
            players ??= "{ \"id\": \"player1\", \"color\": \"blue\" }, { \"id\": \"player2\", \"color\": \"red\" }";
            return "{ \"schemaVersion\": 1, \"id\": \"" + id + "\", \"name\": \"" + name + "\", \"map\": { \"distanceScale\": "
                + scale.ToString(System.Globalization.CultureInfo.InvariantCulture) + " }, \"players\": [" + players
                + "], \"nodes\": [" + nodes + "], \"edges\": [" + edges + "] }";
        }

        static string Node(string id, string type, double x, double y, string owner = null, int darts = 0, double size = 1)
        {
            string ownerText = owner == null ? "null" : "\"" + owner + "\"";
            return "{ \"id\": \"" + id + "\", \"type\": \"" + type + "\", \"size\": "
                + size.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", \"position\": { \"x\": "
                + x.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", \"y\": "
                + y.ToString(System.Globalization.CultureInfo.InvariantCulture) + " }, \"owner\": " + ownerText
                + ", \"darts\": " + darts + " }";
        }

        static string Edge(string a, string b) => "{ \"source\": \"" + a + "\", \"target\": \"" + b + "\" }";

        static string TwoSides(string extraNodes = "", string extraEdges = "") =>
            Mission(Node("A", "planet", 0, 0, "player1", 10) + ", " + Node("B", "planet", 6, 0, "player2", 10) + extraNodes,
                Edge("A", "B") + extraEdges);

        static PreparationOutcome Prepare(string text, string id = "m-test", int seed = 1, WorldPools pools = null)
        {
            return MissionPreparation.Prepare(id, id, text, pools ?? Pools, new Random(seed));
        }

        static PreparedMission Ready(string text, string id = "m-test", int seed = 1)
        {
            PreparationOutcome outcome = Prepare(text, id, seed);
            Assert.IsTrue(outcome.Ok, outcome.Explanation);
            return outcome.Mission;
        }

        static void AssertRefused(PreparationOutcome outcome, string mentions)
        {
            Assert.IsFalse(outcome.Ok);
            Assert.IsNull(outcome.Mission);
            StringAssert.Contains(mentions, outcome.Explanation);
        }

        // ------------------------------------------------------------ First Contact

        [Test]
        public void First_Contact_keeps_its_worlds_owners_Darts_and_routes()
        {
            PreparedMission mission = Ready(FirstContact(), "mission-01");

            Assert.AreEqual("mission-01", mission.Id);
            Assert.AreEqual("First Contact", mission.Name);
            Assert.AreEqual(3, mission.Worlds.Count);

            AssertWorld(mission.Worlds[0], "N1", NodeKind.Planet, -11.6667f, 7.1584f, 1.15f, Faction.Blue, 30);
            AssertWorld(mission.Worlds[1], "N2", NodeKind.Moon, -2.1333f, 10.975f, 0.55f, Faction.Neutral, 0);
            AssertWorld(mission.Worlds[2], "N3", NodeKind.Planet, 7.5333f, 7.5083f, 1.2f, Faction.Red, 30);

            var match = new Match(mission.CreateConfig());
            CollectionAssert.AreEqual(new[] { Faction.Blue, Faction.Red }, match.Players.Select(p => p.Faction));
            Assert.AreEqual(Faction.Blue, match.Human);
            Assert.AreEqual(3, match.Routes.Count);
            foreach ((int a, int b) in new[] { (0, 1), (1, 2), (0, 2) })
            {
                Assert.IsTrue(match.AreConnected(a, b), a + " - " + b);
                Assert.IsTrue(match.AreConnected(b, a), b + " - " + a);
            }

            Assert.IsTrue(match.CanLaunch(Faction.Blue, 0, 1));
            Assert.IsTrue(match.CanLaunch(Faction.Blue, 0, 2));
            Assert.IsTrue(match.CanLaunch(Faction.Red, 2, 0));
            Assert.IsTrue(match.CanLaunch(Faction.Red, 2, 1));
            Assert.AreEqual(MatchStatus.InProgress, match.Status);
        }

        static void AssertWorld(PreparedWorld world, string id, NodeKind kind, float x, float y, float radius,
            Faction owner, int darts)
        {
            Assert.AreEqual(id, world.NodeId);
            Assert.AreEqual(kind, world.Kind, id);
            Assert.AreEqual(x, world.Position.X, 1e-4, id);
            Assert.AreEqual(y, world.Position.Y, 1e-4, id);
            Assert.AreEqual(radius, world.Radius, 1e-6, id);
            Assert.AreEqual(owner, world.Owner, id);
            Assert.AreEqual(darts, world.Darts, id);
        }

        [Test]
        public void First_Contact_is_named_like_its_campaign_entry()
        {
            PreparationOutcome outcome = MissionPreparation.Prepare("mission-01", "mission-01", FirstContact(), Pools,
                new Random(1));

            Assert.IsTrue(outcome.Ok, outcome.Explanation);
        }

        // ------------------------------------------------------------ identity

        [Test]
        public void A_campaign_entry_or_file_name_that_differs_from_the_mission_ID_is_refused()
        {
            string text = TwoSides();

            AssertRefused(MissionPreparation.Prepare("M-TEST", "m-test", text, Pools, new Random(1)), "campaign entry");
            AssertRefused(MissionPreparation.Prepare("m-test", "m-test-2", text, Pools, new Random(1)), "m-test-2.json");
        }

        [Test]
        public void Renaming_a_mission_keeps_its_identity()
        {
            string renamed = Mission(Node("A", "planet", 0, 0, "player1", 10) + ", " + Node("B", "planet", 6, 0, "player2"),
                Edge("A", "B"), name: "A New Name");

            PreparedMission mission = Ready(renamed);

            Assert.AreEqual("m-test", mission.Id);
            Assert.AreEqual("A New Name", mission.Name);
        }

        // ------------------------------------------------------------ what cannot be played

        [Test]
        public void A_file_the_mission_format_refuses_is_refused_with_its_reasons()
        {
            AssertRefused(Prepare("{ not json"), "line 1");
            AssertRefused(Prepare(TwoSides().Replace("\"darts\": 10", "\"darts\": 10, \"extra\": 1")), "extra");
        }

        [Test]
        public void An_unfinished_editor_mission_is_refused_rather_than_started()
        {
            AssertRefused(Prepare(Mission("", "", players: "")), "exactly two players");
            AssertRefused(Prepare(Mission(Node("A", "planet", 0, 0, "player1", 10), "")), "\"player2\" owns no node");
        }

        [Test]
        public void Only_player1_in_blue_and_player2_in_red_can_play_so_far()
        {
            string three = "{ \"id\": \"player1\", \"color\": \"blue\" }, { \"id\": \"player2\", \"color\": \"red\" }, "
                + "{ \"id\": \"player3\", \"color\": \"green\" }";
            AssertRefused(Prepare(Mission(Node("A", "planet", 0, 0, "player1", 10) + ", " + Node("B", "planet", 6, 0, "player2"),
                Edge("A", "B"), three)), "exactly two players");

            string renamed = "{ \"id\": \"human\", \"color\": \"blue\" }, { \"id\": \"player2\", \"color\": \"red\" }";
            AssertRefused(Prepare(Mission(Node("A", "planet", 0, 0, "human", 10) + ", " + Node("B", "planet", 6, 0, "player2"),
                Edge("A", "B"), renamed)), "exactly two players");

            string swapped = "{ \"id\": \"player1\", \"color\": \"red\" }, { \"id\": \"player2\", \"color\": \"blue\" }";
            AssertRefused(Prepare(Mission(Node("A", "planet", 0, 0, "player1", 10) + ", " + Node("B", "planet", 6, 0, "player2"),
                Edge("A", "B"), swapped)), "players[0].color");
        }

        [Test]
        public void Positions_beyond_what_the_game_can_place_are_refused_not_moved()
        {
            string far = Mission(Node("A", "planet", 0, 0, "player1", 10) + ", " + Node("B", "planet", 60000, 0, "player2"),
                Edge("A", "B"), scale: 2);

            AssertRefused(Prepare(far), "nodes[1].position");

            string near = Mission(Node("A", "planet", 0, 0, "player1", 10) + ", " + Node("B", "planet", 60000, 0, "player2"),
                Edge("A", "B"), scale: 1);
            Assert.IsTrue(Prepare(near).Ok);
        }

        [Test]
        public void More_starting_Darts_than_the_game_can_count_are_refused()
        {
            string many = Mission(Node("A", "planet", 0, 0, "player1", 900000) + ", " + Node("B", "planet", 6, 0, "player2", 200000),
                Edge("A", "B"));

            AssertRefused(Prepare(many), "1100000 Darts");
        }

        [Test]
        public void A_type_without_surfaces_or_names_refuses_only_the_missions_that_use_it()
        {
            var noRocks = new WorldPools(Pools.For(Squadra.Missions.MissionNodeType.Planet),
                Pools.For(Squadra.Missions.MissionNodeType.Moon), new WorldPool(new string[0], new[] { "Cairn" }),
                Pools.Backdrops);
            var noRockNames = new WorldPools(Pools.For(Squadra.Missions.MissionNodeType.Planet),
                Pools.For(Squadra.Missions.MissionNodeType.Moon), new WorldPool(new[] { "rock-a" }, new string[0]),
                Pools.Backdrops);
            string withRock = TwoSides(", " + Node("R", "asteroid", 3, 3, size: 0.3));

            AssertRefused(Prepare(withRock, pools: noRocks), "asteroid surface materials");
            AssertRefused(Prepare(withRock, pools: noRockNames), "asteroid name dictionary");
            Assert.IsTrue(Prepare(TwoSides(), pools: noRocks).Ok, "a mission without asteroids does not need them");
        }

        [Test]
        public void Without_any_space_backdrop_no_mission_can_be_played()
        {
            var noBackdrops = new WorldPools(Pools.For(Squadra.Missions.MissionNodeType.Planet),
                Pools.For(Squadra.Missions.MissionNodeType.Moon), Pools.For(Squadra.Missions.MissionNodeType.Asteroid),
                new[] { " ", "" });

            AssertRefused(Prepare(TwoSides(), pools: noBackdrops), "No space backdrop materials");
        }

        [Test]
        public void Warnings_are_kept_but_do_not_stop_a_mission()
        {
            // A world no route reaches makes a disconnected map: a warning, not an error.
            PreparationOutcome outcome = Prepare(TwoSides(", " + Node("Lonely", "moon", 20, 20)));

            Assert.IsTrue(outcome.Ok, outcome.Explanation);
            CollectionAssert.IsNotEmpty(outcome.Warnings);
        }

        // ------------------------------------------------------------ conversion

        [Test]
        public void Distance_scale_spaces_the_worlds_without_resizing_them()
        {
            // Listed in an order unlike the routes, at fractional and negative positions, on a scale of 2.5.
            string text = Mission(
                Node("Far", "moon", -1.5, -2, "player2", 5, size: 0.5) + ", "
                + Node("Home", "planet", 1.5, 2, "player1", 10, size: 1.25) + ", "
                + Node("Rock", "asteroid", 4.5, 2, null, 0, size: 0.3),
                Edge("Rock", "Home") + ", " + Edge("Home", "Far"), scale: 2.5);
            PreparedMission mission = Ready(text);

            int home = mission.IndexOf("Home");
            int far = mission.IndexOf("Far");
            int rock = mission.IndexOf("Rock");
            Assert.AreEqual(1, home);
            Assert.AreEqual(-1, mission.IndexOf("Nowhere"));
            Assert.AreEqual(3.75f, mission.Worlds[home].Position.X, 1e-6);
            Assert.AreEqual(-5f, mission.Worlds[far].Position.Y, 1e-6);
            Assert.AreEqual(1.25f, mission.Worlds[home].Radius, 1e-6, "sizes are radii in game units already");
            Assert.AreEqual(0.3f, mission.Worlds[rock].Radius, 1e-6);
            Assert.AreEqual(NodeKind.Asteroid, mission.Worlds[rock].Kind);

            MatchConfig config = mission.CreateConfig();
            config.FleetSpeed = 2;
            var match = new Match(config);

            // Home to Far is 5 map units: 12.5 game units, 6.25 s. Home to Rock is 3: 7.5 game units, 3.75 s.
            Assert.AreEqual(6.25, match.TravelTime(home, far), 1e-5);
            Assert.AreEqual(3.75, match.TravelTime(home, rock), 1e-5);
            Assert.IsTrue(match.AreConnected(rock, home));
            Assert.IsTrue(match.AreConnected(far, home));
            Assert.IsFalse(match.AreConnected(far, rock), "routes are resolved by ID, not by list position");

            Assert.IsTrue(match.TryLaunch(Faction.Blue, home, far, out Fleet fleet));
            Assert.AreEqual(6.25, fleet.Duration, 1e-5);
        }

        [Test]
        public void Every_attempt_starts_from_the_same_untouched_state()
        {
            PreparedMission mission = Ready(TwoSides());
            var first = new Match(mission.CreateConfig());
            first.TryLaunch(Faction.Blue, 0, 1);
            first.Advance(40);

            var again = new Match(mission.CreateConfig());

            Assert.AreEqual(10, again.Nodes[0].Darts);
            Assert.AreEqual(Faction.Red, again.Nodes[1].Owner);
            Assert.AreEqual(0, again.Fleets.Count);
            Assert.AreEqual(0, again.Elapsed);
            Assert.AreEqual(10, mission.Worlds[0].Darts, "the prepared mission is never changed by play");
        }

        // ------------------------------------------------------------ looks

        static string Crowded() => Mission(
            Node("P1", "planet", 0, 0, "player1", 10) + ", " + Node("P2", "planet", 5, 0, "player2", 10) + ", "
            + Node("P3", "planet", 10, 0) + ", " + Node("P4", "planet", 15, 0) + ", " + Node("P5", "planet", 20, 0) + ", "
            + Node("M1", "moon", 0, 5) + ", " + Node("M2", "moon", 5, 5) + ", " + Node("M3", "moon", 10, 5) + ", "
            + Node("R1", "asteroid", 0, 10) + ", " + Node("R2", "asteroid", 5, 10),
            Edge("P1", "P2"));

        [Test]
        public void Each_world_draws_its_surface_and_name_from_its_own_type()
        {
            foreach (int seed in Enumerable.Range(0, 50))
            {
                PreparedMission mission = Ready(Crowded(), seed: seed);
                foreach (PreparedWorld world in mission.Worlds)
                {
                    WorldPool pool = Pools.For(world.Kind == NodeKind.Planet ? Squadra.Missions.MissionNodeType.Planet
                        : world.Kind == NodeKind.Moon ? Squadra.Missions.MissionNodeType.Moon
                        : Squadra.Missions.MissionNodeType.Asteroid);
                    CollectionAssert.Contains(pool.VariantIds, world.Look.VariantId, world.NodeId);
                    CollectionAssert.Contains(pool.Names, world.Look.Name, world.NodeId);
                }
            }
        }

        [Test]
        public void Surfaces_and_names_are_dealt_independently_and_balanced_within_a_mission()
        {
            foreach (int seed in Enumerable.Range(0, 50))
            {
                PreparedMission mission = Ready(Crowded(), seed: seed);
                List<WorldLook> planets = mission.Worlds.Where(w => w.Kind == NodeKind.Planet).Select(w => w.Look).ToList();
                List<WorldLook> moons = mission.Worlds.Where(w => w.Kind == NodeKind.Moon).Select(w => w.Look).ToList();

                // Five planets: three surfaces are used 2, 2 and 1 times, while four names all appear and
                // only one repeats. The moon pool's single surface goes to all three moons under two names.
                AssertBalanced(planets.Select(l => l.VariantId), 3, 5);
                AssertBalanced(planets.Select(l => l.Name), 4, 5);
                Assert.IsTrue(moons.All(l => l.VariantId == "moon-a"));
                AssertBalanced(moons.Select(l => l.Name), 2, 3);
                Assert.AreEqual(2, mission.Worlds.Where(w => w.Kind == NodeKind.Asteroid).Select(w => w.Look.Name)
                    .Distinct().Count(), "three rock names for two rocks: no repeat");
            }
        }

        static void AssertBalanced(IEnumerable<string> choices, int poolSize, int count)
        {
            List<int> uses = choices.GroupBy(c => c).Select(g => g.Count()).ToList();
            Assert.AreEqual(count, uses.Sum());
            Assert.AreEqual(Math.Min(poolSize, count), uses.Count, "every choice is used before any repeats");
            Assert.LessOrEqual(uses.Max() - uses.Min(), 1);
        }

        [Test]
        public void Uniqueness_is_per_mission_so_another_mission_may_reuse_names()
        {
            var pools = new WorldPools(new WorldPool(new[] { "only" }, new[] { "Aster", "Boreas" }), WorldPool.Empty,
                WorldPool.Empty, new[] { "nebula" });

            foreach (int seed in Enumerable.Range(0, 20))
            {
                var random = new Random(seed);
                PreparationOutcome first = MissionPreparation.Prepare("m-test", "m-test", TwoSides(), pools, random);
                PreparationOutcome second = MissionPreparation.Prepare("m-test", "m-test", TwoSides(), pools, random);

                CollectionAssert.AreEquivalent(new[] { "Aster", "Boreas" }, first.Mission.Worlds.Select(w => w.Look.Name));
                CollectionAssert.AreEquivalent(new[] { "Aster", "Boreas" }, second.Mission.Worlds.Select(w => w.Look.Name));
            }
        }

        // Each type's catalog designations have their own style, told apart from the names by their digits.
        static readonly (string Type, string KeptName, Regex Designation)[] Dictionaries =
        {
            ("Planets", "Thalassa", new Regex(@"^[A-Z]{3}-[0-9]{2}$")),
            ("Asteroids", "Cairn", new Regex(@"^[A-Z]{2}-[0-9]{3}$")),
            ("Moons", "Lune", new Regex(@"^S/[0-9]{2} [A-Z][0-9]$")),
        };

        [Test]
        public void Each_bundled_name_dictionary_holds_forty_names_of_which_eight_are_catalog_designations()
        {
            foreach ((string type, string keptName, Regex designation) in Dictionaries)
            {
                List<string> names = BundledNames(type);

                Assert.AreEqual(40, names.Count, type);
                Assert.AreEqual(40, new WorldPool(new string[0], names).Names.Count, type + ": every name is usable once");
                CollectionAssert.Contains(names, keptName, type);
                List<string> designations = names.Where(n => n.Any(char.IsDigit)).ToList();
                Assert.AreEqual(8, designations.Count, type);
                foreach (string code in designations) StringAssert.IsMatch(designation.ToString(), code, type);
                Assert.IsTrue(names.Except(designations).All(n => n.All(char.IsLetter)), type + ": names are plain words");
            }
        }

        [Test]
        public void The_bundled_dictionaries_give_their_designations_to_worlds_intact()
        {
            var pools = new WorldPools(
                new WorldPool(new[] { "planet-a" }, BundledNames("Planets")),
                new WorldPool(new[] { "moon-a" }, BundledNames("Moons")),
                new WorldPool(new[] { "rock-a" }, BundledNames("Asteroids")),
                new[] { "nebula" });

            var shown = new HashSet<string>();
            foreach (int seed in Enumerable.Range(0, 300))
            {
                PreparationOutcome outcome = Prepare(Crowded(), seed: seed, pools: pools);
                Assert.IsTrue(outcome.Ok, outcome.Explanation);
                foreach (PreparedWorld world in outcome.Mission.Worlds) shown.Add(world.Look.Name);
            }

            // Designations are dealt on equal terms with the names, so over enough missions each one turns up.
            foreach ((string type, _, Regex designation) in Dictionaries)
            {
                foreach (string code in BundledNames(type).Where(n => designation.IsMatch(n)))
                    CollectionAssert.Contains(shown, code, type);
            }

            CollectionAssert.Contains(shown, "S/47 N2", "a moon designation keeps its slash and space");
        }

        [Test]
        public void Each_mission_is_given_one_space_backdrop_from_the_pool()
        {
            var shown = new HashSet<string>();
            foreach (int seed in Enumerable.Range(0, 50))
            {
                string backdrop = Ready(TwoSides(), seed: seed).Assignment.BackdropId;
                CollectionAssert.Contains(Pools.Backdrops, backdrop);
                shown.Add(backdrop);
            }

            CollectionAssert.AreEquivalent(new[] { "nebula", "void" }, shown, "either backdrop can be dealt");
        }

        [Test]
        public void A_shared_assignment_gives_every_participant_the_same_worlds()
        {
            PreparedMission host = Ready(Crowded(), seed: 7);

            // Another participant: its own random state and its pools found in another order.
            var reordered = new WorldPools(
                new WorldPool(new[] { "planet-c", "planet-a", "planet-b" }, new[] { "Draco", "Cygna", "Boreas", "Aster" }),
                new WorldPool(new[] { "moon-a" }, new[] { "Selene", "Lune" }),
                new WorldPool(new[] { "rock-b", "rock-a" }, new[] { "Shard", "Flint", "Cairn" }),
                new[] { "void", "nebula" });
            PreparationOutcome guest = MissionPreparation.Prepare("m-test", "m-test", Crowded(), reordered, host.Assignment);

            Assert.IsTrue(guest.Ok, guest.Explanation);
            Assert.AreEqual(host.Assignment.BackdropId, guest.Mission.Assignment.BackdropId);
            foreach (PreparedWorld world in host.Worlds)
            {
                WorldLook theirs = guest.Mission.Worlds[guest.Mission.IndexOf(world.NodeId)].Look;
                Assert.AreEqual(world.Look.VariantId, theirs.VariantId, world.NodeId);
                Assert.AreEqual(world.Look.Name, theirs.Name, world.NodeId);
            }
        }

        [Test]
        public void An_assignment_with_a_surface_this_game_lacks_or_a_missing_world_is_refused()
        {
            PreparedMission host = Ready(TwoSides());
            var looks = new List<KeyValuePair<string, WorldLook>>
            {
                new KeyValuePair<string, WorldLook>("A", new WorldLook("planet-z", "Aster")),
                new KeyValuePair<string, WorldLook>("B", host.Assignment.For("B")),
            };
            string backdrop = host.Assignment.BackdropId;
            AssertRefused(MissionPreparation.Prepare("m-test", "m-test", TwoSides(), Pools,
                new WorldAssignment("m-test", backdrop, looks)), "\"planet-z\", which this game does not have");

            AssertRefused(MissionPreparation.Prepare("m-test", "m-test", TwoSides(), Pools,
                new WorldAssignment("m-test", backdrop, looks.Skip(1))), "Node A has no look");

            AssertRefused(MissionPreparation.Prepare("m-test", "m-test", TwoSides(), Pools,
                new WorldAssignment("m-other", backdrop, looks)), "for mission \"m-other\"");

            var known = new List<KeyValuePair<string, WorldLook>>
            {
                new KeyValuePair<string, WorldLook>("A", host.Assignment.For("A")),
                new KeyValuePair<string, WorldLook>("B", host.Assignment.For("B")),
            };
            AssertRefused(MissionPreparation.Prepare("m-test", "m-test", TwoSides(), Pools,
                new WorldAssignment("m-test", "aurora", known)), "space backdrop \"aurora\"");
        }
    }
}
