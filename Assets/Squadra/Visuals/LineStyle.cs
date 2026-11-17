using UnityEngine;

namespace Squadra
{
    /// <summary>Drives one <c>Squadra/Line</c> quad stretched between two points.</summary>
    public sealed class LineStyle
    {
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int LengthId = Shader.PropertyToID("_Length");
        static readonly int DashId = Shader.PropertyToID("_Dash");
        static readonly int GapId = Shader.PropertyToID("_Gap");
        static readonly int ScrollId = Shader.PropertyToID("_Scroll");
        static readonly int EndFadeId = Shader.PropertyToID("_EndFade");

        readonly MeshRenderer renderer;
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();

        public Vector4 Tint = Vector4.one;
        public float Width = 0.06f;
        public float Dash;
        public float Gap = 0.2f;
        public float Scroll;
        public float EndFade = 0.3f;

        bool hasLength;

        public LineStyle(MeshRenderer renderer)
        {
            this.renderer = renderer;
        }

        public bool Visible
        {
            get => renderer.enabled;
            set
            {
                value = value && hasLength;
                if (renderer.enabled != value) renderer.enabled = value;
            }
        }

        /// <summary>
        /// Takes the width, dashes and end fade of <paramref name="tuning"/>. Color and scrolling are left
        /// to the caller. Takes effect with the next <see cref="Set"/>.
        /// </summary>
        public void Shape(LineTuning tuning)
        {
            Width = tuning.Width;
            Dash = tuning.Dash;
            Gap = tuning.Gap;
            EndFade = tuning.EndFade;
        }

        public void Set(Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;
            hasLength = length >= 0.01f;
            Visible = true;
            if (!hasLength) return;

            Transform t = renderer.transform;
            t.position = (Vector3)((from + to) * 0.5f);
            t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            t.localScale = new Vector3(length, Width, 1f);

            block.SetVector(TintId, Tint);
            block.SetFloat(LengthId, length);
            block.SetFloat(DashId, Dash);
            block.SetFloat(GapId, Gap);
            block.SetFloat(ScrollId, Scroll);
            block.SetFloat(EndFadeId, EndFade);
            renderer.SetPropertyBlock(block);
        }
    }
}
