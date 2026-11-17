using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// Stretches the space backdrop quad so it always covers the camera view, and keeps the backdrop's sun
    /// just outside the view's top-left corner, wherever the view is: a mission's map can lie anywhere,
    /// and a sun fixed in the world would end up behind one of its worlds or nowhere in sight. How far
    /// outside the corner is part of each backdrop material's look (its Sun Offset).
    /// </summary>
    [ExecuteAlways]
    public sealed class Backdrop : MonoBehaviour
    {
        static readonly int SunPositionId = Shader.PropertyToID("_SunPosition");
        static readonly int SunOffsetId = Shader.PropertyToID("_SunOffset");

        // A material's sun offset is measured for a view this many units high. Larger views move the
        // sun out with them.
        const float ReferenceViewHeight = 10f;

        [SerializeField] CameraRig rig;
        [SerializeField] float margin = 1.5f;

        MeshRenderer quad;
        MaterialPropertyBlock block;

        /// <summary>Shows another space backdrop, such as the one a mission was given. The sun keeps its place.</summary>
        public void Show(Material backdrop)
        {
            if (quad == null) quad = GetComponent<MeshRenderer>();
            if (quad != null) quad.sharedMaterial = backdrop;
            LateUpdate();
        }

        void OnEnable()
        {
            LateUpdate();
        }

        void LateUpdate()
        {
            if (rig == null) return;

            Rect view = rig.View;
            transform.position = new Vector3(view.center.x, view.center.y, 0f);
            transform.localScale = new Vector3(view.width + margin * 2f, view.height + margin * 2f, 1f);
            PlaceSun(view);
        }

        // Per renderer, so the shared material asset is never changed.
        void PlaceSun(Rect view)
        {
            if (quad == null) quad = GetComponent<MeshRenderer>();
            if (quad == null) return;

            Material material = quad.sharedMaterial;
            if (material == null || !material.HasProperty(SunOffsetId)) return;

            block ??= new MaterialPropertyBlock();
            Vector2 offset = material.GetVector(SunOffsetId);
            Vector2 sun = new Vector2(view.xMin, view.yMax) + offset * (view.height / ReferenceViewHeight);
            quad.GetPropertyBlock(block);
            block.SetVector(SunPositionId, new Vector4(sun.x, sun.y, 0f, 0f));
            quad.SetPropertyBlock(block);
        }
    }
}
