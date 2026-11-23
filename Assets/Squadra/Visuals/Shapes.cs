using UnityEngine;

namespace Squadra
{
    /// <summary>Helpers for the shader-driven quads everything in the scene is drawn with.</summary>
    public static class Shapes
    {
        // Planet.shader draws the body as a disc in the middle of its quad and keeps the margin around it
        // for the halo. _BodyRadius is the disc's radius as a share of the quad's half-width.
        static readonly int BodyRadiusId = Shader.PropertyToID("_BodyRadius");

        // The smallest share the side is computed from, so the division stays safe.
        const float MinBodyShare = 0.05f;

        // The share for a material without _BodyRadius, or no material: the shader's own default.
        const float DefaultBodyShare = 0.7f;

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

        /// <summary>
        /// The side of the quad a planet, moon or asteroid surface is drawn on, so the body the material
        /// paints on it has the given radius. Larger than the body: the margin holds the halo.
        /// </summary>
        public static float BodyQuadSide(Material material, float bodyRadius)
        {
            float share = material != null && material.HasProperty(BodyRadiusId)
                ? Mathf.Max(MinBodyShare, material.GetFloat(BodyRadiusId))
                : DefaultBodyShare;
            return bodyRadius * 2f / share;
        }
    }
}
