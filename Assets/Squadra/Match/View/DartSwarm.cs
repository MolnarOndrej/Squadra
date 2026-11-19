using System.Collections.Generic;
using Squadra.Simulation;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// The Darts of one fleet in transit: a loose teardrop, wide at the front and trailing off behind, that
    /// fans out after launch and gathers again before arrival, each Dart facing its course with two fading
    /// trail dots behind it. It flies from the source's orbit to the target's, and stops short of the target's orbit when it
    /// is to form up outside it for a battle. Where it is comes from the fleet's progress; how it looks
    /// from the shared <see cref="FleetStyle"/>. The swarm's shape is the same every time for the same seed.
    /// </summary>
    sealed class DartSwarm
    {
        readonly Vector2[] offsets;
        readonly Vector2[] positions;
        readonly float[] phases;

        /// <summary>
        /// Shapes the swarm of <paramref name="dots"/> dots for the fleet <paramref name="seed"/> stands for,
        /// on its way from <paramref name="source"/> to <paramref name="target"/>.
        /// </summary>
        public DartSwarm(int seed, int dots, WorldView source, WorldView target, FleetStyle style)
        {
            Vector2 delta = target.Position - source.Position;
            Direction = delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector2.right;
            Start = source.Position + Direction * source.OrbitRadius;
            End = target.Position - Direction * target.OrbitRadius;

            var random = new System.Random(seed * 7919 + 17);
            float spread = Spread(dots, style);
            offsets = new Vector2[dots];
            phases = new float[dots];
            positions = new Vector2[dots];
            for (int i = 0; i < dots; i++)
            {
                // A loose teardrop: wide at the front, trailing off behind.
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float radius = Mathf.Sqrt((float)random.NextDouble());
                float along = Mathf.Cos(angle) * radius;
                float across = Mathf.Sin(angle) * radius * Mathf.Lerp(0.35f, 0.8f, (along + 1f) * 0.5f);
                offsets[i] = new Vector2(along * spread * style.SwarmLength, across * spread);
                phases[i] = (float)random.NextDouble() * Mathf.PI * 2f;
            }
        }

        /// <summary>Where the fleet set out from: the source's orbit.</summary>
        public Vector2 Start { get; }

        /// <summary>Where it is headed: the target's orbit.</summary>
        public Vector2 End { get; }

        public Vector2 Direction { get; }

        /// <summary>Where each dot was last placed.</summary>
        public IReadOnlyList<Vector2> Positions => positions;

        /// <summary>
        /// 1 when the fleet is heading for the outer formation of a battle, 0 when it lands on the orbit;
        /// whoever moves the swarm eases it between the two.
        /// </summary>
        public float Standoff;

        /// <summary>
        /// True when a fleet of <paramref name="fleet"/> will form up outside the orbit of a world in
        /// <paramref name="target"/>'s state, facing a force stationed there.
        /// </summary>
        public static bool FormsUpOutside(Faction fleet, WorldState target)
        {
            if (target.Fighting) return target.Arriving == fleet;
            return target.Darts > 0 && target.Occupant != fleet;
        }

        /// <summary>
        /// Places the dots for a fleet <paramref name="progress"/> of the way along its course,
        /// <paramref name="elapsed"/> seconds after it set out and <paramref name="remaining"/> before it
        /// arrives, and lays its course lines. <paramref name="center"/> is the middle of the swarm, and
        /// <paramref name="formation"/> how far it has fanned out, from 0.3 to 1.
        /// </summary>
        public void Place(float progress, double elapsed, double remaining, float time, FleetStyle style,
            FleetCourse course, out Vector2 center, out float formation)
        {
            float standoff = (style.FrontGap + style.RowSpacing) * Mathf.SmoothStep(0f, 1f, Standoff);
            Vector2 end = End - Direction * standoff;
            center = Vector2.Lerp(Start, end, progress);
            course.Follow(Start, center, end);
            var normal = new Vector2(-Direction.y, Direction.x);

            // The swarm fans out after launch and gathers again before arrival.
            float opening = Mathf.Clamp01((float)elapsed / 0.45f);
            float closing = Mathf.Clamp01((float)remaining / 0.3f);
            formation = Mathf.Lerp(0.3f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Min(opening, closing)));

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector2 offset = offsets[i] * formation;
                float wobble = 0.03f * Mathf.Sin(time * 3.1f + phases[i]);
                positions[i] = center + Direction * offset.x + normal * (offset.y + wobble);
            }
        }

        /// <summary>
        /// Draws the Darts where they were last placed, in the color of <paramref name="faction"/>: their
        /// bodies into <paramref name="bodies"/>, facing the course, and their trails into <paramref name="trails"/>.
        /// </summary>
        public void Draw(DartBatch bodies, DotBatch trails, Faction faction, FleetStyle style)
        {
            Vector4 color = style.MovingTint(Palette.Of(faction));
            Vector4 trail = color;
            float dotSize = style.DotSize;
            float heading = Mathf.Atan2(Direction.y, Direction.x);
            foreach (Vector2 position in positions)
            {
                bodies.Add(position, heading, dotSize, color);

                trail.w = style.NearTrailOpacity;
                trails.Add(position - Direction * style.NearTrailDistance, dotSize * style.NearTrailSize, trail);
                trail.w = style.FarTrailOpacity;
                trails.Add(position - Direction * style.FarTrailDistance, dotSize * style.FarTrailSize, trail);
            }
        }

        /// <summary>Where the fleet's Dart count floats: above the swarm, clear of how far it has fanned out.</summary>
        public Vector2 LabelPosition(Vector2 center, float formation, FleetStyle style)
        {
            return center + Vector2.up * (Spread(offsets.Length, style) * formation + style.LabelGap);
        }

        // How wide the swarm of a fleet of this many dots is in transit, before it fans out and after it gathers.
        static float Spread(int dots, FleetStyle style)
        {
            return style.SwarmSpread + style.SwarmSpreadPerDot * Mathf.Sqrt(dots);
        }
    }
}
