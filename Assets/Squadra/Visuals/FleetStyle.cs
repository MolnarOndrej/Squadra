using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// How the Darts are drawn: the shape and material of one Dart and its size and brightness, how far
    /// apart they stand in orbit and in a battle formation, the trail of a fleet in transit, how wide its
    /// swarm spreads, and the shape of a battle's two fronts. One asset is shared by every mission and
    /// the sandbox. A Dart takes its owner's color from <see cref="Palette"/>; how many Darts are drawn
    /// for a fleet and how fast they orbit are behavior (<see cref="DartDisplay"/>), and timing stays in
    /// code. <c>Squadra &gt; Rebuild Mission Scene</c> creates the asset with these defaults when it is
    /// missing and otherwise leaves it as edited.
    ///
    /// The Dart mesh and material are assets, never a look written in code: the material lives in
    /// <c>Visuals/Materials/Darts</c> and the mesh in <c>Visuals/Meshes</c>. The rebuild creates the round
    /// dot there when they are missing and assigns them only where the style has none. A style without a
    /// usable mesh or material is an error (<see cref="DartProblem"/>), as a world without its surface is:
    /// a mission does not start, and the sandbox draws no Darts until it is fixed.
    /// </summary>
    [CreateAssetMenu(menuName = "Squadra/Fleet Style", fileName = "FleetStyle")]
    public sealed class FleetStyle : ScriptableObject
    {
        /// <summary>
        /// The most vertices a Dart mesh may have. Every Dart drawn is a copy of it, rebuilt each frame,
        /// so a detailed model would cost far more than a Dart a few pixels across can show.
        /// </summary>
        public const int MostMeshVertices = 256;

        [Header("Darts")]
        [Tooltip("The shape of one Dart, nose along +X and top toward the camera (or turned so by the "
            + "rotation below); a mesh one unit long is drawn twice the dot radius long. Required: "
            + "Visuals/Meshes/Dart is the round dot. Needs Read/Write enabled in its import settings and at most "
            + "256 vertices. Trails, shots, explosions and sparks stay dots.")]
        [SerializeField] Mesh dartMesh;
        [Tooltip("Turns the Dart mesh, in degrees, so that its nose points along +X and its top faces the camera.")]
        [SerializeField] Vector3 dartMeshRotation;
        [Tooltip("What a Dart is drawn with. Required: one of Visuals/Materials/Darts, such as Dart, the round "
            + "dot. The owner's color reaches it as vertex color and, where the shader has one, as _BaseColor, "
            + "_Color or _Tint; the material itself is never changed.")]
        [SerializeField] Material dartMaterial;
        [Tooltip("Radius of one dot in world units, and so the size of a Dart. Shots, explosions and sparks are "
            + "sized from it too.")]
        [SerializeField, Min(0f)] float dotRadius = 0.055f;
        [Tooltip("Color intensity of a stationed fleet's Darts; above 1 they bloom.")]
        [SerializeField, Min(0f)] float dotBrightness = 1.5f;
        [Tooltip("Brightness of a fleet in transit or arriving to fight, relative to a stationed one.")]
        [SerializeField, Min(0f)] float movingBrightness = 1.15f;

        [Header("Formations")]
        [Tooltip("Distance between a world's three orbit lanes, in world units.")]
        [SerializeField, Min(0f)] float laneSpacing = 0.13f;
        [Tooltip("Distance between neighbouring Darts along a battle front, in world units.")]
        [SerializeField, Min(0.01f)] float frontSpacing = 0.15f;
        [Tooltip("Distance between the rows of a battle formation, in world units.")]
        [SerializeField, Min(0f)] float rowSpacing = 0.14f;

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

        /// <summary>The mesh a Dart is drawn with; usable only when <see cref="DartProblem"/> finds nothing wrong.</summary>
        public Mesh DartMesh => dartMesh;

        public Quaternion DartMeshRotation => Quaternion.Euler(dartMeshRotation);

        /// <summary>The material a Dart is drawn with; usable only when <see cref="DartProblem"/> finds nothing wrong.</summary>
        public Material DartMaterial => dartMaterial;

        public float DotRadius => dotRadius;
        public float DotBrightness => dotBrightness;
        public float MovingBrightness => movingBrightness;

        public float LaneSpacing => laneSpacing;
        public float FrontSpacing => Mathf.Max(0.01f, frontSpacing);
        public float RowSpacing => rowSpacing;

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

        /// <summary>Why the Darts cannot be drawn with this style, or null when they can.</summary>
        public string DartProblem()
        {
            if (dartMesh == null)
                return name + " has no Dart mesh. Assign one; Visuals/Meshes/Dart is the round dot, and "
                    + "Squadra > Rebuild Mission Scene assigns it to a style that has none.";
            if (dartMaterial == null)
                return name + " has no Dart material. Assign one from Visuals/Materials/Darts; Squadra > "
                    + "Rebuild Mission Scene assigns that folder's first to a style that has none.";

            string problem = MeshProblem(dartMesh);
            if (problem != null) return name + " cannot draw Darts with the mesh " + dartMesh.name + ": " + problem + ".";
            problem = MaterialProblem(dartMaterial);
            if (problem != null) return name + " cannot draw Darts with the material " + dartMaterial.name + ": " + problem + ".";
            return null;
        }

        /// <summary>Why <paramref name="mesh"/> cannot be a Dart, or null when it can.</summary>
        static string MeshProblem(Mesh mesh)
        {
            if (!mesh.isReadable) return "it is not readable; turn on Read/Write in its import settings";
            if (mesh.vertexCount == 0) return "it has no vertices";
            if (mesh.vertexCount > MostMeshVertices)
                return "it has " + mesh.vertexCount + " vertices, more than the " + MostMeshVertices + " a Dart may have";
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                if (mesh.GetTopology(i) != MeshTopology.Triangles) return "it is not made of triangles";
            }

            return null;
        }

        /// <summary>Why <paramref name="material"/> cannot draw a Dart, or null when it can.</summary>
        static string MaterialProblem(Material material)
        {
            Shader shader = material.shader;
            if (shader == null || !shader.isSupported) return "its shader cannot run with this render pipeline";
            return null;
        }
    }
}
