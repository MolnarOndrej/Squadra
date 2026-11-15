using System;
using Squadra.MissionEditing;

namespace Squadra
{
    /// <summary>
    /// The questions the session asks, worded for the author and put on screen with the editor's
    /// dialog. The last choice of every question is the one that changes nothing.
    /// </summary>
    sealed class MissionEditorPrompts : IMissionEditorPrompts
    {
        readonly MissionEditorDialog dialog;

        public MissionEditorPrompts(MissionEditorDialog dialog)
        {
            this.dialog = dialog;
        }

        public void AskUnsavedChanges(LeaveReason reason, string missionLabel, Action<UnsavedChoice> answer)
        {
            string before = reason == LeaveReason.NewMission ? "starting a new mission"
                : reason == LeaveReason.OpenMission ? "opening another mission"
                : "closing the editor";
            dialog.Show("Save your changes?",
                "\"" + missionLabel + "\" has changes that are not saved. Save them before " + before + "?\n\n"
                + "Discard goes on without saving.",
                new DialogChoice("Save", () => answer(UnsavedChoice.Save)),
                new DialogChoice("Discard", () => answer(UnsavedChoice.Discard)),
                new DialogChoice("Cancel", () => answer(UnsavedChoice.Cancel)));
        }

        public void AskExternalChange(string path, ExternalChange change, Action<ExternalChangeChoice> answer)
        {
            string what = change == ExternalChange.Deleted ? "has been deleted or moved"
                : change == ExternalChange.Unreadable ? "can no longer be read, so it may have changed"
                : "has been changed by another program";
            dialog.Show("The file changed on disk",
                path + " " + what + " since this editor last opened or saved it.\n\n"
                + "Overwrite replaces what is there now with this mission. "
                + "Save As leaves that file alone and saves this mission under another name.",
                new DialogChoice("Overwrite", () => answer(ExternalChangeChoice.Overwrite)),
                new DialogChoice("Save As", () => answer(ExternalChangeChoice.SaveAs)),
                new DialogChoice("Cancel", () => answer(ExternalChangeChoice.Cancel)));
        }

        public void AskInvalidInput(string fields, Action<bool> discard)
        {
            dialog.Show("A field is not valid",
                "What is typed in " + fields + " is not a valid value, so the mission still has the previous one.\n\n"
                + "Go back to correct it, or discard what was typed and continue.",
                new DialogChoice("Discard Input", () => discard(true)),
                new DialogChoice("Go Back", () => discard(false)));
        }

        public void ConfirmRemovePlayer(string playerId, int ownedNodes, Action<bool> confirmed)
        {
            // Only asked for a player that owns nodes; one that owns none is removed without a question.
            string effect = EditorWording.Count(ownedNodes, "node") + " owned by this player will become neutral. "
                + "Their positions, types, Darts and routes stay as they are.";
            dialog.Show("Remove player " + playerId + "?", effect,
                new DialogChoice("Remove", () => confirmed(true)),
                new DialogChoice("Cancel", () => confirmed(false)));
        }

        public void ConfirmReplaceFile(string path, Action<bool> confirmed)
        {
            dialog.Show("Replace the existing file?",
                path + " already exists. Saving here replaces it.",
                new DialogChoice("Replace", () => confirmed(true)),
                new DialogChoice("Cancel", () => confirmed(false)));
        }

        public void ShowError(string title, string message, Action dismissed)
        {
            dialog.Show(title, message, new DialogChoice("OK", dismissed));
        }
    }
}
