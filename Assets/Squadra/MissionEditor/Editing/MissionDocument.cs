using System;
using System.Collections.Generic;
using System.Globalization;
using Squadra.Missions;

namespace Squadra.MissionEditing
{
    /// <summary>
    /// A mission while it is being edited. Every change to the mission goes through one of its
    /// operations, and each operation either leaves the mission valid or refuses and changes nothing,
    /// so the graph can never hold a route to nowhere, a second route between the same two nodes, two
    /// players of one color or an owner that is not a player. It needs no window, scene or file.
    /// </summary>
    public sealed class MissionDocument
    {
        readonly MissionDefinition mission;
        long highestGeneratedNode;

        /// <summary>Takes a copy of the mission, which must be valid.</summary>
        public MissionDocument(MissionDefinition mission)
        {
            if (mission == null) throw new ArgumentNullException(nameof(mission));
            if (MissionValidator.Validate(mission).HasErrors)
                throw new ArgumentException("A mission with errors cannot be edited.", nameof(mission));

            this.mission = mission.Clone();
        }

        /// <summary>
        /// A new mission: no nodes or routes, the name Untitled Mission, distance scale 1 and two
        /// players, <c>player1</c> in blue and <c>player2</c> in red.
        /// </summary>
        public static MissionDocument CreateNew(string missionId)
        {
            var mission = new MissionDefinition { Id = missionId, Name = DefaultName, DistanceScale = 1d };
            mission.Players.Add(new MissionPlayer("player1", PlayerColor.Blue));
            mission.Players.Add(new MissionPlayer("player2", PlayerColor.Red));
            return new MissionDocument(mission);
        }

        public const string DefaultName = "Untitled Mission";

        /// <summary>A mission ID no other mission is likely ever to get.</summary>
        public static string GenerateMissionId()
        {
            return "mission-" + Guid.NewGuid().ToString("N");
        }

        /// <summary>The mission as it stands. Read it freely; change it only through this document.</summary>
        public MissionDefinition Mission => mission;

        /// <summary>Goes up by one with every edit that changed the mission.</summary>
        public int Revision { get; private set; }

        /// <summary>Raised after every edit that changed the mission.</summary>
        public event Action Changed;

        /// <summary>What the validator says about the mission as it stands.</summary>
        public MissionValidation Validate()
        {
            return MissionValidator.Validate(mission);
        }

