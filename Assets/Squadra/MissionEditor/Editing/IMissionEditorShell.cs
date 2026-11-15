using System;

namespace Squadra.MissionEditing
{
    /// <summary>
    /// What the editor needs from the application around it: a way to pick files and a way to close.
    /// The standalone Windows editor answers with native file dialogs and by ending the process; the
    /// game can answer with screens of its own. Editing never reaches past this for a dialog or an
    /// exit. Every answer comes through a callback, so an implementation may answer at once or later.
    /// </summary>
    public interface IMissionEditorShell
    {
        /// <summary>Asks for a mission file to open. Answers null when the author cancels.</summary>
        void ChooseFileToOpen(Action<string> chosen);

        /// <summary>Asks where to save. Answers null when the author cancels.</summary>
        void ChooseFileToSave(string suggestedFileName, Action<SaveDestination> chosen);

        /// <summary>Closes the editor. Only called once nothing unsaved would be lost against the author's will.</summary>
        void Exit();
    }
}
