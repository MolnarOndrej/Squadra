using System.Collections.Generic;
using Squadra.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Squadra
{
    /// <summary>
    /// The Sandbox scene's battlefield, shown without pressing Play so that how worlds, rings, fleets,
    /// routes and the backdrop look can be tuned in context. It is drawn by the game's own renderers with
    /// the shared looks: <see cref="WorldView"/> for the worlds, <see cref="RouteLanes"/> for the lanes
    /// and the order preview, and the mission fleet layer's swarms and formations for the Darts, so an
    /// edit to a shared style shows here and in every mission alike.
    ///
    /// The worlds are the <see cref="SandboxWorld"/> objects under the worlds root, moved and edited like
    /// any scene object. Here are the routes between them, the order preview, the fleets held on their
    /// way, the backdrop and the sandbox's own behavior overrides; all of it is saved with the scene. The
    /// rule values the samples show, such as a world's production or a flight's length, come from a match
    /// set up from the samples with the game's defaults, or the overrides, and never started: nothing is
    /// produced, captured, fought or flown, and nothing makes a sound, in the editor or in Play Mode.
    ///
    /// The lines, dots and labels are made afresh when the scene opens, after scripts reload and when the
    /// samples change in number, and are never saved: they live in a separate object marked as not saved.
    /// </summary>
    [ExecuteAlways]
    public sealed class SandboxPreview : MonoBehaviour
    {
        const string PreviewName = "Sandbox Preview (made on load, not saved)";
        const HideFlags Transient = HideFlags.DontSave | HideFlags.NotEditable;

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

        [Header("Samples")]
        [Tooltip("ID of the space backdrop in the world looks, such as space.")]
        [SerializeField] string backdropId = "space";
        [SerializeField] SandboxRoute[] routes = new SandboxRoute[0];
        [Tooltip("Where the preview of an order about to be given starts. Leave either end empty for none.")]
        [SerializeField] SandboxWorld orderFrom;
        [Tooltip("Where the preview of an order about to be given ends.")]
        [SerializeField] SandboxWorld orderTo;
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

        void OnEnable()
        {
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
            Redraw();
        }

        /// <summary>
        /// Draws the samples as they are now. It runs every frame the scene updates; the editor also calls
        /// it after an edit, which outside Play Mode does not always bring an update with it.
        /// </summary>
        public void Redraw()
        {
            if (worldsRoot == null || routeStyle == null || fleetStyle == null) return;

            worlds.Clear();
            worldsRoot.GetComponentsInChildren(false, worlds);
            views.Clear();
            foreach (SandboxWorld world in worlds)
                views.Add(world.View);

            MatchOverrides rules = behavior.Rules();
            Match match = SetUp(rules);
            states.Clear();
            for (int i = 0; i < worlds.Count; i++)
            {
                WorldState state = worlds[i].Sample(match.Nodes[i], match.CaptureDuration);
                states.Add(state);
                worlds[i].Present(looks, state);
            }

            if (views.Count > 0 && rig != null) rig.Fit(Battlefield.Frame(views));
            ShowBackdrop();

            if (!SameShape(match)) Rebuild(match);

            busy.Clear();
            foreach (SandboxFleet fleet in fleets)
            {
                if (TryGetFlight(fleet, out int source, out int target)) busy.Add(new Route(source, target));
            }

            lanes.Draw(IndexOf(orderFrom), IndexOf(orderTo), busy);
            DrawDarts(match);
        }

        // A match laid down as a mission would be, from the samples, by the game's rules but for the
        // overridden ones. It is never started: only what it says before the first moment is read.
        Match SetUp(MatchOverrides rules)
        {
            var config = new MatchConfig();
            foreach (SandboxWorld world in worlds)
                config.Nodes.Add(world.ToNode());

            foreach (SandboxRoute route in routes)
            {
                int a = IndexOf(route.From);
                int b = IndexOf(route.To);
                if (a >= 0 && b >= 0 && a != b) config.Connect(a, b);
            }

            rules.ApplyTo(config);
            return new Match(config);
        }

        void DrawDarts(Match match)
        {
            DartDisplay display = behavior.Darts();
            darts.Begin(fleetStyle);
            for (int i = 0; i < worlds.Count; i++)
                darts.Garrison(views[i], states[i], worlds[i].FrontAngle, display, fleetStyle);

            for (int slot = 0; slot < fleets.Length; slot++)
            {
                SandboxFleet fleet = fleets[slot];
                if (!TryGetFlight(fleet, out int source, out int target)) continue;

                darts.Transit(slot, views[source], views[target], states[target], fleet.Faction, fleet.Darts,
                    fleet.Progress, match.TravelTime(source, target), display, fleetStyle, routeStyle);
            }

            darts.End();
        }

        void ShowBackdrop()
        {
            if (backdrop == null || looks == null) return;
            if (!looks.TryGetBackdrop(backdropId, out Material material) || material == shownBackdrop) return;

            backdrop.Show(material);
            shownBackdrop = material;
        }

        bool TryGetFlight(SandboxFleet fleet, out int source, out int target)
        {
            source = IndexOf(fleet.From);
            target = IndexOf(fleet.To);
            return source >= 0 && target >= 0 && source != target && fleet.Darts > 0;
        }

        int IndexOf(SandboxWorld world)
        {
            return world != null ? worlds.IndexOf(world) : -1;
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
