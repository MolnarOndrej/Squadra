using UnityEngine;

namespace Squadra
{
    /// <summary>Helpers for the shader-driven quads everything in the scene is drawn with.</summary>
    public static class Shapes
    {
        static Mesh quad;

        public static Mesh Quad
        {
            get
            {
                if (quad == null) quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                return quad;
            }
        }

        public static MeshRenderer CreateQuad(string name, Transform parent, Material material, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Quad;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }
    }

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

    /// <summary>A mesh of camera-facing quads rebuilt every frame: fleet dots and sparks in one draw call.</summary>
    public sealed class DotBatch
    {
        readonly Mesh mesh;
        readonly System.Collections.Generic.List<Vector3> vertices = new System.Collections.Generic.List<Vector3>(1024);
        readonly System.Collections.Generic.List<Vector2> uvs = new System.Collections.Generic.List<Vector2>(1024);
        readonly System.Collections.Generic.List<Color> colors = new System.Collections.Generic.List<Color>(1024);
        readonly System.Collections.Generic.List<int> triangles = new System.Collections.Generic.List<int>(1536);

        public DotBatch(MeshFilter filter)
        {
            mesh = new Mesh { name = "Fleet Dots", hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
        }

        public void Begin()
        {
            vertices.Clear();
            uvs.Clear();
            colors.Clear();
        }

        /// <param name="color">Linear-space color; values above 1 bloom.</param>
        public void Add(Vector2 center, float halfSize, Vector4 color)
        {
            if (halfSize <= 0f || color.w <= 0f) return;

            vertices.Add(new Vector3(center.x - halfSize, center.y - halfSize, 0f));
            vertices.Add(new Vector3(center.x - halfSize, center.y + halfSize, 0f));
            vertices.Add(new Vector3(center.x + halfSize, center.y + halfSize, 0f));
            vertices.Add(new Vector3(center.x + halfSize, center.y - halfSize, 0f));
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(1f, 0f));
            var c = new Color(color.x, color.y, color.z, color.w);
            colors.Add(c);
            colors.Add(c);
            colors.Add(c);
            colors.Add(c);
        }

        public void End()
        {
            int quads = vertices.Count / 4;
            int have = triangles.Count / 6;
            if (have > quads)
            {
                triangles.RemoveRange(quads * 6, (have - quads) * 6);
            }
            else
            {
                for (int q = have; q < quads; q++)
                {
                    int v = q * 4;
                    triangles.Add(v);
                    triangles.Add(v + 1);
                    triangles.Add(v + 2);
                    triangles.Add(v);
                    triangles.Add(v + 2);
                    triangles.Add(v + 3);
                }
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1000f, 1000f, 10f));
        }

        public void Dispose()
        {
            if (Application.isPlaying) Object.Destroy(mesh);
            else Object.DestroyImmediate(mesh);
        }
    }
}
