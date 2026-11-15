using System;
using Squadra.MissionEditing;
using Squadra.Missions;
using UnityEngine;
using UnityEngine.UIElements;

namespace Squadra
{
    /// <summary>
    /// Draws the mission canvas: the grid, routes as straight lines, a disc for each planet and a
    /// crescent for each moon in its owner's color, the selection and the connection being made.
    /// It reads the session and the viewport and keeps nothing between frames.
    /// </summary>
    static class MissionCanvasPainter
    {
        const float PlanetRadius = 15f;
        const float MoonRadius = 10f;

        static readonly Color GridColor = new Color(0.53f, 0.57f, 0.69f, 0.07f);
        static readonly Color GridMajorColor = new Color(0.53f, 0.57f, 0.69f, 0.14f);
        static readonly Color AxisColor = new Color(0.53f, 0.57f, 0.69f, 0.32f);
        static readonly Color RouteColor = new Color(0.62f, 0.67f, 0.8f, 0.75f);
        static readonly Color OutlineColor = new Color(0.02f, 0.024f, 0.06f, 1f);

        /// <summary>The radius a node's symbol is drawn with, in pixels.</summary>
        public static float RadiusOf(MissionNode node, MapViewport viewport)
        {
            return (node.Type == MissionNodeType.Moon ? MoonRadius : PlanetRadius) * viewport.SymbolScale;
        }

        /// <param name="pointer">
        /// Where the pointer is on the canvas, or null while it is outside: the line of a connection
        /// being made ends there.
        /// </param>
        public static void Draw(Painter2D painter, Rect box, MapViewport viewport, MissionEditorSession session,
            Vector2? pointer)
        {
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;
            DrawGrid(painter, box, viewport);

            MissionDefinition mission = session.Document.Mission;
            Selection selection = session.Selection;
            foreach (MissionEdge edge in mission.Edges)
            {
                MissionNode source = mission.FindNode(edge.Source);
                MissionNode target = mission.FindNode(edge.Target);
                if (source == null || target == null) continue;

                bool selected = selection.IsEdge(edge);
                Line(painter, viewport.MapToCanvas(source.X, source.Y), viewport.MapToCanvas(target.X, target.Y),
                    selected ? Palette.Accent : RouteColor, selected ? 4f : 2f);
            }

            // The connection being made, from its first node to the pointer.
            MissionNode from = session.ConnectionSource != null ? mission.FindNode(session.ConnectionSource) : null;
            if (from != null && pointer.HasValue)
            {
                Color pending = Palette.Accent;
                pending.a = 0.7f;
                Line(painter, viewport.MapToCanvas(from.X, from.Y), pointer.Value, pending, 2f);
            }

            foreach (MissionNode node in mission.Nodes)
            {
                Vector2 center = viewport.MapToCanvas(node.X, node.Y);
                float radius = RadiusOf(node, viewport);
                if (center.x < -radius - 12f || center.x > box.width + radius + 12f
                    || center.y < -radius - 12f || center.y > box.height + radius + 12f)
                    continue;

                DrawNode(painter, center, radius, node.Type, PlayerColors.Of(mission, node));
                if (selection.IsNode(node.Id) || MissionRules.SameId(session.ConnectionSource, node.Id))
                    Ring(painter, center, radius + 6f, Palette.Accent, 3f);
            }
        }

        // A planet is a full disc. A moon is smaller and has a bite taken out of it, a crescent, so
        // the two can be told apart without relying on size alone.
        static void DrawNode(Painter2D painter, Vector2 center, float radius, MissionNodeType type, Color color)
        {
            Disc(painter, center, radius + 2f, OutlineColor);
            Disc(painter, center, radius, color);
            if (type == MissionNodeType.Moon)
            {
                Disc(painter, center + new Vector2(radius * 0.42f, -radius * 0.3f), radius * 0.72f, OutlineColor);
            }
            else
            {
                Color rim = Color.Lerp(color, Color.white, 0.45f);
                Ring(painter, center, radius - 3.5f, rim, 1.5f);
            }
        }

        static void Disc(Painter2D painter, Vector2 center, float radius, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.Arc(center, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            painter.ClosePath();
            painter.Fill();
        }

        static void Ring(Painter2D painter, Vector2 center, float radius, Color color, float width)
        {
            painter.strokeColor = color;
            painter.lineWidth = width;
            painter.BeginPath();
            painter.Arc(center, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            painter.ClosePath();
            painter.Stroke();
        }

        static void Line(Painter2D painter, Vector2 a, Vector2 b, Color color, float width)
        {
            painter.strokeColor = color;
            painter.lineWidth = width;
            painter.BeginPath();
            painter.MoveTo(a);
            painter.LineTo(b);
            painter.Stroke();
        }

        static void DrawGrid(Painter2D painter, Rect box, MapViewport viewport)
        {
            double step = viewport.GridStep();
            viewport.CanvasToMap(new Vector2(0f, box.height), out double left, out double bottom);
            viewport.CanvasToMap(new Vector2(box.width, 0f), out double right, out double top);

            double firstX = Math.Floor(left / step);
            int columns = (int)Math.Min(400d, Math.Ceiling(right / step) - firstX + 1d);
            for (int i = 0; i < columns; i++)
            {
                double index = firstX + i;
                float x = viewport.MapToCanvas(index * step, 0d).x;
                Line(painter, new Vector2(x, 0f), new Vector2(x, box.height), GridLineColor(index), 1f);
            }

            double firstY = Math.Floor(bottom / step);
            int rows = (int)Math.Min(400d, Math.Ceiling(top / step) - firstY + 1d);
            for (int i = 0; i < rows; i++)
            {
                double index = firstY + i;
                float y = viewport.MapToCanvas(0d, index * step).y;
                Line(painter, new Vector2(0f, y), new Vector2(box.width, y), GridLineColor(index), 1f);
            }
        }

        static Color GridLineColor(double index)
        {
            if (index == 0d) return AxisColor;
            return Math.IEEERemainder(index, 5d) == 0d ? GridMajorColor : GridColor;
        }
    }
}
