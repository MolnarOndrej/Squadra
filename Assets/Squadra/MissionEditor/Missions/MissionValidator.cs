using System;
using System.Collections.Generic;
using System.Text;

namespace Squadra.Missions
{
    /// <summary>
    /// The one judge of a mission definition, used for a file being opened, for the mission being
    /// edited and before every save. Errors are things the format forbids. Warnings describe a layout
    /// that is allowed and probably unfinished: separate groups of nodes, nodes on the same spot and
    /// routes of no length. It looks at the definition only; whether the game could play the mission
    /// is not its business.
    /// </summary>
    public static class MissionValidator
    {
        public static MissionValidation Validate(MissionDefinition mission)
        {
            if (mission == null) throw new ArgumentNullException(nameof(mission));

            var issues = new List<MissionIssue>();
            CheckMission(mission, issues);
            CheckPlayers(mission, issues);
            CheckNodes(mission, issues);
            CheckEdges(mission, issues);

            // A layout is only worth describing once the mission itself is sound.
            if (issues.Count == 0) AddWarnings(mission, issues);
            return new MissionValidation(issues);
        }

        // ------------------------------------------------------------------ errors

        static void CheckMission(MissionDefinition mission, List<MissionIssue> issues)
        {
            if (!MissionRules.IsValidId(mission.Id))
                issues.Add(Error(IssueCode.InvalidId, "id",
                    "The mission ID " + Quote(mission.Id) + " is not valid. " + MissionRules.IdRule));

            if (mission.Name == null)
                issues.Add(Error(IssueCode.InvalidName, "name", "The mission has no name. Give it one, even an empty one."));

            double scale = mission.DistanceScale;
            if (!MissionRules.IsFinite(scale) || scale <= 0d)
                issues.Add(Error(IssueCode.InvalidDistanceScale, "map.distanceScale",
                    "The distance scale is " + MissionNumbers.Format(scale) + ". It must be a number greater than 0."));
            else if (scale > MissionRules.MaxDistanceScale)
                issues.Add(Error(IssueCode.InvalidDistanceScale, "map.distanceScale",
                    "The distance scale is " + MissionNumbers.Format(scale) + ". The largest supported scale is "
                    + MissionNumbers.Format(MissionRules.MaxDistanceScale) + "."));
        }

        static void CheckPlayers(MissionDefinition mission, List<MissionIssue> issues)
        {
            if (mission.Players.Count > MissionRules.MaxPlayers)
                issues.Add(Error(IssueCode.TooManyPlayers, "players",
                    "The mission has " + mission.Players.Count + " players. A mission can have at most "
                    + MissionRules.MaxPlayers + "; remove the others."));

            var ids = new Dictionary<string, int>(StringComparer.Ordinal);
            var colors = new Dictionary<PlayerColor, int>();
            for (int i = 0; i < mission.Players.Count; i++)
            {
                MissionPlayer player = mission.Players[i];
                string at = "players[" + i + "]";
                if (player == null)
                {
                    issues.Add(Error(IssueCode.WrongType, at, "This player entry is empty."));
                    continue;
                }

                if (!MissionRules.IsValidId(player.Id))
                    issues.Add(Error(IssueCode.InvalidId, at + ".id",
                        "The player ID " + Quote(player.Id) + " is not valid. " + MissionRules.IdRule));
                else if (ids.TryGetValue(player.Id, out int first))
                    issues.Add(Error(IssueCode.DuplicateId, at + ".id",
                        "The player ID " + Quote(player.Id) + " is already used by players[" + first
                        + "]. Every player needs its own ID."));
                else
                    ids.Add(player.Id, i);

                if (!MissionRules.IsDefined(player.Color))
                    issues.Add(Error(IssueCode.UnsupportedColor, at + ".color",
                        "Player " + Quote(player.Id) + " has a color this version does not know. Use one of: "
                        + ColorList() + "."));
                else if (colors.TryGetValue(player.Color, out int other))
                    issues.Add(Error(IssueCode.DuplicateColor, at + ".color",
                        "Player " + Quote(player.Id) + " is " + MissionRules.NameOf(player.Color)
                        + ", the same color as player " + Quote(mission.Players[other].Id)
                        + ". Every player needs a color of its own."));
                else
                    colors.Add(player.Color, i);
            }
        }

