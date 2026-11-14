using Squadra.Simulation;
using TMPro;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// The heads-up display, drawn as world-space text pinned to the edges of the camera view:
    /// title, fleet totals, match clock and a contextual hint. When the match ends it fades the
    /// battlefield behind a scrim; the results panel is drawn over that by <see cref="ResultsScreen"/>.
    /// The pause menu dims the mission with the same scrim, so a pause over a result never darkens the
    /// view a second time.
    /// </summary>
    [ExecuteAlways]
    public sealed class HudView : MonoBehaviour
    {
        static readonly int TintId = Shader.PropertyToID("_Tint");

        /// <summary>How much of the battlefield the scrim covers at its darkest, for a result and for a pause alike.</summary>
        public const float DimOpacity = 0.72f;

        const float ResultFadeSeconds = 0.6f;
        const float PauseFadeSeconds = 0.2f;

        [SerializeField] CameraRig rig;
        [SerializeField] float margin = 0.45f;

        [Header("Top bar")]
        [SerializeField] TextMeshPro title;
        [SerializeField] TextMeshPro subtitle;
        [SerializeField] TextMeshPro blueTally;
        [SerializeField] TextMeshPro redTally;
        [SerializeField] TextMeshPro versus;
        [SerializeField] TextMeshPro clock;
        [SerializeField] TextMeshPro clockCaption;

        [Header("Bottom")]
        [SerializeField] TextMeshPro hint;

        [Header("Result")]
        [SerializeField] GameObject resultRoot;
        [SerializeField] MeshRenderer scrim;

        MaterialPropertyBlock scrimBlock;
        string shownHint;
        int shownBlue = -1;
        int shownRed = -1;
        int shownSeconds = -1;
        float resultAge = -1f;
        float pauseDim;
        bool dimmed;

        /// <summary>True once the battlefield has started to fade behind the results.</summary>
        public bool ResultVisible => resultAge >= 0f;

        /// <summary>True while the battlefield is dimmed for the pause menu.</summary>
        public bool Dimmed => dimmed;

        /// <summary>How dark the scrim over the battlefield is right now, from 0 to <see cref="DimOpacity"/>.</summary>
        public float ScrimOpacity => DimOpacity * Mathf.Max(ResultCover(), PauseCover());

        public void SetHint(string text)
        {
            if (hint == null || text == shownHint) return;
            shownHint = text;
            hint.text = text;
        }

        public void SetStatus(Match match)
        {
            int blue = match.TotalDarts(Faction.Blue);
            if (blue != shownBlue && blueTally != null)
            {
                shownBlue = blue;
                blueTally.text = blue + "\n<size=34%><cspace=0.12em>BLUE FLEET</cspace></size>";
            }

            int red = match.TotalDarts(Faction.Red);
            if (red != shownRed && redTally != null)
            {
                shownRed = red;
                redTally.text = red + "\n<size=34%><cspace=0.12em>RED FLEET</cspace></size>";
            }

            int seconds = (int)MatchClock.WholeSeconds(match.Elapsed);
            if (seconds != shownSeconds && clock != null)
            {
                shownSeconds = seconds;
                clock.text = MatchClock.Format(seconds);
            }
        }

        /// <summary>Fades the battlefield for the results panel and clears the gameplay hint.</summary>
        public void ShowResult()
        {
            if (resultRoot == null || ResultVisible) return;

            resultAge = 0f;
            resultRoot.SetActive(true);
            SetHint(string.Empty);
            ApplyScrim();
        }

        /// <summary>
        /// Dims the battlefield for the pause menu, or lifts the dimming again. It is the same scrim the
        /// results use and the darker of the two wins, so a result's dimming looks the same before,
        /// during and after a pause.
        /// </summary>
        public void SetDimmed(bool value)
        {
            if (resultRoot == null || value == dimmed) return;

            dimmed = value;
            if (value) resultRoot.SetActive(true);
            ApplyScrim();
        }

        void OnEnable()
        {
            LateUpdate();
        }

        void LateUpdate()
        {
            if (rig == null) return;

            Rect view = rig.View;
            float left = view.xMin + margin;
            float right = view.xMax - margin;
            float top = view.yMax - margin;
            float bottom = view.yMin + margin;
            float centerX = view.center.x;

            Place(title, left, top);
            Place(subtitle, left, top - 0.72f);
            Place(blueTally, centerX - 0.55f, top + 0.05f);
            Place(redTally, centerX + 0.55f, top + 0.05f);
            Place(versus, centerX, top - 0.3f);
            Place(clock, right, top);
            Place(clockCaption, right, top - 0.62f);
            Place(hint, centerX, bottom);

            if (resultRoot != null && resultRoot.activeSelf)
            {
                Vector2 center = view.center;
                scrim.transform.position = new Vector3(center.x, center.y, 0f);
                scrim.transform.localScale = new Vector3(view.width + 3f, view.height + 3f, 1f);

                if (Application.isPlaying && (ResultVisible || dimmed || pauseDim > 0f))
                {
                    // Real time: the scrim keeps fading while the mission itself is paused.
                    if (ResultVisible) resultAge += Time.unscaledDeltaTime;
                    pauseDim = Mathf.MoveTowards(pauseDim, dimmed ? 1f : 0f,
                        Time.unscaledDeltaTime / PauseFadeSeconds);
                    ApplyScrim();
                    if (!ResultVisible && !dimmed && pauseDim <= 0f) resultRoot.SetActive(false);
                }
            }
        }

        float ResultCover()
        {
            if (!ResultVisible) return 0f;

            float t = Mathf.Clamp01(resultAge / ResultFadeSeconds);
            return 1f - (1f - t) * (1f - t) * (1f - t);
        }

        float PauseCover()
        {
            return Mathf.SmoothStep(0f, 1f, pauseDim);
        }

        void ApplyScrim()
        {
            if (scrimBlock == null) scrimBlock = new MaterialPropertyBlock();
            Color cover = Palette.Void.linear;
            scrimBlock.SetVector(TintId, new Vector4(cover.r, cover.g, cover.b, ScrimOpacity));
            scrim.SetPropertyBlock(scrimBlock);
        }

        static void Place(TextMeshPro text, float x, float y)
        {
            if (text != null) text.transform.position = new Vector3(x, y, 0f);
        }
    }
}
