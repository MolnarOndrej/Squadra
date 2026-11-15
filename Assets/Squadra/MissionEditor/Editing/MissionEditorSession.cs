using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Squadra.Missions;

namespace Squadra.MissionEditing
{
    /// <summary>
    /// One author's work in the mission editor: the mission being edited, the file it belongs to,
    /// whether it has unsaved changes, what is selected and which tool is armed. It carries out New,
    /// Open, Save, Save As and Quit, and it is the only place that decides when work may be replaced or
    /// a file overwritten. It asks the author through <see cref="IMissionEditorPrompts"/>, picks files
    /// and exits through <see cref="IMissionEditorShell"/> and touches the disk through
    /// <see cref="IMissionFileStore"/>, so the same session runs in the standalone editor, inside the
    /// game and in a test.
    /// </summary>
    public sealed class MissionEditorSession
    {
        sealed class InvalidInput
        {
            public string Label;
            public bool OfSelection;
        }

        readonly IMissionFileStore files;
        readonly IMissionEditorShell shell;
        readonly IMissionEditorPrompts prompts;
        readonly Func<string> newMissionId;
        readonly Func<MissionDefinition, string> serialize;
        readonly Dictionary<string, InvalidInput> invalidInputs = new Dictionary<string, InvalidInput>(StringComparer.Ordinal);

        // The file's text when it was last opened or saved; null while the mission has no file.
        string baseline;
        bool edited;
        bool discardAuthorized;
        MissionValidation validation;
        int validatedRevision = -1;
        bool showDistances;

        /// <param name="newMissionId">Makes the ID of a new mission. Defaults to a generated one.</param>
        /// <param name="serialize">Turns a mission into file text. Defaults to the mission format.</param>
        public MissionEditorSession(IMissionFileStore files, IMissionEditorShell shell, IMissionEditorPrompts prompts,
            Func<string> newMissionId = null, Func<MissionDefinition, string> serialize = null)
        {
            this.files = files ?? throw new ArgumentNullException(nameof(files));
            this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
            this.prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
            this.newMissionId = newMissionId ?? MissionDocument.GenerateMissionId;
            this.serialize = serialize ?? MissionJson.Write;
            Adopt(MissionDocument.CreateNew(this.newMissionId()), null, null);
        }

        // ------------------------------------------------------------------ state

        public MissionDocument Document { get; private set; }

        /// <summary>The file the mission was opened from or last saved to; null for a mission with no file yet.</summary>
        public string FilePath { get; private set; }

        public string FileName => FilePath != null ? Path.GetFileName(FilePath) : null;

        /// <summary>
        /// True when closing now would lose something: the mission was edited since it was created,
        /// opened or last saved. A new mission nobody has touched has nothing to lose, file or no file.
        /// </summary>
        public bool HasUnsavedChanges => edited;

        /// <summary>What the validator says about the mission as it stands.</summary>
        public MissionValidation Validation
        {
            get
            {
                if (validation == null || validatedRevision != Document.Revision)
                {
                    validation = Document.Validate();
                    validatedRevision = Document.Revision;
                }

                return validation;
            }
        }

        public Selection Selection { get; private set; }
        public EditorTool Tool { get; private set; }

        /// <summary>The first node of the connection being made; null until one is picked.</summary>
        public string ConnectionSource { get; private set; }

        /// <summary>Whether route distances are written on the canvas. A view setting, not part of the mission.</summary>
        public bool ShowDistances
        {
            get => showDistances;
            set
            {
                if (showDistances == value) return;
                showDistances = value;
                RaiseChanged();
            }
        }

        /// <summary>The latest thing worth telling the author: what just happened, or why something did not.</summary>
        public string Status { get; private set; } = "";

        /// <summary>True while a question to the author is open. Commands are ignored until it is answered.</summary>
        public bool Busy { get; private set; }

        public bool HasInvalidInput => invalidInputs.Count > 0;

        /// <summary>Raised when anything the editor shows may have changed.</summary>
        public event Action Changed;

        /// <summary>Raised when another mission has taken the place of the one being edited.</summary>
        public event Action DocumentReplaced;

        /// <summary>Raised when the author gave up invalid field text: fields show the mission's values again.</summary>
        public event Action InvalidInputDiscarded;

        void RaiseChanged()
        {
            Changed?.Invoke();
        }

