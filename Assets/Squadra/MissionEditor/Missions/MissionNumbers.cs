using System;
using System.Globalization;

namespace Squadra.Missions
{
    /// <summary>How mission numbers are written, in files and in editable fields alike.</summary>
    public static class MissionNumbers
    {
        /// <summary>
        /// The shortest text that reads back as exactly the same number, with a period as the decimal
        /// sign whatever the computer's language. Nothing is rounded, so saving a mission again and
        /// again never moves a node.
        /// </summary>
        public static string Format(double value)
        {
            if (double.IsNaN(value)) return "NaN";
            if (double.IsPositiveInfinity(value)) return "Infinity";
            if (double.IsNegativeInfinity(value)) return "-Infinity";
            if (value == 0d) return "0";

            string text = value.ToString("R", CultureInfo.InvariantCulture);
            if (!TryParse(text, out double back) || back != value)
                text = value.ToString("G17", CultureInfo.InvariantCulture);
            return text;
        }

        /// <summary>
        /// Reads a number written the way <see cref="Format"/> writes it. False for text that is not a
        /// number, and for one too large to hold.
        /// </summary>
        public static bool TryParse(string text, out double value)
        {
            const NumberStyles style = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint
                | NumberStyles.AllowExponent;
            try
            {
                return double.TryParse(text, style, CultureInfo.InvariantCulture, out value)
                    && MissionRules.IsFinite(value);
            }
            catch (OverflowException)
            {
                value = 0d;
                return false;
            }
        }
    }
}