        static void CheckNodes(MissionDefinition mission, List<MissionIssue> issues)
        {
            var ids = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < mission.Nodes.Count; i++)
            {
                MissionNode node = mission.Nodes[i];
                string at = "nodes[" + i + "]";
                if (node == null)
                {
                    issues.Add(Error(IssueCode.WrongType, at, "This node entry is empty."));
                    continue;
                }

                if (!MissionRules.IsValidId(node.Id))
                    issues.Add(Error(IssueCode.InvalidId, at + ".id",
                        "The node ID " + Quote(node.Id) + " is not valid. " + MissionRules.IdRule));
                else if (ids.TryGetValue(node.Id, out int first))
                    issues.Add(Error(IssueCode.DuplicateId, at + ".id",
                        "The node ID " + Quote(node.Id) + " is already used by nodes[" + first
                        + "]. Every node needs its own ID.", node.Id));
                else
                    ids.Add(node.Id, i);

                if (!MissionRules.IsDefined(node.Type))
                    issues.Add(Error(IssueCode.UnsupportedNodeType, at + ".type",
                        "Node " + Quote(node.Id) + " has a type this version does not know. Use \"planet\" or \"moon\".",
                        node.Id));

                CheckCoordinate(node, node.X, at + ".position.x", "X", issues);
                CheckCoordinate(node, node.Y, at + ".position.y", "Y", issues);

                if (node.Darts < 0)
                    issues.Add(Error(IssueCode.InvalidDarts, at + ".darts",
                        "Node " + Quote(node.Id) + " starts with " + node.Darts
                        + " Darts. The number must be a whole number from 0 to " + MissionRules.MaxDarts + ".",
                        node.Id));

                if (node.Owner != null && mission.FindPlayer(node.Owner) == null)
                    issues.Add(Error(IssueCode.UnknownOwner, at + ".owner",
                        "Node " + Quote(node.Id) + " is owned by " + Quote(node.Owner)
                        + ", which is not a player of this mission. Add that player or make the node neutral (null).",
                        node.Id));
            }
        }

        static void CheckCoordinate(MissionNode node, double value, string at, string axis, List<MissionIssue> issues)
        {
            if (MissionRules.IsValidCoordinate(value)) return;

            string problem = MissionRules.IsFinite(value)
                ? "is " + MissionNumbers.Format(value) + ", outside the supported range of "
                  + MissionNumbers.Format(-MissionRules.MaxCoordinate) + " to "
                  + MissionNumbers.Format(MissionRules.MaxCoordinate)
                : "is not a finite number";
            issues.Add(Error(IssueCode.InvalidCoordinate, at,
                "The " + axis + " coordinate of node " + Quote(node.Id) + " " + problem + ".", node.Id));
        }

