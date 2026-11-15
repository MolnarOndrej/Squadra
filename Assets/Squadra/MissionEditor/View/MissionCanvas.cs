using System;
using System.Collections.Generic;
using Squadra.MissionEditing;
using Squadra.Missions;
using UnityEngine;
using UnityEngine.UIElements;

namespace Squadra
{
    /// <summary>
    /// The mission editor's schematic graph: a grid, routes as straight lines, a disc for each planet
    /// and a crescent for each moon in its owner's color, node IDs and, when asked for, route
    /// distances. It draws the session's mission and turns clicks and drags into the session's
    /// commands; it holds no mission data of its own.
    ///
    /// What it does own is the view: which map point is in the middle and how many pixels a map unit
    /// takes. Panning, zooming and Fit to Map change only that, never the mission. Map X runs to the
    /// right and map Y up, so the view flips Y on the way to the screen.
    /// </summary>
    [UxmlElement]
    public sealed partial class MissionCanvas : VisualElement
    {
        /// <summary>Pixels per map unit at 100%.</summary>
        public const double DefaultZoom = 40d;

        public const double MinZoom = 1e-7;
        public const double MaxZoom = 1e5;

        const double ButtonZoomStep = 1.25;
        const double WheelZoomStep = 1.12;
        const double MaxFitZoom = DefaultZoom * 4d;
        const float FitMargin = 90f;
        const float DragThreshold = 4f;
        const float EdgeReach = 6f;
        const float NodeReach = 4f;
        const float PlanetRadius = 15f;
        const float MoonRadius = 10f;

        const string NodeLabelClass = "canvas__node-label";
        const string DistanceLabelClass = "canvas__distance-label";

        static readonly Color GridColor = new Color(0.53f, 0.57f, 0.69f, 0.07f);
        static readonly Color GridMajorColor = new Color(0.53f, 0.57f, 0.69f, 0.14f);
        static readonly Color AxisColor = new Color(0.53f, 0.57f, 0.69f, 0.32f);
        static readonly Color RouteColor = new Color(0.62f, 0.67f, 0.8f, 0.75f);
        static readonly Color OutlineColor = new Color(0.02f, 0.024f, 0.06f, 1f);

        enum Gesture
        {
            None,

            /// <summary>The left button is down on empty space: a click if it comes up here, a pan if it moves.</summary>
            BackgroundPress,

            /// <summary>The left button is down on the selected node: a drag moves it.</summary>
            NodePress,
            MovingNode,
            Panning,
        }

        readonly VisualElement labelLayer;
        readonly List<Label> nodeLabels = new List<Label>();
        readonly List<Label> distanceLabels = new List<Label>();

        MissionEditorSession session;
        double centerX;
        double centerY;
        double zoom = DefaultZoom;
        bool fitWhenLaidOut = true;

        Gesture gesture;
        int gesturePointer = -1;
        Vector2 pressedAt;
        Vector2 lastPointer;
        bool pointerInside;
        string draggedNode;
        double grabOffsetX;
        double grabOffsetY;

        public MissionCanvas()
        {
            focusable = true;
            style.overflow = Overflow.Hidden;

            labelLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            labelLayer.style.position = Position.Absolute;
            labelLayer.style.left = 0f;
            labelLayer.style.top = 0f;
            labelLayer.style.right = 0f;
            labelLayer.style.bottom = 0f;
            Add(labelLayer);

            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => OnLaidOut());
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerLeaveEvent>(_ => SetPointerInside(false));
            RegisterCallback<PointerCaptureOutEvent>(_ => EndGesture());
            RegisterCallback<WheelEvent>(OnWheel);
        }

        // ------------------------------------------------------------------ the view

        /// <summary>Pixels per map unit.</summary>
        public double Zoom => zoom;

        /// <summary>The map position shown in the middle of the canvas.</summary>
        public double ViewCenterX => centerX;

        public double ViewCenterY => centerY;

        /// <summary>The zoom as the toolbar shows it: 100% is the view a new mission opens with.</summary>
        public string ZoomText
        {
            get
            {
                double percent = zoom / DefaultZoom * 100d;
                string format = percent >= 10d ? "0" : percent >= 1d ? "0.#" : "0.#######";
                return percent.ToString(format, System.Globalization.CultureInfo.InvariantCulture) + "%";
            }
        }

