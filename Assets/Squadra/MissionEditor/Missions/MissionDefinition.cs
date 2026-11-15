using System.Collections.Generic;

namespace Squadra.Missions
{
    /// <summary>
    /// A complete mission as its file describes it: identity, the distance scale, the players, the
    /// nodes and the routes between them. It is plain data with no rules of its own; it can hold a
    /// mission that is not valid, and <see cref="MissionValidator"/> says what is wrong with it. It is
    /// not the simulation's match state and knows nothing of factions or node indices.
    /// </summary>
    public sealed class MissionDefinition
    {
        /// <summary>The only file format version this code reads and writes.</summary>
        public const int SchemaVersion = 1;

        public string Id { get; set; } = "";
        public string Name { get; set; } = "";

        /// <summary>Game distance per unit of map distance.</summary>
        public double DistanceScale { get; set; } = 1d;

        public List<MissionPlayer> Players { get; } = new List<MissionPlayer>();
        public List<MissionNode> Nodes { get; } = new List<MissionNode>();
        public List<MissionEdge> Edges { get; } = new List<MissionEdge>();

        public MissionNode FindNode(string id)
        {
            foreach (MissionNode node in Nodes)
                if (MissionRules.SameId(node.Id, id)) return node;
            return null;
        }

        public MissionPlayer FindPlayer(string id)
        {
            foreach (MissionPlayer player in Players)
                if (MissionRules.SameId(player.Id, id)) return player;
            return null;
        }

        public MissionEdge FindEdge(string a, string b)
        {
            foreach (MissionEdge edge in Edges)
                if (edge.Joins(a, b)) return edge;
            return null;
        }

        /// <summary>
        /// The straight-line distance between a route's ends in map units, or NaN when an end is
        /// missing.
        /// </summary>
        public double MapDistance(MissionEdge edge)
        {
            MissionNode source = edge != null ? FindNode(edge.Source) : null;
            MissionNode target = edge != null ? FindNode(edge.Target) : null;
            if (source == null || target == null) return double.NaN;
            return MissionRules.Distance(source.X, source.Y, target.X, target.Y);
        }

        /// <summary>A route's length in game units: its map distance times the distance scale.</summary>
        public double GameDistance(MissionEdge edge)
        {
            return MapDistance(edge) * DistanceScale;
        }

        /// <summary>A copy that shares nothing with this mission.</summary>
        public MissionDefinition Clone()
        {
            var copy = new MissionDefinition { Id = Id, Name = Name, DistanceScale = DistanceScale };
            foreach (MissionPlayer player in Players) copy.Players.Add(new MissionPlayer(player.Id, player.Color));
            foreach (MissionNode node in Nodes) copy.Nodes.Add(node.Clone());
            foreach (MissionEdge edge in Edges) copy.Edges.Add(new MissionEdge(edge.Source, edge.Target));
            return copy;
        }
    }
}
