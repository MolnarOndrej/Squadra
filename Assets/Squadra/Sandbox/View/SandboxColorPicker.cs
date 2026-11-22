using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Squadra
{
    /// <summary>
    /// Picks a color on the sandbox panel: a square of strength (left to right) and brightness (bottom to
    /// top) for the chosen hue, and a strip of hues below it. Press and drag on either. It keeps the hue
    /// while the color is gray or black, so going back to a strong color keeps the hue it had.
    /// </summary>
    public sealed class SandboxColorPicker : VisualElement
    {
        // Texels per side of the square; filtering blends them, and the blend of strength and brightness
        // at one hue is exact between texels.
        const int Shades = 16;
        const int Hues = 96;

        readonly VisualElement square;
        readonly VisualElement squareMarker;
        readonly VisualElement hueStrip;
        readonly VisualElement hueMarker;
        Texture2D squareTexture;
        Texture2D hueTexture;
        float hue;
        float saturation;
        float value;
        float paintedHue = -1f;

        /// <summary>Raised with the new color while the user presses or drags.</summary>
        public event Action<Color> Picked;

        public SandboxColorPicker()
        {
            AddToClassList("sandbox-picker");

            square = new VisualElement();
            square.AddToClassList("sandbox-picker__square");
            squareMarker = new VisualElement { pickingMode = PickingMode.Ignore };
            squareMarker.AddToClassList("sandbox-picker__marker");
            square.Add(squareMarker);

            hueStrip = new VisualElement();
            hueStrip.AddToClassList("sandbox-picker__hues");
            hueMarker = new VisualElement { pickingMode = PickingMode.Ignore };
            hueMarker.AddToClassList("sandbox-picker__hue-marker");
            hueStrip.Add(hueMarker);

            Add(square);
            Add(hueStrip);

            Drag(square, at =>
            {
                saturation = at.x;
                value = 1f - at.y;
                Pick();
            });
            Drag(hueStrip, at =>
            {
                hue = at.x;
                Pick();
            });

            RegisterCallback<GeometryChangedEvent>(_ => PlaceMarkers());
            RegisterCallback<DetachFromPanelEvent>(_ => DestroyTextures());
        }

        /// <summary>Shows <paramref name="color"/> as picked, without raising <see cref="Picked"/>.</summary>
        public void Show(Color color)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            if (s > 0.001f && v > 0.001f) hue = h;
            saturation = s;
            value = v;
            Paint();
            PlaceMarkers();
        }

        void Pick()
        {
            Paint();
            PlaceMarkers();
            Picked?.Invoke(Color.HSVToRGB(hue, saturation, value));
        }

        void Paint()
        {
            if (hueTexture == null)
            {
                hueTexture = MakeTexture(Hues, 1);
                for (int i = 0; i < Hues; i++)
                    hueTexture.SetPixel(i, 0, Color.HSVToRGB(i / (float)(Hues - 1), 1f, 1f));
                hueTexture.Apply();
                hueStrip.style.backgroundImage = new StyleBackground(hueTexture);
            }

            if (squareTexture == null)
            {
                squareTexture = MakeTexture(Shades, Shades);
                square.style.backgroundImage = new StyleBackground(squareTexture);
            }

            if (Mathf.Approximately(paintedHue, hue)) return;
            paintedHue = hue;
            for (int y = 0; y < Shades; y++)
            {
                for (int x = 0; x < Shades; x++)
                    squareTexture.SetPixel(x, y, Color.HSVToRGB(hue, x / (float)(Shades - 1), y / (float)(Shades - 1)));
            }

            squareTexture.Apply();
            square.MarkDirtyRepaint();
        }

        void PlaceMarkers()
        {
            Rect area = square.contentRect;
            squareMarker.style.left = saturation * area.width;
            squareMarker.style.top = (1f - value) * area.height;
            hueMarker.style.left = hue * hueStrip.contentRect.width;
        }

        static Texture2D MakeTexture(int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        void DestroyTextures()
        {
            if (squareTexture != null) UnityEngine.Object.Destroy(squareTexture);
            if (hueTexture != null) UnityEngine.Object.Destroy(hueTexture);
            squareTexture = null;
            hueTexture = null;
            paintedHue = -1f;
        }

        // Press, then drag: the point under the pointer, from (0, 0) at the top left to (1, 1) at the bottom
        // right of the area, held to its edges.
        static void Drag(VisualElement area, Action<Vector2> at)
        {
            area.RegisterCallback<PointerDownEvent>(e =>
            {
                area.CapturePointer(e.pointerId);
                at(Fraction(area, e.localPosition));
                e.StopPropagation();
            });
            area.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (area.HasPointerCapture(e.pointerId)) at(Fraction(area, e.localPosition));
            });
            area.RegisterCallback<PointerUpEvent>(e =>
            {
                if (area.HasPointerCapture(e.pointerId)) area.ReleasePointer(e.pointerId);
            });
        }

        static Vector2 Fraction(VisualElement area, Vector3 local)
        {
            Rect size = area.layout;
            return new Vector2(
                size.width > 0f ? Mathf.Clamp01(local.x / size.width) : 0f,
                size.height > 0f ? Mathf.Clamp01(local.y / size.height) : 0f);
        }
    }
}
