using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Squadra.Missions
{
    /// <summary>
    /// The mission file format, version 1: one human-readable JSON document that holds a whole mission.
    /// Reading is strict. Every field is required, and an unknown field, a wrong type, a repeated
    /// property name, an unknown color or node type or another format version rejects the file, so
    /// nothing in it is ever dropped or guessed. A file that has the right shape is then judged by
    /// <see cref="MissionValidator"/>, the same as a mission being edited.
    /// </summary>
    public static class MissionJson
    {
        // ------------------------------------------------------------------ writing

        /// <summary>
        /// The mission as file text. Throws <see cref="InvalidOperationException"/> for a mission the
        /// format cannot express: a number that is not finite, or a color or node type it has no word
        /// for. Validate first.
        /// </summary>
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

        // ------------------------------------------------------------------ reading

        /// <summary>
        /// Reads file text as a mission. Any error leaves <see cref="MissionReadResult.Mission"/> null:
        /// a file is never loaded in part or repaired.
        /// </summary>
        public static MissionReadResult Read(string text)
        {
            var issues = new List<MissionIssue>();
            if (text == null)
            {
                issues.Add(Error(IssueCode.MalformedJson, "", "There is no text to read."));
                return new MissionReadResult(null, issues);
            }

            JsonValue root;
            try
            {
                root = new JsonParser(text).ParseDocument();
            }
            catch (JsonSyntaxException problem)
            {
                issues.Add(Error(problem.Code, "line " + problem.Line + ", column " + problem.Column, problem.Message));
                return new MissionReadResult(null, issues);
            }

            MissionDefinition mission = BindMission(root, issues);
            if (issues.Count > 0) return new MissionReadResult(null, issues);

            MissionValidation validation = MissionValidator.Validate(mission);
            return new MissionReadResult(validation.HasErrors ? null : mission, validation.Issues);
        }

        static MissionDefinition BindMission(JsonValue root, List<MissionIssue> issues)
        {
            if (root.Kind != JsonKind.Object)
            {
                issues.Add(Error(IssueCode.WrongType, "",
                    "A mission file is one JSON object, but this file holds " + Describe(root.Kind) + "."));
                return null;
            }

            var fields = new ObjectFields(root, "", issues);

            // The version decides what every other field means, so it is settled before they are read.
            JsonValue version = fields.Take("schemaVersion", JsonKind.Number);
            if (version == null) return null;

            if (!TryInteger(version, out long versionNumber) || versionNumber != MissionDefinition.SchemaVersion)
            {
                issues.Add(Error(IssueCode.UnsupportedSchemaVersion, "schemaVersion",
                    "This file uses mission format version " + version.Text + ". This editor reads version "
                    + MissionDefinition.SchemaVersion + " only, and will not guess at another."));
                return null;
            }

            var mission = new MissionDefinition();
            JsonValue id = fields.Take("id", JsonKind.String);
            JsonValue name = fields.Take("name", JsonKind.String);
            JsonValue map = fields.Take("map", JsonKind.Object);
            JsonValue players = fields.Take("players", JsonKind.Array);
            JsonValue nodes = fields.Take("nodes", JsonKind.Array);
            JsonValue edges = fields.Take("edges", JsonKind.Array);
            fields.RejectTheRest();

            if (id != null) mission.Id = id.Text;
            if (name != null) mission.Name = name.Text;

            if (map != null)
            {
                var mapFields = new ObjectFields(map, "map", issues);
                JsonValue scale = mapFields.Take("distanceScale", JsonKind.Number);
                mapFields.RejectTheRest();
                if (scale != null && TryNumber(scale, "map.distanceScale", issues, out double value))
                    mission.DistanceScale = value;
            }

            if (players != null)
                for (int i = 0; i < players.Items.Count; i++)
                    BindPlayer(players.Items[i], "players[" + i + "]", mission, issues);

            if (nodes != null)
                for (int i = 0; i < nodes.Items.Count; i++)
                    BindNode(nodes.Items[i], "nodes[" + i + "]", mission, issues);

            if (edges != null)
                for (int i = 0; i < edges.Items.Count; i++)
                    BindEdge(edges.Items[i], "edges[" + i + "]", mission, issues);

            return mission;
        }

        static void BindPlayer(JsonValue value, string at, MissionDefinition mission, List<MissionIssue> issues)
        {
            if (!ExpectObject(value, at, "A player", issues)) return;

            var fields = new ObjectFields(value, at, issues);
            JsonValue id = fields.Take("id", JsonKind.String);
            JsonValue color = fields.Take("color", JsonKind.String);
            fields.RejectTheRest();

            var player = new MissionPlayer { Id = id?.Text };
            if (color != null)
            {
                if (MissionRules.TryParseColor(color.Text, out PlayerColor parsed)) player.Color = parsed;
                else
                    issues.Add(Error(IssueCode.UnsupportedColor, at + ".color",
                        "The color \"" + color.Text + "\" is not one this version knows. Use one of: "
                        + "blue, red, green, yellow, purple, orange (lower case)."));
            }

            mission.Players.Add(player);
        }

        static void BindNode(JsonValue value, string at, MissionDefinition mission, List<MissionIssue> issues)
        {
            if (!ExpectObject(value, at, "A node", issues)) return;

            var fields = new ObjectFields(value, at, issues);
            JsonValue id = fields.Take("id", JsonKind.String);
            JsonValue type = fields.Take("type", JsonKind.String);
            JsonValue position = fields.Take("position", JsonKind.Object);
            JsonValue owner = fields.Take("owner", JsonKind.String, JsonKind.Null);
            JsonValue darts = fields.Take("darts", JsonKind.Number);
            fields.RejectTheRest();

            var node = new MissionNode { Id = id?.Text };
            if (type != null)
            {
                if (MissionRules.TryParseType(type.Text, out MissionNodeType parsed)) node.Type = parsed;
                else
                    issues.Add(Error(IssueCode.UnsupportedNodeType, at + ".type",
                        "The node type \"" + type.Text + "\" is not one this version knows. "
                        + "Use \"planet\" or \"moon\" (lower case).", node.Id));
            }

            if (position != null)
            {
                var positionFields = new ObjectFields(position, at + ".position", issues);
                JsonValue x = positionFields.Take("x", JsonKind.Number);
                JsonValue y = positionFields.Take("y", JsonKind.Number);
                positionFields.RejectTheRest();
                if (x != null && TryNumber(x, at + ".position.x", issues, out double xValue)) node.X = xValue;
                if (y != null && TryNumber(y, at + ".position.y", issues, out double yValue)) node.Y = yValue;
            }

            if (owner != null && owner.Kind == JsonKind.String) node.Owner = owner.Text;

            if (darts != null)
            {
                string range = " Starting Darts are a whole number from 0 to " + MissionRules.MaxDarts + ".";
                if (!TryInteger(darts, out long count))
                    issues.Add(Error(IssueCode.InvalidDarts, at + ".darts",
                        "The Dart count " + darts.Text + " is not a whole number." + range, node.Id));
                else if (count < int.MinValue)
                    issues.Add(Error(IssueCode.InvalidDarts, at + ".darts",
                        "The Dart count " + darts.Text + " is negative." + range, node.Id));
                else if (count > MissionRules.MaxDarts)
                    issues.Add(Error(IssueCode.InvalidDarts, at + ".darts",
                        "The Dart count " + darts.Text + " is too large." + range, node.Id));
                else
                    node.Darts = (int)count; // a negative count is the validator's to report, with the rest
            }

            mission.Nodes.Add(node);
        }

        static void BindEdge(JsonValue value, string at, MissionDefinition mission, List<MissionIssue> issues)
        {
            if (!ExpectObject(value, at, "A route", issues)) return;

            var fields = new ObjectFields(value, at, issues);
            JsonValue source = fields.Take("source", JsonKind.String);
            JsonValue target = fields.Take("target", JsonKind.String);
            fields.RejectTheRest();
            mission.Edges.Add(new MissionEdge(source?.Text, target?.Text));
        }

        static bool ExpectObject(JsonValue value, string at, string what, List<MissionIssue> issues)
        {
            if (value.Kind == JsonKind.Object) return true;

            issues.Add(Error(IssueCode.WrongType, at, what + " must be a JSON object, but this is " + Describe(value.Kind) + "."));
            return false;
        }

        // A number the file wrote must survive as written: one too large, or so small that it would
        // silently become zero, is refused instead of being changed.
        static bool TryNumber(JsonValue number, string at, List<MissionIssue> issues, out double value)
        {
            if (MissionNumbers.TryParse(number.Text, out value) && (value != 0d || IsWrittenZero(number.Text)))
                return true;

            issues.Add(Error(IssueCode.UnrepresentableNumber, at,
                "The number " + number.Text + " cannot be represented. Use an ordinary finite number."));
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
        static bool TryInteger(JsonValue number, out long value)
        {
            string literal = number.Text;
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

        static MissionIssue Error(IssueCode code, string location, string message, string nodeId = null)
        {
            return new MissionIssue(IssueSeverity.Error, code, location, message, nodeId);
        }

        static string Describe(JsonKind kind)
        {
            switch (kind)
            {
                case JsonKind.Object: return "an object";
                case JsonKind.Array: return "a list";
                case JsonKind.String: return "text";
                case JsonKind.Number: return "a number";
                case JsonKind.Null: return "null";
                default: return "true or false";
            }
        }

        /// <summary>
        /// Reads the fields of one JSON object and accounts for every one of them: a field that is
        /// missing or of the wrong type is reported when it is asked for, and whatever nobody asked for
        /// is reported as unknown.
        /// </summary>
        sealed class ObjectFields
        {
            readonly JsonValue source;
            readonly string path;
            readonly List<MissionIssue> issues;
            readonly HashSet<string> taken = new HashSet<string>(StringComparer.Ordinal);

            public ObjectFields(JsonValue source, string path, List<MissionIssue> issues)
            {
                this.source = source;
                this.path = path;
                this.issues = issues;
            }

            string At(string name)
            {
                return path.Length > 0 ? path + "." + name : name;
            }

            public JsonValue Take(string name, JsonKind kind, JsonKind? alternative = null)
            {
                taken.Add(name);
                JsonValue value = null;
                foreach (KeyValuePair<string, JsonValue> member in source.Members)
                    if (string.Equals(member.Key, name, StringComparison.Ordinal)) value = member.Value;

                if (value == null)
                {
                    issues.Add(Error(IssueCode.MissingField, At(name),
                        "The required field \"" + name + "\" is missing. Field names are case-sensitive."));
                    return null;
                }

                if (value.Kind == kind || value.Kind == alternative) return value;

                string wanted = Describe(kind) + (alternative.HasValue ? " or " + Describe(alternative.Value) : "");
                issues.Add(Error(IssueCode.WrongType, At(name),
                    "The field \"" + name + "\" must be " + wanted + ", but it is " + Describe(value.Kind) + "."));
                return null;
            }

            public void RejectTheRest()
            {
                foreach (KeyValuePair<string, JsonValue> member in source.Members)
                {
                    if (taken.Contains(member.Key)) continue;

                    issues.Add(Error(IssueCode.UnknownField, At(member.Key),
                        "The field \"" + member.Key + "\" is not part of mission format version "
                        + MissionDefinition.SchemaVersion + ". The file is refused so that nothing in it is lost."));
                }
            }
        }
    }
}
