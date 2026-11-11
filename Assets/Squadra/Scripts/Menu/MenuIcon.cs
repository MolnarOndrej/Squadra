using UnityEngine;
using UnityEngine.UIElements;

namespace Squadra
{
    public enum MenuIconKind
    {
        Lock,
        Check,
        Previous,
        Next,
    }

    /// <summary>
    /// A small vector icon for the menu, drawn to fit the element's box. Its color comes from the
    /// <c>--icon-color</c> style variable, so a parent's state class can recolor it.
    /// </summary>
    [UxmlElement]
    public sealed partial class MenuIcon : VisualElement
    {
        static readonly CustomStyleProperty<Color> ColorProperty = new CustomStyleProperty<Color>("--icon-color");

        MenuIconKind kind;
        Color color = Color.white;

        [UxmlAttribute]
        public MenuIconKind Kind
        {
            get => kind;
            set
            {
                kind = value;
                MarkDirtyRepaint();
            }
        }

        public MenuIcon()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(OnStyleResolved);
        }

        void OnStyleResolved(CustomStyleResolvedEvent resolved)
        {
            if (!resolved.customStyle.TryGetValue(ColorProperty, out Color value) || value == color) return;

            color = value;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            Rect box = contentRect;
            float size = Mathf.Min(box.width, box.height);
            if (size < 1f) return;

            // Every shape below is laid out in a unit square centered in the box.
            var origin = new Vector2(box.x + (box.width - size) * 0.5f, box.y + (box.height - size) * 0.5f);
            Vector2 At(float x, float y) => origin + new Vector2(x, y) * size;

            Painter2D painter = context.painter2D;
            painter.strokeColor = color;
            painter.fillColor = color;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;

            switch (kind)
            {
                case MenuIconKind.Lock:
                    painter.lineWidth = size * 0.1f;
                    painter.BeginPath();
                    painter.MoveTo(At(0.32f, 0.48f));
                    painter.LineTo(At(0.32f, 0.33f));
                    painter.Arc(At(0.5f, 0.33f), size * 0.18f, Angle.Degrees(180f), Angle.Degrees(360f));
                    painter.LineTo(At(0.68f, 0.48f));
                    painter.Stroke();

                    float corner = size * 0.07f;
                    painter.BeginPath();
                    painter.MoveTo(At(0.2f, 0.46f) + new Vector2(corner, 0f));
                    painter.ArcTo(At(0.8f, 0.46f), At(0.8f, 0.9f), corner);
                    painter.ArcTo(At(0.8f, 0.9f), At(0.2f, 0.9f), corner);
                    painter.ArcTo(At(0.2f, 0.9f), At(0.2f, 0.46f), corner);
                    painter.ArcTo(At(0.2f, 0.46f), At(0.8f, 0.46f), corner);
                    painter.ClosePath();
                    painter.Fill();
                    break;

                case MenuIconKind.Check:
                    painter.lineWidth = size * 0.13f;
                    painter.BeginPath();
                    painter.MoveTo(At(0.2f, 0.53f));
                    painter.LineTo(At(0.42f, 0.74f));
                    painter.LineTo(At(0.8f, 0.28f));
                    painter.Stroke();
                    break;

                default:
                    float tip = kind == MenuIconKind.Previous ? 0.36f : 0.64f;
                    float tail = kind == MenuIconKind.Previous ? 0.6f : 0.4f;
                    painter.lineWidth = size * 0.12f;
                    painter.BeginPath();
                    painter.MoveTo(At(tail, 0.2f));
                    painter.LineTo(At(tip, 0.5f));
                    painter.LineTo(At(tail, 0.8f));
                    painter.Stroke();
                    break;
            }
        }
    }
}
