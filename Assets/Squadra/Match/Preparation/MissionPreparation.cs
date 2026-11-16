using System;
using System.Collections.Generic;
using System.Globalization;
using Squadra.Missions;
using Squadra.Simulation;

namespace Squadra.Preparation
{
    /// <summary>
    /// Turns a mission file into a mission the game can play, or says why it cannot. The file is read with
    /// the mission format's own strict reader and validator; what this adds are the checks of what the game
    /// can run today, which are deliberately not part of the format: the Mission Editor may still save an
    /// unfinished map. Preparing a mission creates no match and advances no time.
    ///
    /// Map positions become game positions by the distance scale, X to the right and Y up; body sizes are
    /// radii in game units already and are kept as they are. Nodes and players keep their file IDs; the
    /// match gets node indices in file order, and routes are resolved by node ID. Every world is given a
    /// surface variant and a name from its own type's pool, dealt so that a mission repeats neither before
    /// its pool runs out, and the mission is given one space backdrop.
    /// </summary>
    public static class MissionPreparation
    {
        /// <summary>The player the human controls, shown as Blue.</summary>
        public const string HumanPlayerId = "player1";

        /// <summary>The opponent, shown as Red.</summary>
        public const string OpponentPlayerId = "player2";

        /// <summary>
        /// The largest game coordinate, either sign, after the distance scale. Positions are single
        /// precision on the Unity side, which still places them to better than a hundredth of a unit here.
        /// </summary>
        public const double MaxWorldCoordinate = 100000;

        /// <summary>
        /// The most Darts a mission may start with, all worlds together. Counts are 32-bit and production
        /// keeps adding to them, so a start near that limit could not be played safely.
        /// </summary>
        public const long MaxStartingDarts = 1000000;

        /// <summary>
        /// Prepares a mission and deals its worlds' looks with <paramref name="random"/>.
        /// <paramref name="catalogId"/> is the campaign entry's ID and <paramref name="fileName"/> the file's
        /// name without <c>.json</c>; both must equal the mission's ID exactly. Pass null for either to skip
        /// that check, as for a mission played outside the campaign.
        /// </summary>
        public static PreparationOutcome Prepare(string catalogId, string fileName, string text, WorldPools pools,
            Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            return Prepare(catalogId, fileName, text, pools, random, null);
        }

        /// <summary>
        /// Prepares a mission with looks decided elsewhere, as every participant of a shared match must. An
        /// assignment that misses a node or names a surface variant this game does not have is refused,
        /// never patched.
        /// </summary>
        public static PreparationOutcome Prepare(string catalogId, string fileName, string text, WorldPools pools,
            WorldAssignment assignment)
        {
            if (assignment == null) throw new ArgumentNullException(nameof(assignment));
            return Prepare(catalogId, fileName, text, pools, null, assignment);
        }

        static PreparationOutcome Prepare(string catalogId, string fileName, string text, WorldPools pools,
            Random random, WorldAssignment assignment)
        {
            if (pools == null) throw new ArgumentNullException(nameof(pools));

            var problems = new List<string>();
            var warnings = new List<string>();
            MissionReadResult read = MissionJson.Read(text);
            foreach (MissionIssue issue in read.Issues)
                (issue.IsError ? problems : warnings).Add(issue.ToString());
            if (!read.Ok) return new PreparationOutcome(null, problems, warnings);

            MissionDefinition mission = read.Mission;
            CheckIdentity(catalogId, fileName, mission, problems);
            CheckPlayers(mission, problems);
            CheckWorlds(mission, pools, problems);
            if (problems.Count > 0) return new PreparationOutcome(null, problems, warnings);

            WorldAssignment looks = assignment ?? Deal(mission, pools, random);
            if (assignment != null) CheckAssignment(mission, pools, assignment, problems);
            if (problems.Count > 0) return new PreparationOutcome(null, problems, warnings);

            return new PreparationOutcome(Build(mission, looks), problems, warnings);
        }

        // ------------------------------------------------------------------ checks

        static void CheckIdentity(string catalogId, string fileName, MissionDefinition mission, List<string> problems)
        {
            if (catalogId != null && !MissionRules.SameId(catalogId, mission.Id))
            {
                problems.Add("id: The campaign entry is \"" + catalogId + "\" but the file's ID is \"" + mission.Id
                    + "\". They must match exactly, including case.");
            }

            if (fileName != null && !MissionRules.SameId(fileName, mission.Id))
            {
                problems.Add("id: The file is named \"" + fileName + ".json\" but its ID is \"" + mission.Id
                    + "\". They must match exactly, including case.");
            }
        }

