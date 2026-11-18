using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// How the Darts' dots are drawn: their size and brightness, the trail of a fleet in transit, how
    /// wide its swarm spreads, and the shape of a battle's two fronts. One asset is shared by every
    /// mission. The dots take their owner's color from <see cref="Palette"/>; how many dots stand for a
    /// fleet and how fast they orbit are behavior (<see cref="DartDisplay"/>), and timing stays in code.
    /// <c>Squadra &gt; Rebuild Mission Scene</c> creates the asset with these defaults when it is missing
    /// and otherwise leaves it as edited.
    /// </summary>
    [CreateAssetMenu(menuName = "Squadra/Fleet Style", fileName = "FleetStyle")]
    public sealed class FleetStyle : ScriptableObject
    {
        [Header("Dots")]
        [Tooltip("Radius of one dot in world units. Shots, explosions and sparks are sized from it too.")]
        [SerializeField, Min(0f)] float dotRadius = 0.055f;
        [Tooltip("Color intensity of a stationed fleet's dots; above 1 they bloom.")]
        [SerializeField, Min(0f)] float dotBrightness = 1.5f;
        [Tooltip("Brightness of a fleet in transit or arriving to fight, relative to a stationed one.")]
        [SerializeField, Min(0f)] float movingBrightness = 1.15f;

        [Header("Transit")]
        [Tooltip("Width of a fleet's swarm in transit, in world units, however few dots it has.")]
        [SerializeField, Min(0f)] float swarmSpread = 0.14f;
        [Tooltip("Width the swarm gains per square root of its dots, in world units.")]
        [SerializeField, Min(0f)] float swarmSpreadPerDot = 0.045f;
        [Tooltip("Length of the swarm along its course, relative to its width.")]
        [SerializeField, Min(0f)] float swarmLength = 1.5f;
        [Tooltip("Height of the fleet's Dart count above its swarm, in world units.")]
        [SerializeField] float labelGap = 0.38f;
        [Tooltip("Distance behind each dot of the first trail dot, in world units.")]
        [SerializeField, Min(0f)] float nearTrailDistance = 0.1f;
        [Tooltip("Size of the first trail dot, relative to a dot.")]
        [SerializeField, Min(0f)] float nearTrailSize = 0.75f;
        [SerializeField, Range(0f, 1f)] float nearTrailOpacity = 0.38f;
        [Tooltip("Distance behind each dot of the second trail dot, in world units.")]
        [SerializeField, Min(0f)] float farTrailDistance = 0.2f;
        [Tooltip("Size of the second trail dot, relative to a dot.")]
        [SerializeField, Min(0f)] float farTrailSize = 0.55f;
        [SerializeField, Range(0f, 1f)] float farTrailOpacity = 0.16f;

        [Header("Battle")]
        [Tooltip("Gap between the defenders' front and the attackers' front, in world units.")]
        [SerializeField, Min(0f)] float frontGap = 0.5f;
        [Tooltip("How far a fighting dot strays from its place in the formation, in world units.")]
        [SerializeField, Min(0f)] float strayDistance = 0.1f;

        public float DotRadius => dotRadius;
        public float DotBrightness => dotBrightness;
        public float MovingBrightness => movingBrightness;

        public float SwarmSpread => swarmSpread;
        public float SwarmSpreadPerDot => swarmSpreadPerDot;
        public float SwarmLength => swarmLength;
        public float LabelGap => labelGap;
        public float NearTrailDistance => nearTrailDistance;
        public float NearTrailSize => nearTrailSize;
        public float NearTrailOpacity => nearTrailOpacity;
        public float FarTrailDistance => farTrailDistance;
        public float FarTrailSize => farTrailSize;
        public float FarTrailOpacity => farTrailOpacity;

        public float FrontGap => frontGap;
        public float StrayDistance => strayDistance;

        /// <summary>The width of a dot, in world units.</summary>
        public float DotSize => dotRadius * 2f;

        /// <summary>The color of a stationed dot of an owner whose color is <paramref name="owner"/>.</summary>
        public Vector4 StationedTint(Color owner) => Palette.Hdr(owner, dotBrightness, 1f);

        /// <summary>The color of a dot in transit, or arriving to fight, of an owner whose color is <paramref name="owner"/>.</summary>
        public Vector4 MovingTint(Color owner) => Palette.Hdr(owner, dotBrightness * movingBrightness, 1f);
    }
}
