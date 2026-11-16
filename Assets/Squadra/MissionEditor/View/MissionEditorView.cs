using System;
using Squadra.MissionEditing;
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
    ///
    /// This component finds the parts in the UI document, connects them to the session, reads the
    /// keyboard and keeps the toolbar and status line current. Each section of the property panel
    /// is a class of its own, and <see cref="MissionEditorPrompts"/> words the session's questions.
    /// </summary>
    public sealed class MissionEditorView : MonoBehaviour
    {
        const string OnClass = "tool-button--on";
        const string PopupClass = "unity-base-dropdown";

        [SerializeField] UIDocument document;
        [SerializeField] MissionEditorShell shell;

        MissionEditorSession session;
        VisualElement root;
        VisualElement content;
        MissionCanvas canvas;
        MissionEditorDialog dialog;

        Button addNodeButton;
        Button addConnectionButton;
        Button deleteButton;
        Button distancesButton;
        Label zoomLabel;
        Label fileLabel;
        Label statusLabel;
        Label hintLabel;

        MissionSettingsPanel settingsPanel;
        MissionSelectionPanel selectionPanel;
        MissionPlayersPanel playersPanel;
        MissionValidationPanel validationPanel;

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
            addNodeButton = root.Q<Button>("addNodeButton");
            addConnectionButton = root.Q<Button>("addConnectionButton");
            deleteButton = root.Q<Button>("deleteButton");
            distancesButton = root.Q<Button>("distancesButton");
            zoomLabel = root.Q<Label>("zoomLabel");
            fileLabel = root.Q<Label>("fileLabel");
            statusLabel = root.Q<Label>("statusLabel");
            hintLabel = root.Q<Label>("hintLabel");
            dialog = new MissionEditorDialog(root.Q("dialogLayer"), OnDialogOpened, OnDialogClosed);

            session = new MissionEditorSession(shell.Files, shell, new MissionEditorPrompts(dialog));
            session.Changed += () => refreshPending = true;
            session.DocumentReplaced += OnDocumentReplaced;
            session.InvalidInputDiscarded += () => refreshPending = true;
            shell.Attach(session);

            settingsPanel = new MissionSettingsPanel(session, root.Q("missionBody"));
            selectionPanel = new MissionSelectionPanel(session, root.Q("selectionBody"));
            playersPanel = new MissionPlayersPanel(session, root.Q("playersBody"));
            validationPanel = new MissionValidationPanel(session, canvas, root.Q<Label>("validationSummary"),
                root.Q("validationBody"));

            BindToolbar();
            canvas.Bind(session);
            canvas.ViewChanged += () => zoomLabel.text = canvas.ZoomText;
            settingsPanel.Build();
            RefreshNow();
        }

        void BindToolbar()
        {
            root.Q<Button>("newButton").clicked += session.New;
            root.Q<Button>("openButton").clicked += session.Open;
            root.Q<Button>("saveButton").clicked += session.Save;
            root.Q<Button>("saveAsButton").clicked += session.SaveAs;
            addNodeButton.clicked += session.ArmAddNode;
            addConnectionButton.clicked += session.ArmAddConnection;
            deleteButton.clicked += () => session.DeleteSelected();
            root.Q<Button>("zoomOutButton").clicked += canvas.ZoomOut;
            root.Q<Button>("zoomInButton").clicked += canvas.ZoomIn;
            root.Q<Button>("fitButton").clicked += canvas.FitToMap;
            distancesButton.clicked += () => session.ShowDistances = !session.ShowDistances;
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
        /// What Escape does: answers an open dialog with its safe choice, otherwise cancels as a
        /// right-click on the canvas does (a pending tool or connection first, then the selection),
        /// otherwise leaves the field being typed in.
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
            if (!session.Cancel() && TypingInField) canvas.Focus();
        }

        /// <summary>What the Delete key does: deletes the selected object, unless a dialog is open.</summary>
        public void PressDelete()
        {
            if (session != null && !DialogOpen) session.DeleteSelected();
        }

        /// <summary>What the Add Player button does with the ID and color in its row.</summary>
        public void AddPlayer()
        {
            if (session != null) playersPanel.AddPlayer();
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
            selectionPanel.Invalidate();
            playersPanel.Invalidate();
            validationPanel.Invalidate();
            settingsPanel.Build();
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
            settingsPanel.Refresh();
            playersPanel.Refresh();
            selectionPanel.Refresh();
            validationPanel.Refresh();
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
                        return "Click the canvas to place a node.  Esc or right-click cancels.";
                    case EditorTool.AddConnection:
                        return session.ConnectionSource == null
                            ? "Click the first node of the route.  Esc or right-click cancels."
                            : "Click the node to connect to " + session.ConnectionSource + ".  Esc or right-click cancels.";
                    default:
                        return "Drag a node to move it.  Drag the background to pan.  Scroll to zoom.";
                }
            }
        }
    }
}
