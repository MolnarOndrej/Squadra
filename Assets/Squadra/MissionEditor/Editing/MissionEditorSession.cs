using System;
using System.IO;
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
    ///
    /// The steps of reading and writing a file, with their messages, are in
    /// <see cref="MissionFileOperations"/>, and the list of fields holding invalid text is in
    /// <see cref="InvalidInputs"/>. The session keeps the state and decides what each outcome means.
    /// </summary>
    public sealed class MissionEditorSession
    {
        readonly IMissionEditorShell shell;
        readonly IMissionEditorPrompts prompts;
        readonly Func<string> newMissionId;
        readonly MissionFileOperations fileOperations;
        readonly InvalidInputs invalidInputs = new InvalidInputs();

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
            if (files == null) throw new ArgumentNullException(nameof(files));
            this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
            this.prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
            this.newMissionId = newMissionId ?? MissionDocument.GenerateMissionId;
            fileOperations = new MissionFileOperations(files, shell, prompts, serialize ?? MissionJson.Write);
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

        public bool HasInvalidInput => invalidInputs.Any;

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
            invalidInputs.ClearAll();
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
            invalidInputs.Report(field, label, ofSelection);
        }

        public void ClearInvalidInput(string field)
        {
            invalidInputs.Clear(field);
        }

        /// <summary>True while a field's invalid text is still waiting to be corrected or given up.</summary>
        public bool IsInvalidInput(string field)
        {
            return invalidInputs.Contains(field);
        }

        void DiscardInvalidInput(bool selectionOnly)
        {
            if (invalidInputs.Discard(selectionOnly)) InvalidInputDiscarded?.Invoke();
        }

        // Settles invalid field text before an action that would put it away. A save discards it at
        // once, because the file gets the mission's values. A change of mission only notes the
        // author's permission: the text stays until that change really happens.
        void SettleInvalidInput(bool discardNow, Action<bool> then)
        {
            if (!invalidInputs.Any)
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

            prompts.AskInvalidInput(invalidInputs.Labels(false), discard =>
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
            string labels = invalidInputs.Labels(true);
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
            fileOperations.Read(path, (mission, fileText) =>
            {
                if (mission != null)
                {
                    Adopt(new MissionDocument(mission), path, fileText);
                    int warnings = Validation.WarningCount;
                    Status = "Opened " + FileName
                        + (warnings == 0 ? "." : " with " + EditorWording.Count(warnings, "warning") + ".");
                    DocumentReplaced?.Invoke();
                }

                then();
            });
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

                fileOperations.Prepare(Document.Mission, Validation, text =>
                {
                    if (text == null) then(false);
                    else fileOperations.WriteOverFile(FilePath, baseline, text, SuggestFileName, path => Saved(path, text, then));
                });
            });
        }

        void SaveToChosenFile(Action<bool> then)
        {
            fileOperations.Prepare(Document.Mission, Validation, text =>
            {
                if (text == null) then(false);
                else fileOperations.WriteToChosenFile(text, SuggestFileName, path => Saved(path, text, then));
            });
        }

        // Asked for at the moment the file dialog opens, which can be after other questions.
        string SuggestFileName()
        {
            return FilePath != null ? FileName : Document.Mission.Id + ".json";
        }

        // The end of every save. A path means the text is in that file, which is the mission's file
        // from now on; null means nothing was written and nothing changes.
        void Saved(string path, string text, Action<bool> then)
        {
            if (path == null)
            {
                then(false);
                return;
            }

            FilePath = path;
            baseline = text;
            edited = false;
            int warnings = Validation.WarningCount;
            Status = "Saved " + FileName
                + (warnings == 0 ? "." : " with " + EditorWording.Count(warnings, "warning") + ".");
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

        /// <summary>
        /// What Escape and a right-click on the canvas do, one step at a time: an armed tool is put
        /// away first, with its half-made connection, and the selection stays; only with no tool armed
        /// is the selection cleared, asking first when its fields hold invalid text. Changes nothing in
        /// the mission. False when there was nothing to cancel.
        /// </summary>
        public bool Cancel()
        {
            if (Busy) return false;

            if (Tool != EditorTool.Select)
            {
                Tool = EditorTool.Select;
                ConnectionSource = null;
                Status = "";
                RaiseChanged();
                return true;
            }

            if (Selection.Kind == SelectionKind.None) return false;

            Select(Selection.None);
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
                Status = "Deleted node " + selected.NodeId
                    + (routes == 0 ? "." : " and " + EditorWording.Count(routes, "route") + ".");
            }
            else
            {
                Document.Disconnect(selected.NodeId, selected.OtherNodeId);
                Status = "Deleted the route between " + selected.NodeId + " and " + selected.OtherNodeId + ".";
            }

            RaiseChanged();
            return true;
        }
    }
}
