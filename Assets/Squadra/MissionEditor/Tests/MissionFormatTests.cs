using System;
using System.Collections.Generic;
using NUnit.Framework;
using Squadra.Missions;

namespace Squadra.Tests
{
    /// <summary>
    /// Exercises the mission file contract: what a saved mission holds, that it reads back unchanged,
    /// which files are refused and with what explanation, and which layouts only earn a warning.
    /// </summary>
    public class MissionFormatTests
    {
        // A small complete mission: two players, three nodes, two routes.
        const string Sample = @"{
  ""schemaVersion"": 1,
  ""id"": ""sample-mission"",
  ""name"": ""Sample"",
  ""map"": { ""distanceScale"": 2.5 },
  ""players"": [
    { ""id"": ""player1"", ""color"": ""blue"" },
    { ""id"": ""player2"", ""color"": ""red"" }
  ],
  ""nodes"": [
    { ""id"": ""N1"", ""type"": ""planet"", ""position"": { ""x"": 0, ""y"": 0 }, ""owner"": ""player1"", ""darts"": 30 },
    { ""id"": ""N2"", ""type"": ""moon"", ""position"": { ""x"": 3, ""y"": 4 }, ""owner"": null, ""darts"": 0 },
    { ""id"": ""N3"", ""type"": ""planet"", ""position"": { ""x"": -6.5, ""y"": 0.25 }, ""owner"": ""player2"", ""darts"": 30 }
  ],
  ""edges"": [
    { ""source"": ""N1"", ""target"": ""N2"" },
    { ""source"": ""N3"", ""target"": ""N1"" }
  ]
}";

        static MissionDefinition Read(string json)
        {
            MissionReadResult result = MissionJson.Read(json);
            Assert.IsTrue(result.Ok, "expected the file to open, but: " + string.Join(" | ", result.Issues));
            return result.Mission;
        }

        static MissionIssue Rejected(string json, IssueCode code)
        {
            MissionReadResult result = MissionJson.Read(json);
            Assert.IsFalse(result.Ok, "the file should have been refused");
            Assert.IsNull(result.Mission, "a refused file must not be loaded in part");
            foreach (MissionIssue issue in result.Issues)
            {
                Assert.IsTrue(issue.IsError);
                Assert.IsNotEmpty(issue.Message);
                if (issue.Code == code) return issue;
            }

            Assert.Fail("expected " + code + ", got: " + string.Join(" | ", result.Issues));
            return null;
        }

        static string With(string find, string replacement)
        {
            StringAssert.Contains(find, Sample, "the test edits text that is not in the sample");
            return Sample.Replace(find, replacement);
        }

        static MissionDefinition Mission(params (string id, double x, double y)[] nodes)
        {
            var mission = new MissionDefinition { Id = "m", Name = "M" };
            foreach ((string id, double x, double y) in nodes)
                mission.Nodes.Add(new MissionNode { Id = id, X = x, Y = y });
            return mission;
        }

        static IssueCode[] Codes(MissionValidation validation)
        {
            var codes = new List<IssueCode>();
            foreach (MissionIssue issue in validation.Issues) codes.Add(issue.Code);
            return codes.ToArray();
        }

        // ------------------------------------------------------------ reading and writing

        [Test]
        public void A_mission_file_is_read_in_full()
        {
            MissionDefinition mission = Read(Sample);

            Assert.AreEqual("sample-mission", mission.Id);
            Assert.AreEqual("Sample", mission.Name);
            Assert.AreEqual(2.5, mission.DistanceScale);

            Assert.AreEqual(2, mission.Players.Count);
            Assert.AreEqual("player1", mission.Players[0].Id);
            Assert.AreEqual(PlayerColor.Blue, mission.Players[0].Color);
            Assert.AreEqual("player2", mission.Players[1].Id);
            Assert.AreEqual(PlayerColor.Red, mission.Players[1].Color);

            Assert.AreEqual(3, mission.Nodes.Count);
            MissionNode moon = mission.Nodes[1];
            Assert.AreEqual("N2", moon.Id);
            Assert.AreEqual(MissionNodeType.Moon, moon.Type);
            Assert.AreEqual(3d, moon.X);
            Assert.AreEqual(4d, moon.Y);
            Assert.IsNull(moon.Owner, "null ownership is neutral");
            Assert.AreEqual(0, moon.Darts);
            Assert.AreEqual(-6.5, mission.Nodes[2].X);
            Assert.AreEqual(0.25, mission.Nodes[2].Y);
            Assert.AreEqual("player2", mission.Nodes[2].Owner);
            Assert.AreEqual(30, mission.Nodes[2].Darts);

            Assert.AreEqual(2, mission.Edges.Count);
            Assert.AreEqual("N3", mission.Edges[1].Source);
            Assert.AreEqual("N1", mission.Edges[1].Target);
        }

