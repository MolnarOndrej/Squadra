using System.Collections.Generic;
using Squadra.Simulation;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// The dashed lines between worlds: an idle lane for every route of the mission, and the preview of
    /// the order the player is about to give, which only a route allows. A lane that carries a fleet or
    /// the preview is hidden, because the animated line drawn over it would fill its gaps. How both look
    /// comes from the shared <see cref="RouteStyle"/>.
    /// </summary>
    sealed class RouteLanes
    {
        readonly IReadOnlyList<WorldView> worlds;
        readonly Faction player;
        readonly Dictionary<Route, int> laneOf = new Dictionary<Route, int>();
        readonly LineStyle[] idleLanes;
        readonly bool[] activeLanes;
        readonly LineStyle orderPreview;
        readonly RouteStyle style;

        // How fast the preview's dashes scroll toward the target, in world units per second.
        const float PreviewScroll = 1.15f;

        /// <summary>Creates the lines for the match's routes as children of <paramref name="parent"/>.</summary>
        public RouteLanes(Match match, IReadOnlyList<WorldView> worlds, Faction player, Transform parent,
            Material lineMaterial, RouteStyle style)
        {
            this.worlds = worlds;
            this.player = player;
            this.style = style;

            // Keep the idle lanes readable under active orders.
            IReadOnlyList<Route> routes = match.Routes;
            idleLanes = new LineStyle[routes.Count];
            activeLanes = new bool[routes.Count];
            for (int i = 0; i < routes.Count; i++)
            {
                GetRoute(routes[i].A, routes[i].B, out Vector2 from, out Vector2 to);
                var lane = new LineStyle(Shapes.CreateQuad("Lane", parent, lineMaterial, -50))
                {
                    Tint = Palette.Hdr(style.LaneColor, style.Lane.Brightness, style.Lane.Opacity),
                };
                lane.Shape(style.Lane);
                lane.Set(from, to);
                idleLanes[i] = lane;
                laneOf[routes[i]] = i;
            }

            orderPreview = new LineStyle(Shapes.CreateQuad("Order Preview", parent, lineMaterial, 8))
            {
                Scroll = PreviewScroll,
            };
            orderPreview.Visible = false;
        }

        /// <summary>
        /// Shows the preview from the selected world to the hovered one when that order could be
        /// given, and hides the idle lanes that the preview or a fleet in transit runs along.
        /// </summary>
        public void Refresh(Match match, int selected, int hovered)
        {
            bool show = selected >= 0 && hovered >= 0 && hovered != selected
                && match.CanLaunch(player, selected, hovered);
            orderPreview.Visible = show;
            if (show)
            {
                GetRoute(selected, hovered, out Vector2 from, out Vector2 to);
                LineTuning preview = style.Preview;
                orderPreview.Shape(preview);
                orderPreview.Tint = Palette.Hdr(style.PreviewColor, preview.Brightness, preview.Opacity);
                orderPreview.Set(from, to);
            }

            System.Array.Clear(activeLanes, 0, activeLanes.Length);
            if (show) MarkActive(selected, hovered);
            foreach (Fleet fleet in match.Fleets)
                MarkActive(fleet.SourceId, fleet.TargetId);

            for (int i = 0; i < idleLanes.Length; i++)
                idleLanes[i].Visible = !activeLanes[i];
        }

        // A route is drawn between the two orbits, not between the centers of the worlds. The rules still
        // measure it from center to center.
        void GetRoute(int source, int target, out Vector2 from, out Vector2 to)
        {
            Vector2 delta = worlds[target].Position - worlds[source].Position;
            Vector2 direction = delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector2.right;
            from = worlds[source].Position + direction * worlds[source].OrbitRadius;
            to = worlds[target].Position - direction * worlds[target].OrbitRadius;
        }

        void MarkActive(int source, int target)
        {
            if (laneOf.TryGetValue(new Route(source, target), out int lane)) activeLanes[lane] = true;
        }
    }
}
