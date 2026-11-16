using System;
using System.Collections.Generic;
using Squadra.MissionEditing;
using Squadra.Missions;
using UnityEngine;
using UnityEngine.UIElements;

namespace Squadra
{
    /// <summary>
    /// Draws the mission canvas: the grid, routes as straight lines, a disc for each planet, a
    /// crescent for each moon and a rock for each asteroid in its owner's color, the selection and
    /// the connection being made. A node's symbol is as large as its body is on the map, the
    /// <see cref="NodeFootprint"/>; the dark edge, a planet's rim, the marker of a body too small to
    /// see and the selection ring are drawn around that in fixed pixels and are no part of its size.
    /// It reads the session and the viewport and keeps nothing between frames.
    /// </summary>
    static class MissionCanvasPainter
    {
        // The dark edge that sets a symbol off from the grid and the routes: this far outside the body.
        const float EdgeWidth = 2f;

        // A planet shows its rim once there is room for one inside it.
        const double RimInset = 3.5;
        const double SmallestRimmedRadius = 8d;

        // A body drawn smaller than this gets a thin ring around it, so that it can be found.
        const double SmallestPlainRadius = 4d;
        const double MarkerRadius = 7d;

        const double SelectionGap = 6d;
        const float SelectionWidth = 3f;

        // How far outside the canvas a body may be and still have something to draw on it.
        const double DrawMargin = 16d;

        static readonly Color GridColor = new Color(0.53f, 0.57f, 0.69f, 0.07f);
        static readonly Color GridMajorColor = new Color(0.53f, 0.57f, 0.69f, 0.14f);
        static readonly Color AxisColor = new Color(0.53f, 0.57f, 0.69f, 0.32f);
        static readonly Color RouteColor = new Color(0.62f, 0.67f, 0.8f, 0.75f);
        static readonly Color OutlineColor = new Color(0.02f, 0.024f, 0.06f, 1f);

        /// <summary>
        /// How far from a node's middle its drawing reaches, in pixels, the edge and the selection
        /// ring aside: the body's radius, or the marker's for a body too small to see. Labels stand
        /// clear of it.
        /// </summary>
        public static double ReachOf(NodeFootprint body)
        {
            return body.Radius < SmallestPlainRadius ? MarkerRadius : body.Radius;
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

            // In mission order, so that of two overlapping bodies the later one is on top: the one a
            // click picks.
            var outline = new NodeOutline(box);
            foreach (MissionNode node in mission.Nodes)
            {
                NodeFootprint body = viewport.FootprintOf(node, mission.DistanceScale);
                if (!body.Touches(box, DrawMargin)) continue;

                DrawNode(painter, outline, body, node.Type, PlayerColors.Of(mission, node));
                if (selection.IsNode(node.Id) || MissionRules.SameId(session.ConnectionSource, node.Id))
                {
                    double ring = ReachOf(body) + SelectionGap;
                    Stroke(painter, outline.Circle(body.X, body.Y, ring), Palette.Accent, SelectionWidth);
                }
            }
        }

        // A planet is a full disc with a rim, a moon a crescent and an asteroid a rock with corners,
        // so the three can be told apart at one size. Each is exactly as large as the body.
        static void DrawNode(Painter2D painter, NodeOutline outline, NodeFootprint body, MissionNodeType type, Color color)
        {
            // The edge is a line along the outline whose inner half the color then covers. A body
            // thinner than the edge gets a thinner one, or the edge would be all there is to see.
            List<Vector2> symbol = outline.Symbol(type, body.X, body.Y, body.Radius);
            Stroke(painter, symbol, OutlineColor, (float)Math.Min(2d * EdgeWidth, 2d * body.Radius));
            Fill(painter, symbol, color);

            if (type == MissionNodeType.Planet && body.Radius >= SmallestRimmedRadius)
            {
                Color rim = Color.Lerp(color, Color.white, 0.45f);
                Stroke(painter, outline.Circle(body.X, body.Y, body.Radius - RimInset), rim, 1.5f);
            }

            if (body.Radius < SmallestPlainRadius)
            {
                Color marker = color;
                marker.a = 0.8f;
                Stroke(painter, outline.Circle(body.X, body.Y, MarkerRadius), marker, 1f);
            }
        }

        static void Fill(Painter2D painter, List<Vector2> outline, Color color)
        {
            if (!Trace(painter, outline)) return;

            painter.fillColor = color;
            painter.Fill();
        }

        static void Stroke(Painter2D painter, List<Vector2> outline, Color color, float width)
        {
            if (!Trace(painter, outline)) return;

            painter.strokeColor = color;
            painter.lineWidth = width;
            painter.Stroke();
        }

        // Makes a closed outline the painter's path. False for an outline with nothing to draw.
        static bool Trace(Painter2D painter, List<Vector2> outline)
        {
            if (outline.Count < 3) return false;

            painter.BeginPath();
            painter.MoveTo(outline[0]);
            for (int i = 1; i < outline.Count; i++) painter.LineTo(outline[i]);
            painter.ClosePath();
            return true;
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