        [Test]
        public void A_written_mission_reads_back_with_every_value_order_and_route()
        {
            MissionDefinition original = Read(Sample);
            original.Name = "Quotes \" and \\ slashes, a\ttab, a\nline break and ünïcödé";

            MissionDefinition copy = Read(MissionJson.Write(original));

            Assert.AreEqual(original.Id, copy.Id);
            Assert.AreEqual(original.Name, copy.Name);
            Assert.AreEqual(original.DistanceScale, copy.DistanceScale);
            Assert.AreEqual(original.Players.Count, copy.Players.Count);
            for (int i = 0; i < original.Players.Count; i++)
            {
                Assert.AreEqual(original.Players[i].Id, copy.Players[i].Id);
                Assert.AreEqual(original.Players[i].Color, copy.Players[i].Color);
            }

            Assert.AreEqual(original.Nodes.Count, copy.Nodes.Count);
            for (int i = 0; i < original.Nodes.Count; i++)
            {
                Assert.AreEqual(original.Nodes[i].Id, copy.Nodes[i].Id);
                Assert.AreEqual(original.Nodes[i].Type, copy.Nodes[i].Type);
                Assert.AreEqual(original.Nodes[i].X, copy.Nodes[i].X);
                Assert.AreEqual(original.Nodes[i].Y, copy.Nodes[i].Y);
                Assert.AreEqual(original.Nodes[i].Owner, copy.Nodes[i].Owner);
                Assert.AreEqual(original.Nodes[i].Darts, copy.Nodes[i].Darts);
            }

            Assert.AreEqual(original.Edges.Count, copy.Edges.Count);
            for (int i = 0; i < original.Edges.Count; i++)
            {
                Assert.AreEqual(original.Edges[i].Source, copy.Edges[i].Source);
                Assert.AreEqual(original.Edges[i].Target, copy.Edges[i].Target);
            }
        }

        [Test]
        public void Saving_again_and_again_never_moves_a_node()
        {
            // Values that change when rounded for display, or that have no short decimal form.
            double[] awkward =
            {
                0.1 + 0.2, 1d / 3d, -2d / 3d, 1e-7, 123456789.12345679, -0.000123456789012345, 5e-324,
                999999999.99999988, Math.PI, -Math.E, 1e9, -1e9,
            };

            var mission = new MissionDefinition { Id = "drift", Name = "Drift", DistanceScale = 1d / 7d };
            for (int i = 0; i < awkward.Length; i++)
                mission.Nodes.Add(new MissionNode { Id = "N" + (i + 1), X = awkward[i], Y = awkward[awkward.Length - 1 - i] });

            string first = MissionJson.Write(mission);
            string text = first;
            for (int pass = 0; pass < 5; pass++) text = MissionJson.Write(Read(text));

            Assert.AreEqual(first, text, "a save of a reopened file must be the same file");
            MissionDefinition last = Read(text);
            Assert.AreEqual(1d / 7d, last.DistanceScale);
            for (int i = 0; i < awkward.Length; i++)
            {
                Assert.AreEqual(awkward[i], last.Nodes[i].X, "X of " + last.Nodes[i].Id);
                Assert.AreEqual(awkward[awkward.Length - 1 - i], last.Nodes[i].Y, "Y of " + last.Nodes[i].Id);
            }
        }

        [Test]
        public void A_file_holds_the_mission_and_nothing_derived_from_it()
        {
            string text = MissionJson.Write(Read(Sample));

            StringAssert.Contains("\"schemaVersion\": 1", text);
            StringAssert.Contains("\"owner\": null", text);
            StringAssert.DoesNotContain("distance\"", text, "route distances are calculated, never stored");
            StringAssert.DoesNotContain("neutral", text);
            StringAssert.DoesNotContain("selection", text);
            StringAssert.DoesNotContain("zoom", text);
        }

        [Test]
        public void An_empty_mission_with_no_players_is_valid_and_round_trips()
        {
            var empty = new MissionDefinition { Id = "empty", Name = "" };

            MissionValidation validation = MissionValidator.Validate(empty);
            Assert.AreEqual(0, validation.Issues.Count, "no errors, and no disconnected-graph warning for an empty graph");

            MissionDefinition copy = Read(MissionJson.Write(empty));
            Assert.AreEqual(0, copy.Players.Count);
            Assert.AreEqual(0, copy.Nodes.Count);
            Assert.AreEqual(0, copy.Edges.Count);
        }

