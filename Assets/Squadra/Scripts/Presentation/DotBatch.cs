using System.Collections.Generic;
using UnityEngine;

namespace Squadra
{
    /// <summary>A mesh of camera-facing quads rebuilt every frame: fleet dots and sparks in one draw call.</summary>
    public sealed class DotBatch
    {
        readonly Mesh mesh;
        readonly List<Vector3> vertices = new List<Vector3>(1024);
        readonly List<Vector2> uvs = new List<Vector2>(1024);
        readonly List<Color> colors = new List<Color>(1024);
        readonly List<int> triangles = new List<int>(1536);

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