        void Adopt(MissionDocument document, string path, string fileText)
        {
            if (Document != null) Document.Changed -= OnDocumentChanged;

            Document = document;
            Document.Changed += OnDocumentChanged;
            FilePath = path;
            baseline = fileText;
            edited = false;
            validation = null;
            Selection = Selection.None;
            Tool = EditorTool.Select;
            ConnectionSource = null;
            invalidInputs.Clear();
        }

        void OnDocumentChanged()
        {
            edited = true;

            // A deleted node takes the selection and a half-made connection with it.
            MissionDefinition mission = Document.Mission;
            bool gone = (Selection.Kind == SelectionKind.Node && mission.FindNode(Selection.NodeId) == null)
                || (Selection.Kind == SelectionKind.Edge && mission.FindEdge(Selection.NodeId, Selection.OtherNodeId) == null);
            if (gone) Selection = Selection.None;
            if (ConnectionSource != null && mission.FindNode(ConnectionSource) == null) ConnectionSource = null;

            RaiseChanged();
        }

        bool Begin()
        {
            if (Busy) return false;

            Busy = true;
            discardAuthorized = false;
            return true;
        }

        void End()
        {
            Busy = false;
            discardAuthorized = false;
            RaiseChanged();
        }

        // ------------------------------------------------------------------ invalid field input

        /// <summary>
        /// Tells the session that a field holds text the mission could not take. While it does, a save
        /// or a change of mission asks the author first. <paramref name="ofSelection"/> marks a field
        /// of the selected object, which selecting something else would also put away.
        /// </summary>
        public void ReportInvalidInput(string field, string label, bool ofSelection)
        {
            invalidInputs[field] = new InvalidInput { Label = label, OfSelection = ofSelection };
        }

        public void ClearInvalidInput(string field)
        {
            invalidInputs.Remove(field);
        }

        /// <summary>True while a field's invalid text is still waiting to be corrected or given up.</summary>
        public bool IsInvalidInput(string field)
        {
            return invalidInputs.ContainsKey(field);
        }

        string InvalidLabels(bool selectionOnly)
        {
            var text = new StringBuilder();
            foreach (InvalidInput input in invalidInputs.Values)
            {
                if (selectionOnly && !input.OfSelection) continue;
                if (text.Length > 0) text.Append(", ");
                text.Append(input.Label);
            }

            return text.ToString();
        }

        void DiscardInvalidInput(bool selectionOnly)
        {
            var doomed = new List<string>();
            foreach (KeyValuePair<string, InvalidInput> input in invalidInputs)
                if (!selectionOnly || input.Value.OfSelection) doomed.Add(input.Key);
            if (doomed.Count == 0) return;

            foreach (string field in doomed) invalidInputs.Remove(field);
            InvalidInputDiscarded?.Invoke();
        }

        // Settles invalid field text before an action that would put it away. A save discards it at
        // once, because the file gets the mission's values. A change of mission only notes the
        // author's permission: the text stays until that change really happens.
        void SettleInvalidInput(bool discardNow, Action<bool> then)
        {
            if (invalidInputs.Count == 0)
            {
                then(true);
                return;
            }

            if (discardAuthorized)
            {
                if (discardNow) DiscardInvalidInput(false);
                then(true);
                return;
            }

            prompts.AskInvalidInput(InvalidLabels(false), discard =>
            {
                if (!discard)
                {
                    then(false);
                    return;
                }

                discardAuthorized = true;
                if (discardNow) DiscardInvalidInput(false);
                then(true);
            });
        }

        // Runs an action that changes the selection, once the selected object's fields are settled.
        void LeaveSelection(Action then)
        {
            string labels = InvalidLabels(true);
            if (labels.Length == 0)
            {
                then();
                return;
            }

            Busy = true;
            prompts.AskInvalidInput(labels, discard =>
            {
                Busy = false;
                if (discard)
                {
                    DiscardInvalidInput(true);
                    then();
                }

                RaiseChanged();
            });
        }

        // ------------------------------------------------------------------ New, Open, Quit

        /// <summary>Starts a new mission in place of the current one.</summary>
        public void New()
        {
            if (!Begin()) return;

            ConfirmLeave(LeaveReason.NewMission, proceed =>
            {
                if (proceed)
                {
                    Adopt(MissionDocument.CreateNew(newMissionId()), null, null);
                    Status = "Started a new mission.";
                    DocumentReplaced?.Invoke();
                }

                End();
            });
        }

