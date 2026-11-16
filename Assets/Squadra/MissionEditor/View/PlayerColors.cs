using System.Collections.Generic;
using Squadra.Missions;
using UnityEngine;

namespace Squadra
{
    /// <summary>How the mission editor shows a player's color: the color it draws and the name it lists.</summary>
    static class PlayerColors
    {
        /// <summary>A node's color: its owner's, or gray for a neutral node.</summary>
        public static Color Of(MissionDefinition mission, MissionNode node)
        {
            MissionPlayer owner = node.Owner != null ? mission.FindPlayer(node.Owner) : null;
            return owner != null ? Of(owner.Color) : Palette.Neutral;
        }

        public static Color Of(PlayerColor color)
        {
            switch (color)
            {
                case PlayerColor.Blue: return Palette.Blue;
                case PlayerColor.Red: return Palette.Red;
                case PlayerColor.Green: return Palette.Green;
                case PlayerColor.Yellow: return Palette.Yellow;
                case PlayerColor.Purple: return Palette.Purple;
                case PlayerColor.Orange: return Palette.Orange;
                default: return Palette.Neutral;
            }
        }

        /// <summary>The color's name as a list shows it: the file's word with a capital letter.</summary>
        public static string NameOf(PlayerColor color)
        {
            string name = MissionRules.NameOf(color) ?? "?";
            return char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        public static List<string> NamesOf(List<PlayerColor> colors)
        {
            var names = new List<string>();
            foreach (PlayerColor color in colors) names.Add(NameOf(color));
            return names;
        }

        /// <summary>Reads a name as <see cref="NameOf"/> shows it. False for an empty or unknown one.</summary>
        public static bool TryParse(string shown, out PlayerColor color)
        {
            color = default;
            return !string.IsNullOrEmpty(shown) && MissionRules.TryParseColor(shown.ToLowerInvariant(), out color);
        }
    }
}
