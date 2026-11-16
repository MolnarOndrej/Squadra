using System;
using System.Collections.Generic;

namespace Squadra.Missions
{
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

        /// <summary>The smallest and the largest body radius a node of any type can have, both allowed.</summary>
        public const double MinNodeSize = 0.1;

        public const double MaxNodeSize = 5d;

        static readonly PlayerColor[] Colors =
        {
            PlayerColor.Blue, PlayerColor.Red, PlayerColor.Green,
            PlayerColor.Yellow, PlayerColor.Purple, PlayerColor.Orange,
        };

        static readonly string[] ColorNames = { "blue", "red", "green", "yellow", "purple", "orange" };
        static readonly string[] TypeNames = { "planet", "moon", "asteroid" };

        // The size a node gets when it is made or its type is changed, by type. A file never gets
        // one of these in place of a size it does not state.
        static readonly double[] DefaultSizes = { 1d, 0.5, 0.3 };

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

        /// <summary>The node types as the file spells them, for a message that lists them.</summary>
        public const string TypeNameList = "\"planet\", \"moon\" or \"asteroid\"";

        /// <summary>The size a node of this type starts with. Throws for an unknown type.</summary>
        public static double DefaultSize(MissionNodeType type)
        {
            if (!IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
            return DefaultSizes[(int)type];
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

        public static bool IsValidNodeSize(double value)
        {
            return IsFinite(value) && value >= MinNodeSize && value <= MaxNodeSize;
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
