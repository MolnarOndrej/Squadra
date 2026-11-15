using System;
using System.Globalization;

namespace Squadra.Missions
{
    /// <summary>
    /// Reads the number literals of a mission file. The literal is kept as text until here so that
    /// nothing about how it was written is lost before it is judged.
    /// </summary>
    static class JsonNumberLiteral
    {
        // A number the file wrote must survive as written: one too large, or so small that it would
        // silently become zero, is refused instead of being changed.
        public static bool TryReadDouble(string literal, out double value)
        {
            if (MissionNumbers.TryParse(literal, out value) && (value != 0d || IsWrittenZero(literal)))
                return true;

            value = 0d;
            return false;
        }

        static bool IsWrittenZero(string literal)
        {
            foreach (char c in literal)
            {
                if (c == 'e' || c == 'E') break;
                if (c >= '1' && c <= '9') return false;
            }

            return true;
        }

        // True for a number with a whole value, however it is spelled: 30, 30.0 and 3e1 are all thirty.
        // A whole number beyond what a long holds comes back as the nearest end of that range, which
        // every caller reports as out of range.
        public static bool TryReadInteger(string literal, out long value)
        {
            long limit = literal.StartsWith("-", StringComparison.Ordinal) ? long.MinValue : long.MaxValue;
            if (literal.IndexOfAny(new[] { '.', 'e', 'E' }) < 0)
            {
                if (!long.TryParse(literal, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
                    value = limit;
                return true;
            }

            value = limit;
            if (!MissionNumbers.TryParse(literal, out double real)) return true;

            value = 0;
            if (real == 0d && !IsWrittenZero(literal)) return false;
            if (real != Math.Floor(real)) return false;

            value = Math.Abs(real) >= 9e18 ? limit : (long)real;
            return true;
        }
    }
}
