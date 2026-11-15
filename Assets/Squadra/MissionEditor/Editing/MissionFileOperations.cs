using System;
using System.Collections.Generic;
using System.IO;
using Squadra.Missions;

namespace Squadra.MissionEditing
{
    /// <summary>
    /// Reads and writes mission files for the session, with every question and error message that
    /// belongs to it: a file that cannot be read or is not a mission, a mission that cannot be saved,
    /// a file that would be replaced, and a file that something else changed since the editor last
    /// read or wrote it. It only reports what happened. What that means for the mission being edited,
    /// its file and its unsaved changes stays with <see cref="MissionEditorSession"/>.
    /// </summary>
    sealed class MissionFileOperations
    {
        readonly IMissionFileStore files;
        readonly IMissionEditorShell shell;
        readonly IMissionEditorPrompts prompts;
        readonly Func<MissionDefinition, string> serialize;

        public MissionFileOperations(IMissionFileStore files, IMissionEditorShell shell, IMissionEditorPrompts prompts,
            Func<MissionDefinition, string> serialize)
        {
            this.files = files;
            this.shell = shell;
            this.prompts = prompts;
            this.serialize = serialize;
        }

        // ------------------------------------------------------------------ reading

        /// <summary>
        /// Reads and validates a mission file. Answers the mission and the file's text, or two nulls
        /// once the author has been told why the file was not opened.
        /// </summary>
        public void Read(string path, Action<MissionDefinition, string> then)
        {
            string name = Path.GetFileName(path);
            MissionFileContent content = files.Read(path);
            if (content.Status != MissionFileStatus.Ok)
            {
                prompts.ShowError("Could not open " + name,
                    content.Error + "\n\nThe current mission is unchanged.", () => then(null, null));
                return;
            }

            MissionReadResult result = MissionJson.Read(content.Text);
            if (!result.Ok)
            {
                prompts.ShowError("Could not open " + name,
                    name + " is not a valid mission file:\n\n" + EditorWording.List(result.Issues)
                    + "\n\nThe file was not opened and the current mission is unchanged.", () => then(null, null));
                return;
            }

            then(result.Mission, content.Text);
        }

        // ------------------------------------------------------------------ writing

        /// <summary>
        /// Validates and serializes before anything is written or even asked. Answers the file text,
        /// or null once the author has been told why the mission cannot be saved.
        /// </summary>
        public void Prepare(MissionDefinition mission, MissionValidation validation, Action<string> then)
        {
            if (validation.HasErrors)
            {
                var errors = new List<MissionIssue>();
                foreach (MissionIssue issue in validation.Issues)
                    if (issue.IsError) errors.Add(issue);
                prompts.ShowError("The mission cannot be saved",
                    "Correct " + (errors.Count == 1 ? "this error" : "these errors") + " first:\n\n"
                    + EditorWording.List(errors) + "\n\nNothing was written.", () => then(null));
                return;
            }

            string text;
            try
            {
                text = serialize(mission);
            }
            catch (Exception problem)
            {
                prompts.ShowError("The mission cannot be saved",
                    "The mission could not be turned into file text: " + problem.Message
                    + "\n\nNothing was written. Your changes are still here.", () => then(null));
                return;
            }

            then(text);
        }

        /// <summary>
        /// Asks the author where to save and writes there, asking again before a file that exists is
        /// replaced. Answers the path that was written, or null when nothing was.
        /// </summary>
        public void WriteToChosenFile(string text, Func<string> suggestedFileName, Action<string> then)
        {
            shell.ChooseFileToSave(suggestedFileName(), destination =>
            {
                if (destination == null || string.IsNullOrEmpty(destination.Path))
                {
                    then(null);
                    return;
                }

                if (destination.ReplacementConfirmed || !files.Exists(destination.Path))
                {
                    Write(destination.Path, text, then);
                    return;
                }

                prompts.ConfirmReplaceFile(destination.Path, confirmed =>
                {
                    if (confirmed) Write(destination.Path, text, then);
                    else then(null);
                });
            });
        }

        /// <summary>
        /// Saves over the mission's own file, unless that file no longer holds
        /// <paramref name="baseline"/>, the text the editor last read from it or wrote to it. Then the
        /// author decides. Answers the path that was written, or null when nothing was.
        /// </summary>
        public void WriteOverFile(string path, string baseline, string text, Func<string> suggestedFileName,
            Action<string> then)
        {
            WriteOverFile(path, baseline, text, suggestedFileName, null, then);
        }

        // Overwrite covers the file only as it was when the author was asked: if it has changed yet
        // again by the time of writing, they are asked again.
        void WriteOverFile(string path, string baseline, string text, Func<string> suggestedFileName,
            MissionFileContent? authorized, Action<string> then)
        {
            MissionFileContent found = files.Read(path);
            bool untouched = found.Status == MissionFileStatus.Ok && string.Equals(found.Text, baseline, StringComparison.Ordinal);
            if (untouched || (authorized.HasValue && authorized.Value.Same(found)))
            {
                Write(path, text, then);
                return;
            }

            ExternalChange change = found.Status == MissionFileStatus.Missing ? ExternalChange.Deleted
                : found.Status == MissionFileStatus.Unreadable ? ExternalChange.Unreadable
                : ExternalChange.Modified;
            prompts.AskExternalChange(path, change, choice =>
            {
                if (choice == ExternalChangeChoice.Overwrite)
                    WriteOverFile(path, baseline, text, suggestedFileName, found, then);
                else if (choice == ExternalChangeChoice.SaveAs)
                    WriteToChosenFile(text, suggestedFileName, then);
                else
                    then(null);
            });
        }

        void Write(string path, string text, Action<string> then)
        {
            try
            {
                files.WriteAtomically(path, text);
            }
            catch (MissionFileException problem)
            {
                prompts.ShowError("Could not save " + Path.GetFileName(path),
                    problem.Message + "\n\nNothing was written to " + path
                    + ". Your changes are still here; try again or use Save As.", () => then(null));
                return;
            }

            then(path);
        }
    }
}
