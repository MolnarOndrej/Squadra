using Squadra.Simulation;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// One world of the sandbox, drawn by the <see cref="WorldView"/> beside it exactly as a mission's
    /// world is. Here is what a mission file would say of it (its type, surface, size, owner and Darts;
    /// its place is the object's own) and the states only a running match could bring, set by hand to see
    /// how they look: a highlight, a capture under way and a battle. Those are a preview and nothing more:
    /// no match ever starts from them, in the editor or in Play Mode.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WorldView))]
    public sealed class SandboxWorld : MonoBehaviour
    {
        [Header("World")]
        [SerializeField] string worldName = "World";
        [SerializeField] NodeKind kind = NodeKind.Planet;
        [Tooltip("ID of the surface in the world looks, such as thalassa or lune. Empty, or not a surface of "
            + "this type, for the type's first one.")]
        [SerializeField] string surface = string.Empty;
        [Tooltip("Radius of the body in game world units, as a mission file gives it.")]
        [SerializeField, Range(0.1f, 5f)] float size = 1f;
        [SerializeField] Faction owner = Faction.Neutral;
        [Tooltip("Darts stationed here.")]
        [SerializeField, Min(0)] int darts;

        [Header("Preview only, never played")]
        [SerializeField] SandboxHighlight highlight = SandboxHighlight.None;
        [Tooltip("Shows a capture under way: the captor's ring and countdown, its Darts as the ones stationed here.")]
        [SerializeField] bool capturing;
        [SerializeField] Faction captor = Faction.Blue;
        [SerializeField, Range(0f, 1f)] float captureProgress = 0.4f;
        [Tooltip("Shows a battle: the attackers' formation facing the Darts stationed here. With a capture, "
            + "the capture is shown held.")]
        [SerializeField] bool fighting;
        [SerializeField] Faction attacker = Faction.Red;
        [SerializeField, Min(0)] int attackingDarts = 12;
        [Tooltip("Where the two fronts meet, in degrees counter-clockwise from the right of the world.")]
        [SerializeField] float frontAngle;

        WorldView view;

        public WorldView View
        {
            get
            {
                if (view == null) view = GetComponent<WorldView>();
                return view;
            }
        }

        /// <summary>The direction the attackers stand in, in radians.</summary>
        public float FrontAngle => frontAngle * Mathf.Deg2Rad;

        /// <summary>The world as a mission would lay it down, with the game's defaults for its type.</summary>
        public NodeConfig ToNode()
        {
            var position = new MapPoint(transform.position.x, transform.position.y);
            switch (kind)
            {
                case NodeKind.Moon: return NodeConfig.Moon(worldName, position, owner, darts);
                case NodeKind.Asteroid: return NodeConfig.Asteroid(worldName, position, owner, darts);
                default: return NodeConfig.Planet(worldName, position, owner, darts);
            }
        }

        /// <summary>
        /// What the world shows: <paramref name="node"/>, the world as a match that has not started has it,
        /// with the capture and battle of this sample on top. A capture by the owner, or a battle with the
        /// side stationed here, cannot happen and is left out.
        /// </summary>
        public WorldState Sample(Node node, double captureDuration)
        {
            WorldState state = WorldState.Of(node);
            if (capturing && captor != Faction.Neutral && captor != state.Owner)
            {
                state.Captor = captor;
                state.Occupant = captor;
                state.CaptureProgress = captureProgress;
                state.CaptureRemaining = (float)((1.0 - captureProgress) * captureDuration);
            }

            if (fighting && attacker != Faction.Neutral && attacker != state.Occupant)
            {
                state.Fighting = true;
                state.Arriving = attacker;
                state.ArrivingDarts = attackingDarts;
            }

            return state;
        }

        /// <summary>
        /// Has the world drawn with its surface from <paramref name="looks"/>, showing <paramref name="state"/>.
        /// Without a surface for its type it keeps the one it has.
        /// </summary>
        public void Present(WorldLooks looks, WorldState state)
        {
            WorldView world = View;
            if (looks != null && looks.TryGetSurfaceOrFirst(kind, surface, out Material material))
                world.Setup(worldName, transform.position, size, material);

            world.ShowSample(state, highlight == SandboxHighlight.Selected, highlight == SandboxHighlight.Hovered);
        }
    }
}
