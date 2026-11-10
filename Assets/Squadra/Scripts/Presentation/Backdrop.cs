using UnityEngine;

namespace Squadra
{
    /// <summary>Stretches the space backdrop quad so it always covers the camera view.</summary>
    [ExecuteAlways]
    public sealed class Backdrop : MonoBehaviour
    {
        [SerializeField] CameraRig rig;
        [SerializeField] float margin = 1.5f;

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
        }
    }
}
