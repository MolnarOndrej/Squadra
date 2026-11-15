using System;
using System.Collections.Generic;

namespace Squadra.Missions
{
    /// <summary>
    /// The mission file format, version 1: one human-readable JSON document that holds a whole mission.
    /// Reading is strict. Every field is required, and an unknown field, a wrong type, a repeated
    /// property name, an unknown color or node type or another format version rejects the file, so
    /// nothing in it is ever dropped or guessed. A file that has the right shape is then judged by
    /// <see cref="MissionValidator"/>, the same as a mission being edited.
    ///
    /// Reading is three steps, each in its own type: <see cref="JsonParser"/> turns text into JSON
    /// values, <see cref="MissionJsonBinder"/> reads those as a mission, and the validator judges
    /// it. <see cref="MissionJsonWriter"/> is the way back.
    /// </summary>
    public static class MissionJson
    {
        /// <summary>
        /// The mission as file text. Throws <see cref="InvalidOperationException"/> for a mission the
        /// format cannot express: a number that is not finite, or a color or node type it has no word
        /// for. Validate first.
        /// </summary>
        public static string Write(MissionDefinition mission)
        {
            return MissionJsonWriter.Write(mission);
        }

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

            MissionDefinition mission = MissionJsonBinder.Bind(root, issues);
            if (issues.Count > 0) return new MissionReadResult(null, issues);

            MissionValidation validation = MissionValidator.Validate(mission);
            return new MissionReadResult(validation.HasErrors ? null : mission, validation.Issues);
        }

        static MissionIssue Error(IssueCode code, string location, string message)
        {
            return new MissionIssue(IssueSeverity.Error, code, location, message);
        }
    }
}
