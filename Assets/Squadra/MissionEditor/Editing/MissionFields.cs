using System.Globalization;
using Squadra.Missions;

namespace Squadra.MissionEditing
{
    /// <summary>
    /// How the editor's fields read what an author types and write what a mission holds. Typed text is
    /// either exactly a valid value or it is refused with a reason; nothing is rounded, clamped or
    /// corrected on the author's behalf. Numbers use a period as the decimal sign.
    /// </summary>
    public static class MissionFields
    {
        public const string CoordinateRule = "Enter a number between -1000000000 and 1000000000, such as 12 or -3.5.";
        public const string DistanceScaleRule = "Enter a number greater than 0 and at most 1000000000.";
        public const string DartsRule = "Enter a whole number from 0 to 2147483647.";
        public const string SizeRule = "Enter a number from 0.1 to 5, such as 1 or 0.35.";

        /// <summary>A number as an editable field shows it: in full, so no digit is lost by editing.</summary>
        public static string Format(double value)
        {
            return MissionNumbers.Format(value);
        }

        public static string Format(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>A distance as labels show it, rounded to three decimals. For reading only.</summary>
        public static string FormatDistance(double distance)
        {
            if (!MissionRules.IsFinite(distance)) return "?";
            return distance.ToString("0.###", CultureInfo.InvariantCulture);
        }

        public static bool TryParseCoordinate(string text, out double value, out string error)
        {
            error = null;
            if (MissionNumbers.TryParse(Trimmed(text), out value) && MissionRules.IsValidCoordinate(value)) return true;

            error = CoordinateRule;
            return false;
        }

        public static bool TryParseDistanceScale(string text, out double value, out string error)
        {
            error = null;
            if (MissionNumbers.TryParse(Trimmed(text), out value) && MissionRules.IsValidDistanceScale(value)) return true;

            error = DistanceScaleRule;
            return false;
        }

        public static bool TryParseDarts(string text, out int value, out string error)
        {
            error = null;
            if (int.TryParse(Trimmed(text), NumberStyles.None, CultureInfo.InvariantCulture, out value)) return true;

            error = DartsRule;
            return false;
        }

        public static bool TryParseSize(string text, out double value, out string error)
        {
            error = null;
            if (MissionNumbers.TryParse(Trimmed(text), out value) && MissionRules.IsValidNodeSize(value)) return true;

            error = SizeRule;
            return false;
        }

        public static bool TryParseId(string text, out string error)
        {
            error = MissionRules.IsValidId(text) ? null : MissionRules.IdRule;
            return error == null;
        }

        static string Trimmed(string text)
        {
            return text == null ? "" : text.Trim();
        }
    }
}
