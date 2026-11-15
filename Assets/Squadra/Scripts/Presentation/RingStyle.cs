using UnityEngine;

namespace Squadra
{
    /// <summary>Drives one <c>Squadra/Ring</c> quad. Sizes are given in world units.</summary>
    public sealed class RingStyle
    {
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        static readonly int SoftnessId = Shader.PropertyToID("_Softness");
        static readonly int GlowId = Shader.PropertyToID("_Glow");
        static readonly int DashesId = Shader.PropertyToID("_Dashes");
        static readonly int DashFillId = Shader.PropertyToID("_DashFill");
        static readonly int SpinId = Shader.PropertyToID("_Spin");
        static readonly int ArcId = Shader.PropertyToID("_Arc");
        static readonly int TrackId = Shader.PropertyToID("_Track");
        static readonly int AdditiveId = Shader.PropertyToID("_Additive");

        readonly MeshRenderer renderer;
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();

        public Vector4 Tint = Vector4.one;
        public float Radius = 1f;       // world units
        public float Thickness = 0.03f; // world units
        public float Softness = 0.08f;  // world units
        public float Glow = 0.4f;
        public float Dashes;
        public float DashFill = 0.5f;
        public float Spin;
        public float Arc = 1f;
        public float Track;
        public float Additive = 0.6f;

        public RingStyle(MeshRenderer renderer)
        {
            this.renderer = renderer;
        }

        public MeshRenderer Renderer => renderer;

        public bool Visible
        {
            get => renderer.enabled;
            set
            {
                if (renderer.enabled != value) renderer.enabled = value;
            }
        }

        public void Apply()
        {
            // The quad is sized so the ring and its glow sit inside the shader's edge fade.
            float half = (Radius + Thickness + Softness * 2.5f) / 0.9f;
            renderer.transform.localScale = new Vector3(half * 2f, half * 2f, 1f);

            block.SetVector(TintId, Tint);
            block.SetFloat(RadiusId, Radius / half);
            block.SetFloat(ThicknessId, Thickness / half);
            block.SetFloat(SoftnessId, Mathf.Max(0.001f, Softness / half));
            block.SetFloat(GlowId, Glow);
            block.SetFloat(DashesId, Dashes);
            block.SetFloat(DashFillId, DashFill);
            block.SetFloat(SpinId, Spin);
            block.SetFloat(ArcId, Arc);
            block.SetFloat(TrackId, Track);
            block.SetFloat(AdditiveId, Additive);
            renderer.SetPropertyBlock(block);
        }
    }
}