        /// <summary>Raised when the view moved or zoomed.</summary>
        public event Action ViewChanged;

        public void Bind(MissionEditorSession editorSession)
        {
            session = editorSession;
            fitWhenLaidOut = true;
            Refresh();
        }

        /// <summary>Redraws the mission as it is now. Call after anything the session holds has changed.</summary>
        public void Refresh()
        {
            LayoutLabels();
            MarkDirtyRepaint();
        }

        public Vector2 MapToCanvas(double x, double y)
        {
            Rect box = contentRect;
            return new Vector2(
                (float)(box.width * 0.5 + (x - centerX) * zoom),
                (float)(box.height * 0.5 - (y - centerY) * zoom));
        }

        public void CanvasToMap(Vector2 point, out double x, out double y)
        {
            Rect box = contentRect;
            x = centerX + (point.x - box.width * 0.5) / zoom;
            y = centerY - (point.y - box.height * 0.5) / zoom;
        }

        // A position picked with the pointer is only as exact as a pixel. It is stored to a hundredth
        // of a pixel in round decimals, far finer than can be seen, so the mission gets -3.475 and not
        // the pointer's arithmetic noise. Typed coordinates are never touched.
        double Tidy(double value)
        {
            double digits = Math.Ceiling(-Math.Log10(0.01 / zoom));
            if (digits >= 0d) return Math.Round(value, (int)Math.Min(15d, digits));

            double step = Math.Pow(10d, -digits);
            return Math.Round(value / step) * step;
        }

        /// <summary>Zooms in one step, keeping the middle of the canvas where it is.</summary>
        public void ZoomIn()
        {
            ZoomAround(Middle, ButtonZoomStep);
        }

        public void ZoomOut()
        {
            ZoomAround(Middle, 1d / ButtonZoomStep);
        }

        Vector2 Middle => new Vector2(contentRect.width * 0.5f, contentRect.height * 0.5f);

        /// <summary>Changes the zoom by a factor while the map point under <paramref name="anchor"/> stays put.</summary>
        public void ZoomAround(Vector2 anchor, double factor)
        {
            double target = Math.Min(MaxZoom, Math.Max(MinZoom, zoom * factor));
            if (target == zoom) return;

            CanvasToMap(anchor, out double x, out double y);
            zoom = target;
            Rect box = contentRect;
            centerX = x - (anchor.x - box.width * 0.5) / zoom;
            centerY = y + (anchor.y - box.height * 0.5) / zoom;
            OnViewChanged();
        }

        /// <summary>
        /// Frames every node with a margin around them. A mission without nodes gets the view a new
        /// mission opens with: the origin in the middle at 100%.
        /// </summary>
        public void FitToMap()
        {
            Rect box = contentRect;
            if (session == null || float.IsNaN(box.width) || box.width < 1f || box.height < 1f)
            {
                fitWhenLaidOut = true;
                return;
            }

            fitWhenLaidOut = false;
            List<MissionNode> nodes = session.Document.Mission.Nodes;
            if (nodes.Count == 0)
            {
                centerX = 0d;
                centerY = 0d;
                zoom = DefaultZoom;
                OnViewChanged();
                return;
            }

            double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
            foreach (MissionNode node in nodes)
            {
                minX = Math.Min(minX, node.X);
                maxX = Math.Max(maxX, node.X);
                minY = Math.Min(minY, node.Y);
                maxY = Math.Max(maxY, node.Y);
            }

            centerX = (minX + maxX) * 0.5;
            centerY = (minY + maxY) * 0.5;

            double roomX = Math.Max(40d, box.width - 2d * FitMargin);
            double roomY = Math.Max(40d, box.height - 2d * FitMargin);
            double fit = MaxFitZoom;
            if (maxX > minX) fit = Math.Min(fit, roomX / (maxX - minX));
            if (maxY > minY) fit = Math.Min(fit, roomY / (maxY - minY));
            zoom = Math.Min(MaxZoom, Math.Max(MinZoom, fit));
            OnViewChanged();
        }

        /// <summary>Brings a map position to the middle of the canvas without changing the zoom.</summary>
        public void CenterOn(double x, double y)
        {
            centerX = x;
            centerY = y;
            OnViewChanged();
        }

