using System.Collections.Generic;
using Squadra.Simulation;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// The dashed lines between worlds: an idle lane for every pair, and the preview of the order
    /// the player is about to give. A lane that carries a fleet or the preview is hidden, because the
    /// animated line drawn over it would fill its gaps.
    /// </summary>
    sealed class RouteLanes
    {
        readonly IReadOnlyList<WorldView> worlds;
        readonly Faction player;
        readonly LineStyle[,] idleLanes;
        readonly bool[,] activeLanes;
        readonly LineStyle orderPreview;

        /// <summary>Creates the lines as children of <paramref name="parent"/>.</summary>
        public RouteLanes(IReadOnlyList<WorldView> worlds, Faction player, Transform parent, Material lineMaterial)
        {
            this.worlds = worlds;
            this.player = player;

            // Every pair of worlds has a direct route. Keep the idle lanes readable under active orders.
            idleLanes = new LineStyle[worlds.Count, worlds.Count];
            activeLanes = new bool[worlds.Count, worlds.Count];
            for (int a = 0; a < worlds.Count; a++)
            {
                for (int b = a + 1; b < worlds.Count; b++)
                {
                    GetRoute(a, b, out Vector2 from, out Vector2 to);
                    var lane = new LineStyle(Shapes.CreateQuad("Lane", parent, lineMaterial, -50))
                    {
                        Tint = Palette.Hdr(Palette.Starlight, 0.66f, 0.53f),
                        Width = 0.10f,
                        Dash = 0.13f,
                        Gap = 0.16f,
                        EndFade = 0.04f,
                    };
                    lane.Set(from, to);
                    idleLanes[a, b] = lane;
                }
            }

            orderPreview = new LineStyle(Shapes.CreateQuad("Order Preview", parent, lineMaterial, 8))
            {
                Width = 0.16f,
                Dash = 0.26f,
                Gap = 0.16f,
                Scroll = 1.15f,
                EndFade = 0.05f,
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
                orderPreview.Tint = Palette.Hdr(Palette.Accent, 1.55f, 0.95f);
                orderPreview.Set(from, to);
            }

            System.Array.Clear(activeLanes, 0, activeLanes.Length);
            if (show) MarkActive(selected, hovered);
            foreach (Fleet fleet in match.Fleets)
                MarkActive(fleet.SourceId, fleet.TargetId);

            for (int a = 0; a < worlds.Count; a++)
                for (int b = a + 1; b < worlds.Count; b++)
                    idleLanes[a, b].Visible = !activeLanes[a, b];
        }

        // A route runs between the two orbits, not between the centers of the worlds.
        void GetRoute(int source, int target, out Vector2 from, out Vector2 to)
        {
            Vector2 delta = worlds[target].Position - worlds[source].Position;
            Vector2 direction = delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector2.right;
            from = worlds[source].Position + direction * worlds[source].OrbitRadius;
            to = worlds[target].Position - direction * worlds[target].OrbitRadius;
        }

        void MarkActive(int source, int target)
        {
            activeLanes[Mathf.Min(source, target), Mathf.Max(source, target)] = true;
        }
    }
}
