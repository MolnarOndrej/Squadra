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
}