        void OnLaidOut()
        {
            if (fitWhenLaidOut) FitToMap();
            else Refresh();
        }

        void OnViewChanged()
        {
            Refresh();
            ViewChanged?.Invoke();
        }

        // ------------------------------------------------------------------ pointer

        void OnPointerDown(PointerDownEvent pressed)
        {
            if (gesture != Gesture.None) return;

            Focus();
            if (PointerPress(pressed.localPosition, pressed.button))
            {
                gesturePointer = pressed.pointerId;
                this.CapturePointer(gesturePointer);
            }

            pressed.StopPropagation();
        }

        void OnPointerMove(PointerMoveEvent moved)
        {
            SetPointerInside(true);
            PointerDrag(moved.localPosition);
        }

        void OnPointerUp(PointerUpEvent released)
        {
            if (gesture == Gesture.None || released.pointerId != gesturePointer) return;

            PointerRelease(released.localPosition);
            released.StopPropagation();
        }

        void OnWheel(WheelEvent wheel)
        {
            Scroll(wheel.localMousePosition, wheel.delta.y);
            wheel.StopPropagation();
        }

        /// <summary>
        /// A mouse button going down at a canvas position: 0 left, 1 right, 2 middle. The left button
        /// selects and may begin a drag; the others pan. Returns true when a gesture began that the
        /// matching <see cref="PointerRelease"/> ends.
        /// </summary>
        public bool PointerPress(Vector2 position, int button)
        {
            if (session == null || session.Busy || gesture != Gesture.None) return false;

            pressedAt = position;
            lastPointer = position;
            if (button != 0)
            {
                gesture = Gesture.Panning;
                return true;
            }

            // With Add Node armed the press is always for the canvas, whatever lies under it.
            MissionNode node = session.Tool == EditorTool.AddNode ? null : NodeAt(position);
            if (node == null)
            {
                MissionEdge edge = session.Tool == EditorTool.Select ? EdgeAt(position) : null;
                if (edge != null)
                {
                    session.ClickEdge(edge.Source, edge.Target);
                    return false;
                }

                gesture = Gesture.BackgroundPress;
                return true;
            }

            session.ClickNode(node.Id);

            // Only a node that ended up selected can be dragged: not one picked as the end of a
            // connection, and not while the session is asking the author something.
            if (session.Tool != EditorTool.Select || session.Busy || !session.Selection.IsNode(node.Id)) return false;

            CanvasToMap(position, out double x, out double y);
            draggedNode = node.Id;
            grabOffsetX = node.X - x;
            grabOffsetY = node.Y - y;
            gesture = Gesture.NodePress;
            return true;
        }

        /// <summary>The pointer moving to a canvas position, with or without a button held.</summary>
        public void PointerDrag(Vector2 position)
        {
            Vector2 delta = position - lastPointer;
            lastPointer = position;

            bool moved = (position - pressedAt).sqrMagnitude > DragThreshold * DragThreshold;
            if (gesture == Gesture.BackgroundPress && moved)
            {
                gesture = Gesture.Panning;
                delta = position - pressedAt;
            }
            else if (gesture == Gesture.NodePress && moved)
            {
                gesture = Gesture.MovingNode;
            }

            if (gesture == Gesture.Panning)
            {
                centerX -= delta.x / zoom;
                centerY += delta.y / zoom;
                OnViewChanged();
            }
            else if (gesture == Gesture.MovingNode)
            {
                if (session.Busy || session.Document.Mission.FindNode(draggedNode) == null)
                {
                    EndGesture();
                    return;
                }

                // A position the mission cannot hold is refused by the document; the node stays put.
                CanvasToMap(position, out double x, out double y);
                session.Document.MoveNode(draggedNode, Tidy(x + grabOffsetX), Tidy(y + grabOffsetY));
            }
            else if (session != null && session.Tool == EditorTool.AddConnection && session.ConnectionSource != null)
            {
                MarkDirtyRepaint(); // the line to the pointer follows it
            }
        }

        /// <summary>The button of the current gesture coming up. A press that never moved is a click.</summary>
        public void PointerRelease(Vector2 position)
        {
            Gesture finished = gesture;
            EndGesture();
            if (finished != Gesture.BackgroundPress || session == null) return;

            CanvasToMap(pressedAt, out double x, out double y);
            session.ClickCanvas(Tidy(x), Tidy(y));
        }

