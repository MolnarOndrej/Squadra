using System.Collections.Generic;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// The bodies of the Darts drawn in one frame, rebuilt every frame into one mesh: a copy of the
    /// shared Dart mesh, or of a round dot, for each Dart drawn, turned to its heading and colored for its
    /// owner. Darts of one color share a submesh, and the renderer draws each submesh with the Dart
    /// material and that color in a property block of its own, so the shared material is never changed;
    /// the color is in the vertices too, for a shader that reads it there. Trails, shots and sparks are
    /// not bodies: they stay round dots in a <see cref="DotBatch"/>.
    /// </summary>
    public sealed class DartBatch
    {
        // However many Darts there are, no frame's mesh grows beyond this; Darts past it are left out.
        const int MostVertices = 1 << 19;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int TintId = Shader.PropertyToID("_Tint");

        struct Body
        {
            public Vector2 Center;
            public float Heading;
            public float Size;
        }

        sealed class Group
        {
            public Vector4 Tint;
            public readonly List<Body> Bodies = new List<Body>();
        }

        readonly Mesh mesh;
        readonly MeshRenderer renderer;
        readonly Material dotMaterial;
        readonly Mesh dot;
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        readonly List<Group> groups = new List<Group>();
        readonly Stack<Group> spareGroups = new Stack<Group>();

        // The shape as it is copied for each Dart: the mesh turned by its rotation, read once per change.
        Mesh shapeSource;
        int shapeVertexCount;
        Quaternion shapeRotation;
        Vector3[] shapeVertices;
        Vector3[] shapeNormals;
        Vector2[] shapeUvs;
        int[] shapeTriangles;

        // What is uploaded each frame. The UVs and triangles of a Dart never change, so they are written
        // for as many Darts as the buffers hold when they grow, and only positions and colors every frame.
        int capacity;
        Vector3[] vertices;
        Vector3[] normals;
        Vector2[] uvs;
        Color[] colors;
        int[] triangles;

        Material[] materials = new Material[0];
        Material material;
        bool warnedFull;

        /// <summary>
        /// Draws into <paramref name="filter"/>'s mesh with <paramref name="target"/>; Darts take
        /// <paramref name="dotMaterial"/> when the style gives no material of its own.
        /// </summary>
        public DartBatch(MeshFilter filter, MeshRenderer target, Material dotMaterial)
        {
            mesh = new Mesh
            {
                name = "Darts",
                hideFlags = HideFlags.DontSave,
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, // a detailed mesh outgrows 16-bit indices
            };
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
            renderer = target;
            this.dotMaterial = dotMaterial;
            dot = DotShape();
        }

        /// <summary>Starts a frame drawn with the mesh, rotation and material <paramref name="style"/> has now.</summary>
        public void Begin(FleetStyle style)
        {
            foreach (Group group in groups)
            {
                group.Bodies.Clear();
                spareGroups.Push(group);
            }

            groups.Clear();
            Mesh shape = style.DartMesh;
            if (shape != null) UseShape(shape, style.DartMeshRotation);
            else UseShape(dot, Quaternion.identity);
            material = style.DartMaterial != null ? style.DartMaterial : dotMaterial;
        }

        /// <summary>
        /// One Dart at <paramref name="center"/>, its nose toward <paramref name="heading"/> (radians
        /// counter-clockwise from +X), drawn <paramref name="size"/> times the mesh's size.
        /// </summary>
        /// <param name="tint">Linear-space color; values above 1 bloom.</param>
        public void Add(Vector2 center, float heading, float size, Vector4 tint)
        {
            if (size <= 0f || tint.w <= 0f) return;

            Group group = GroupOf(tint);
            group.Bodies.Add(new Body { Center = center, Heading = heading, Size = size });
        }

        public void End()
        {
            int perBody = shapeVertices.Length;
            int bodies = 0;
            foreach (Group group in groups)
                bodies += group.Bodies.Count;
            int most = MostVertices / perBody;
            if (bodies > most)
            {
                WarnFull();
                bodies = most;
            }

            Reserve(bodies);
            bool withNormals = shapeNormals.Length == perBody;
            int drawn = 0;
            foreach (Group group in groups)
            {
                var color = new Color(group.Tint.x, group.Tint.y, group.Tint.z, group.Tint.w);
                foreach (Body body in group.Bodies)
                {
                    if (drawn == bodies) break;

                    int first = drawn * perBody;
                    float cos = Mathf.Cos(body.Heading) * body.Size;
                    float sin = Mathf.Sin(body.Heading) * body.Size;
                    for (int v = 0; v < perBody; v++)
                    {
                        Vector3 p = shapeVertices[v];
                        vertices[first + v] = new Vector3(body.Center.x + p.x * cos - p.y * sin,
                            body.Center.y + p.x * sin + p.y * cos, p.z * body.Size);
                        colors[first + v] = color;
                    }

                    if (withNormals)
                    {
                        float turnCos = Mathf.Cos(body.Heading);
                        float turnSin = Mathf.Sin(body.Heading);
                        for (int v = 0; v < perBody; v++)
                        {
                            Vector3 n = shapeNormals[v];
                            normals[first + v] = new Vector3(n.x * turnCos - n.y * turnSin, n.x * turnSin + n.y * turnCos, n.z);
                        }
                    }

                    drawn++;
                }
            }

            int count = drawn * perBody;
            mesh.Clear();
            mesh.SetVertices(vertices, 0, count);
            if (withNormals) mesh.SetNormals(normals, 0, count);
            mesh.SetUVs(0, uvs, 0, count);
            mesh.SetColors(colors, 0, count);

            // One submesh for each color, in the order the groups were filled: the Darts of a color are
            // next to each other, and so are their triangles.
            mesh.subMeshCount = Mathf.Max(1, groups.Count);
            int perBodyIndices = shapeTriangles.Length;
            int left = drawn;
            int start = 0;
            for (int g = 0; g < groups.Count; g++)
            {
                int inGroup = Mathf.Min(groups[g].Bodies.Count, left);
                mesh.SetTriangles(triangles, start, inGroup * perBodyIndices, g, false);
                start += inGroup * perBodyIndices;
                left -= inGroup;
            }

            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1000f, 1000f, 10f));
            Paint();
        }

        public void Dispose()
        {
            Destroy(mesh);
            Destroy(dot);
        }

        // ------------------------------------------------------------------ helpers

        Group GroupOf(Vector4 tint)
        {
            foreach (Group group in groups)
            {
                if (group.Tint == tint) return group;
            }

            Group added = spareGroups.Count > 0 ? spareGroups.Pop() : new Group();
            added.Tint = tint;
            groups.Add(added);
            return added;
        }

        // Each submesh is drawn with the Dart material and its group's color, set beside the material.
        void Paint()
        {
            int count = Mathf.Max(1, groups.Count);
            if (materials.Length != count || materials[0] != material)
            {
                materials = new Material[count];
                for (int i = 0; i < count; i++)
                    materials[i] = material;
                renderer.sharedMaterials = materials;
            }

            if (material == null) return;
            bool baseColor = material.HasProperty(BaseColorId);
            bool color = material.HasProperty(ColorId);
            bool tint = material.HasProperty(TintId);
            if (!baseColor && !color && !tint) return; // the vertex colors carry it, as the dot material reads them

            for (int g = 0; g < groups.Count; g++)
            {
                block.Clear();
                Vector4 value = groups[g].Tint;
                if (baseColor) block.SetVector(BaseColorId, value);
                if (color) block.SetVector(ColorId, value);
                if (tint) block.SetVector(TintId, value);
                renderer.SetPropertyBlock(block, g);
            }
        }

        void UseShape(Mesh source, Quaternion rotation)
        {
            if (source == shapeSource && rotation == shapeRotation && source.vertexCount == shapeVertexCount) return;

            shapeSource = source;
            shapeRotation = rotation;
            shapeVertexCount = source.vertexCount;

            Vector3[] sourceVertices = source.vertices;
            Vector3[] sourceNormals = source.normals;
            shapeVertices = new Vector3[sourceVertices.Length];
            shapeNormals = new Vector3[sourceNormals.Length];
            for (int i = 0; i < sourceVertices.Length; i++)
                shapeVertices[i] = rotation * sourceVertices[i];
            for (int i = 0; i < sourceNormals.Length; i++)
                shapeNormals[i] = rotation * sourceNormals[i];

            Vector2[] sourceUvs = source.uv;
            shapeUvs = sourceUvs.Length == sourceVertices.Length ? sourceUvs : new Vector2[sourceVertices.Length];
            shapeTriangles = source.triangles;

            capacity = 0; // the buffers' UVs and triangles are another shape's
            warnedFull = false;
        }

        // Makes the buffers hold at least this many Darts of the current shape.
        void Reserve(int bodies)
        {
            if (bodies <= capacity && vertices != null) return;

            int perBody = shapeVertices.Length;
            capacity = Mathf.Min(Mathf.Max(bodies, capacity * 2, 64), MostVertices / perBody);
            int count = capacity * perBody;
            vertices = new Vector3[count];
            normals = new Vector3[count];
            uvs = new Vector2[count];
            colors = new Color[count];
            triangles = new int[capacity * shapeTriangles.Length];
            for (int b = 0; b < capacity; b++)
            {
                System.Array.Copy(shapeUvs, 0, uvs, b * perBody, perBody);
                int first = b * shapeTriangles.Length;
                for (int i = 0; i < shapeTriangles.Length; i++)
                    triangles[first + i] = b * perBody + shapeTriangles[i];
            }
        }

        // Said once for each shape, not every frame it happens.
        void WarnFull()
        {
            if (warnedFull) return;
            warnedFull = true;
            Debug.LogWarning("Squadra: too many Darts to draw with a mesh of " + shapeVertices.Length
                + " vertices; some are left out this frame. Use a simpler Dart mesh or lower the visible limits.");
        }

        // The round dot: a quad twice as wide as the dot's core, which the dot material draws as a soft
        // disc. At a size of the style's dot width it is the dot the fleets were always drawn with.
        static Mesh DotShape()
        {
            var shape = new Mesh { name = "Dart Dot", hideFlags = HideFlags.DontSave };
            shape.SetVertices(new[]
            {
                new Vector3(-1f, -1f, 0f), new Vector3(-1f, 1f, 0f), new Vector3(1f, 1f, 0f), new Vector3(1f, -1f, 0f),
            });
            shape.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) });
            shape.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            return shape;
        }

        static void Destroy(Object target)
        {
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }
    }
}
