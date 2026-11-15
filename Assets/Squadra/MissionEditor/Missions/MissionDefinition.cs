using System;
using System.Collections.Generic;

namespace Squadra.Missions
{
    /// <summary>What a map node is. The words are the game's own; the file spells them in lower case.</summary>
    public enum MissionNodeType
    {
        Planet,
        Moon,
    }

    /// <summary>
    /// The colors a mission player can be drawn in. A color is presentation only: a blue player is not
    /// the game's Blue faction, a human or any particular AI. Neutral is not a color; a node without an
    /// owner has no player at all.
    /// </summary>
    public enum PlayerColor
    {
        Blue,
        Red,
        Green,
        Yellow,
        Purple,
        Orange,
    }

    /// <summary>One player of a mission: a stable identity and the color it is drawn in.</summary>
    public sealed class MissionPlayer
    {
        public MissionPlayer()
        {
        }

        public MissionPlayer(string id, PlayerColor color)
        {
            Id = id;
            Color = color;
        }

        public string Id { get; set; }
        public PlayerColor Color { get; set; }
    }

    /// <summary>
    /// One world of a mission. Its position is in map coordinates, X to the right and Y up, and has
    /// nothing to do with pixels. <see cref="Owner"/> is a player's ID, or null for a neutral world.
    /// </summary>
    public sealed class MissionNode
    {
        public string Id { get; set; }
        public MissionNodeType Type { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public string Owner { get; set; }
        public int Darts { get; set; }

        public MissionNode Clone()
        {
            return (MissionNode)MemberwiseClone();
        }
    }

    /// <summary>
    /// A route between two nodes. Routes run both ways: source and target only name the two ends.
    /// </summary>
    public sealed class MissionEdge
    {
        public MissionEdge()
        {
        }

        public MissionEdge(string source, string target)
        {
            Source = source;
            Target = target;
        }

        public string Source { get; set; }
        public string Target { get; set; }

        /// <summary>True when this route joins the two nodes, whichever end each is.</summary>
        public bool Joins(string a, string b)
        {
            return (MissionRules.SameId(Source, a) && MissionRules.SameId(Target, b))
                || (MissionRules.SameId(Source, b) && MissionRules.SameId(Target, a));
        }
    }

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

    /// <summary>The limits and spellings every part of the mission format agrees on.</summary>
    public static class MissionRules
    {
        public const int MaxPlayers = 6;

        /// <summary>Starting Darts use the game's range: a nonnegative 32-bit integer.</summary>
        public const int MaxDarts = int.MaxValue;

        /// <summary>
        /// The largest coordinate, either sign. Within it every distance can be calculated without
        /// overflow and a position keeps better than a millionth of a unit.
        /// </summary>
        public const double MaxCoordinate = 1e9;

        public const double MaxDistanceScale = 1e9;

        static readonly PlayerColor[] Colors =
        {
            PlayerColor.Blue, PlayerColor.Red, PlayerColor.Green,
            PlayerColor.Yellow, PlayerColor.Purple, PlayerColor.Orange,
        };

        static readonly string[] ColorNames = { "blue", "red", "green", "yellow", "purple", "orange" };
        static readonly string[] TypeNames = { "planet", "moon" };

        /// <summary>Every color, in the order new players are offered them.</summary>
        public static IReadOnlyList<PlayerColor> AllColors => Colors;

        /// <summary>
        /// True for a usable ID: at least one character, and only ASCII letters, digits, underscores
        /// and hyphens. IDs are case-sensitive.
        /// </summary>
        public static bool IsValidId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;

            foreach (char c in id)
            {
                bool allowed = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')
                    || c == '_' || c == '-';
                if (!allowed) return false;
            }

            return true;
        }

        public const string IdRule = "Use only letters, digits, underscores and hyphens, and at least one character.";

        /// <summary>IDs are compared exactly, so <c>n1</c> and <c>N1</c> are different nodes.</summary>
        public static bool SameId(string a, string b)
        {
            return string.Equals(a, b, StringComparison.Ordinal);
        }

        public static bool IsDefined(PlayerColor color)
        {
            return (int)color >= 0 && (int)color < ColorNames.Length;
        }

        public static bool IsDefined(MissionNodeType type)
        {
            return (int)type >= 0 && (int)type < TypeNames.Length;
        }

        /// <summary>The color as the file spells it, or null for a value outside the palette.</summary>
        public static string NameOf(PlayerColor color)
        {
            return IsDefined(color) ? ColorNames[(int)color] : null;
        }

        /// <summary>The node type as the file spells it, or null for an unknown value.</summary>
        public static string NameOf(MissionNodeType type)
        {
            return IsDefined(type) ? TypeNames[(int)type] : null;
        }

        public static bool TryParseColor(string name, out PlayerColor color)
        {
            int index = Array.IndexOf(ColorNames, name);
            color = index >= 0 ? (PlayerColor)index : default;
            return index >= 0;
        }

        public static bool TryParseType(string name, out MissionNodeType type)
        {
            int index = Array.IndexOf(TypeNames, name);
            type = index >= 0 ? (MissionNodeType)index : default;
            return index >= 0;
        }

        public static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        public static bool IsValidCoordinate(double value)
        {
            return IsFinite(value) && Math.Abs(value) <= MaxCoordinate;
        }

        public static bool IsValidDistanceScale(double value)
        {
            return IsFinite(value) && value > 0d && value <= MaxDistanceScale;
        }

        /// <summary>The straight-line distance between two map positions.</summary>
        public static double Distance(double x1, double y1, double x2, double y2)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