        /// <summary>The mouse wheel at a canvas position: up zooms in, down zooms out, around the pointer.</summary>
        public void Scroll(Vector2 position, float wheelDelta)
        {
            if (session == null || session.Busy || wheelDelta == 0f) return;

            double steps = Math.Max(-8d, Math.Min(8d, -wheelDelta));
            ZoomAround(position, Math.Pow(WheelZoomStep, steps));
        }

        /// <summary>Drops a drag or pan in progress, as when a dialog opens over the canvas.</summary>
        public void EndGesture()
        {
            int pointer = gesturePointer;
            gesture = Gesture.None;
            gesturePointer = -1;
            draggedNode = null;
            if (pointer >= 0 && this.HasPointerCapture(pointer)) this.ReleasePointer(pointer);
        }

        void SetPointerInside(bool inside)
        {
            if (pointerInside == inside) return;

            pointerInside = inside;
            MarkDirtyRepaint();
        }

        // ------------------------------------------------------------------ what is where

        float SymbolScale => Mathf.Clamp(Mathf.Sqrt((float)(zoom / DefaultZoom)), 0.65f, 1.5f);

        float RadiusOf(MissionNode node)
        {
            return (node.Type == MissionNodeType.Moon ? MoonRadius : PlanetRadius) * SymbolScale;
        }

        /// <summary>The node drawn at a canvas position, the topmost when several overlap; null for none.</summary>
        public MissionNode NodeAt(Vector2 position)
        {
            if (session == null) return null;

            List<MissionNode> nodes = session.Document.Mission.Nodes;
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                float reach = RadiusOf(nodes[i]) + NodeReach;
                if ((MapToCanvas(nodes[i].X, nodes[i].Y) - position).sqrMagnitude <= reach * reach) return nodes[i];
            }

