using UnityEngine;
using UnityEngine.UIElements;

namespace Squadra
{
    /// <summary>
    /// A small icon on the sandbox's controls, drawn as shapes in the element's text color, so a USS class
    /// can light it up or fade it. It is laid out on a 24 by 24 grid scaled to the element's height; the
    /// speed arrows grow sideways, one arrow per step.
    /// </summary>
    public sealed class SandboxIcon : VisualElement
    {
        const float Grid = 24f;
        const float ArrowWidth = 9f;

        SandboxIconShape shape;
        int arrows = 1;

        // Where the grid's origin is on the element and how large one grid unit is, for this drawing.
        Vector2 origin;
        float unit;

        public SandboxIcon(SandboxIconShape shape)
        {
            this.shape = shape;
            pickingMode = PickingMode.Ignore;
            AddToClassList("sandbox-icon");
            generateVisualContent += Draw;
        }

        public SandboxIconShape Shape
        {
            get => shape;
            set
            {
                if (shape == value) return;
                shape = value;
                MarkDirtyRepaint();
            }
        }

        /// <summary>How many arrows <see cref="SandboxIconShape.Speed"/> shows, from 1.</summary>
        public int Arrows
        {
            get => arrows;
            set
            {
                value = Mathf.Max(1, value);
                if (arrows == value) return;
                arrows = value;
                MarkDirtyRepaint();
            }
        }

        void Draw(MeshGenerationContext context)
        {
            Rect box = contentRect;
            if (box.width <= 0f || box.height <= 0f) return;

            float width = shape == SandboxIconShape.Speed ? arrows * ArrowWidth + 2f : Grid;
            unit = Mathf.Min(box.height / Grid, box.width / width);
            origin = box.center - new Vector2(width, Grid) * (0.5f * unit);

            Painter2D painter = context.painter2D;
            Color color = resolvedStyle.color;
            painter.fillColor = color;
            painter.strokeColor = color;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;

            switch (shape)
            {
                case SandboxIconShape.Hand: DrawHand(painter); break;
                case SandboxIconShape.Pause:
                    Bar(painter, 6.5f, 5f, 4f, 14f, 1.2f);
                    Bar(painter, 13.5f, 5f, 4f, 14f, 1.2f);
                    break;
                case SandboxIconShape.Play: Triangle(painter, P(8f, 5f), P(19.5f, 12f), P(8f, 19f)); break;
                case SandboxIconShape.Speed:
                    for (int i = 0; i < arrows; i++)
                    {
                        float x = 1f + i * ArrowWidth;
                        Triangle(painter, P(x, 6f), P(x + ArrowWidth - 0.5f, 12f), P(x, 18f));
                    }
                    break;
                case SandboxIconShape.Reset: DrawReset(painter); break;
                case SandboxIconShape.Collapse: Chevron(painter, 9.5f, 15.5f); break;
                case SandboxIconShape.Expand: Chevron(painter, 14.5f, 8.5f); break;
            }
        }

        // A hand pointing up, as the pointer over a link: the index finger raised, the other three folded
        // on the palm and the thumb to the side.
        void DrawHand(Painter2D painter)
        {
            Line(painter, 3.6f, 10f, 4f, 10f, 13.5f);
            Line(painter, 3.3f, 13.5f, 9.8f, 13.5f, 14.5f);
            Line(painter, 3.3f, 16.8f, 10.8f, 16.8f, 15f);
            Line(painter, 3.3f, 20f, 12.2f, 20f, 15.5f);
            Line(painter, 3.3f, 5.4f, 12.4f, 8.8f, 16.6f);
            Bar(painter, 8.2f, 13f, 13.45f, 8.5f, 3.4f);
        }

        // Most of a circle, with an arrowhead where it ends.
        void DrawReset(Painter2D painter)
        {
            const float radius = 7f;
            const float start = 40f;
            const float end = 320f;
            var center = new Vector2(12f, 12.5f);

            painter.lineWidth = 2.6f * unit;
            painter.BeginPath();
            painter.Arc(P(center.x, center.y), radius * unit, Angle.Degrees(start), Angle.Degrees(end));
            painter.Stroke();

            float angle = end * Mathf.Deg2Rad;
            Vector2 tip = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            var along = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));
            var across = new Vector2(-along.y, along.x);
            Vector2 point = tip + along * 3.2f;
            Vector2 left = tip - along * 0.8f + across * 3.4f;
            Vector2 right = tip - along * 0.8f - across * 3.4f;
            Triangle(painter, P(point.x, point.y), P(left.x, left.y), P(right.x, right.y));
        }

        void Chevron(Painter2D painter, float fromX, float toX)
        {
            painter.lineWidth = 2.8f * unit;
            painter.BeginPath();
            painter.MoveTo(P(fromX, 5f));
            painter.LineTo(P(toX, 12f));
            painter.LineTo(P(fromX, 19f));
            painter.Stroke();
        }

        void Line(Painter2D painter, float width, float x0, float y0, float x1, float y1)
        {
            painter.lineWidth = width * unit;
            painter.BeginPath();
            painter.MoveTo(P(x0, y0));
            painter.LineTo(P(x1, y1));
            painter.Stroke();
        }

        void Bar(Painter2D painter, float x, float y, float width, float height, float radius)
        {
            painter.BeginPath();
            painter.MoveTo(P(x + radius, y));
            painter.ArcTo(P(x + width, y), P(x + width, y + height), radius * unit);
            painter.ArcTo(P(x + width, y + height), P(x, y + height), radius * unit);
            painter.ArcTo(P(x, y + height), P(x, y), radius * unit);
            painter.ArcTo(P(x, y), P(x + width, y), radius * unit);
            painter.ClosePath();
            painter.Fill();
        }

        static void Triangle(Painter2D painter, Vector2 a, Vector2 b, Vector2 c)
        {
            painter.BeginPath();
            painter.MoveTo(a);
            painter.LineTo(b);
            painter.LineTo(c);
            painter.ClosePath();
            painter.Fill();
        }

        Vector2 P(float x, float y)
        {
            return origin + new Vector2(x, y) * unit;
        }
    }
}