        static void CheckEdges(MissionDefinition mission, List<MissionIssue> issues)
        {
            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < mission.Edges.Count; i++)
            {
                MissionEdge edge = mission.Edges[i];
                string at = "edges[" + i + "]";
                if (edge == null)
                {
                    issues.Add(Error(IssueCode.WrongType, at, "This route entry is empty."));
                    continue;
                }

                bool sourceKnown = mission.FindNode(edge.Source) != null;
                bool targetKnown = mission.FindNode(edge.Target) != null;
                if (!sourceKnown)
                    issues.Add(Error(IssueCode.DanglingEdge, at + ".source",
                        "This route starts at " + Quote(edge.Source) + ", which is not a node of this mission. "
                        + "Remove the route or add the node."));
                if (!targetKnown)
                    issues.Add(Error(IssueCode.DanglingEdge, at + ".target",
                        "This route ends at " + Quote(edge.Target) + ", which is not a node of this mission. "
                        + "Remove the route or add the node."));
                if (!sourceKnown || !targetKnown) continue;

                if (MissionRules.SameId(edge.Source, edge.Target))
                {
                    issues.Add(Error(IssueCode.SelfConnection, at,
                        "This route connects node " + Quote(edge.Source) + " to itself. A route needs two different nodes.",
                        edge.Source));
                    continue;
                }

                string key = PairKey(edge.Source, edge.Target);
                if (seen.TryGetValue(key, out int first))
                    issues.Add(Error(IssueCode.DuplicateEdge, at,
                        "Nodes " + Quote(edge.Source) + " and " + Quote(edge.Target)
                        + " are already connected by edges[" + first
                        + "]. Routes run both ways, so one entry per pair is enough.",
                        edge.Source, edge.Target));
                else
                    seen.Add(key, i);
            }
        }

        // ------------------------------------------------------------------ warnings

        static void AddWarnings(MissionDefinition mission, List<MissionIssue> issues)
        {
            AddCoincidentNodes(mission, issues);
            AddZeroLengthRoutes(mission, issues);
            AddDisconnectedGroups(mission, issues);
        }

        static void AddCoincidentNodes(MissionDefinition mission, List<MissionIssue> issues)
        {
            var groups = new Dictionary<(double, double), List<MissionNode>>();
            var order = new List<List<MissionNode>>();
            foreach (MissionNode node in mission.Nodes)
            {
                // Adding zero turns -0 into 0, so the two spellings of the origin are one place.
                var key = (node.X + 0d, node.Y + 0d);
                if (!groups.TryGetValue(key, out List<MissionNode> group))
                {
                    group = new List<MissionNode>();
                    groups.Add(key, group);
                    order.Add(group);
                }

                group.Add(node);
            }

            foreach (List<MissionNode> group in order)
            {
                if (group.Count < 2) continue;

                MissionNode first = group[0];
                issues.Add(new MissionIssue(IssueSeverity.Warning, IssueCode.CoincidentNodes, "nodes",
                    "Nodes " + IdList(group) + " are at the same position (" + MissionNumbers.Format(first.X) + ", "
                    + MissionNumbers.Format(first.Y) + "). Move one of them if that is not intended.",
                    first.Id));
            }
        }

        static void AddZeroLengthRoutes(MissionDefinition mission, List<MissionIssue> issues)
        {
            for (int i = 0; i < mission.Edges.Count; i++)
            {
                MissionEdge edge = mission.Edges[i];
                if (mission.MapDistance(edge) != 0d) continue;

                issues.Add(new MissionIssue(IssueSeverity.Warning, IssueCode.ZeroLengthRoute, "edges[" + i + "]",
                    "The route between " + Quote(edge.Source) + " and " + Quote(edge.Target)
                    + " has no length because both nodes are at the same position.",
                    edge.Source, edge.Target));
            }
        }

        static void AddDisconnectedGroups(MissionDefinition mission, List<MissionIssue> issues)
        {
            List<List<MissionNode>> groups = ConnectedGroups(mission);
            if (groups.Count < 2) return;

            var text = new StringBuilder();
            text.Append("The map is in ").Append(groups.Count)
                .Append(" separate groups that no route joins: ");
            const int shown = 4;
            for (int i = 0; i < groups.Count && i < shown; i++)
            {
                if (i > 0) text.Append("; ");
                text.Append(IdList(groups[i]));
            }

            if (groups.Count > shown) text.Append("; and ").Append(groups.Count - shown).Append(" more");
            text.Append(". Fleets could not travel between them.");

            // Points at the smallest group, the one most likely to have been forgotten.
            List<MissionNode> smallest = groups[0];
            foreach (List<MissionNode> group in groups)
                if (group.Count < smallest.Count) smallest = group;
            issues.Add(new MissionIssue(IssueSeverity.Warning, IssueCode.DisconnectedGraph, "edges",
                text.ToString(), smallest[0].Id));
        }

        /// <summary>
        /// The groups of nodes that routes join together, in node order. A node with no route is a
        /// group of its own. Only meaningful for a mission without errors.
        /// </summary>
        public static List<List<MissionNode>> ConnectedGroups(MissionDefinition mission)
        {
            if (mission == null) throw new ArgumentNullException(nameof(mission));

            int count = mission.Nodes.Count;
            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < count; i++)
                if (mission.Nodes[i]?.Id != null && !index.ContainsKey(mission.Nodes[i].Id)) index.Add(mission.Nodes[i].Id, i);

            var parent = new int[count];
            for (int i = 0; i < count; i++) parent[i] = i;

            int Find(int i)
            {
                while (parent[i] != i)
                {
                    parent[i] = parent[parent[i]];
                    i = parent[i];
                }

                return i;
            }

            foreach (MissionEdge edge in mission.Edges)
            {
                if (edge?.Source == null || edge.Target == null) continue;
                if (!index.TryGetValue(edge.Source, out int a) || !index.TryGetValue(edge.Target, out int b)) continue;

                int rootA = Find(a);
                int rootB = Find(b);
                if (rootA != rootB) parent[Math.Max(rootA, rootB)] = Math.Min(rootA, rootB);
            }

            var byRoot = new Dictionary<int, List<MissionNode>>();
            var groups = new List<List<MissionNode>>();
            for (int i = 0; i < count; i++)
            {
                int root = Find(i);
                if (!byRoot.TryGetValue(root, out List<MissionNode> group))
                {
                    group = new List<MissionNode>();
                    byRoot.Add(root, group);
                    groups.Add(group);
                }

                group.Add(mission.Nodes[i]);
            }

            return groups;
        }

        // ------------------------------------------------------------------ wording

        static MissionIssue Error(IssueCode code, string location, string message, string nodeId = null,
            string otherNodeId = null)
        {
            return new MissionIssue(IssueSeverity.Error, code, location, message, nodeId, otherNodeId);
        }

        // A length prefix keeps the key of one pair from ever reading as another's.
        static string PairKey(string a, string b)
        {
            if (string.CompareOrdinal(a, b) > 0) (a, b) = (b, a);
            return a.Length + ":" + a + "|" + b;
        }

        static string Quote(string text)
        {
            return text == null ? "(none)" : "\"" + text + "\"";
        }

        static string IdList(List<MissionNode> nodes)
        {
            const int shown = 6;
            var text = new StringBuilder();
            for (int i = 0; i < nodes.Count && i < shown; i++)
            {
                if (i > 0) text.Append(i == nodes.Count - 1 ? " and " : ", ");
                text.Append(nodes[i].Id);
            }

            if (nodes.Count > shown) text.Append(" and ").Append(nodes.Count - shown).Append(" more");
            return text.ToString();
        }

        static string ColorList()
        {
            var text = new StringBuilder();
            foreach (PlayerColor color in MissionRules.AllColors)
            {
                if (text.Length > 0) text.Append(", ");
                text.Append(MissionRules.NameOf(color));
            }

            return text.ToString();
        }
    }
}