        [Test]
        public void A_mission_where_every_node_is_neutral_is_valid()
        {
            MissionDefinition mission = Read(Sample.Replace("\"owner\": \"player1\"", "\"owner\": null")
                .Replace("\"owner\": \"player2\"", "\"owner\": null"));

            Assert.IsFalse(MissionValidator.Validate(mission).HasErrors);
            foreach (MissionNode node in mission.Nodes) Assert.IsNull(node.Owner);
        }

        [Test]
        public void A_mission_that_cannot_be_expressed_is_not_written()
        {
            MissionDefinition mission = Mission(("N1", double.NaN, 0));
            Assert.Throws<InvalidOperationException>(() => MissionJson.Write(mission));

            mission = Mission(("N1", 0, 0));
            mission.Nodes[0].Type = (MissionNodeType)7;
            Assert.Throws<InvalidOperationException>(() => MissionJson.Write(mission));
        }

        // ------------------------------------------------------------ files that are not mission documents

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("{")]
        [TestCase("{ \"schemaVersion\": 1, }")]
        [TestCase("{ 'schemaVersion': 1 }")]
        [TestCase("{ \"schemaVersion\": 1 } trailing")]
        [TestCase("{ \"schemaVersion\": 01 }")]
        [TestCase("{ \"schemaVersion\": NaN }")]
        [TestCase("{ \"schemaVersion\": 1 // comment\n}")]
        [TestCase("{ \"name\": \"unterminated }")]
        [TestCase("{ \"name\": \"bad \\q escape\" }")]
        [TestCase("{ \"schemaVersion\" 1 }")]
        [TestCase("{ \"list\": [1, 2,] }")]
        [TestCase("not json at all")]
        public void Malformed_JSON_is_refused_with_its_position(string json)
        {
            MissionIssue issue = Rejected(json, IssueCode.MalformedJson);
            StringAssert.StartsWith("line ", issue.Location);
        }

        [Test]
        public void A_property_named_twice_is_refused_at_any_level()
        {
            MissionIssue root = Rejected(With("\"name\": \"Sample\",", "\"name\": \"Sample\", \"name\": \"Other\","),
                IssueCode.DuplicateProperty);
            StringAssert.Contains("\"name\"", root.Message);

            MissionIssue nested = Rejected(With("{ \"x\": 3, \"y\": 4 }", "{ \"x\": 3, \"y\": 4, \"x\": 5 }"),
                IssueCode.DuplicateProperty);
            StringAssert.Contains("\"x\"", nested.Message);
        }

        [Test]
        public void A_file_that_is_not_one_object_is_refused()
        {
            Rejected("[]", IssueCode.WrongType);
            Rejected("\"mission\"", IssueCode.WrongType);
            Rejected("12", IssueCode.WrongType);
            Rejected("null", IssueCode.WrongType);
        }

        [TestCase("\"schemaVersion\": 1,", "", "schemaVersion")]
        [TestCase("\"id\": \"sample-mission\",", "", "id")]
        [TestCase("\"name\": \"Sample\",", "", "name")]
        [TestCase("\"map\": { \"distanceScale\": 2.5 },", "", "map")]
        [TestCase("{ \"distanceScale\": 2.5 }", "{ }", "map.distanceScale")]
        [TestCase("{ \"id\": \"player2\", \"color\": \"red\" }", "{ \"id\": \"player2\" }", "players[1].color")]
        [TestCase("{ \"id\": \"player1\", \"color\": \"blue\" }", "{ \"color\": \"blue\" }", "players[0].id")]
        [TestCase("\"id\": \"N2\", ", "", "nodes[1].id")]
        [TestCase("\"type\": \"moon\", ", "", "nodes[1].type")]
        [TestCase("\"position\": { \"x\": 3, \"y\": 4 }, ", "", "nodes[1].position")]
        [TestCase("{ \"x\": 3, \"y\": 4 }", "{ \"x\": 3 }", "nodes[1].position.y")]
        [TestCase("\"owner\": null, ", "", "nodes[1].owner")]
        [TestCase(", \"darts\": 0 }", " }", "nodes[1].darts")]
        [TestCase("{ \"source\": \"N3\", \"target\": \"N1\" }", "{ \"source\": \"N3\" }", "edges[1].target")]
        [TestCase("{ \"source\": \"N1\", \"target\": \"N2\" }", "{ \"target\": \"N2\" }", "edges[0].source")]
        public void Every_field_is_required(string find, string replacement, string missing)
        {
            MissionIssue issue = Rejected(With(find, replacement), IssueCode.MissingField);
            Assert.AreEqual(missing, issue.Location);
        }

        [Test]
        public void The_three_lists_are_required_too()
        {
            string json = "{ \"schemaVersion\": 1, \"id\": \"a\", \"name\": \"A\", \"map\": { \"distanceScale\": 1 } }";
            MissionReadResult result = MissionJson.Read(json);

            Assert.IsFalse(result.Ok);
            var missing = new List<string>();
            foreach (MissionIssue issue in result.Issues)
                if (issue.Code == IssueCode.MissingField) missing.Add(issue.Location);
            CollectionAssert.AreEquivalent(new[] { "players", "nodes", "edges" }, missing);
        }

        [TestCase("\"name\": \"Sample\",", "\"name\": \"Sample\", \"author\": \"me\",", "author")]
        [TestCase("{ \"distanceScale\": 2.5 }", "{ \"distanceScale\": 2.5, \"width\": 10 }", "map.width")]
        [TestCase("\"color\": \"red\" }", "\"color\": \"red\", \"human\": true }", "players[1].human")]
        [TestCase("\"owner\": null, ", "\"owner\": null, \"name\": \"Luna\", ", "nodes[1].name")]
        [TestCase("{ \"x\": 3, \"y\": 4 }", "{ \"x\": 3, \"y\": 4, \"z\": 0 }", "nodes[1].position.z")]
        [TestCase("\"target\": \"N2\" }", "\"target\": \"N2\", \"distance\": 5 }", "edges[0].distance")]
        [TestCase("\"schemaVersion\": 1,", "\"schemaVersion\": 1, \"SchemaVersion\": 1,", "SchemaVersion")]
        public void An_unknown_field_is_refused_at_every_level(string find, string replacement, string unknown)
        {
            MissionIssue issue = Rejected(With(find, replacement), IssueCode.UnknownField);
            Assert.AreEqual(unknown, issue.Location);
        }

        [TestCase("\"id\": \"sample-mission\"", "\"id\": 7", "id")]
        [TestCase("\"name\": \"Sample\"", "\"name\": null", "name")]
        [TestCase("\"map\": { \"distanceScale\": 2.5 }", "\"map\": 2.5", "map")]
        [TestCase("\"distanceScale\": 2.5", "\"distanceScale\": \"2.5\"", "map.distanceScale")]
        [TestCase("\"type\": \"moon\"", "\"type\": 1", "nodes[1].type")]
        [TestCase("\"position\": { \"x\": 3, \"y\": 4 }", "\"position\": [3, 4]", "nodes[1].position")]
        [TestCase("\"x\": 3", "\"x\": \"3\"", "nodes[1].position.x")]
        [TestCase("\"owner\": null", "\"owner\": 0", "nodes[1].owner")]
        [TestCase("\"owner\": null", "\"owner\": false", "nodes[1].owner")]
        [TestCase("\"darts\": 0", "\"darts\": \"0\"", "nodes[1].darts")]
        [TestCase("\"darts\": 0", "\"darts\": null", "nodes[1].darts")]
        [TestCase("{ \"source\": \"N1\", \"target\": \"N2\" }", "\"N1-N2\"", "edges[0]")]
        [TestCase("{ \"id\": \"player2\", \"color\": \"red\" }", "[\"player2\", \"red\"]", "players[1]")]
        [TestCase("\"schemaVersion\": 1", "\"schemaVersion\": \"1\"", "schemaVersion")]
        public void A_value_of_the_wrong_type_is_refused(string find, string replacement, string where)
        {
            MissionIssue issue = Rejected(With(find, replacement), IssueCode.WrongType);
            Assert.AreEqual(where, issue.Location);
        }

        [TestCase("2")]
        [TestCase("0")]
        [TestCase("-1")]
        [TestCase("1.5")]
        [TestCase("10000000000000000000000")]
        public void Another_format_version_is_refused(string version)
        {
            MissionIssue issue = Rejected(With("\"schemaVersion\": 1", "\"schemaVersion\": " + version),
                IssueCode.UnsupportedSchemaVersion);
            StringAssert.Contains(version, issue.Message);
            StringAssert.Contains("version 1", issue.Message);
        }

        [Test]
        public void A_future_version_is_refused_on_its_version_alone()
        {
            // A later format may have fields this one does not know; only the version is reported.
            string json = "{ \"schemaVersion\": 2, \"id\": \"a\", \"campaign\": { \"chapter\": 3 } }";
            MissionReadResult result = MissionJson.Read(json);

            Assert.IsFalse(result.Ok);
            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(IssueCode.UnsupportedSchemaVersion, result.Issues[0].Code);
        }

        // ------------------------------------------------------------ values the format forbids

        [TestCase("teal")]
        [TestCase("Blue")]
        [TestCase("BLUE")]
        [TestCase("neutral")]
        [TestCase("")]
        public void A_color_outside_the_palette_is_refused(string color)
        {
            MissionIssue issue = Rejected(With("\"color\": \"blue\"", "\"color\": \"" + color + "\""),
                IssueCode.UnsupportedColor);
            Assert.AreEqual("players[0].color", issue.Location);
            StringAssert.Contains("orange", issue.Message, "the message lists the colors that can be used");
        }

        [TestCase("asteroid")]
        [TestCase("Planet")]
        [TestCase("")]
        public void A_node_type_other_than_planet_or_moon_is_refused(string type)
        {
            MissionIssue issue = Rejected(With("\"type\": \"moon\"", "\"type\": \"" + type + "\""),
                IssueCode.UnsupportedNodeType);
            Assert.AreEqual("nodes[1].type", issue.Location);
        }

        [TestCase("-1")]
        [TestCase("1.5")]
        [TestCase("0.5")]
        [TestCase("2147483648")]
        [TestCase("99999999999999999999999")]
        [TestCase("1e400")]
        [TestCase("-1e400")]
        [TestCase("1e-400")]
        public void A_Dart_count_that_is_negative_fractional_or_too_large_is_refused(string darts)
        {
            MissionIssue issue = Rejected(With("\"darts\": 0", "\"darts\": " + darts), IssueCode.InvalidDarts);
            Assert.AreEqual("nodes[1].darts", issue.Location);
            Assert.AreEqual("N2", issue.NodeId);
            StringAssert.Contains("2147483647", issue.Message);
        }

        [Test]
        public void Darts_reach_the_top_of_the_games_range()
        {
            Assert.AreEqual(int.MaxValue, Read(With("\"darts\": 0", "\"darts\": 2147483647")).Nodes[1].Darts);
            Assert.AreEqual(0, Read(With("\"darts\": 0", "\"darts\": -0")).Nodes[1].Darts);
            Assert.AreEqual(30, Read(With("\"darts\": 0", "\"darts\": 3e1")).Nodes[1].Darts, "a whole number, however it is spelled");
        }

        [TestCase("1e999")]
        [TestCase("-1e999")]
        [TestCase("1e-999")]
        public void A_number_that_cannot_be_held_is_refused_rather_than_changed(string number)
        {
            MissionIssue coordinate = Rejected(With("\"x\": 3", "\"x\": " + number), IssueCode.UnrepresentableNumber);
            Assert.AreEqual("nodes[1].position.x", coordinate.Location);

            MissionIssue scale = Rejected(With("\"distanceScale\": 2.5", "\"distanceScale\": " + number),
                IssueCode.UnrepresentableNumber);
            Assert.AreEqual("map.distanceScale", scale.Location);
        }

        [TestCase("1000000000.5")]
        [TestCase("-1e10")]
        [TestCase("1e300")]
        public void A_coordinate_beyond_the_supported_range_is_refused(string coordinate)
        {
            MissionIssue issue = Rejected(With("\"y\": 4", "\"y\": " + coordinate), IssueCode.InvalidCoordinate);
            Assert.AreEqual("nodes[1].position.y", issue.Location);
            Assert.AreEqual("N2", issue.NodeId);
        }

        [Test]
        public void Coordinates_may_be_negative_fractional_and_as_far_as_the_range_goes()
        {
            MissionDefinition mission = Read(With("{ \"x\": 3, \"y\": 4 }", "{ \"x\": -1000000000, \"y\": 1e9 }"));

            Assert.AreEqual(-1e9, mission.Nodes[1].X);
            Assert.AreEqual(1e9, mission.Nodes[1].Y);
        }

        [TestCase("0")]
        [TestCase("-1")]
        [TestCase("-0.5")]
        [TestCase("1e10")]
        public void A_distance_scale_that_is_not_positive_or_is_too_large_is_refused(string scale)
        {
            MissionIssue issue = Rejected(With("\"distanceScale\": 2.5", "\"distanceScale\": " + scale),
                IssueCode.InvalidDistanceScale);
            Assert.AreEqual("map.distanceScale", issue.Location);
        }

        [TestCase("")]
        [TestCase("two words")]
        [TestCase("dot.ted")]
        [TestCase("naïve")]
        [TestCase("N1/2")]
        public void An_ID_with_other_characters_or_none_is_refused_wherever_it_is(string id)
        {
            Assert.AreEqual("id", Rejected(With("\"id\": \"sample-mission\"", "\"id\": \"" + id + "\""), IssueCode.InvalidId).Location);
            Assert.AreEqual("players[0].id",
                Rejected(With("{ \"id\": \"player1\", \"color\"", "{ \"id\": \"" + id + "\", \"color\""), IssueCode.InvalidId).Location);
            Assert.AreEqual("nodes[1].id",
                Rejected(With("{ \"id\": \"N2\",", "{ \"id\": \"" + id + "\","), IssueCode.InvalidId).Location);
        }

        [Test]
        public void Letters_digits_underscores_and_hyphens_make_an_ID()
        {
            Assert.IsTrue(MissionRules.IsValidId("N1"));
            Assert.IsTrue(MissionRules.IsValidId("a"));
            Assert.IsTrue(MissionRules.IsValidId("Outer_Rim-07"));
            Assert.IsTrue(MissionRules.IsValidId("-"));
            Assert.IsFalse(MissionRules.IsValidId(null));
            Assert.IsFalse(MissionRules.IsValidId(""));
            Assert.IsFalse(MissionRules.IsValidId(" N1"));
        }

        [Test]
        public void Two_nodes_or_two_players_cannot_share_an_ID()
        {
            MissionIssue node = Rejected(With("{ \"id\": \"N2\",", "{ \"id\": \"N1\","), IssueCode.DuplicateId);
            Assert.AreEqual("nodes[1].id", node.Location);

            MissionIssue player = Rejected(With("{ \"id\": \"player2\",", "{ \"id\": \"player1\","), IssueCode.DuplicateId);
            Assert.AreEqual("players[1].id", player.Location);
        }

        [Test]
        public void IDs_are_case_sensitive_and_each_kind_has_its_own_names()
        {
            // n1 beside N1, and a player named like a node and like the mission.
            string json = With("{ \"id\": \"N2\",", "{ \"id\": \"n1\",")
                .Replace("\"target\": \"N2\"", "\"target\": \"n1\"")
                .Replace("\"player1\"", "\"N1\"")
                .Replace("\"player2\"", "\"sample-mission\"");

            MissionDefinition mission = Read(json);

            Assert.AreEqual("N1", mission.Nodes[0].Id);
            Assert.AreEqual("n1", mission.Nodes[1].Id);
            Assert.AreEqual("N1", mission.Players[0].Id);
            Assert.AreEqual("N1", mission.Nodes[0].Owner);
            Assert.AreEqual("sample-mission", mission.Players[1].Id);
            Assert.AreSame(mission.Nodes[1], mission.FindNode("n1"));
        }

        [Test]
        public void An_owner_must_be_one_of_the_missions_players()
        {
            MissionIssue issue = Rejected(With("\"owner\": null", "\"owner\": \"player3\""), IssueCode.UnknownOwner);
            Assert.AreEqual("nodes[1].owner", issue.Location);
            Assert.AreEqual("N2", issue.NodeId);

            Rejected(With("\"owner\": null", "\"owner\": \"Player1\""), IssueCode.UnknownOwner);
            Rejected(With("\"owner\": null", "\"owner\": \"\""), IssueCode.UnknownOwner);
        }

        [Test]
        public void Six_players_are_the_most_a_mission_can_have()
        {
            string six = "{ \"id\": \"player2\", \"color\": \"red\" }, { \"id\": \"p3\", \"color\": \"green\" }, "
                + "{ \"id\": \"p4\", \"color\": \"yellow\" }, { \"id\": \"p5\", \"color\": \"purple\" }, "
                + "{ \"id\": \"p6\", \"color\": \"orange\" }";
            Assert.AreEqual(6, Read(With("{ \"id\": \"player2\", \"color\": \"red\" }", six)).Players.Count);

            MissionReadResult seven = MissionJson.Read(With("{ \"id\": \"player2\", \"color\": \"red\" }",
                six + ", { \"id\": \"p7\", \"color\": \"blue\" }"));
            Assert.IsFalse(seven.Ok);
            Assert.IsTrue(Has(seven, IssueCode.TooManyPlayers));
        }

        static bool Has(MissionReadResult result, IssueCode code)
        {
            foreach (MissionIssue issue in result.Issues)
                if (issue.Code == code) return true;
            return false;
        }

        [Test]
        public void Two_players_cannot_have_the_same_color()
        {
            MissionIssue issue = Rejected(With("\"color\": \"red\"", "\"color\": \"blue\""), IssueCode.DuplicateColor);
            Assert.AreEqual("players[1].color", issue.Location);
            StringAssert.Contains("player1", issue.Message);
        }

        [Test]
        public void A_route_must_join_two_existing_different_nodes_once()
        {
            MissionIssue dangling = Rejected(With("\"target\": \"N2\"", "\"target\": \"N9\""), IssueCode.DanglingEdge);
            Assert.AreEqual("edges[0].target", dangling.Location);

            MissionIssue self = Rejected(With("\"target\": \"N2\"", "\"target\": \"N1\""), IssueCode.SelfConnection);
            Assert.AreEqual("edges[0]", self.Location);

            MissionIssue repeated = Rejected(With("{ \"source\": \"N3\", \"target\": \"N1\" }",
                "{ \"source\": \"N1\", \"target\": \"N2\" }"), IssueCode.DuplicateEdge);
            Assert.AreEqual("edges[1]", repeated.Location);

            MissionIssue reversed = Rejected(With("{ \"source\": \"N3\", \"target\": \"N1\" }",
                "{ \"source\": \"N2\", \"target\": \"N1\" }"), IssueCode.DuplicateEdge);
            Assert.AreEqual("edges[1]", reversed.Location);
            Assert.AreEqual("N2", reversed.NodeId);
            Assert.AreEqual("N1", reversed.OtherNodeId);
        }

        [Test]
        public void A_self_connection_is_an_error_even_between_valid_coordinates()
        {
            MissionDefinition mission = Mission(("N1", 1, 1), ("N2", 2, 2));
            mission.Edges.Add(new MissionEdge("N1", "N1"));

            MissionValidation validation = MissionValidator.Validate(mission);

            Assert.IsTrue(validation.HasErrors);
            CollectionAssert.AreEqual(new[] { IssueCode.SelfConnection }, Codes(validation));
        }

        [Test]
        public void The_errors_of_a_file_are_reported_together()
        {
            string json = With("\"darts\": 0", "\"darts\": 1.5")
                .Replace("\"color\": \"red\"", "\"color\": \"pink\"")
                .Replace("\"y\": 0.25 }", "\"y\": 0.25, \"z\": 1 }");
            MissionReadResult result = MissionJson.Read(json);

            Assert.IsFalse(result.Ok);
            Assert.IsTrue(Has(result, IssueCode.InvalidDarts));
            Assert.IsTrue(Has(result, IssueCode.UnsupportedColor));
            Assert.IsTrue(Has(result, IssueCode.UnknownField));

            // So are the errors of a file that has the right shape and wrong values.
            json = With("\"darts\": 0", "\"darts\": -4")
                .Replace("\"owner\": \"player2\"", "\"owner\": \"ghost\"")
                .Replace("\"target\": \"N2\"", "\"target\": \"N1\"");
            result = MissionJson.Read(json);

            Assert.IsFalse(result.Ok);
            Assert.IsTrue(Has(result, IssueCode.InvalidDarts));
            Assert.IsTrue(Has(result, IssueCode.UnknownOwner));
            Assert.IsTrue(Has(result, IssueCode.SelfConnection));
        }

        [Test]
        public void The_validator_judges_a_mission_built_in_memory_the_same_way()
        {
            MissionDefinition mission = Mission(("N1", double.PositiveInfinity, 0), ("N2", 0, double.NaN), ("bad id", 0, 0));
            mission.Id = "";
            mission.Name = null;
            mission.DistanceScale = double.NaN;
            mission.Nodes[0].Darts = -1;
            mission.Nodes[1].Type = (MissionNodeType)9;
            mission.Nodes[1].Owner = "ghost";
            mission.Players.Add(new MissionPlayer("p", (PlayerColor)42));
            mission.Edges.Add(new MissionEdge("N1", null));

            MissionValidation validation = MissionValidator.Validate(mission);

            Assert.IsTrue(validation.HasErrors);
            Assert.AreEqual(0, validation.WarningCount, "a layout is only described once the mission is sound");
            foreach (IssueCode code in new[]
                     {
                         IssueCode.InvalidId, IssueCode.InvalidName, IssueCode.InvalidDistanceScale, IssueCode.InvalidCoordinate,
                         IssueCode.InvalidDarts, IssueCode.UnsupportedNodeType, IssueCode.UnknownOwner,
                         IssueCode.UnsupportedColor, IssueCode.DanglingEdge,
                     })
                Assert.IsTrue(validation.Has(code), code + " should be reported");
        }

        // ------------------------------------------------------------ warnings

        [Test]
        public void A_map_in_separate_groups_earns_one_warning_and_isolated_nodes_count_as_groups()
        {
            MissionDefinition mission = Mission(("N1", 0, 0), ("N2", 1, 0), ("N3", 2, 0), ("N4", 3, 0));
            mission.Edges.Add(new MissionEdge("N1", "N2"));

            MissionValidation validation = MissionValidator.Validate(mission);

            Assert.IsFalse(validation.HasErrors);
            CollectionAssert.AreEqual(new[] { IssueCode.DisconnectedGraph }, Codes(validation));
            StringAssert.Contains("3 separate groups", validation.Issues[0].Message);
            Assert.AreEqual(IssueSeverity.Warning, validation.Issues[0].Severity);
            Assert.AreEqual(3, MissionValidator.ConnectedGroups(mission).Count);
        }

        [Test]
        public void A_connected_map_and_a_single_node_earn_no_warning()
        {
            MissionDefinition chain = Mission(("N1", 0, 0), ("N2", 1, 0), ("N3", 2, 0));
            chain.Edges.Add(new MissionEdge("N2", "N1"));
            chain.Edges.Add(new MissionEdge("N2", "N3"));
            Assert.AreEqual(0, MissionValidator.Validate(chain).Issues.Count);

            Assert.AreEqual(0, MissionValidator.Validate(Mission(("N1", 5, 5))).Issues.Count);
        }

        [Test]
        public void Nodes_on_the_same_spot_and_a_route_of_no_length_earn_warnings()
        {
            MissionDefinition mission = Mission(("N1", 2.5, -1), ("N2", 2.5, -1), ("N3", 0, 0));
            mission.Edges.Add(new MissionEdge("N1", "N2"));
            mission.Edges.Add(new MissionEdge("N2", "N3"));

            MissionValidation validation = MissionValidator.Validate(mission);

            Assert.IsFalse(validation.HasErrors, "overlapping nodes and zero-length routes are allowed");
            CollectionAssert.AreEquivalent(new[] { IssueCode.CoincidentNodes, IssueCode.ZeroLengthRoute }, Codes(validation));
            foreach (MissionIssue issue in validation.Issues)
            {
                Assert.AreEqual(IssueSeverity.Warning, issue.Severity);
                StringAssert.Contains("N1", issue.Message);
                StringAssert.Contains("N2", issue.Message);
            }
        }

        [Test]
        public void A_file_with_only_warnings_opens_and_brings_its_warnings()
        {
            string json = With("{ \"source\": \"N3\", \"target\": \"N1\" }", "{ \"source\": \"N2\", \"target\": \"N1\" }")
                .Replace("{ \"source\": \"N1\", \"target\": \"N2\" },", "");
            MissionReadResult result = MissionJson.Read(json);

            Assert.IsTrue(result.Ok);
            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(IssueCode.DisconnectedGraph, result.Issues[0].Code);
        }

        // ------------------------------------------------------------ distances

        [Test]
        public void Distance_is_the_straight_line_between_two_nodes_times_the_scale()
        {
            MissionDefinition mission = Read(Sample);
            MissionEdge edge = mission.FindEdge("N2", "N1");

            Assert.AreEqual(5d, mission.MapDistance(edge), "a 3 by 4 difference is 5 apart");
            Assert.AreEqual(12.5, mission.GameDistance(edge), "scale 2.5");

            mission.DistanceScale = 0.5;
            Assert.AreEqual(2.5, mission.GameDistance(edge));
            Assert.AreEqual(5d, mission.MapDistance(edge), "the scale does not change the map");

            mission.FindNode("N1").X = -3;
            mission.FindNode("N1").Y = -4;
            Assert.AreEqual(10d, mission.MapDistance(edge));
            Assert.AreEqual(5d, mission.GameDistance(edge));
        }

        [Test]
        public void Numbers_are_written_in_full_with_a_period_whatever_the_language()
        {
            Assert.AreEqual("0.1", MissionNumbers.Format(0.1));
            Assert.AreEqual("-2.5", MissionNumbers.Format(-2.5));
            Assert.AreEqual("3", MissionNumbers.Format(3d));
            Assert.AreEqual("0", MissionNumbers.Format(-0d));

            foreach (double value in new[] { 0.1 + 0.2, 1d / 3d, 1e-9, 1e21, 123456789.12345679 })
            {
                Assert.IsTrue(MissionNumbers.TryParse(MissionNumbers.Format(value), out double back));
                Assert.AreEqual(value, back);
            }
        }
    }
}
