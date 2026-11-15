using Squadra.Simulation;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// The art bible's palette and the single source of color for the scene.
    /// Style: a deep-space tactical display. Painterly procedural worlds on a near-black indigo nebula;
    /// faction color lives in light (rings, auras, dots) and bloom carries it.
    /// Mood: one warm sun just outside the top-left of the frame, cool nebula bounce everywhere else.
    /// </summary>
    public static class Palette
    {
        // Space
        public static readonly Color Void = Hex(0x05060F);        // background black-indigo
        public static readonly Color NebulaIndigo = Hex(0x232A6B);
        public static readonly Color NebulaViolet = Hex(0x4A2370);
        public static readonly Color NebulaTeal = Hex(0x12596B);
        public static readonly Color Starlight = Hex(0xDCE6FF);
        public static readonly Color Sunlight = Hex(0xFFDDB0);

        // Factions
        public static readonly Color Blue = Hex(0x3FA9FF);
        public static readonly Color Red = Hex(0xFF4D5E);
        public static readonly Color Green = Hex(0x4FD675);
        public static readonly Color Yellow = Hex(0xF2E24A);
        public static readonly Color Purple = Hex(0xB47CFF);
        public static readonly Color Orange = Hex(0xFF9340);
        public static readonly Color Neutral = Hex(0x9AA3B5);

        // Interface
        public static readonly Color Accent = Hex(0xFFD166);      // selection, orders, victory
        public static readonly Color Text = Hex(0xE6ECFF);
        public static readonly Color TextDim = Hex(0x8791B0);

        public static Color Of(Faction faction)
        {
            switch (faction)
            {
                case Faction.Blue: return Blue;
                case Faction.Red: return Red;
                case Faction.Green: return Green;
                case Faction.Yellow: return Yellow;
                case Faction.Purple: return Purple;
                case Faction.Orange: return Orange;
                default: return Neutral;
            }
        }

        public static string NameOf(Faction faction)
        {
            switch (faction)
            {
                case Faction.Neutral: return "NEUTRAL";
                default: return faction.ToString().ToUpperInvariant();
            }
        }

        /// <summary>
        /// Converts a palette color to the linear-space HDR value the Squadra shaders expect in their
        /// <c>_Tint</c> vectors and vertex colors. Intensity above 1 pushes the color into bloom.
        /// </summary>
        public static Vector4 Hdr(Color color, float intensity = 1f, float alpha = 1f)
        {
            Color linear = color.linear;
            return new Vector4(linear.r * intensity, linear.g * intensity, linear.b * intensity, alpha);
        }

        /// <summary>A lighter tint of a palette color, for text that must stay readable on the nebula.</summary>
        public static Color Bright(Color color, float amount = 0.35f)
        {
            return Color.Lerp(color, Color.white, amount);
        }

        static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }
    }
}
