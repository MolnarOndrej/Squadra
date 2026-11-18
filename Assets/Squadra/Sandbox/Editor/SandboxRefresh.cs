using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Squadra.EditorTools
{
    /// <summary>
    /// Redraws an open sandbox outside Play Mode after any edit: to a sample, a world moved in the Scene
    /// view, or a shared style or material edited in the Inspector. The editor runs a scene's update only
    /// when it decides the scene changed, which an edit to an asset is not, and not at all while the
    /// editor is in the background; this redraws at once either way.
    /// </summary>
    [InitializeOnLoad]
    static class SandboxRefresh
    {
        static SandboxRefresh()
        {
            ObjectChangeEvents.changesPublished += OnChangesPublished;
        }

        static void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            if (stream.length == 0 || EditorApplication.isPlayingOrWillChangePlaymode) return;

            // Once for however many changes come in before the editor's next turn.
            EditorApplication.delayCall -= Redraw;
            EditorApplication.delayCall += Redraw;
        }

        static void Redraw()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            bool any = false;
            foreach (SandboxPreview preview in Object.FindObjectsByType<SandboxPreview>(FindObjectsInactive.Exclude))
            {
                if (!preview.isActiveAndEnabled) continue;
                preview.Redraw();
                any = true;
            }

            if (any) InternalEditorUtility.RepaintAllViews();
        }
    }
}
