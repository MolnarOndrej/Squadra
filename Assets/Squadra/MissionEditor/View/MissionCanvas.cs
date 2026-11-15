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
    /// distances. It shows the session's mission and turns clicks and drags into the session's
    /// commands; it holds no mission data of its own.
    ///
    /// The element itself reads the pointer: which gesture is under way and what lies under it. The
    /// view it owns, which map point is in the middle and at what zoom, is a <see cref="MapViewport"/>;
    /// <see cref="MissionCanvasPainter"/> draws and <see cref="MissionCanvasLabels"/> places the text.
    /// Panning, zooming and Fit to Map change only the view, never the mission.
    /// </summary>
    // Partial because the UxmlElement source generator adds the other part; the code is all here.
    [UxmlElement]
    public sealed partial class MissionCanvas : VisualElement
    {
        /// <summary>Pixels per map unit at 100%.</summary>
        public const double DefaultZoom = MapViewport.DefaultZoom;

        public const double MinZoom = MapViewport.MinZoom;
        public const double MaxZoom = MapViewport.MaxZoom;

        const double ButtonZoomStep = 1.25;
        const double WheelZoomStep = 1.12;
        const float DragThreshold = 4f;
        const float EdgeReach = 6f;
        const float NodeReach = 4f;

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

        readonly MapViewport viewport;
        readonly MissionCanvasLabels labels;

        MissionEditorSession session;
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

            viewport = new MapViewport(() => contentRect);
            labels = new MissionCanvasLabels(this);

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
        public double Zoom => viewport.Zoom;

        /// <summary>The map position shown in the middle of the canvas.</summary>
        public double ViewCenterX => viewport.CenterX;

        public double ViewCenterY => viewport.CenterY;

        /// <summary>The zoom as the toolbar shows it: 100% is the view a new mission opens with.</summary>
        public string ZoomText => viewport.ZoomText;

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
            labels.Layout(contentRect, viewport, session);
            MarkDirtyRepaint();
        }

        public Vector2 MapToCanvas(double x, double y)
        {
            return viewport.MapToCanvas(x, y);
        }

        public void CanvasToMap(Vector2 point, out double x, out double y)
        {
            viewport.CanvasToMap(point, out x, out y);
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
            if (viewport.ZoomAround(anchor, factor)) OnViewChanged();
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
            viewport.Fit(session.Document.Mission.Nodes);
            OnViewChanged();
        }

        /// <summary>Brings a map position to the middle of the canvas without changing the zoom.</summary>
        public void CenterOn(double x, double y)
        {
            viewport.CenterOn(x, y);
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

        void Draw(MeshGenerationContext context)
        {
            Rect box = contentRect;
            if (session == null || float.IsNaN(box.width) || box.width < 1f || box.height < 1f) return;

            MissionCanvasPainter.Draw(context.painter2D, box, viewport, session,
                pointerInside ? lastPointer : (Vector2?)null);
        }

        /// <summary>The text of the labels now showing, for checks: node IDs, or route distances.</summary>
        public List<string> VisibleLabels(bool distances)
        {
            return labels.Visible(distances);
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

            viewport.CanvasToMap(position, out double x, out double y);
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
                viewport.Pan(delta);
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
                viewport.CanvasToMap(position, out double x, out double y);
                session.Document.MoveNode(draggedNode, viewport.Tidy(x + grabOffsetX), viewport.Tidy(y + grabOffsetY));
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

            viewport.CanvasToMap(pressedAt, out double x, out double y);
            session.ClickCanvas(viewport.Tidy(x), viewport.Tidy(y));
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

        /// <summary>The node drawn at a canvas position, the topmost when several overlap; null for none.</summary>
        public MissionNode NodeAt(Vector2 position)
        {
            if (session == null) return null;

            List<MissionNode> nodes = session.Document.Mission.Nodes;
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                float reach = MissionCanvasPainter.RadiusOf(nodes[i], viewport) + NodeReach;
                if ((viewport.MapToCanvas(nodes[i].X, nodes[i].Y) - position).sqrMagnitude <= reach * reach) return nodes[i];
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

                float distance = DistanceToSegment(position, viewport.MapToCanvas(source.X, source.Y),
                    viewport.MapToCanvas(target.X, target.Y));
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
    }
}
