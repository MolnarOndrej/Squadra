namespace Squadra.MissionEditing
{
    public enum EditOutcome
    {
        /// <summary>The mission changed.</summary>
        Applied,

        /// <summary>The mission already was as asked. Nothing changed and nothing became unsaved.</summary>
        Unchanged,

        /// <summary>The edit would make the mission invalid, so it was refused. Nothing changed.</summary>
        Rejected,
    }
}