        void Touch()
        {
            Revision++;
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ mission settings

        public EditResult SetMissionId(string id)
        {
            if (!MissionRules.IsValidId(id)) return EditResult.Refused("That is not a valid mission ID. " + MissionRules.IdRule);
            if (MissionRules.SameId(mission.Id, id)) return EditResult.NoChange;

            mission.Id = id;
            Touch();
            return EditResult.Done();
        }

        public EditResult SetName(string name)
        {
            name = name ?? "";
            if (string.Equals(mission.Name, name, StringComparison.Ordinal)) return EditResult.NoChange;

            mission.Name = name;
            Touch();
            return EditResult.Done();
        }

        public EditResult SetDistanceScale(double scale)
        {
            if (!MissionRules.IsValidDistanceScale(scale)) return EditResult.Refused(MissionFields.DistanceScaleRule);
            if (mission.DistanceScale == scale) return EditResult.NoChange;

            mission.DistanceScale = scale;
            Touch();
            return EditResult.Done();
        }

        // ------------------------------------------------------------------ players

        public bool CanAddPlayer => mission.Players.Count < MissionRules.MaxPlayers;

        /// <summary>
        /// The colors no player has, in palette order. With <paramref name="keepFor"/> the list also
        /// holds that player's own color, in its place, as the choice that changes nothing.
        /// </summary>
        public List<PlayerColor> AvailableColors(string keepFor = null)
        {
            var colors = new List<PlayerColor>();
            foreach (PlayerColor color in MissionRules.AllColors)
            {
                MissionPlayer holder = PlayerWith(color);
                if (holder == null || (keepFor != null && MissionRules.SameId(holder.Id, keepFor))) colors.Add(color);
            }

            return colors;
        }

        /// <summary>
        /// The players' IDs in alphabetical order, whatever order the players were added in. Lists an
        /// author picks an owner from use this, so a player that was removed and added again is back
        /// where it was. The mission itself keeps the order players were added in.
        /// </summary>
        public List<string> PlayerIdsByName()
        {
            var ids = new List<string>();
            foreach (MissionPlayer player in mission.Players) ids.Add(player.Id);
            ids.Sort((a, b) =>
            {
                int order = string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
                return order != 0 ? order : string.CompareOrdinal(a, b);
            });
            return ids;
        }

        public int NodesOwnedBy(string playerId)
        {
            int count = 0;
            foreach (MissionNode node in mission.Nodes)
                if (node.Owner != null && MissionRules.SameId(node.Owner, playerId)) count++;
            return count;
        }

        public EditResult AddPlayer(string id, PlayerColor color)
        {
            if (!CanAddPlayer)
                return EditResult.Refused("A mission can have at most " + MissionRules.MaxPlayers + " players.");
            if (!MissionRules.IsValidId(id)) return EditResult.Refused("That is not a valid player ID. " + MissionRules.IdRule);
            if (mission.FindPlayer(id) != null)
                return EditResult.Refused("There already is a player with the ID \"" + id + "\". IDs are case-sensitive.");
            if (!MissionRules.IsDefined(color)) return EditResult.Refused("That color is not in the palette.");

            MissionPlayer holder = PlayerWith(color);
            if (holder != null)
                return EditResult.Refused("Player \"" + holder.Id + "\" already is " + MissionRules.NameOf(color) + ".");

            mission.Players.Add(new MissionPlayer(id, color));
            Touch();
            return EditResult.Done();
        }

        /// <summary>Changes a player's color. Every node the player owns is that color from now on.</summary>
        public EditResult SetPlayerColor(string playerId, PlayerColor color)
        {
            MissionPlayer player = mission.FindPlayer(playerId);
            if (player == null) return EditResult.Refused("There is no player \"" + playerId + "\".");
            if (!MissionRules.IsDefined(color)) return EditResult.Refused("That color is not in the palette.");
            if (player.Color == color) return EditResult.NoChange;

            MissionPlayer holder = PlayerWith(color);
            if (holder != null)
                return EditResult.Refused("Player \"" + holder.Id + "\" already is " + MissionRules.NameOf(color) + ".");

            player.Color = color;
            Touch();
            return EditResult.Done();
        }

        /// <summary>
        /// Removes a player. Its nodes become neutral and keep everything else: position, type, size,
        /// Darts and routes. Its color is free again.
        /// </summary>
        public EditResult RemovePlayer(string playerId)
        {
            MissionPlayer player = mission.FindPlayer(playerId);
            if (player == null) return EditResult.Refused("There is no player \"" + playerId + "\".");

            foreach (MissionNode node in mission.Nodes)
                if (node.Owner != null && MissionRules.SameId(node.Owner, playerId)) node.Owner = null;
            mission.Players.Remove(player);
            Touch();
            return EditResult.Done();
        }

        MissionPlayer PlayerWith(PlayerColor color)
        {
            foreach (MissionPlayer player in mission.Players)
                if (player.Color == color) return player;
            return null;
        }

        // ------------------------------------------------------------------ nodes

        /// <summary>
        /// Adds a neutral planet with no Darts and a planet's default size at a map position. Its ID is
        /// the next of N1, N2, ...: one above the highest such number in the mission, and never one this
        /// document has already handed out, so no surviving node is ever renamed and a deleted node's
        /// ID is not reused while the mission stays open.
        /// </summary>
        public EditResult AddNode(double x, double y)
        {
            if (!MissionRules.IsValidCoordinate(x) || !MissionRules.IsValidCoordinate(y))
                return EditResult.Refused(MissionFields.CoordinateRule);

            string id = NextNodeId();
            mission.Nodes.Add(new MissionNode
            {
                Id = id,
                Type = MissionNodeType.Planet,
                Size = MissionRules.DefaultSize(MissionNodeType.Planet),
                X = x,
                Y = y,
                Owner = null,
                Darts = 0,
            });
            Touch();
            return EditResult.Done(id);
        }

        string NextNodeId()
        {
            long highest = highestGeneratedNode;
            foreach (MissionNode node in mission.Nodes)
            {
                long number = GeneratedNumber(node.Id);
                if (number > highest) highest = number;
            }

            string id;
            do
            {
                highest++;
                id = "N" + highest.ToString(CultureInfo.InvariantCulture);
            }
            while (mission.FindNode(id) != null);

            highestGeneratedNode = highest;
            return id;
        }

        // The number of an ID shaped like the generated ones, N1 upward; 0 for any other ID.
        static long GeneratedNumber(string id)
        {
            if (id.Length < 2 || id.Length > 18 || id[0] != 'N' || id[1] == '0') return 0;

            long number = 0;
            for (int i = 1; i < id.Length; i++)
            {
                if (id[i] < '0' || id[i] > '9') return 0;
                number = number * 10 + (id[i] - '0');
            }

            return number;
        }

        public EditResult MoveNode(string nodeId, double x, double y)
        {
            MissionNode node = mission.FindNode(nodeId);
            if (node == null) return NoSuchNode(nodeId);
            if (!MissionRules.IsValidCoordinate(x) || !MissionRules.IsValidCoordinate(y))
                return EditResult.Refused(MissionFields.CoordinateRule);
            if (node.X == x && node.Y == y) return EditResult.NoChange;

            node.X = x;
            node.Y = y;
            Touch();
            return EditResult.Done();
        }

        /// <summary>
        /// Changes what a node is. Its size becomes the new type's default in the same edit, whatever
        /// it was before, so every change of type starts from a known size. Choosing the type the node
        /// already has changes nothing, its size included.
        /// </summary>
        public EditResult SetNodeType(string nodeId, MissionNodeType type)
        {
            MissionNode node = mission.FindNode(nodeId);
            if (node == null) return NoSuchNode(nodeId);
            if (!MissionRules.IsDefined(type)) return EditResult.Refused("A node is a planet, a moon or an asteroid.");
            if (node.Type == type) return EditResult.NoChange;

            node.Type = type;
            node.Size = MissionRules.DefaultSize(type);
            Touch();
            return EditResult.Done();
        }

        /// <summary>Sets a node's body radius in game world units. Nothing is rounded or clamped.</summary>
        public EditResult SetNodeSize(string nodeId, double size)
        {
            MissionNode node = mission.FindNode(nodeId);
            if (node == null) return NoSuchNode(nodeId);
            if (!MissionRules.IsValidNodeSize(size)) return EditResult.Refused(MissionFields.SizeRule);
            if (node.Size == size) return EditResult.NoChange;

            node.Size = size;
            Touch();
            return EditResult.Done();
        }

        /// <summary>Gives a node to a player, or to nobody with a null <paramref name="playerId"/>.</summary>
        public EditResult SetNodeOwner(string nodeId, string playerId)
        {
            MissionNode node = mission.FindNode(nodeId);
            if (node == null) return NoSuchNode(nodeId);
            if (playerId != null && mission.FindPlayer(playerId) == null)
                return EditResult.Refused("There is no player \"" + playerId + "\".");
            if (MissionRules.SameId(node.Owner, playerId)) return EditResult.NoChange;

            node.Owner = playerId;
            Touch();
            return EditResult.Done();
        }

        public EditResult SetNodeDarts(string nodeId, int darts)
        {
            MissionNode node = mission.FindNode(nodeId);
            if (node == null) return NoSuchNode(nodeId);
            if (darts < 0) return EditResult.Refused(MissionFields.DartsRule);
            if (node.Darts == darts) return EditResult.NoChange;

            node.Darts = darts;
            Touch();
            return EditResult.Done();
        }

        /// <summary>Removes a node together with every route that ends at it, as one edit.</summary>
        public EditResult RemoveNode(string nodeId)
        {
            MissionNode node = mission.FindNode(nodeId);
            if (node == null) return NoSuchNode(nodeId);

            mission.Edges.RemoveAll(edge => MissionRules.SameId(edge.Source, nodeId) || MissionRules.SameId(edge.Target, nodeId));
            mission.Nodes.Remove(node);
            Touch();
            return EditResult.Done();
        }

        static EditResult NoSuchNode(string nodeId)
        {
            return EditResult.Refused("There is no node \"" + nodeId + "\".");
        }

        // ------------------------------------------------------------------ routes

        /// <summary>
        /// Adds the route between two nodes. Refused for a node and itself, for a missing node and for
        /// two nodes that are already connected, whichever way round they are named.
        /// </summary>
        public EditResult Connect(string sourceId, string targetId)
        {
            if (mission.FindNode(sourceId) == null) return NoSuchNode(sourceId);
            if (mission.FindNode(targetId) == null) return NoSuchNode(targetId);
            if (MissionRules.SameId(sourceId, targetId))
                return EditResult.Refused("A node cannot be connected to itself. Pick a different second node.");
            if (mission.FindEdge(sourceId, targetId) != null)
                return EditResult.Refused(sourceId + " and " + targetId + " are already connected.");

            mission.Edges.Add(new MissionEdge(sourceId, targetId));
            Touch();
            return EditResult.Done();
        }

        /// <summary>Removes the route between two nodes and nothing else.</summary>
        public EditResult Disconnect(string a, string b)
        {
            MissionEdge edge = mission.FindEdge(a, b);
            if (edge == null) return EditResult.Refused("There is no route between " + a + " and " + b + ".");

            mission.Edges.Remove(edge);
            Touch();
            return EditResult.Done();
        }
    }
}
