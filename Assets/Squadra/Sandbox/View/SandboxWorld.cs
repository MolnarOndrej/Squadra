using Squadra.Sandbox;
using Squadra.Simulation;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// One world of the sandbox, drawn by the <see cref="WorldView"/> beside it exactly as a mission's
    /// world is. Here is what a mission file would say of it (its type, surface, size, owner and Darts;
    /// its place is the object's own) and what is already under way there when the sandbox starts: a
    /// capture and a battle. Edit Mode shows that moment frozen, and Play Mode plays on from it. The
    /// highlight is the one thing only Edit Mode shows; a run starts with nothing selected or hovered.
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
        [Tooltip("Darts stationed here. During a capture they are the captor's.")]
        [SerializeField, Min(0)] int darts;

        [Header("Edit Mode only")]
        [Tooltip("An outline as the pointer or a selection would draw it. A run starts without any.")]
        [SerializeField] SandboxHighlight highlight = SandboxHighlight.None;

        [Header("Under way at the start")]
        [Tooltip("A capture under way: the Darts stationed here are the captor's, and the world stays its "
            + "owner's until the capture completes. With no Darts here there is no capture.")]
        [SerializeField] bool capturing;
        [SerializeField] Faction captor = Faction.Blue;
        [SerializeField, Range(0f, 1f)] float captureProgress = 0.4f;
        [Tooltip("A battle: the attackers' Darts facing the ones stationed here, nobody lost yet. During a "
            + "capture the captor's Darts defend and the capture is held. With no attackers there is no "
            + "battle; with nobody stationed here the attackers land.")]
        [SerializeField] bool fighting;
        [SerializeField] Faction attacker = Faction.Red;
        [SerializeField, Min(0)] int attackingDarts = 12;
        [Tooltip("Where the two fronts meet, in degrees counter-clockwise from the right of the world.")]
        [SerializeField] float frontAngle;

        WorldView view;
        NodeKind dressedKind;
        WorldLooks dressedLooks;
        bool dressed;

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

        /// <summary>The outline Edit Mode shows.</summary>
        public SandboxHighlight Highlight => highlight;

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

        /// <summary>The capture under way here at the start, as world <paramref name="index"/>; null for none.</summary>
        public StartingCapture ToCapture(int index)
        {
            if (!capturing) return null;
            return new StartingCapture { World = index, Captor = captor, Progress = captureProgress };
        }

        /// <summary>The battle being fought here at the start, as world <paramref name="index"/>; null for none.</summary>
        public StartingBattle ToBattle(int index)
        {
            if (!fighting) return null;
            return new StartingBattle { World = index, Attacker = attacker, Darts = attackingDarts };
        }

        /// <summary>
        /// Gives the world the surface of <paramref name="shownKind"/> from <paramref name="looks"/>: its own
        /// surface when that is one of the kind, else the kind's first. A run that changes the world's type
        /// passes the new one; nothing here is changed by it. Without a surface for the kind it keeps the one
        /// it has.
        /// </summary>
        public void Dress(WorldLooks looks, NodeKind shownKind)
        {
            if (looks != null && looks.TryGetSurfaceOrFirst(shownKind, surface, out Material material))
                View.Setup(worldName, transform.position, size, material);
            dressedKind = shownKind;
            dressedLooks = looks;
            dressed = true;
        }

        /// <summary>Dresses the world again only when its kind or the looks differ from what it was dressed for.</summary>
        public void DressIfChanged(WorldLooks looks, NodeKind shownKind)
        {
            if (!dressed || shownKind != dressedKind || looks != dressedLooks) Dress(looks, shownKind);
        }

        /// <summary>
        /// Has the world drawn, in Edit Mode, with its own type's surface from <paramref name="looks"/> and
        /// showing <paramref name="state"/> with its highlight.
        /// </summary>
        public void Present(WorldLooks looks, WorldState state)
        {
            Dress(looks, kind);
            View.ShowSample(state, highlight == SandboxHighlight.Selected, highlight == SandboxHighlight.Hovered);
        }
    }
}
