using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// How the lines between worlds are drawn: the idle lane of every route, the preview of the order
    /// about to be given, and the course ahead of a fleet in transit and the wake behind it. One asset is
    /// shared by every mission. A fleet's lines take its owner's color from <see cref="Palette"/>; how
    /// fast dashes scroll is timing and stays in code. <c>Squadra &gt; Rebuild Mission Scene</c> creates
    /// the asset with these defaults when it is missing and otherwise leaves it as edited.
    /// </summary>
    [CreateAssetMenu(menuName = "Squadra/Route Style", fileName = "RouteStyle")]
    public sealed class RouteStyle : ScriptableObject
    {
        [Header("Idle lanes")]
        [SerializeField] Color laneColor = Palette.Starlight;
        [SerializeField]
        LineTuning lane = new LineTuning(
            brightness: 0.66f, opacity: 0.53f, width: 0.10f, dash: 0.13f, gap: 0.16f, endFade: 0.04f);

        [Header("Order preview")]
        [SerializeField] Color previewColor = Palette.Accent;
        [SerializeField]
        LineTuning preview = new LineTuning(
            brightness: 1.55f, opacity: 0.95f, width: 0.16f, dash: 0.26f, gap: 0.16f, endFade: 0.05f);

        [Header("Fleet course")]
        [Tooltip("How far the fleet's color is lightened toward white for the line ahead of it.")]
        [SerializeField, Range(0f, 1f)] float courseWhiten = 0.25f;
        [SerializeField]
        LineTuning course = new LineTuning(
            brightness: 1.4f, opacity: 0.84f, width: 0.14f, dash: 0.23f, gap: 0.17f, endFade: 0.08f);

        [Header("Fleet wake")]
        [Tooltip("How far the fleet's color is lightened toward white for the line behind it.")]
        [SerializeField, Range(0f, 1f)] float wakeWhiten = 0.18f;
        [SerializeField]
        LineTuning wake = new LineTuning(
            brightness: 1f, opacity: 0.62f, width: 0.10f, dash: 0.16f, gap: 0.18f, endFade: 0.08f);

        public Color LaneColor => laneColor;
        public LineTuning Lane => lane;
        public Color PreviewColor => previewColor;
        public LineTuning Preview => preview;
        public float CourseWhiten => courseWhiten;
        public LineTuning Course => course;
        public float WakeWhiten => wakeWhiten;
        public LineTuning Wake => wake;
    }
}
