namespace Squadra.MissionEditing
{
    /// <summary>
    /// Where mission files live. The editor reads and writes whole files through this and nothing else,
    /// so a test, or another kind of storage, can stand in for the disk.
    /// </summary>
    public interface IMissionFileStore
    {
        MissionFileContent Read(string path);

        bool Exists(string path);

        /// <summary>
        /// Replaces the file at <paramref name="path"/> with <paramref name="text"/>, or creates it.
        /// Either the whole new text is there afterwards or the previous file is untouched; a
        /// <see cref="MissionFileException"/> means the latter.
        /// </summary>
        void WriteAtomically(string path, string text);
    }
}