        static void CheckPlayers(MissionDefinition mission, List<string> problems)
        {
            MissionPlayer human = mission.FindPlayer(HumanPlayerId);
            MissionPlayer opponent = mission.FindPlayer(OpponentPlayerId);
            if (mission.Players.Count != 2 || human == null || opponent == null)
            {
                var ids = new List<string>();
                foreach (MissionPlayer player in mission.Players) ids.Add("\"" + player.Id + "\"");
                problems.Add("players: The game plays exactly two players so far, \"" + HumanPlayerId
                    + "\" (blue, the human player) and \"" + OpponentPlayerId + "\" (red). This mission has "
                    + (ids.Count == 0 ? "none" : string.Join(", ", ids)) + ".");
                return;
            }

            CheckColor(mission, human, PlayerColor.Blue, "Blue", problems);
            CheckColor(mission, opponent, PlayerColor.Red, "Red", problems);
            CheckOwnsANode(mission, HumanPlayerId, problems);
            CheckOwnsANode(mission, OpponentPlayerId, problems);
        }

        static void CheckColor(MissionDefinition mission, MissionPlayer player, PlayerColor color, string side,
            List<string> problems)
        {
            if (player.Color == color) return;

            problems.Add("players[" + mission.Players.IndexOf(player) + "].color: \"" + player.Id + "\" plays " + side
                + ", so its color must be \"" + MissionRules.NameOf(color) + "\", not \""
                + MissionRules.NameOf(player.Color) + "\".");
        }

        static void CheckOwnsANode(MissionDefinition mission, string playerId, List<string> problems)
        {
            foreach (MissionNode node in mission.Nodes)
                if (MissionRules.SameId(node.Owner, playerId)) return;

            problems.Add("players: \"" + playerId + "\" owns no node, so that side would be out of the match before"
                + " it starts. Give it at least one node.");
        }

        static void CheckWorlds(MissionDefinition mission, WorldPools pools, List<string> problems)
        {
            long darts = 0;
            var reported = new HashSet<MissionNodeType>();
            for (int i = 0; i < mission.Nodes.Count; i++)
            {
                MissionNode node = mission.Nodes[i];
                string at = "nodes[" + i + "]";
                double x = node.X * mission.DistanceScale;
                double y = node.Y * mission.DistanceScale;
                if (!(Math.Abs(x) <= MaxWorldCoordinate && Math.Abs(y) <= MaxWorldCoordinate))
                {
                    problems.Add(at + ".position: Node " + node.Id + " is at (" + Number(x) + ", " + Number(y)
                        + ") in game units, its position times the distance scale. The game can place worlds up to "
                        + Number(MaxWorldCoordinate) + " either way; move it or lower the distance scale.");
                }

                darts += node.Darts;

                if (!reported.Add(node.Type)) continue;

                string type = MissionRules.NameOf(node.Type);
                WorldPool pool = pools.For(node.Type);
                if (pool.VariantIds.Count == 0)
                {
                    problems.Add(at + ".type: No " + type + " surface materials come with the game, so node " + node.Id
                        + " cannot be shown. Add a material to the " + type + " pool.");
                }

                if (pool.Names.Count == 0)
                {
                    problems.Add(at + ".type: The " + type + " name dictionary has no names, so node " + node.Id
                        + " cannot be named. Add names to it.");
                }
            }

            if (darts > MaxStartingDarts)
            {
                problems.Add("nodes: The mission starts with " + darts.ToString(CultureInfo.InvariantCulture)
                    + " Darts in all; the game can start with at most "
                    + MaxStartingDarts.ToString(CultureInfo.InvariantCulture) + ".");
            }

            if (pools.Backdrops.Count == 0)
            {
                problems.Add("No space backdrop materials come with the game, so no mission can be shown. Add a material"
                    + " to the backdrop pool.");
            }
        }

