using System.Collections.Generic;
using Squadra.Simulation;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// The dashed lines between worlds: an idle lane for every route, and the preview of the order the
    /// player is about to give, which only a route allows. A lane that carries a fleet or the preview is
    /// hidden, because the animated line drawn over it would fill its gaps. How both look comes from the
    /// shared <see cref="RouteStyle"/>, read every frame, so an edit to it or a world moved in the sandbox
    /// shows at once.
    /// </summary>
    sealed class RouteLanes
    {
        readonly IReadOnlyList<WorldView> worlds;
        readonly IReadOnlyList<Route> routes;
        readonly LineStyle[] idleLanes;
        readonly HashSet<Route> flown = new HashSet<Route>();
        readonly LineStyle orderPreview;
        readonly RouteStyle style;

        // How fast the preview's dashes scroll toward the target, in world units per second.
        const float PreviewScroll = 1.15f;

        /// <summary>Creates the lines for <paramref name="routes"/> as children of <paramref name="parent"/>.</summary>
        public RouteLanes(IReadOnlyList<Route> routes, IReadOnlyList<WorldView> worlds, Transform parent,
            Material lineMaterial, RouteStyle style)
        {
            this.worlds = worlds;
            this.routes = routes;
            this.style = style;

            // Keep the idle lanes readable under active orders.
            idleLanes = new LineStyle[routes.Count];
            for (int i = 0; i < routes.Count; i++)
            {
                idleLanes[i] = new LineStyle(Shapes.CreateQuad("Lane", parent, lineMaterial, -50));
                LayLane(i);
            }

            orderPreview = new LineStyle(Shapes.CreateQuad("Order Preview", parent, lineMaterial, 8))
            {
                Scroll = PreviewScroll,
            };
            orderPreview.Visible = false;
        }

        /// <summary>
        /// Shows the preview from the selected world to the hovered one when <paramref name="player"/>
        /// could give that order, and hides the idle lanes that the preview or a fleet in transit runs along.
        /// </summary>
        public void Refresh(Match match, Faction player, int selected, int hovered)
        {
            bool show = selected >= 0 && hovered >= 0 && hovered != selected
                && match.CanLaunch(player, selected, hovered);

            flown.Clear();
            foreach (Fleet fleet in match.Fleets)
                flown.Add(new Route(fleet.SourceId, fleet.TargetId));

            Draw(show ? selected : -1, show ? hovered : -1, flown);
        }

        /// <summary>
        /// Draws the preview of an order from one world to another, or none when either is negative, and
        /// every idle lane except those the preview or a fleet runs along: the routes in <paramref name="busy"/>.
        /// </summary>
        public void Draw(int previewFrom, int previewTo, ICollection<Route> busy)
        {
            bool show = previewFrom >= 0 && previewTo >= 0 && previewFrom != previewTo;
            orderPreview.Visible = show;
            if (show)
            {
                GetRoute(previewFrom, previewTo, out Vector2 from, out Vector2 to);
                LineTuning preview = style.Preview;
                orderPreview.Shape(preview);
                orderPreview.Tint = Palette.Hdr(style.PreviewColor, preview.Brightness, preview.Opacity);
                orderPreview.Set(from, to);
            }

            for (int i = 0; i < idleLanes.Length; i++)
            {
                Route route = routes[i];
                bool active = (show && route.Joins(previewFrom, previewTo)) || busy.Contains(route);
                if (active) idleLanes[i].Visible = false;
                else LayLane(i);
            }
        }

        void LayLane(int index)
        {
            GetRoute(routes[index].A, routes[index].B, out Vector2 from, out Vector2 to);
            LineStyle lane = idleLanes[index];
            lane.Tint = Palette.Hdr(style.LaneColor, style.Lane.Brightness, style.Lane.Opacity);
            lane.Shape(style.Lane);
            lane.Set(from, to);
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
    }
}