            return null;
        }

        /// <summary>The route drawn at a canvas position; null for none.</summary>
        public MissionEdge EdgeAt(Vector2 position)
        {
            if (session == null) return null;

            MissionDefinition mission = session.Document.Mission;
            MissionEdge nearest = null;
            float nearestDistance = EdgeReach;
            foreach (MissionEdge edge in mission.Edges)
            {
                MissionNode source = mission.FindNode(edge.Source);
                MissionNode target = mission.FindNode(edge.Target);
                if (source == null || target == null) continue;

                float distance = DistanceToSegment(position, MapToCanvas(source.X, source.Y), MapToCanvas(target.X, target.Y));
                if (distance <= nearestDistance)
                {
                    nearest = edge;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 along = b - a;
            float length = along.sqrMagnitude;
            float t = length > 0f ? Mathf.Clamp01(Vector2.Dot(point - a, along) / length) : 0f;
            return (point - (a + along * t)).magnitude;
        }

        // ------------------------------------------------------------------ drawing

        void Draw(MeshGenerationContext context)
        {
            Rect box = contentRect;
            if (session == null || float.IsNaN(box.width) || box.width < 1f || box.height < 1f) return;

            Painter2D painter = context.painter2D;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;
            DrawGrid(painter, box);

            MissionDefinition mission = session.Document.Mission;
            Selection selection = session.Selection;
            foreach (MissionEdge edge in mission.Edges)
            {
                MissionNode source = mission.FindNode(edge.Source);
                MissionNode target = mission.FindNode(edge.Target);
                if (source == null || target == null) continue;

                bool selected = selection.IsEdge(edge);
                Line(painter, MapToCanvas(source.X, source.Y), MapToCanvas(target.X, target.Y),
                    selected ? Palette.Accent : RouteColor, selected ? 4f : 2f);
            }

            // The connection being made, from its first node to the pointer.
            MissionNode from = session.ConnectionSource != null ? mission.FindNode(session.ConnectionSource) : null;
            if (from != null && pointerInside)
            {
                Color pending = Palette.Accent;
                pending.a = 0.7f;
                Line(painter, MapToCanvas(from.X, from.Y), lastPointer, pending, 2f);
            }

            foreach (MissionNode node in mission.Nodes)
            {
                Vector2 center = MapToCanvas(node.X, node.Y);
                float radius = RadiusOf(node);
                if (center.x < -radius - 12f || center.x > box.width + radius + 12f
                    || center.y < -radius - 12f || center.y > box.height + radius + 12f)
                    continue;

                DrawNode(painter, center, radius, node.Type, ColorOf(mission, node));
                if (selection.IsNode(node.Id) || MissionRules.SameId(session.ConnectionSource, node.Id))
                    Ring(painter, center, radius + 6f, Palette.Accent, 3f);
            }
        }

        /// <summary>A node's color: its owner's, or gray for a neutral node.</summary>
        public static Color ColorOf(MissionDefinition mission, MissionNode node)
        {
            MissionPlayer owner = node.Owner != null ? mission.FindPlayer(node.Owner) : null;
            return owner != null ? ColorOf(owner.Color) : Palette.Neutral;
        }

        public static Color ColorOf(PlayerColor color)
        {
            switch (color)
            {
                case PlayerColor.Blue: return Palette.Blue;
                case PlayerColor.Red: return Palette.Red;
                case PlayerColor.Green: return Palette.Green;
                case PlayerColor.Yellow: return Palette.Yellow;
                case PlayerColor.Purple: return Palette.Purple;
                case PlayerColor.Orange: return Palette.Orange;
                default: return Palette.Neutral;
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

        // Grid lines a round number of map units apart, never closer than a comfortable gap on screen.
        void DrawGrid(Painter2D painter, Rect box)
        {
            double step = GridStep();
            CanvasToMap(new Vector2(0f, box.height), out double left, out double bottom);
            CanvasToMap(new Vector2(box.width, 0f), out double right, out double top);

            double firstX = Math.Floor(left / step);
            int columns = (int)Math.Min(400d, Math.Ceiling(right / step) - firstX + 1d);
            for (int i = 0; i < columns; i++)
            {
                double index = firstX + i;
                float x = MapToCanvas(index * step, 0d).x;
                Line(painter, new Vector2(x, 0f), new Vector2(x, box.height), GridLineColor(index), 1f);
            }

            double firstY = Math.Floor(bottom / step);
            int rows = (int)Math.Min(400d, Math.Ceiling(top / step) - firstY + 1d);
            for (int i = 0; i < rows; i++)
            {
                double index = firstY + i;
                float y = MapToCanvas(0d, index * step).y;
                Line(painter, new Vector2(0f, y), new Vector2(box.width, y), GridLineColor(index), 1f);
            }
        }

        static Color GridLineColor(double index)
        {
            if (index == 0d) return AxisColor;
            return Math.IEEERemainder(index, 5d) == 0d ? GridMajorColor : GridColor;
        }

        double GridStep()
        {
            const double minimumGap = 28d;
            double wanted = minimumGap / zoom;
            double power = Math.Pow(10d, Math.Floor(Math.Log10(wanted)));
            foreach (double multiple in new[] { 1d, 2d, 5d, 10d })
                if (power * multiple >= wanted) return power * multiple;
            return power * 10d;
        }

        // ------------------------------------------------------------------ labels

        // IDs and distances are ordinary labels laid over the drawing, so their text keeps one size on
        // screen at any zoom. Labels are reused; those of nodes out of view are hidden.
        void LayoutLabels()
        {
            Rect box = contentRect;
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
                Vector2 center = MapToCanvas(node.X, node.Y);
                if (center.x < -80f || center.x > box.width + 80f || center.y < -40f || center.y > box.height + 20f) continue;

                Label label = LabelAt(nodeLabels, used++, NodeLabelClass);
                label.text = node.Id;
                label.style.left = center.x;
                label.style.top = center.y + RadiusOf(node) + 4f;
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

                    Vector2 middle = (MapToCanvas(source.X, source.Y) + MapToCanvas(target.X, target.Y)) * 0.5f;
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
            labelLayer.Add(label);
            pool.Add(label);
            return label;
        }

        static void Trim(List<Label> pool, int used)
        {
            for (int i = used; i < pool.Count; i++) pool[i].style.display = DisplayStyle.None;
        }

        /// <summary>The text of the labels now showing, for checks: node IDs first, then distances.</summary>
        public List<string> VisibleLabels(bool distances)
        {
            var texts = new List<string>();
            foreach (Label label in distances ? distanceLabels : nodeLabels)
                if (label.style.display != DisplayStyle.None) texts.Add(label.text);
            return texts;
        }
    }
}