        /// <summary>
        /// Opens a mission file in place of the current mission. The file is read and validated first;
        /// if anything is wrong with it the current mission, its file and its unsaved changes stay
        /// exactly as they were.
        /// </summary>
        public void Open()
        {
            if (!Begin()) return;

            ConfirmLeave(LeaveReason.OpenMission, proceed =>
            {
                if (!proceed)
                {
                    End();
                    return;
                }

                shell.ChooseFileToOpen(path =>
                {
                    if (string.IsNullOrEmpty(path)) End();
                    else Load(path, End);
                });
            });
        }

        void Load(string path, Action then)
        {
            string name = Path.GetFileName(path);
            MissionFileContent content = files.Read(path);
            if (content.Status != MissionFileStatus.Ok)
            {
                prompts.ShowError("Could not open " + name,
                    content.Error + "\n\nThe current mission is unchanged.", then);
                return;
            }

            MissionReadResult result = MissionJson.Read(content.Text);
            if (!result.Ok)
            {
                prompts.ShowError("Could not open " + name,
                    name + " is not a valid mission file:\n\n" + Describe(result.Issues)
                    + "\n\nThe file was not opened and the current mission is unchanged.", then);
                return;
            }

            Adopt(new MissionDocument(result.Mission), path, content.Text);
            int warnings = Validation.WarningCount;
            Status = "Opened " + name + (warnings == 0 ? "." : " with " + Count(warnings, "warning") + ".");
            DocumentReplaced?.Invoke();
            then();
        }

        /// <summary>
        /// Asks to close the editor. Unsaved work is offered for saving first; the editor only exits
        /// when nothing would be lost against the author's will. The window's close button and a Quit
        /// command both come here.
        /// </summary>
        public void RequestQuit()
        {
            if (!Begin()) return;

            ConfirmLeave(LeaveReason.Quit, proceed =>
            {
                End();
                if (proceed) shell.Exit();
            });
        }

        // Decides whether the current mission may be put away. Discard only gives permission: the
        // mission is still there, unsaved, until whatever was asked for actually replaces it.
        void ConfirmLeave(LeaveReason reason, Action<bool> then)
        {
            SettleInvalidInput(false, settled =>
            {
                if (!settled || !HasUnsavedChanges)
                {
                    then(settled);
                    return;
                }

                prompts.AskUnsavedChanges(reason, MissionLabel, choice =>
                {
                    if (choice == UnsavedChoice.Save) SaveCore(then);
                    else then(choice == UnsavedChoice.Discard);
                });
            });
        }

        /// <summary>The mission as dialogs name it: its file, or its name while it has none.</summary>
        public string MissionLabel
        {
            get
            {
                if (FilePath != null) return FileName;
                string name = Document.Mission.Name;
                return string.IsNullOrWhiteSpace(name) ? MissionDocument.DefaultName : name;
            }
        }

        // ------------------------------------------------------------------ Save, Save As

        /// <summary>Saves to the mission's file, or asks for one when it has none yet.</summary>
        public void Save()
        {
            if (!Begin()) return;
            SaveCore(_ => End());
        }

        /// <summary>Saves to a file the author picks. That file becomes the mission's file only once it is written.</summary>
        public void SaveAs()
        {
            if (!Begin()) return;

            SettleInvalidInput(true, settled =>
            {
                if (!settled) End();
                else SaveToChosenFile(_ => End());
            });
        }

        void SaveCore(Action<bool> then)
        {
            SettleInvalidInput(true, settled =>
            {
                if (!settled)
                {
                    then(false);
                    return;
                }

                if (FilePath == null)
                {
                    SaveToChosenFile(then);
                    return;
                }

                Prepare(text =>
                {
                    if (text == null) then(false);
                    else WriteOverCurrentFile(text, null, then);
                });
            });
        }

        void SaveToChosenFile(Action<bool> then)
        {
            Prepare(text =>
            {
                if (text == null) then(false);
                else ChooseAndWrite(text, then);
            });
        }

