using System.Collections.Generic;
using Squadra.MissionEditing;
using Squadra.Missions;
using UnityEngine;
using UnityEngine.UIElements;

namespace Squadra
{
    /// <summary>
    /// The text on the mission canvas: node IDs and, when asked for, route distances. They are
    /// ordinary labels laid over the drawing, so their text keeps one size on screen at any zoom.
    /// Labels are reused; those of nodes out of view are hidden.
    /// </summary>
    sealed class MissionCanvasLabels
    {
        const string NodeLabelClass = "canvas__node-label";
        const string DistanceLabelClass = "canvas__distance-label";

        readonly VisualElement layer;
        readonly List<Label> nodeLabels = new List<Label>();
        readonly List<Label> distanceLabels = new List<Label>();

        /// <summary>Adds the layer the labels live in to the canvas, over its whole area.</summary>
        public MissionCanvasLabels(VisualElement canvas)
        {
            layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute;
            layer.style.left = 0f;
            layer.style.top = 0f;
            layer.style.right = 0f;
            layer.style.bottom = 0f;
            canvas.Add(layer);
        }

        /// <summary>Puts every label where its node or route is now. A null session hides them all.</summary>
        public void Layout(Rect box, MapViewport viewport, MissionEditorSession session)
        {
            if (session == null || float.IsNaN(box.width) || box.width < 1f)
            {
                Trim(nodeLabels, 0);
                Trim(distanceLabels, 0);
                return;
            }

            MissionDefinition mission = session.Document.Mission;
            int used = 0;
            foreach (MissionNode node in mission.Nodes)
            {
                Vector2 center = viewport.MapToCanvas(node.X, node.Y);
                if (center.x < -80f || center.x > box.width + 80f || center.y < -40f || center.y > box.height + 20f) continue;

                Label label = LabelAt(nodeLabels, used++, NodeLabelClass);
                label.text = node.Id;
                label.style.left = center.x;
                label.style.top = center.y + MissionCanvasPainter.RadiusOf(node, viewport) + 4f;
                label.style.translate = new Translate(Length.Percent(-50f), 0f);
                label.EnableInClassList(NodeLabelClass + "--selected", session.Selection.IsNode(node.Id));
            }

            Trim(nodeLabels, used);

            used = 0;
            if (session.ShowDistances)
            {
                foreach (MissionEdge edge in mission.Edges)
                {
                    MissionNode source = mission.FindNode(edge.Source);
                    MissionNode target = mission.FindNode(edge.Target);
                    if (source == null || target == null) continue;

                    Vector2 middle = (viewport.MapToCanvas(source.X, source.Y) + viewport.MapToCanvas(target.X, target.Y)) * 0.5f;
                    if (middle.x < -60f || middle.x > box.width + 60f || middle.y < -20f || middle.y > box.height + 20f) continue;

                    Label label = LabelAt(distanceLabels, used++, DistanceLabelClass);
                    label.text = MissionFields.FormatDistance(mission.GameDistance(edge));
                    label.style.left = middle.x;
                    label.style.top = middle.y;
                    label.style.translate = new Translate(Length.Percent(-50f), Length.Percent(-50f));
                    label.EnableInClassList(DistanceLabelClass + "--selected", session.Selection.IsEdge(edge));
                }
            }

            Trim(distanceLabels, used);
        }

        /// <summary>The text of the labels now showing: node IDs, or route distances.</summary>
        public List<string> Visible(bool distances)
        {
            var texts = new List<string>();
            foreach (Label label in distances ? distanceLabels : nodeLabels)
                if (label.style.display != DisplayStyle.None) texts.Add(label.text);
            return texts;
        }

        Label LabelAt(List<Label> pool, int index, string className)
        {
            if (index < pool.Count)
            {
                pool[index].style.display = DisplayStyle.Flex;
                return pool[index];
            }

            var label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            label.style.position = Position.Absolute;
            layer.Add(label);
            pool.Add(label);
            return label;
        }

        static void Trim(List<Label> pool, int used)
        {
            for (int i = used; i < pool.Count; i++) pool[i].style.display = DisplayStyle.None;
        }
    }
}
