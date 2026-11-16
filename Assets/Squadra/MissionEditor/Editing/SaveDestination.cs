namespace Squadra.MissionEditing
{
    /// <summary>Where a Save As should write.</summary>
    public sealed class SaveDestination
    {
        public SaveDestination(string path, bool replacementConfirmed)
        {
            Path = path;
            ReplacementConfirmed = replacementConfirmed;
        }

        public string Path { get; }

        /// <summary>
        /// True when the author has already agreed, in the file dialog, to replace a file that exists
        /// at the path. When false the editor asks before it replaces one.
        /// </summary>
        public bool ReplacementConfirmed { get; }
    }
}
