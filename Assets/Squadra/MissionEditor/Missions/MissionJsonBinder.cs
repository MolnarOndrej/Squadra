using System;
using System.Collections.Generic;

namespace Squadra.Missions
{
    /// <summary>
    /// Interprets a parsed JSON document as a version 1 mission. It knows the format's fields and
    /// their types and nothing of JSON text: every field must be there, every field that is there
    /// must be known, and each problem is reported at the place the file would name it. Whether the
    /// values make a sound mission is left to <see cref="MissionValidator"/>.
    /// </summary>
    static class MissionJsonBinder
    {
        public static MissionDefinition Bind(JsonValue root, List<MissionIssue> issues)
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

            if (!JsonNumberLiteral.TryReadInteger(version.Text, out long versionNumber) || versionNumber != MissionDefinition.SchemaVersion)
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
            JsonValue size = fields.Take("size", JsonKind.Number);
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
                        + "Use " + MissionRules.TypeNameList + " (lower case).", node.Id));
            }

            // Whether the size is in range is the validator's to say. A type's default never stands in
            // for a size the file does not state: the missing field has already refused the file.
            if (size != null && TryNumber(size, at + ".size", issues, out double sizeValue)) node.Size = sizeValue;

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
                if (!JsonNumberLiteral.TryReadInteger(darts.Text, out long count))
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

        // A number that cannot be kept as written is an error of the file, not something to round.
        static bool TryNumber(JsonValue number, string at, List<MissionIssue> issues, out double value)
        {
            if (JsonNumberLiteral.TryReadDouble(number.Text, out value)) return true;

            issues.Add(Error(IssueCode.UnrepresentableNumber, at,
                "The number " + number.Text + " cannot be represented. Use an ordinary finite number."));
            return false;
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
