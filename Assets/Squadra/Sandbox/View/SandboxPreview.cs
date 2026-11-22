using System.Collections.Generic;
using Squadra.Sandbox;
using Squadra.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Squadra
{
    /// <summary>
    /// The Sandbox scene's starting moment, as its author lays it down, and its picture in Edit Mode, so
    /// that how worlds, rings, fleets, routes and the backdrop look can be tuned in context. It is drawn
    /// by the game's own renderers with the shared looks: <see cref="WorldView"/> for the worlds,
    /// <see cref="RouteLanes"/> for the lanes and the order preview, and the mission fleet layer's swarms
    /// and formations for the Darts, so an edit to a shared style shows here and in every mission alike.
    ///
    /// The worlds are the <see cref="SandboxWorld"/> objects under the worlds root, moved and edited like
    /// any scene object, with the captures and battles under way at them. Here are the routes between
    /// them, the fleets already on their way, the order preview, the backdrop and the sandbox's own
    /// behavior overrides; all of it is saved with the scene and read into one <see cref="SandboxSetup"/>.
    /// Edit Mode shows the first moment of a <see cref="SandboxSession"/> set up from it, frozen: nothing is
    /// produced, captured, fought or flown, and nothing makes a sound. In Play Mode the scene's
    /// <see cref="SandboxRun"/> plays on from that same moment and this draws nothing. What of the setup
    /// cannot be laid down, such as a fleet without a route, is named in a warning and left out.
    ///
    /// The lines, dots and labels are made afresh when the scene opens, after scripts reload and when the
    /// setup changes in shape, and are never saved: they live in a separate object marked as not saved.
    /// </summary>
    [ExecuteAlways]
    public sealed class SandboxPreview : MonoBehaviour
    {
        const string PreviewName = "Sandbox Preview (made on load, not saved)";
        const HideFlags Transient = HideFlags.DontSave | HideFlags.NotEditable;

        // The sandbox's three players, every one of them under the user's orders.
        static readonly Faction[] Players = { Faction.Blue, Faction.Red, Faction.Green };

        [Header("Shared looks")]
        [Tooltip("The surfaces and space backdrops of the game.")]
        [SerializeField] WorldLooks looks;
        [SerializeField] RouteStyle routeStyle;
        [SerializeField] FleetStyle fleetStyle;
        [SerializeField] Material lineMaterial;
        [SerializeField] Material dotMaterial;

        [Header("Scene")]
        [SerializeField] CameraRig rig;
        [SerializeField] Backdrop backdrop;
        [Tooltip("Every active SandboxWorld under it is a world of the sandbox, in hierarchy order.")]
        [SerializeField] Transform worldsRoot;
        [Tooltip("Inactive template copied for the Dart count of each fleet on its way.")]
        [SerializeField] TextMeshPro fleetLabelTemplate;

        [Header("Starting setup")]
        [Tooltip("ID of the space backdrop in the world looks, such as space.")]
        [SerializeField] string backdropId = "space";
        [SerializeField] SandboxRoute[] routes = new SandboxRoute[0];
        [Tooltip("Edit Mode only: where the preview of an order about to be given starts. Leave either end "
            + "empty for none.")]
        [SerializeField] SandboxWorld orderFrom;
        [Tooltip("Edit Mode only: where the preview of an order about to be given ends.")]
        [SerializeField] SandboxWorld orderTo;
        [Tooltip("Fleets already on their way when the sandbox starts.")]
        [SerializeField] SandboxFleet[] fleets = new SandboxFleet[0];

        [Header("Behavior")]
        [SerializeField] SandboxBehavior behavior = new SandboxBehavior();

        readonly List<SandboxWorld> worlds = new List<SandboxWorld>();
        readonly List<WorldView> views = new List<WorldView>();
        readonly List<WorldState> states = new List<WorldState>();
        readonly List<int> shape = new List<int>();
        readonly List<int> builtShape = new List<int>();
        readonly HashSet<Route> busy = new HashSet<Route>();

        GameObject previewRoot;
        RouteLanes lanes;
        SandboxDarts darts;
        Material shownBackdrop;
        string reportedProblems;

        public WorldLooks Looks => looks;
        public RouteStyle RouteStyle => routeStyle;
        public Material LineMaterial => lineMaterial;
        public CameraRig Rig => rig;

        /// <summary>The behavior overrides saved with the scene.</summary>
        public SandboxBehavior Behavior => behavior;

        void OnEnable()
        {
            if (Application.isPlaying) return;

            shownBackdrop = null;
            Redraw();
        }

        void OnDisable()
        {
            Clear();
        }

        void OnValidate()
        {
            string problem = behavior.DartsProblem();
            if (problem != null) Debug.LogWarning("Squadra: the sandbox's " + problem, this);
        }

        void Update()
        {
            if (!Application.isPlaying) Redraw();
        }

        // ------------------------------------------------------------------ the starting setup

        /// <summary>
        /// The starting moment as the scene lays it down now: the sandbox's players, its active worlds in
        /// hierarchy order (also put into <paramref name="sceneWorlds"/>), their routes, the captures and
        /// battles under way at them, the fleets on their way and the rules overridden. An entry that names
        /// a world that is missing or inactive is kept with no world, for the session to name and leave out.
        /// </summary>
        public SandboxSetup Setup(List<SandboxWorld> sceneWorlds)
        {
            sceneWorlds.Clear();
            if (worldsRoot != null) worldsRoot.GetComponentsInChildren(false, sceneWorlds);

            var setup = new SandboxSetup();
            setup.Players.AddRange(Players);
            for (int i = 0; i < sceneWorlds.Count; i++)
            {
                SandboxWorld world = sceneWorlds[i];
                setup.Worlds.Add(world.ToNode());
                StartingCapture capture = world.ToCapture(i);
                if (capture != null) setup.Captures.Add(capture);
                StartingBattle battle = world.ToBattle(i);
                if (battle != null) setup.Battles.Add(battle);
            }

            foreach (SandboxRoute route in routes)
                setup.Routes.Add(new Route(IndexOf(sceneWorlds, route.From), IndexOf(sceneWorlds, route.To)));

            foreach (SandboxFleet fleet in fleets)
            {
                setup.Fleets.Add(new StartingFleet
                {
                    Faction = fleet.Faction,
                    Darts = fleet.Darts,
                    From = IndexOf(sceneWorlds, fleet.From),
                    To = IndexOf(sceneWorlds, fleet.To),
                    Progress = fleet.Progress,
                });
            }

            setup.Rules = behavior.Rules();
            return setup;
        }

        /// <summary>Where the fronts of a battle at each of <paramref name="sceneWorlds"/> meet, in radians.</summary>
        public static float[] FrontAngles(List<SandboxWorld> sceneWorlds)
        {
            var angles = new float[sceneWorlds.Count];
            for (int i = 0; i < angles.Length; i++)
                angles[i] = sceneWorlds[i].FrontAngle;
            return angles;
        }

        /// <summary>Shows the space backdrop the setup names, unless it is already shown.</summary>
        public void ShowBackdrop()
        {
            if (backdrop == null || looks == null) return;
            if (!looks.TryGetBackdrop(backdropId, out Material material) || material == shownBackdrop) return;

            backdrop.Show(material);
            shownBackdrop = material;
        }

        /// <summary>Names in the Console what of the setup was left out, once for each set of problems.</summary>
        public void Report(IReadOnlyList<string> problems)
        {
            string all = string.Join("\n", problems);
            if (all == reportedProblems) return;

            reportedProblems = all;
            foreach (string problem in problems)
                Debug.LogWarning("Squadra: in the sandbox's setup, " + problem, this);
        }

        static int IndexOf(List<SandboxWorld> sceneWorlds, SandboxWorld world)
        {
            return world != null ? sceneWorlds.IndexOf(world) : -1;
        }

        // ------------------------------------------------------------------ the Edit Mode picture

        /// <summary>
        /// Draws the starting moment as it is now. It runs every frame the scene updates outside Play Mode;
        /// the editor also calls it after an edit, which outside Play Mode does not always bring an update.
        /// </summary>
        public void Redraw()
        {
            if (Application.isPlaying || worldsRoot == null || routeStyle == null || fleetStyle == null) return;

            var session = new SandboxSession(Setup(worlds));
            Report(session.Problems);
            Match match = session.Match;

            views.Clear();
            states.Clear();
            for (int i = 0; i < worlds.Count; i++)
            {
                views.Add(worlds[i].View);
                WorldState state = WorldState.Of(match.Nodes[i]);
                states.Add(state);
                worlds[i].Present(looks, state);
            }

            if (views.Count > 0 && rig != null) rig.Fit(Battlefield.Frame(views));
            ShowBackdrop();

            if (!SameShape(match)) Rebuild(match);

            busy.Clear();
            foreach (Fleet fleet in match.Fleets)
                busy.Add(new Route(fleet.SourceId, fleet.TargetId));

            lanes.Draw(IndexOf(worlds, orderFrom), IndexOf(worlds, orderTo), busy);
            DrawDarts(match);
        }

        void DrawDarts(Match match)
        {
            DartDisplay display = behavior.Darts();
            darts.Begin(fleetStyle);
            for (int i = 0; i < worlds.Count; i++)
                darts.Garrison(views[i], states[i], worlds[i].FrontAngle, display, fleetStyle);

            for (int slot = 0; slot < match.Fleets.Count; slot++)
            {
                Fleet fleet = match.Fleets[slot];
                darts.Transit(slot, views[fleet.SourceId], views[fleet.TargetId], states[fleet.TargetId],
                    fleet.Faction, fleet.Darts, fleet.Progress, fleet.Duration, display, fleetStyle, routeStyle);
            }

            darts.End();
        }

        // ------------------------------------------------------------------ the objects drawn with

        // The lines, dots and labels are made for these worlds, routes and fleets; other ones need others.
        bool SameShape(Match match)
        {
            shape.Clear();
            foreach (SandboxWorld world in worlds)
                shape.Add(world.GetHashCode());
            foreach (Route route in match.Routes)
            {
                shape.Add(route.A);
                shape.Add(route.B);
            }

            shape.Add(-1 - fleets.Length);

            if (previewRoot == null || shape.Count != builtShape.Count) return false;
            for (int i = 0; i < shape.Count; i++)
            {
                if (shape[i] != builtShape[i]) return false;
            }

            return true;
        }

        void Rebuild(Match match)
        {
            Clear();

            // Kept apart from the scene's own objects, so that it can go whenever the sandbox does.
            previewRoot = new GameObject(PreviewName) { hideFlags = Transient };
            SceneManager.MoveGameObjectToScene(previewRoot, gameObject.scene);
            Transform parent = previewRoot.transform;
            lanes = new RouteLanes(new List<Route>(match.Routes), views, parent, lineMaterial, routeStyle);
            darts = new SandboxDarts(parent, dotMaterial, lineMaterial, fleetLabelTemplate, fleets.Length);

            foreach (Transform part in previewRoot.GetComponentsInChildren<Transform>(true))
            {
                part.gameObject.hideFlags = Transient;
                foreach (Component component in part.GetComponents<Component>())
                    component.hideFlags = Transient;
            }

            builtShape.Clear();
            builtShape.AddRange(shape);
        }

        void Clear()
        {
            darts?.Dispose();
            darts = null;
            lanes = null;
            builtShape.Clear();
            if (previewRoot == null) return;

            if (Application.isPlaying) Destroy(previewRoot);
            else DestroyImmediate(previewRoot);
            previewRoot = null;
        }
    }
}
