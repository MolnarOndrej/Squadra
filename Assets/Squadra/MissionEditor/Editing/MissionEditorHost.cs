using System;
using Squadra.Missions;

namespace Squadra.MissionEditing
{
    public enum SelectionKind
    {
        None,
        Node,
        Edge,
    }

    /// <summary>The one object the author has selected: nothing, a node, or the route between two nodes.</summary>
    public readonly struct Selection : IEquatable<Selection>
    {
        Selection(SelectionKind kind, string nodeId, string otherNodeId)
        {
            Kind = kind;
            NodeId = nodeId;
            OtherNodeId = otherNodeId;
        }

        public SelectionKind Kind { get; }

        /// <summary>The selected node, or one end of the selected route.</summary>
        public string NodeId { get; }

        /// <summary>The other end of the selected route; null otherwise.</summary>
        public string OtherNodeId { get; }

        public static readonly Selection None = default;

        public static Selection Node(string id)
        {
            return new Selection(SelectionKind.Node, id, null);
        }

        public static Selection Edge(string a, string b)
        {
            return new Selection(SelectionKind.Edge, a, b);
        }

        public bool IsNode(string id)
        {
            return Kind == SelectionKind.Node && MissionRules.SameId(NodeId, id);
        }

        public bool IsEdge(MissionEdge edge)
        {
            return Kind == SelectionKind.Edge && edge != null && edge.Joins(NodeId, OtherNodeId);
        }

        public bool Equals(Selection other)
        {
            if (Kind != other.Kind) return false;
            if (Kind == SelectionKind.None) return true;
            if (Kind == SelectionKind.Node) return MissionRules.SameId(NodeId, other.NodeId);
            return (MissionRules.SameId(NodeId, other.NodeId) && MissionRules.SameId(OtherNodeId, other.OtherNodeId))
                || (MissionRules.SameId(NodeId, other.OtherNodeId) && MissionRules.SameId(OtherNodeId, other.NodeId));
        }

        public override bool Equals(object obj)
        {
            return obj is Selection other && Equals(other);
        }

        public override int GetHashCode()
        {
            int a = NodeId != null ? StringComparer.Ordinal.GetHashCode(NodeId) : 0;
            int b = OtherNodeId != null ? StringComparer.Ordinal.GetHashCode(OtherNodeId) : 0;
            return (int)Kind ^ a ^ b;
        }
    }

    public enum EditorTool
    {
        /// <summary>Clicking selects, dragging a node moves it.</summary>
        Select,

        /// <summary>The next click on the canvas places a node.</summary>
        AddNode,

        /// <summary>The next two nodes clicked are connected.</summary>
        AddConnection,
    }

    /// <summary>What the author asked for that would put the current mission away.</summary>
    public enum LeaveReason
    {
        NewMission,
        OpenMission,
        Quit,
    }

    public enum UnsavedChoice
    {
        Save,
        Discard,
        Cancel,
    }

    /// <summary>What happened to the mission's file since the editor last read or wrote it.</summary>
    public enum ExternalChange
    {
        Modified,
        Deleted,
        Unreadable,
    }

    public enum ExternalChangeChoice
    {
        /// <summary>Replace the file as it now is with the mission being edited.</summary>
        Overwrite,

        /// <summary>Leave that file alone and save under another name.</summary>
        SaveAs,

        Cancel,
    }

    /// <summary>Where a Save As should write.</summary>
    public sealed class SaveDestination
    {
        public SaveDestination(string path, bool replacementConfirmed)
        {
            Path = path;
            ReplacementConfirmed = replacementConfirmed;
        }

        public string Path { get; }

        /// <summary>
        /// True when the author has already agreed, in the file dialog, to replace a file that exists
        /// at the path. When false the editor asks before it replaces one.
        /// </summary>
        public bool ReplacementConfirmed { get; }
    }

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
