using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// Keeps the whole map on one screen at any aspect ratio, and adds a short shake on impacts.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        [Tooltip("World-space rectangle that must always be fully visible.")]
        [SerializeField] Rect content = new Rect(-8.9f, -5f, 17.8f, 10f);
        [SerializeField] float shakeDecay = 5f;
        [SerializeField] float maxShake = 0.3f;

        Camera cam;
        float shake;

        public Camera Camera
        {
            get
            {
                if (cam == null) cam = GetComponent<Camera>();
                return cam;
            }
        }

        /// <summary>The world-space rectangle the camera shows, ignoring shake.</summary>
        public Rect View
        {
            get
            {
                float halfHeight = Camera.orthographicSize;
                float halfWidth = halfHeight * Camera.aspect;
                Vector2 center = content.center;
                return new Rect(center.x - halfWidth, center.y - halfHeight, halfWidth * 2f, halfHeight * 2f);
            }
        }

        public void Shake(float amplitude)
        {
            shake = Mathf.Min(maxShake, Mathf.Max(shake, amplitude));
        }

        void OnEnable()
        {
            LateUpdate();
        }

        void LateUpdate()
        {
            Camera camera = Camera;
            float aspect = Mathf.Max(0.1f, camera.aspect);
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(content.height * 0.5f, content.width * 0.5f / aspect);

            Vector2 center = content.center;
            Vector2 offset = Vector2.zero;
            if (Application.isPlaying && shake > 0.0005f)
            {
                float t = Time.unscaledTime * 38f;
                offset = new Vector2(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f) * (2f * shake);
                shake = Mathf.MoveTowards(shake, 0f, shakeDecay * shake * Time.unscaledDeltaTime + 0.02f * Time.unscaledDeltaTime);
            }

            transform.position = new Vector3(center.x + offset.x, center.y + offset.y, -10f);
        }
    }
}
