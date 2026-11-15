namespace Squadra.MissionEditing
{
    public enum ExternalChangeChoice
    {
        /// <summary>Replace the file as it now is with the mission being edited.</summary>
        Overwrite,

        /// <summary>Leave that file alone and save under another name.</summary>
        SaveAs,

        Cancel,
    }
}
