using System;
using System.Collections.Generic;
using System.Text;
using Squadra.MissionEditing;
using Squadra.Missions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Squadra
{
    /// <summary>
    /// The mission editor on screen: the toolbar, the schematic canvas, the property panel with the
    /// selected object, the mission's settings, its players and the validation messages, the status
    /// line, and the dialogs the editor asks its questions with. It shows a
    /// <see cref="MissionEditorSession"/> and passes the author's gestures on to it; what an edit does,
    /// when work may be replaced and how a file is written are the session's decisions, not this
    /// component's. Files and the exit come from the <see cref="MissionEditorShell"/> beside it.
    /// </summary>
    public sealed class MissionEditorView : MonoBehaviour, IMissionEditorPrompts
    {
        const string OnClass = "tool-button--on";
        const string InvalidClass = "row__control--invalid";
        const string PopupClass = "unity-base-dropdown";
        const string NeutralChoice = "(neutral)";

        static readonly string[] TypeChoices = { "Planet", "Moon" };

        [SerializeField] UIDocument document;
        [SerializeField] MissionEditorShell shell;

        MissionEditorSession session;
        VisualElement root;
        VisualElement content;
        MissionCanvas canvas;
        MissionEditorDialog dialog;

        Button saveButton;
        Button addNodeButton;
        Button addConnectionButton;
        Button deleteButton;
        Button distancesButton;
        Label zoomLabel;
        Label fileLabel;
        Label statusLabel;
        Label hintLabel;
        Label validationSummary;
        VisualElement selectionBody;
        VisualElement missionBody;
        VisualElement playersBody;
        VisualElement validationBody;

        readonly List<TextEntry> missionEntries = new List<TextEntry>();
        readonly List<TextEntry> selectionEntries = new List<TextEntry>();
        Selection shownSelection;
        bool selectionBuilt;
        DropdownField typeField;
        DropdownField ownerField;
        Label mapDistanceValue;
        Label gameDistanceValue;

        string shownPlayers;
        TextField newPlayerId;
        DropdownField newPlayerColor;
        Label newPlayerError;
        MissionValidation shownValidation;

        bool refreshPending;
        bool popupWasOpen;

        /// <summary>The editing session this view shows; null until the scene has started.</summary>
        public MissionEditorSession Session => session;

        public MissionCanvas Canvas => canvas;

        public bool DialogOpen => dialog != null && dialog.IsOpen;
        public string DialogTitle => DialogOpen ? dialog.Title : string.Empty;
        public string DialogMessage => DialogOpen ? dialog.Message : string.Empty;

        /// <summary>The labels of the open dialog's buttons, in order; empty when none is open.</summary>
        public string[] DialogChoices
        {
            get
            {
                if (!DialogOpen) return Array.Empty<string>();

                var labels = new string[dialog.ChoiceCount];
                for (int i = 0; i < labels.Length; i++) labels[i] = dialog.ChoiceLabel(i);
                return labels;
            }
        }

        /// <summary>Gives the open dialog the answer at an index, as clicking that button does.</summary>
        public void ChooseDialog(int index)
        {
            if (DialogOpen) dialog.Choose(index);
        }

        // ------------------------------------------------------------------ lifecycle

        void Start()
        {
            if (document == null) document = GetComponent<UIDocument>();
            if (shell == null) shell = GetComponent<MissionEditorShell>();

            root = document != null ? document.rootVisualElement?.Q("editorRoot") : null;
            if (root == null || shell == null)
            {
                Debug.LogError("Squadra: the mission editor needs its UI document and a shell.", this);
                return;
            }

            content = root.Q("editorContent");
            canvas = root.Q<MissionCanvas>("canvas");
            saveButton = root.Q<Button>("saveButton");
            addNodeButton = root.Q<Button>("addNodeButton");
            addConnectionButton = root.Q<Button>("addConnectionButton");
            deleteButton = root.Q<Button>("deleteButton");
            distancesButton = root.Q<Button>("distancesButton");
            zoomLabel = root.Q<Label>("zoomLabel");
            fileLabel = root.Q<Label>("fileLabel");
            statusLabel = root.Q<Label>("statusLabel");
            hintLabel = root.Q<Label>("hintLabel");
            validationSummary = root.Q<Label>("validationSummary");
            selectionBody = root.Q("selectionBody");
            missionBody = root.Q("missionBody");
            playersBody = root.Q("playersBody");
            validationBody = root.Q("validationBody");
            dialog = new MissionEditorDialog(root.Q("dialogLayer"), OnDialogOpened, OnDialogClosed);

            session = new MissionEditorSession(shell.Files, shell, this);
            session.Changed += () => refreshPending = true;
            session.DocumentReplaced += OnDocumentReplaced;
            session.InvalidInputDiscarded += () => refreshPending = true;
            shell.Attach(session);

            root.Q<Button>("newButton").clicked += session.New;
            root.Q<Button>("openButton").clicked += session.Open;
            saveButton.clicked += session.Save;
            root.Q<Button>("saveAsButton").clicked += session.SaveAs;
            addNodeButton.clicked += session.ArmAddNode;
            addConnectionButton.clicked += session.ArmAddConnection;
            deleteButton.clicked += () => session.DeleteSelected();
            root.Q<Button>("zoomOutButton").clicked += canvas.ZoomOut;
            root.Q<Button>("zoomInButton").clicked += canvas.ZoomIn;
            root.Q<Button>("fitButton").clicked += canvas.FitToMap;
            distancesButton.clicked += () => session.ShowDistances = !session.ShowDistances;

            canvas.Bind(session);
            canvas.ViewChanged += () => zoomLabel.text = canvas.ZoomText;
            BuildMissionFields();
            RefreshNow();
        }

        void Update()
        {
            if (session == null) return;

            // Escape also closes an open dropdown list; that press must not cancel a tool as well.
            bool popupOpen = root.panel != null && root.panel.visualTree.Q(className: PopupClass) != null;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) PressEscape(popupOpen || popupWasOpen);
                if (!DialogOpen && !popupOpen) HandleShortcuts(keyboard);
            }

            popupWasOpen = popupOpen;
            if (refreshPending) RefreshNow();
        }

        void HandleShortcuts(Keyboard keyboard)
        {
            if (keyboard.deleteKey.wasPressedThisFrame && !TypingInField) PressDelete();

            if (!keyboard.ctrlKey.isPressed) return;

            if (keyboard.nKey.wasPressedThisFrame) session.New();
            else if (keyboard.oKey.wasPressedThisFrame) session.Open();
            else if (keyboard.sKey.wasPressedThisFrame && keyboard.shiftKey.isPressed) session.SaveAs();
            else if (keyboard.sKey.wasPressedThisFrame) session.Save();
            else if (keyboard.qKey.wasPressedThisFrame) session.RequestQuit();
        }

        /// <summary>
        /// What Escape does: answers an open dialog with its safe choice, otherwise cancels a pending
        /// tool or connection, otherwise leaves the field being typed in.
        /// </summary>
        public void PressEscape(bool usedByControl = false)
        {
            if (session == null) return;

            if (DialogOpen)
            {
                dialog.Escape();
                return;
            }

            if (usedByControl) return;
            if (!session.CancelTool() && TypingInField) canvas.Focus();
        }

        /// <summary>What the Delete key does: deletes the selected object, unless a dialog is open.</summary>
        public void PressDelete()
        {
            if (session != null && !DialogOpen) session.DeleteSelected();
        }

        // True while the keyboard is typing into a text field: Delete then belongs to the text.
        bool TypingInField
        {
            get
            {
                for (var element = root.focusController?.focusedElement as VisualElement; element != null; element = element.parent)
                    if (element is TextField) return true;
                return false;
            }
        }

        void OnDocumentReplaced()
        {
            // Another mission: nothing typed for the old one applies any more.
            selectionBuilt = false;
            shownPlayers = null;
            shownValidation = null;
            BuildMissionFields();
            canvas.FitToMap();
            refreshPending = true;
        }

        void OnDialogOpened()
        {
            // Nothing under a dialog can be used: not by the pointer, which the dialog's layer stops,
            // and not by the keyboard, because the controls are disabled.
            canvas.EndGesture();
            content.SetEnabled(false);
        }

        void OnDialogClosed()
        {
            content.SetEnabled(true);
            refreshPending = true;
        }

        // ------------------------------------------------------------------ refresh

        /// <summary>Brings everything on screen up to date with the session at once.</summary>
        public void RefreshNow()
        {
            if (session == null) return;

            refreshPending = false;
            RefreshToolbar();
            foreach (TextEntry entry in missionEntries) entry.Refresh();
            RefreshPlayers();
            RefreshSelection();
            RefreshValidation();
            canvas.Refresh();
        }

        void RefreshToolbar()
        {
            addNodeButton.EnableInClassList(OnClass, session.Tool == EditorTool.AddNode);
            addConnectionButton.EnableInClassList(OnClass, session.Tool == EditorTool.AddConnection);
            distancesButton.EnableInClassList(OnClass, session.ShowDistances);
            deleteButton.SetEnabled(session.Selection.Kind != SelectionKind.None);
            zoomLabel.text = canvas.ZoomText;

            bool unsaved = session.HasUnsavedChanges;
            fileLabel.text = (session.FilePath ?? "No file yet") + (unsaved ? "  -  unsaved" : "");
            fileLabel.EnableInClassList("toolbar__file--unsaved", unsaved);

            statusLabel.text = session.Status;
            hintLabel.text = Hint;
        }

        string Hint
        {
            get
            {
                switch (session.Tool)
                {
                    case EditorTool.AddNode:
                        return "Click the canvas to place a node.  Esc cancels.";
                    case EditorTool.AddConnection:
                        return session.ConnectionSource == null
                            ? "Click the first node of the route.  Esc cancels."
                            : "Click the node to connect to " + session.ConnectionSource + ".  Esc cancels.";
                    default:
                        return "Drag a node to move it.  Drag the background to pan.  Scroll to zoom.";
                }
            }
        }

        // ------------------------------------------------------------------ mission settings

        void BuildMissionFields()
        {
            missionBody.Clear();
            missionEntries.Clear();

            missionEntries.Add(new TextEntry(session, missionBody, "mission.id", "Mission ID", false,
                () => session.Document.Mission.Id,
                text => MissionFields.TryParseId(text, out string error) ? Problem(session.Document.SetMissionId(text)) : error,
                text => text == session.Document.Mission.Id));

            missionEntries.Add(new TextEntry(session, missionBody, "mission.name", "Name", false,
                () => session.Document.Mission.Name,
                text => Problem(session.Document.SetName(text)),
                text => text == session.Document.Mission.Name));

            missionEntries.Add(new TextEntry(session, missionBody, "mission.scale", "Distance scale", false,
                () => MissionFields.Format(session.Document.Mission.DistanceScale),
                text => MissionFields.TryParseDistanceScale(text, out double scale, out string error)
                    ? Problem(session.Document.SetDistanceScale(scale))
                    : error,
                text => MissionFields.TryParseDistanceScale(text, out double scale, out _)
                    && scale == session.Document.Mission.DistanceScale));
        }

        static string Problem(EditResult result)
        {
            return result.Rejected ? result.Message : null;
        }

        // ------------------------------------------------------------------ selection

        void RefreshSelection()
        {
            if (!selectionBuilt || !session.Selection.Equals(shownSelection)) BuildSelection();

            MissionDefinition mission = session.Document.Mission;
            if (shownSelection.Kind == SelectionKind.Node)
            {
                MissionNode node = mission.FindNode(shownSelection.NodeId);
                if (node == null) return;

                typeField.SetValueWithoutNotify(TypeChoices[(int)node.Type]);
                List<string> owners = OwnerChoices();
                if (!Same(ownerField.choices, owners)) ownerField.choices = owners;
                ownerField.SetValueWithoutNotify(node.Owner ?? NeutralChoice);
                foreach (TextEntry entry in selectionEntries) entry.Refresh();
            }
            else if (shownSelection.Kind == SelectionKind.Edge)
            {
                MissionEdge edge = mission.FindEdge(shownSelection.NodeId, shownSelection.OtherNodeId);
                if (edge == null) return;

                mapDistanceValue.text = MissionFields.FormatDistance(mission.MapDistance(edge));
                gameDistanceValue.text = MissionFields.FormatDistance(mission.GameDistance(edge));
            }
        }

        void BuildSelection()
        {
            foreach (TextEntry entry in selectionEntries) entry.Forget();
            selectionEntries.Clear();
            selectionBody.Clear();
            typeField = null;
            ownerField = null;
            shownSelection = session.Selection;
            selectionBuilt = true;

            if (shownSelection.Kind == SelectionKind.None)
            {
                selectionBody.Add(Note("Nothing is selected. Click a node or a route on the canvas."));
                return;
            }

            if (shownSelection.Kind == SelectionKind.Edge)
            {
                MissionEdge edge = session.Document.Mission.FindEdge(shownSelection.NodeId, shownSelection.OtherNodeId);
                ReadOnlyRow(selectionBody, "Route from", edge != null ? edge.Source : shownSelection.NodeId);
                ReadOnlyRow(selectionBody, "To", edge != null ? edge.Target : shownSelection.OtherNodeId);
                mapDistanceValue = ReadOnlyRow(selectionBody, "Map distance", "");
                gameDistanceValue = ReadOnlyRow(selectionBody, "Distance", "");
                return;
            }

            string id = shownSelection.NodeId;
            ReadOnlyRow(selectionBody, "Node ID", id);

            typeField = Dropdown(selectionBody, "Type", new List<string>(TypeChoices));
            typeField.RegisterValueChangedCallback(changed =>
            {
                int index = Array.IndexOf(TypeChoices, changed.newValue);
                if (index >= 0) session.Document.SetNodeType(id, (MissionNodeType)index);
            });

            ownerField = Dropdown(selectionBody, "Owner", OwnerChoices());
            ownerField.RegisterValueChangedCallback(changed =>
                session.Document.SetNodeOwner(id, changed.newValue == NeutralChoice ? null : changed.newValue));

            selectionEntries.Add(new TextEntry(session, selectionBody, "node.darts", "Starting Darts", true,
                () => MissionFields.Format(NodeOr(id).Darts),
                text => MissionFields.TryParseDarts(text, out int darts, out string error)
                    ? Problem(session.Document.SetNodeDarts(id, darts))
                    : error,
                text => MissionFields.TryParseDarts(text, out int darts, out _) && darts == NodeOr(id).Darts));

            selectionEntries.Add(new TextEntry(session, selectionBody, "node.x", "X", true,
                () => MissionFields.Format(NodeOr(id).X),
                text => MissionFields.TryParseCoordinate(text, out double x, out string error)
                    ? Problem(session.Document.MoveNode(id, x, NodeOr(id).Y))
                    : error,
                text => MissionFields.TryParseCoordinate(text, out double x, out _) && x == NodeOr(id).X));

            selectionEntries.Add(new TextEntry(session, selectionBody, "node.y", "Y", true,
                () => MissionFields.Format(NodeOr(id).Y),
                text => MissionFields.TryParseCoordinate(text, out double y, out string error)
                    ? Problem(session.Document.MoveNode(id, NodeOr(id).X, y))
                    : error,
                text => MissionFields.TryParseCoordinate(text, out double y, out _) && y == NodeOr(id).Y));
        }

        // The selected node, or a blank one in the instant between its deletion and the next refresh.
        MissionNode NodeOr(string id)
        {
            return session.Document.Mission.FindNode(id) ?? new MissionNode { Id = id };
        }

        // Neutral first, then the players by name, so a player keeps its place in the list however
        // often it is removed and added again.
        List<string> OwnerChoices()
        {
            var choices = new List<string> { NeutralChoice };
            choices.AddRange(session.Document.PlayerIdsByName());
            return choices;
        }

        static bool Same(List<string> a, List<string> b)
        {
            if (a == null || a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        // ------------------------------------------------------------------ players

        void RefreshPlayers()
        {
            MissionDefinition mission = session.Document.Mission;
            var signature = new StringBuilder();
            foreach (MissionPlayer player in mission.Players)
                signature.Append(player.Id).Append(':').Append((int)player.Color).Append(';');
            if (signature.ToString() == shownPlayers) return;

            shownPlayers = signature.ToString();
            playersBody.Clear();
            if (mission.Players.Count == 0) playersBody.Add(Note("This mission has no players. Every node is neutral."));

            foreach (MissionPlayer player in mission.Players)
            {
                string id = player.Id;
                var row = new VisualElement();
                row.AddToClassList("player");

                var swatch = new VisualElement();
                swatch.AddToClassList("player__swatch");
                swatch.style.backgroundColor = MissionCanvas.ColorOf(player.Color);
                row.Add(swatch);

                var name = new Label(id);
                name.AddToClassList("player__id");
                row.Add(name);

                // The colors nobody has, and this player's own as the choice that changes nothing.
                var color = new DropdownField { choices = ColorNames(session.Document.AvailableColors(id)) };
                color.AddToClassList("row__control");
                color.AddToClassList("player__color");
                color.SetValueWithoutNotify(ColorName(player.Color));
                color.RegisterValueChangedCallback(changed =>
                {
                    if (TryColor(changed.newValue, out PlayerColor chosen)) session.Document.SetPlayerColor(id, chosen);
                });
                row.Add(color);

                var remove = new Button(() => session.RequestRemovePlayer(id)) { text = "Remove" };
                remove.AddToClassList("tool-button");
                remove.AddToClassList("small-button");
                row.Add(remove);
                playersBody.Add(row);
            }

            BuildAddPlayer(mission);
        }

        void BuildAddPlayer(MissionDefinition mission)
        {
            var add = new VisualElement();
            add.AddToClassList("players__add");
            playersBody.Add(add);

            bool room = session.Document.CanAddPlayer;
            var row = new VisualElement();
            row.AddToClassList("player");
            add.Add(row);

            newPlayerId = new TextField { value = room ? SuggestedPlayerId(mission) : "" };
            newPlayerId.AddToClassList("row__control");
            newPlayerId.AddToClassList("player__add-id");
            newPlayerId.RegisterValueChangedCallback(_ => ShowNewPlayerError(null));
            row.Add(newPlayerId);

            List<string> free = ColorNames(session.Document.AvailableColors());
            newPlayerColor = new DropdownField { choices = free };
            newPlayerColor.AddToClassList("row__control");
            newPlayerColor.AddToClassList("player__color");
            if (free.Count > 0) newPlayerColor.SetValueWithoutNotify(free[0]);
            row.Add(newPlayerColor);

            var button = new Button(AddPlayer) { name = "addPlayerButton", text = "Add Player" };
            button.AddToClassList("tool-button");
            button.AddToClassList("small-button");
            row.Add(button);

            newPlayerError = new Label();
            newPlayerError.AddToClassList("row__error");
            newPlayerError.style.marginLeft = 0f;
            add.Add(newPlayerError);

            // Six players is the most a mission holds: Add Player is unavailable until one is removed.
            newPlayerId.SetEnabled(room);
            newPlayerColor.SetEnabled(room);
            button.SetEnabled(room);
            ShowNewPlayerError(room ? null : "A mission can have at most " + MissionRules.MaxPlayers + " players.");
            if (!room) newPlayerError.style.color = new StyleColor(Palette.TextDim);
        }

        /// <summary>What the Add Player button does with the ID and color in its row.</summary>
        public void AddPlayer()
        {
            if (session == null || session.Busy || newPlayerId == null) return;

            if (!TryColor(newPlayerColor.value, out PlayerColor color))
            {
                ShowNewPlayerError("Every color is taken.");
                return;
            }

            EditResult result = session.Document.AddPlayer(newPlayerId.value, color);
            if (result.Rejected) ShowNewPlayerError(result.Message);
        }

        void ShowNewPlayerError(string message)
        {
            newPlayerError.text = message ?? "";
            newPlayerError.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
            newPlayerId.EnableInClassList(InvalidClass, !string.IsNullOrEmpty(message) && session.Document.CanAddPlayer);
        }

        static string SuggestedPlayerId(MissionDefinition mission)
        {
            for (int number = 1;; number++)
            {
                string id = "player" + number;
                if (mission.FindPlayer(id) == null) return id;
            }
        }

        static string ColorName(PlayerColor color)
        {
            string name = MissionRules.NameOf(color) ?? "?";
            return char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        static List<string> ColorNames(List<PlayerColor> colors)
        {
            var names = new List<string>();
            foreach (PlayerColor color in colors) names.Add(ColorName(color));
            return names;
        }

        static bool TryColor(string shown, out PlayerColor color)
        {
            color = default;
            return !string.IsNullOrEmpty(shown) && MissionRules.TryParseColor(shown.ToLowerInvariant(), out color);
        }

        // ------------------------------------------------------------------ validation

        void RefreshValidation()
        {
            MissionValidation validation = session.Validation;
            if (ReferenceEquals(validation, shownValidation)) return;

            shownValidation = validation;
            validationBody.Clear();
            validationSummary.EnableInClassList("validation__summary--errors", validation.HasErrors);
            validationSummary.EnableInClassList("validation__summary--warnings",
                !validation.HasErrors && validation.WarningCount > 0);
            if (validation.Issues.Count == 0)
            {
                validationSummary.text = "No problems found.";
                return;
            }

            var summary = new List<string>();
            if (validation.ErrorCount > 0) summary.Add(Count(validation.ErrorCount, "error"));
            if (validation.WarningCount > 0) summary.Add(Count(validation.WarningCount, "warning"));
            validationSummary.text = string.Join(", ", summary)
                + (validation.HasErrors ? ". Errors must be corrected before saving." : ". Warnings do not stop a save.");

            foreach (MissionIssue issue in validation.Issues)
            {
                MissionIssue shown = issue;
                var row = new Button(() => ShowIssue(shown));
                row.AddToClassList("issue");
                if (issue.IsError) row.AddToClassList("issue--error");

                var text = new VisualElement();
                text.style.flexShrink = 1f;
                var message = new Label((issue.IsError ? "Error: " : "Warning: ") + issue.Message);
                message.AddToClassList("issue__text");
                text.Add(message);
                if (issue.Location.Length > 0)
                {
                    var where = new Label(issue.Location);
                    where.AddToClassList("issue__where");
                    text.Add(where);
                }

                row.Add(text);
                validationBody.Add(row);
            }
        }

        // Clicking a message selects what it is about and brings it into view.
        void ShowIssue(MissionIssue issue)
        {
            MissionDefinition mission = session.Document.Mission;
            MissionNode node = issue.NodeId != null ? mission.FindNode(issue.NodeId) : null;
            if (node == null) return;

            MissionNode other = issue.OtherNodeId != null ? mission.FindNode(issue.OtherNodeId) : null;
            if (other != null && mission.FindEdge(node.Id, other.Id) != null)
            {
                session.Select(Selection.Edge(node.Id, other.Id));
                canvas.CenterOn((node.X + other.X) * 0.5, (node.Y + other.Y) * 0.5);
            }
            else
            {
                session.Select(Selection.Node(node.Id));
                canvas.CenterOn(node.X, node.Y);
            }
        }

        static string Count(int count, string noun)
        {
            return count + " " + noun + (count == 1 ? "" : "s");
        }

        // ------------------------------------------------------------------ rows

        static Label Note(string text)
        {
            var note = new Label(text);
            note.AddToClassList("section__note");
            return note;
        }

        static VisualElement Row(VisualElement parent, string label)
        {
            var row = new VisualElement();
            row.AddToClassList("row");
            var name = new Label(label);
            name.AddToClassList("row__label");
            row.Add(name);
            parent.Add(row);
            return row;
        }

        static Label ReadOnlyRow(VisualElement parent, string label, string value)
        {
            var text = new Label(value);
            text.AddToClassList("row__value");
            Row(parent, label).Add(text);
            return text;
        }

        static DropdownField Dropdown(VisualElement parent, string label, List<string> choices)
        {
            var field = new DropdownField { choices = choices };
            field.AddToClassList("row__control");
            Row(parent, label).Add(field);
            return field;
        }

        /// <summary>
        /// One editable text row. What is typed is applied to the mission at once when it is a valid
        /// value. Otherwise the text stays in the field, marked invalid with the reason under it, the
        /// mission keeps its value, and the session is told, so that a save or a change of mission
        /// cannot quietly lose what was typed.
        /// </summary>
        sealed class TextEntry
        {
            readonly MissionEditorSession session;
            readonly string key;
            readonly string label;
            readonly bool ofSelection;
            readonly Func<string> read;
            readonly Func<string, string> apply;
            readonly Func<string, bool> shows;
            readonly TextField field;
            readonly Label error;

            /// <param name="read">The mission's value as field text.</param>
            /// <param name="apply">Applies typed text; returns why it could not, or null.</param>
            /// <param name="shows">True when typed text already stands for the mission's value.</param>
            public TextEntry(MissionEditorSession session, VisualElement parent, string key, string label,
                bool ofSelection, Func<string> read, Func<string, string> apply, Func<string, bool> shows)
            {
                this.session = session;
                this.key = key;
                this.label = label;
                this.ofSelection = ofSelection;
                this.read = read;
                this.apply = apply;
                this.shows = shows;

                field = new TextField { name = key, value = read() };
                field.AddToClassList("row__control");
                Row(parent, label).Add(field);

                error = new Label();
                error.AddToClassList("row__error");
                error.style.display = DisplayStyle.None;
                parent.Add(error);

                field.RegisterValueChangedCallback(changed => OnTyped(changed.newValue));
            }

            void OnTyped(string text)
            {
                string problem = apply(text);
                if (problem == null) session.ClearInvalidInput(key);
                else session.ReportInvalidInput(key, label, ofSelection);
                Mark(problem);
            }

            /// <summary>Shows the mission's value, unless the author's invalid text is still waiting.</summary>
            public void Refresh()
            {
                if (session.IsInvalidInput(key)) return;

                Mark(null);
                if (!shows(field.value)) field.SetValueWithoutNotify(read());
            }

            /// <summary>The row is going away; whatever was typed in it goes with it.</summary>
            public void Forget()
            {
                session.ClearInvalidInput(key);
            }

            void Mark(string problem)
            {
                field.EnableInClassList(InvalidClass, problem != null);
                error.text = problem ?? "";
                error.style.display = problem != null ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        // ------------------------------------------------------------------ questions

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
            string effect = Count(ownedNodes, "node") + " owned by this player will become neutral. "
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
