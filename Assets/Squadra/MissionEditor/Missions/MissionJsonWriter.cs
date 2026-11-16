using System;
using System.Globalization;
using System.Text;

namespace Squadra.Missions
{
    /// <summary>
    /// Writes a mission as version 1 file text: indented by two spaces, one player, node or route to
    /// a line, numbers in the shortest form that reads back exactly.
    /// </summary>
    static class MissionJsonWriter
    {
        public static string Write(MissionDefinition mission)
        {
            if (mission == null) throw new ArgumentNullException(nameof(mission));

            var text = new StringBuilder();
            text.Append("{\n");
            text.Append("  \"schemaVersion\": ").Append(MissionDefinition.SchemaVersion).Append(",\n");
            text.Append("  \"id\": ").Append(Quoted(mission.Id)).Append(",\n");
            text.Append("  \"name\": ").Append(Quoted(mission.Name)).Append(",\n");
            text.Append("  \"map\": { \"distanceScale\": ").Append(Number(mission.DistanceScale, "the distance scale"))
                .Append(" },\n");

            text.Append("  \"players\": [");
            for (int i = 0; i < mission.Players.Count; i++)
            {
                MissionPlayer player = mission.Players[i];
                string color = MissionRules.NameOf(player.Color)
                    ?? throw new InvalidOperationException("Player " + player.Id + " has a color the mission format has no name for.");
                text.Append(i == 0 ? "\n" : ",\n");
                text.Append("    { \"id\": ").Append(Quoted(player.Id)).Append(", \"color\": ").Append(Quoted(color))
                    .Append(" }");
            }

            text.Append(mission.Players.Count > 0 ? "\n  ],\n" : "],\n");

            text.Append("  \"nodes\": [");
            for (int i = 0; i < mission.Nodes.Count; i++)
            {
                MissionNode node = mission.Nodes[i];
                string type = MissionRules.NameOf(node.Type)
                    ?? throw new InvalidOperationException("Node " + node.Id + " has a type the mission format has no name for.");
                text.Append(i == 0 ? "\n" : ",\n");
                text.Append("    { \"id\": ").Append(Quoted(node.Id));
                text.Append(", \"type\": ").Append(Quoted(type));
                text.Append(", \"position\": { \"x\": ").Append(Number(node.X, "the X coordinate of node " + node.Id));
                text.Append(", \"y\": ").Append(Number(node.Y, "the Y coordinate of node " + node.Id)).Append(" }");
                text.Append(", \"owner\": ").Append(node.Owner == null ? "null" : Quoted(node.Owner));
                text.Append(", \"darts\": ").Append(node.Darts.ToString(CultureInfo.InvariantCulture)).Append(" }");
            }

            text.Append(mission.Nodes.Count > 0 ? "\n  ],\n" : "],\n");

            text.Append("  \"edges\": [");
            for (int i = 0; i < mission.Edges.Count; i++)
            {
                MissionEdge edge = mission.Edges[i];
                text.Append(i == 0 ? "\n" : ",\n");
                text.Append("    { \"source\": ").Append(Quoted(edge.Source)).Append(", \"target\": ")
                    .Append(Quoted(edge.Target)).Append(" }");
            }

            text.Append(mission.Edges.Count > 0 ? "\n  ]\n" : "]\n");
            text.Append("}\n");
            return text.ToString();
        }

        static string Number(double value, string what)
        {
            if (!MissionRules.IsFinite(value))
                throw new InvalidOperationException("The mission cannot be written because " + what + " is not a finite number.");
            return MissionNumbers.Format(value);
        }

        static string Quoted(string value)
        {
            if (value == null) throw new InvalidOperationException("The mission cannot be written because a text value is missing.");

            var text = new StringBuilder(value.Length + 2);
            text.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"': text.Append("\\\""); break;
                    case '\\': text.Append("\\\\"); break;
                    case '\n': text.Append("\\n"); break;
                    case '\r': text.Append("\\r"); break;
                    case '\t': text.Append("\\t"); break;
                    default:
                        // Half of a surrogate pair cannot be stored as UTF-8, so it is written as an escape.
                        bool paired = (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                            || (char.IsLowSurrogate(c) && i > 0 && char.IsHighSurrogate(value[i - 1]));
                        if (c < ' ' || (char.IsSurrogate(c) && !paired))
                            text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            text.Append(c);
                        break;
                }
            }

            text.Append('"');
            return text.ToString();
        }
    }
}
