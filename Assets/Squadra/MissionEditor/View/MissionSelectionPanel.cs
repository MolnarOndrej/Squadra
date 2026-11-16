using System;
using System.Collections.Generic;
using Squadra.MissionEditing;
using Squadra.Missions;
using UnityEngine.UIElements;

namespace Squadra
{
    /// <summary>
    /// The Selection section of the property panel: the selected node's type, size, owner, Darts and
    /// position, or the selected route's ends and distances. Its controls are made anew when the
    /// selection changes and only brought up to date while it stays the same.
    /// </summary>
    sealed class MissionSelectionPanel
    {
        const string NeutralChoice = "(neutral)";

        // In the order of MissionNodeType.
        static readonly string[] TypeChoices = { "Planet", "Moon", "Asteroid" };

        readonly MissionEditorSession session;
        readonly VisualElement body;
        readonly List<MissionTextEntry> entries = new List<MissionTextEntry>();

        Selection shown;
        bool built;
        DropdownField typeField;
        DropdownField ownerField;
        Label mapDistanceValue;
        Label gameDistanceValue;

        public MissionSelectionPanel(MissionEditorSession session, VisualElement body)
        {
            this.session = session;
            this.body = body;
        }

        /// <summary>The controls belong to a mission that is gone: the next refresh makes them anew.</summary>
        public void Invalidate()
        {
            built = false;
        }

        public void Refresh()
        {
            if (!built || !session.Selection.Equals(shown)) Build();

            MissionDefinition mission = session.Document.Mission;
            if (shown.Kind == SelectionKind.Node)
            {
                MissionNode node = mission.FindNode(shown.NodeId);
                if (node == null) return;

                typeField.SetValueWithoutNotify(TypeChoices[(int)node.Type]);
                List<string> owners = OwnerChoices();
                if (!Same(ownerField.choices, owners)) ownerField.choices = owners;
                ownerField.SetValueWithoutNotify(node.Owner ?? NeutralChoice);
                foreach (MissionTextEntry entry in entries) entry.Refresh();
            }
            else if (shown.Kind == SelectionKind.Edge)
            {
                MissionEdge edge = mission.FindEdge(shown.NodeId, shown.OtherNodeId);
                if (edge == null) return;

                mapDistanceValue.text = MissionFields.FormatDistance(mission.MapDistance(edge));
                gameDistanceValue.text = MissionFields.FormatDistance(mission.GameDistance(edge));
            }
        }

        void Build()
        {
            foreach (MissionTextEntry entry in entries) entry.Forget();
            entries.Clear();
            body.Clear();
            typeField = null;
            ownerField = null;
            shown = session.Selection;
            built = true;

            if (shown.Kind == SelectionKind.None)
                body.Add(MissionEditorRows.Note("Nothing is selected. Click a node or a route on the canvas."));
            else if (shown.Kind == SelectionKind.Edge)
                BuildRoute();
            else
                BuildNode(shown.NodeId);
        }

        void BuildRoute()
        {
            MissionEdge edge = session.Document.Mission.FindEdge(shown.NodeId, shown.OtherNodeId);
            MissionEditorRows.ReadOnlyRow(body, "Route from", edge != null ? edge.Source : shown.NodeId);
            MissionEditorRows.ReadOnlyRow(body, "To", edge != null ? edge.Target : shown.OtherNodeId);
            mapDistanceValue = MissionEditorRows.ReadOnlyRow(body, "Map distance", "");
            gameDistanceValue = MissionEditorRows.ReadOnlyRow(body, "Distance", "");
        }

        void BuildNode(string id)
        {
            MissionEditorRows.ReadOnlyRow(body, "Node ID", id);

            typeField = MissionEditorRows.Dropdown(body, "Type", new List<string>(TypeChoices));
            typeField.RegisterValueChangedCallback(changed =>
            {
                int index = Array.IndexOf(TypeChoices, changed.newValue);
                if (index >= 0) session.Document.SetNodeType(id, (MissionNodeType)index);
            });

            // Under Type, because a change of type sets the size to the new type's default.
            entries.Add(new MissionTextEntry(session, body, "node.size", "Size", true,
                () => MissionFields.Format(NodeOr(id).Size),
                text => MissionFields.TryParseSize(text, out double size, out string error)
                    ? MissionTextEntry.Problem(session.Document.SetNodeSize(id, size))
                    : error,
                text => MissionFields.TryParseSize(text, out double size, out _) && size == NodeOr(id).Size));
            MissionEditorRows.RowNote(body, "The body's radius in game world units.");

            ownerField = MissionEditorRows.Dropdown(body, "Owner", OwnerChoices());
            ownerField.RegisterValueChangedCallback(changed =>
                session.Document.SetNodeOwner(id, changed.newValue == NeutralChoice ? null : changed.newValue));

            entries.Add(new MissionTextEntry(session, body, "node.darts", "Starting Darts", true,
                () => MissionFields.Format(NodeOr(id).Darts),
                text => MissionFields.TryParseDarts(text, out int darts, out string error)
                    ? MissionTextEntry.Problem(session.Document.SetNodeDarts(id, darts))
                    : error,
                text => MissionFields.TryParseDarts(text, out int darts, out _) && darts == NodeOr(id).Darts));

            entries.Add(new MissionTextEntry(session, body, "node.x", "X", true,
                () => MissionFields.Format(NodeOr(id).X),
                text => MissionFields.TryParseCoordinate(text, out double x, out string error)
                    ? MissionTextEntry.Problem(session.Document.MoveNode(id, x, NodeOr(id).Y))
                    : error,
                text => MissionFields.TryParseCoordinate(text, out double x, out _) && x == NodeOr(id).X));

            entries.Add(new MissionTextEntry(session, body, "node.y", "Y", true,
                () => MissionFields.Format(NodeOr(id).Y),
                text => MissionFields.TryParseCoordinate(text, out double y, out string error)
                    ? MissionTextEntry.Problem(session.Document.MoveNode(id, NodeOr(id).X, y))
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
    }
}
