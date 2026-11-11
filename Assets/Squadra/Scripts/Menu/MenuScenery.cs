using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// The quiet scenery behind the menu: a planet resting in the bottom-right corner of the view, two
    /// faint orbit lines around it and a moon drifting along the inner one. It follows the camera view,
    /// so it stays in its corner at any window shape.
    /// </summary>
    [ExecuteAlways]
    public sealed class MenuScenery : MonoBehaviour
    {
        static readonly int BodyRadiusId = Shader.PropertyToID("_BodyRadius");

        [SerializeField] CameraRig rig;
        [SerializeField] MeshRenderer planet;
        [SerializeField] MeshRenderer moon;
        [SerializeField] MeshRenderer innerOrbit;
        [SerializeField] MeshRenderer outerOrbit;

        [Header("Planet")]
        [SerializeField] float planetRadius = 3.4f;
        [Tooltip("Where the planet's center sits, measured in from the bottom-right corner of the view.")]
        [SerializeField] Vector2 cornerInset = new Vector2(0.9f, -0.4f);

        [Header("Orbits")]
        [SerializeField] float innerOrbitRadius = 4.5f;
        [SerializeField] float outerOrbitRadius = 5.5f;
        [Range(0f, 1f)] [SerializeField] float orbitOpacity = 0.22f;
        [SerializeField] float moonRadius = 0.34f;
        [Tooltip("Where on the inner orbit the moon starts, in degrees counter-clockwise from the right.")]
        [SerializeField] float moonStartAngle = 118f;
        [SerializeField] float moonDegreesPerSecond = 0.5f;

        RingStyle inner;
        RingStyle outer;

        void OnEnable()
        {
            LateUpdate();
        }

        void LateUpdate()
        {
            if (rig == null || planet == null) return;

            Rect view = rig.View;
            var center = new Vector2(view.xMax - cornerInset.x, view.yMin - cornerInset.y);
            Place(planet, center, planetRadius);

            if (moon != null)
            {
                float angle = moonStartAngle + (Application.isPlaying ? Time.unscaledTime * moonDegreesPerSecond : 0f);
                float radians = angle * Mathf.Deg2Rad;
                Vector2 offset = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * innerOrbitRadius;
                Place(moon, center + offset, moonRadius);
            }

            if (inner == null && innerOrbit != null) inner = new RingStyle(innerOrbit);
            if (outer == null && outerOrbit != null) outer = new RingStyle(outerOrbit);
            DrawOrbit(inner, center, innerOrbitRadius, 0f);
            DrawOrbit(outer, center, outerOrbitRadius, 120f);
        }

        // Sizes a planet-shader quad so the body it draws has the given radius.
        static void Place(MeshRenderer body, Vector2 center, float radius)
        {
            Material material = body.sharedMaterial;
            float fraction = material != null && material.HasProperty(BodyRadiusId)
                ? Mathf.Max(0.05f, material.GetFloat(BodyRadiusId))
                : 0.7f;
            float side = radius * 2f / fraction;
            body.transform.position = new Vector3(center.x, center.y, 0f);
            body.transform.localScale = new Vector3(side, side, 1f);
        }

        void DrawOrbit(RingStyle orbit, Vector2 center, float radius, float dashes)
        {
            if (orbit == null) return;

            orbit.Renderer.transform.position = new Vector3(center.x, center.y, 0f);
            orbit.Tint = Palette.Hdr(Palette.Starlight, 0.8f, orbitOpacity);
            orbit.Radius = radius;
            orbit.Thickness = 0.012f;
            orbit.Softness = 0.03f;
            orbit.Glow = 0.25f;
            orbit.Dashes = dashes;
            orbit.DashFill = 0.45f;
            orbit.Spin = dashes > 0f ? 0.0015f : 0f;
            orbit.Additive = 0.6f;
            orbit.Apply();
        }
    }
}