        // Validates and serializes before anything is written or even asked. Answers null on failure.
        void Prepare(Action<string> then)
        {
            MissionValidation current = Validation;
            if (current.HasErrors)
            {
                var errors = new List<MissionIssue>();
                foreach (MissionIssue issue in current.Issues)
                    if (issue.IsError) errors.Add(issue);
                prompts.ShowError("The mission cannot be saved",
                    "Correct " + (errors.Count == 1 ? "this error" : "these errors") + " first:\n\n" + Describe(errors)
                    + "\n\nNothing was written.", () => then(null));
                return;
            }

            string text;
            try
            {
                text = serialize(Document.Mission);
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

        void ChooseAndWrite(string text, Action<bool> then)
        {
            shell.ChooseFileToSave(SuggestedFileName, destination =>
            {
                if (destination == null || string.IsNullOrEmpty(destination.Path))
                {
                    then(false);
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
                    else then(false);
                });
            });
        }

        string SuggestedFileName => FilePath != null ? FileName : Document.Mission.Id + ".json";

        // Saves over the mission's own file, unless that file is no longer what the editor last read or
        // wrote. Then the author decides, and Overwrite covers the file only as it was when they were
        // asked: if it has changed yet again by the time of writing, they are asked again.
        void WriteOverCurrentFile(string text, MissionFileContent? authorized, Action<bool> then)
        {
            MissionFileContent found = files.Read(FilePath);
            bool untouched = found.Status == MissionFileStatus.Ok && string.Equals(found.Text, baseline, StringComparison.Ordinal);
            if (untouched || (authorized.HasValue && authorized.Value.Same(found)))
            {
                Write(FilePath, text, then);
                return;
            }

            ExternalChange change = found.Status == MissionFileStatus.Missing ? ExternalChange.Deleted
                : found.Status == MissionFileStatus.Unreadable ? ExternalChange.Unreadable
                : ExternalChange.Modified;
            prompts.AskExternalChange(FilePath, change, choice =>
            {
                if (choice == ExternalChangeChoice.Overwrite) WriteOverCurrentFile(text, found, then);
                else if (choice == ExternalChangeChoice.SaveAs) ChooseAndWrite(text, then);
                else then(false);
            });
        }

        void Write(string path, string text, Action<bool> then)
        {
            try
            {
                files.WriteAtomically(path, text);
            }
            catch (MissionFileException problem)
            {
                prompts.ShowError("Could not save " + Path.GetFileName(path),
                    problem.Message + "\n\nNothing was written to " + path
                    + ". Your changes are still here; try again or use Save As.", () => then(false));
                return;
            }

            FilePath = path;
            baseline = text;
            edited = false;
            int warnings = Validation.WarningCount;
            Status = "Saved " + FileName + (warnings == 0 ? "." : " with " + Count(warnings, "warning") + ".");
            then(true);
        }

        // ------------------------------------------------------------------ players

        /// <summary>
        /// Removes a player. A player that owns nodes is removed only once the author has confirmed it,
        /// knowing how many nodes become neutral. A player that owns none changes no node, so it is
        /// removed at once, without a question.
        /// </summary>
        public void RequestRemovePlayer(string playerId)
        {
            if (Busy || Document.Mission.FindPlayer(playerId) == null) return;

            int ownedNodes = Document.NodesOwnedBy(playerId);
            if (ownedNodes == 0)
            {
                if (Document.RemovePlayer(playerId).Applied) Status = "Removed player " + playerId + ".";
                RaiseChanged();
                return;
            }

            if (!Begin()) return;

            prompts.ConfirmRemovePlayer(playerId, ownedNodes, confirmed =>
            {
                if (confirmed && Document.RemovePlayer(playerId).Applied) Status = "Removed player " + playerId + ".";
                End();
            });
        }

        // ------------------------------------------------------------------ tools and selection

        /// <summary>Arms Add Node: the next click on the canvas places a node. Asking again disarms it.</summary>
        public void ArmAddNode()
        {
            Arm(EditorTool.AddNode);
        }

        /// <summary>
        /// Arms Add Connection. With a node selected the connection starts from that node, and the next
        /// node clicked is connected to it; with none, the next two nodes clicked are connected. Asking
        /// again disarms it.
        /// </summary>
        public void ArmAddConnection()
        {
            Arm(EditorTool.AddConnection);
        }

        void Arm(EditorTool tool)
        {
            if (Busy) return;

            Tool = Tool == tool ? EditorTool.Select : tool;
            ConnectionSource = Tool == EditorTool.AddConnection && Selection.Kind == SelectionKind.Node
                ? Selection.NodeId
                : null;
            Status = "";
            RaiseChanged();
        }

        /// <summary>What Escape does: puts a pending tool or connection away. False when there was none.</summary>
        public bool CancelTool()
        {
            if (Busy || Tool == EditorTool.Select) return false;

            Tool = EditorTool.Select;
            ConnectionSource = null;
            Status = "";
            RaiseChanged();
            return true;
        }

        /// <summary>
        /// A click on the canvas at a map position. With Add Node armed it places a node there, on top
        /// of whatever is there; otherwise it is a click on empty space.
        /// </summary>
        public void ClickCanvas(double x, double y)
        {
            if (Busy) return;

            if (Tool == EditorTool.AddNode)
            {
                LeaveSelection(() =>
                {
                    EditResult result = Document.AddNode(x, y);
                    if (result.Applied)
                    {
                        Selection = Selection.Node(result.CreatedId);
                        Tool = EditorTool.Select;
                        Status = "Added node " + result.CreatedId + ".";
                    }
                    else
                    {
                        Status = result.Message;
                    }

                    RaiseChanged();
                });
            }
            else if (Tool == EditorTool.Select)
            {
                Select(Selection.None);
            }
        }

        /// <summary>A click on a node: selects it, or takes it as an end of the connection being made.</summary>
        public void ClickNode(string nodeId)
        {
            if (Busy || Document.Mission.FindNode(nodeId) == null) return;

            if (Tool == EditorTool.Select)
            {
                Select(Selection.Node(nodeId));
                return;
            }

            if (Tool != EditorTool.AddConnection) return;

            if (ConnectionSource == null)
            {
                LeaveSelection(() =>
                {
                    ConnectionSource = nodeId;
                    Selection = Selection.Node(nodeId);
                    Status = "";
                    RaiseChanged();
                });
                return;
            }

            // A refused connection stays pending, so another second node can be picked.
            string source = ConnectionSource;
            if (MissionRules.SameId(source, nodeId) || Document.Mission.FindEdge(source, nodeId) != null)
            {
                Status = Document.Connect(source, nodeId).Message;
                RaiseChanged();
                return;
            }

            LeaveSelection(() =>
            {
                EditResult result = Document.Connect(source, nodeId);
                if (result.Applied)
                {
                    Selection = Selection.Edge(source, nodeId);
                    Tool = EditorTool.Select;
                    ConnectionSource = null;
                    Status = "Connected " + source + " and " + nodeId + ".";
                }
                else
                {
                    Status = result.Message;
                }

                RaiseChanged();
            });
        }

        /// <summary>A click on a route: selects it. Ignored while a tool is armed.</summary>
        public void ClickEdge(string a, string b)
        {
            if (Busy || Tool != EditorTool.Select) return;
            Select(Selection.Edge(a, b));
        }

        /// <summary>Selects one object, or nothing. An object that does not exist selects nothing.</summary>
        public void Select(Selection selection)
        {
            if (Busy) return;

            MissionDefinition mission = Document.Mission;
            if (selection.Kind == SelectionKind.Node && mission.FindNode(selection.NodeId) == null) selection = Selection.None;
            if (selection.Kind == SelectionKind.Edge && mission.FindEdge(selection.NodeId, selection.OtherNodeId) == null)
                selection = Selection.None;
            if (selection.Equals(Selection)) return;

            LeaveSelection(() =>
            {
                Selection = selection;
                RaiseChanged();
            });
        }

        /// <summary>
        /// Deletes the selected object: a route alone, or a node together with the routes that end at
        /// it. Returns false when nothing was selected.
        /// </summary>
        public bool DeleteSelected()
        {
            if (Busy) return false;

            Selection selected = Selection;
            if (selected.Kind == SelectionKind.None)
            {
                Status = "Nothing is selected.";
                RaiseChanged();
                return false;
            }

            // Text typed into the fields of an object that is being deleted goes with it.
            DiscardInvalidInput(true);
            if (selected.Kind == SelectionKind.Node)
            {
                int routes = 0;
                foreach (MissionEdge edge in Document.Mission.Edges)
                    if (MissionRules.SameId(edge.Source, selected.NodeId) || MissionRules.SameId(edge.Target, selected.NodeId)) routes++;
                Document.RemoveNode(selected.NodeId);
                Status = "Deleted node " + selected.NodeId + (routes == 0 ? "." : " and " + Count(routes, "route") + ".");
            }
            else
            {
                Document.Disconnect(selected.NodeId, selected.OtherNodeId);
                Status = "Deleted the route between " + selected.NodeId + " and " + selected.OtherNodeId + ".";
            }

            RaiseChanged();
            return true;
        }

        // ------------------------------------------------------------------ wording

        static string Count(int count, string noun)
        {
            return count + " " + noun + (count == 1 ? "" : "s");
        }

        static string Describe(IReadOnlyList<MissionIssue> issues)
        {
            const int shown = 8;
            var text = new StringBuilder();
            for (int i = 0; i < issues.Count && i < shown; i++)
            {
                if (i > 0) text.Append('\n');
                text.Append("- ").Append(issues[i]);
            }

            if (issues.Count > shown) text.Append("\n- and ").Append(issues.Count - shown).Append(" more");
            return text.ToString();
        }
    }
}
