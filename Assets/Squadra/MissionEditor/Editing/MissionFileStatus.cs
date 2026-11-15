namespace Squadra.MissionEditing
{
    public enum MissionFileStatus
    {
        Ok,

        /// <summary>There is no file at the path.</summary>
        Missing,

        /// <summary>There is a file, but its text could not be read.</summary>
        Unreadable,
    }
}
