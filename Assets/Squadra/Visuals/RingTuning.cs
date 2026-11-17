using System;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// How one kind of ring looks in one state: its band, its glow, its dashes and how bright it is.
    /// Where it sits, what color it takes and how fast it turns are decided by whoever draws it. Edited
    /// in the Inspector of the style asset that holds it.
    /// </summary>
    [Serializable]
    public struct RingTuning
    {
        [Tooltip("Width of the band, in world units.")]
        [SerializeField] float thickness;
        [Tooltip("Width of the glow around the band, in world units.")]
        [SerializeField] float softness;
        [SerializeField, Range(0f, 2f)] float glow;
        [Tooltip("Number of dashes around the ring; 0 is a solid ring.")]
        [SerializeField, Min(0f)] float dashes;
        [Tooltip("Share of each dash that is drawn.")]
        [SerializeField, Range(0f, 1f)] float dashFill;
        [Tooltip("Opacity of the part of the ring not yet filled, such as the rest of a capture.")]
        [SerializeField, Range(0f, 1f)] float track;
        [Tooltip("0 blends normally, 1 adds light.")]
        [SerializeField, Range(0f, 1f)] float additive;
        [Tooltip("Color intensity; above 1 the ring blooms.")]
        [SerializeField, Min(0f)] float brightness;
        [SerializeField, Range(0f, 1f)] float opacity;

        public RingTuning(float thickness, float softness, float glow, float dashes, float dashFill, float track,
            float additive, float brightness, float opacity)
        {
            this.thickness = thickness;
            this.softness = softness;
            this.glow = glow;
            this.dashes = dashes;
            this.dashFill = dashFill;
            this.track = track;
            this.additive = additive;
            this.brightness = brightness;
            this.opacity = opacity;
        }

        public float Thickness => thickness;
        public float Softness => softness;
        public float Glow => glow;
        public float Dashes => dashes;
        public float DashFill => dashFill;
        public float Track => track;
        public float Additive => additive;
        public float Brightness => brightness;
        public float Opacity => opacity;

        /// <summary>The look part of the way from <paramref name="a"/> to <paramref name="b"/>, value by value.</summary>
        public static RingTuning Lerp(RingTuning a, RingTuning b, float t)
        {
            return new RingTuning(
                Mathf.Lerp(a.thickness, b.thickness, t),
                Mathf.Lerp(a.softness, b.softness, t),
                Mathf.Lerp(a.glow, b.glow, t),
                Mathf.Lerp(a.dashes, b.dashes, t),
                Mathf.Lerp(a.dashFill, b.dashFill, t),
                Mathf.Lerp(a.track, b.track, t),
                Mathf.Lerp(a.additive, b.additive, t),
                Mathf.Lerp(a.brightness, b.brightness, t),
                Mathf.Lerp(a.opacity, b.opacity, t));
        }
    }
}
