using System.Collections.Generic;
using Squadra.MissionEditing;
using Squadra.Missions;
using UnityEngine.UIElements;

namespace Squadra
{
    /// <summary>
    /// The Validation section of the property panel: a one-line summary and a row for every error
    /// and warning. Clicking a row selects what it is about and brings it into view on the canvas.
    /// </summary>
    sealed class MissionValidationPanel
    {
        readonly MissionEditorSession session;
        readonly MissionCanvas canvas;
        readonly Label summary;
        readonly VisualElement body;

        MissionValidation shown;

        public MissionValidationPanel(MissionEditorSession session, MissionCanvas canvas, Label summary, VisualElement body)
        {
            this.session = session;
            this.canvas = canvas;
            this.summary = summary;
            this.body = body;
        }

        /// <summary>The rows belong to a mission that is gone: the next refresh makes them anew.</summary>
        public void Invalidate()
        {
            shown = null;
        }

        public void Refresh()
        {
            MissionValidation validation = session.Validation;
            if (ReferenceEquals(validation, shown)) return;

            shown = validation;
            body.Clear();
            summary.EnableInClassList("validation__summary--errors", validation.HasErrors);
            summary.EnableInClassList("validation__summary--warnings",
                !validation.HasErrors && validation.WarningCount > 0);
            if (validation.Issues.Count == 0)
            {
                summary.text = "No problems found.";
                return;
            }

            var counts = new List<string>();
            if (validation.ErrorCount > 0) counts.Add(EditorWording.Count(validation.ErrorCount, "error"));
            if (validation.WarningCount > 0) counts.Add(EditorWording.Count(validation.WarningCount, "warning"));
            summary.text = string.Join(", ", counts)
                + (validation.HasErrors ? ". Errors must be corrected before saving." : ". Warnings do not stop a save.");

            foreach (MissionIssue issue in validation.Issues) body.Add(IssueRow(issue));
        }

        Button IssueRow(MissionIssue issue)
        {
            var row = new Button(() => Show(issue));
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
            return row;
        }

        // Selects what a message is about and brings it into view.
        void Show(MissionIssue issue)
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
    }
}
