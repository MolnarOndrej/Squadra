using System;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// How one kind of line looks: its width, its dashes, how it fades at the ends and how bright it is.
    /// Where it runs, what color it takes and how fast its dashes move are decided by whoever draws it.
    /// Edited in the Inspector of the style asset that holds it.
    /// </summary>
    [Serializable]
    public struct LineTuning
    {
        [Tooltip("Color intensity; above 1 the line blooms.")]
        [SerializeField, Min(0f)] float brightness;
        [SerializeField, Range(0f, 1f)] float opacity;
        [Tooltip("Width in world units.")]
        [SerializeField, Min(0f)] float width;
        [Tooltip("Length of each dash in world units; 0 is a solid line.")]
        [SerializeField, Min(0f)] float dash;
        [Tooltip("Gap between dashes in world units.")]
        [SerializeField, Min(0f)] float gap;
        [Tooltip("Length over which each end fades out, in world units.")]
        [SerializeField, Min(0f)] float endFade;

        public LineTuning(float brightness, float opacity, float width, float dash, float gap, float endFade)
        {
            this.brightness = brightness;
            this.opacity = opacity;
            this.width = width;
            this.dash = dash;
            this.gap = gap;
            this.endFade = endFade;
        }

        public float Brightness => brightness;
        public float Opacity => opacity;
        public float Width => width;
        public float Dash => dash;
        public float Gap => gap;
        public float EndFade => endFade;
    }
}
