namespace Squadra.MissionEditing
{
    /// <summary>What happened to the mission's file since the editor last read or wrote it.</summary>
    public enum ExternalChange
    {
        Modified,
        Deleted,
        Unreadable,
    }
}
