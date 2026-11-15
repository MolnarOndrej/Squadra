using System;

namespace Squadra.MissionEditing
{
    /// <summary>What was found at a path: the file's text, or why there is none.</summary>
    public readonly struct MissionFileContent
    {
        MissionFileContent(MissionFileStatus status, string text, string error)
        {
            Status = status;
            Text = text;
            Error = error;
        }

        public MissionFileStatus Status { get; }
        public string Text { get; }
        public string Error { get; }

        public static MissionFileContent Of(string text)
        {
            return new MissionFileContent(MissionFileStatus.Ok, text, null);
        }

        public static MissionFileContent Missing(string error = null)
        {
            return new MissionFileContent(MissionFileStatus.Missing, null, error ?? "The file does not exist.");
        }

        public static MissionFileContent Unreadable(string error)
        {
            return new MissionFileContent(MissionFileStatus.Unreadable, null, error);
        }

        /// <summary>True when both describe the same state of a file.</summary>
        public bool Same(MissionFileContent other)
        {
            return Status == other.Status && string.Equals(Text, other.Text, StringComparison.Ordinal);
        }
    }
}
