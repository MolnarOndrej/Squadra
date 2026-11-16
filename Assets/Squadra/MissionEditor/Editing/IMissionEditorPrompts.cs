using System;

namespace Squadra.MissionEditing
{
    /// <summary>
    /// The questions the editor puts to the author before it does something that cannot be taken back.
    /// Each must be answered exactly once.
    /// </summary>
    public interface IMissionEditorPrompts
    {
        /// <summary>The mission has unsaved changes and <paramref name="reason"/> would put it away.</summary>
        void AskUnsavedChanges(LeaveReason reason, string missionLabel, Action<UnsavedChoice> answer);

        /// <summary>The mission's file was changed, deleted or made unreadable by something else.</summary>
        void AskExternalChange(string path, ExternalChange change, Action<ExternalChangeChoice> answer);

        /// <summary>
        /// A field holds text that is not a valid value, so the mission does not have it. Answer true
        /// to throw that text away and go on, false to go back and correct it.
        /// </summary>
        void AskInvalidInput(string fields, Action<bool> discard);

        /// <summary>
        /// Removing the player would make <paramref name="ownedNodes"/> nodes neutral. Never asked for
        /// a player that owns no nodes.
        /// </summary>
        void ConfirmRemovePlayer(string playerId, int ownedNodes, Action<bool> confirmed);

        void ConfirmReplaceFile(string path, Action<bool> confirmed);

        void ShowError(string title, string message, Action dismissed);
    }
}