        static void CheckAssignment(MissionDefinition mission, WorldPools pools, WorldAssignment assignment,
            List<string> problems)
        {
            if (!MissionRules.SameId(assignment.MissionId, mission.Id))
            {
                problems.Add("The world assignment is for mission \"" + assignment.MissionId + "\", not \""
                    + mission.Id + "\".");
                return;
            }

            if (!pools.HasBackdrop(assignment.BackdropId))
            {
                problems.Add("The world assignment shows the space backdrop \"" + assignment.BackdropId
                    + "\", which this game does not have.");
            }

            for (int i = 0; i < mission.Nodes.Count; i++)
            {
                MissionNode node = mission.Nodes[i];
                string at = "nodes[" + i + "]: Node " + node.Id;
                WorldLook look = assignment.For(node.Id);
                if (look == null)
                {
                    problems.Add(at + " has no look in the world assignment.");
                    continue;
                }

                if (!pools.For(node.Type).HasVariant(look.VariantId))
                {
                    problems.Add(at + " is assigned the " + MissionRules.NameOf(node.Type) + " surface \""
                        + look.VariantId + "\", which this game does not have.");
                }

                if (look.Name.Trim().Length == 0) problems.Add(at + " has no name in the world assignment.");
            }

            foreach (string nodeId in assignment.NodeIds)
            {
                if (mission.FindNode(nodeId) == null)
                    problems.Add("The world assignment names node \"" + nodeId + "\", which this mission does not have.");
            }
        }

        // ------------------------------------------------------------------ results

        // Each type is dealt on its own, surfaces and names separately, so one pool running out never makes
        // the other repeat early. Types and nodes go in a fixed order, and the backdrop comes last, so one
        // random sequence gives one deal.
        static WorldAssignment Deal(MissionDefinition mission, WorldPools pools, Random random)
        {
            var looks = new Dictionary<string, WorldLook>(StringComparer.Ordinal);
            foreach (MissionNodeType type in new[] { MissionNodeType.Planet, MissionNodeType.Moon, MissionNodeType.Asteroid })
            {
                List<MissionNode> nodes = mission.Nodes.FindAll(node => node.Type == type);
                if (nodes.Count == 0) continue;

                WorldPool pool = pools.For(type);
                List<string> variants = AppearanceDealer.Deal(pool.VariantIds, nodes.Count, random);
                List<string> names = AppearanceDealer.Deal(pool.Names, nodes.Count, random);
                for (int i = 0; i < nodes.Count; i++)
                    looks.Add(nodes[i].Id, new WorldLook(variants[i], names[i]));
            }

            var ordered = new List<KeyValuePair<string, WorldLook>>();
            foreach (MissionNode node in mission.Nodes)
                ordered.Add(new KeyValuePair<string, WorldLook>(node.Id, looks[node.Id]));

            string backdrop = AppearanceDealer.Deal(pools.Backdrops, 1, random)[0];
            return new WorldAssignment(mission.Id, backdrop, ordered);
        }

        static PreparedMission Build(MissionDefinition mission, WorldAssignment looks)
        {
            var worlds = new List<PreparedWorld>();
            for (int i = 0; i < mission.Nodes.Count; i++)
            {
                MissionNode node = mission.Nodes[i];
                var position = new MapPoint((float)(node.X * mission.DistanceScale), (float)(node.Y * mission.DistanceScale));
                worlds.Add(new PreparedWorld(node.Id, i, KindOf(node.Type), FactionOf(node.Owner), node.Darts, position,
                    (float)node.Size, looks.For(node.Id)));
            }

            var routes = new List<Route>();
            foreach (MissionEdge edge in mission.Edges)
            {
                int source = mission.Nodes.FindIndex(node => MissionRules.SameId(node.Id, edge.Source));
                int target = mission.Nodes.FindIndex(node => MissionRules.SameId(node.Id, edge.Target));
                routes.Add(new Route(source, target));
            }

            return new PreparedMission(mission.Id, mission.Name, worlds, routes, looks);
        }

        static NodeKind KindOf(MissionNodeType type)
        {
            switch (type)
            {
                case MissionNodeType.Planet: return NodeKind.Planet;
                case MissionNodeType.Moon: return NodeKind.Moon;
                default: return NodeKind.Asteroid;
            }
        }

        // The player IDs, not their colors, decide the side: the colors are checked to agree.
        static Faction FactionOf(string owner)
        {
            if (owner == null) return Faction.Neutral;
            return MissionRules.SameId(owner, HumanPlayerId) ? Faction.Blue : Faction.Red;
        }

        static string Number(double value) => value.ToString("G6", CultureInfo.InvariantCulture);
    }
}
