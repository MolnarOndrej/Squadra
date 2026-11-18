using Squadra.Simulation;
using TMPro;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// Draws the Darts of the sandbox's samples, standing still: the fleet stationed at each world on its
    /// orbit, the two fronts of a battle, and fleets held on their way with their course, wake and count.
    /// Places, swarms, lines and colors come from what the mission's fleet layer draws with
    /// (<see cref="DartFormations"/>, <see cref="DartSwarm"/>, <see cref="FleetCourse"/> and the shared
    /// <see cref="FleetStyle"/>); only the movement is left out: dots do not orbit, settle, lunge or fire.
    /// Each frame is drawn between <see cref="Begin"/> and <see cref="End"/>.
    /// </summary>
    sealed class SandboxDarts
    {
        // The moment every sample is drawn at. Nothing moves, so it is always the same picture.
        const float Moment = 0f;

        // Drawn where the mission scene draws its fleets.
        const int DotsSortingOrder = 10;

        readonly DotBatch batch;
        readonly FleetCourse[] courses;
        readonly TextMeshPro[] labels;
        readonly bool[] shown;

        /// <summary>Creates the dots, and the lines and label of <paramref name="fleets"/> fleets, under <paramref name="parent"/>.</summary>
        public SandboxDarts(Transform parent, Material dotMaterial, Material lineMaterial, TextMeshPro labelTemplate,
            int fleets)
        {
            var go = new GameObject("Dots");
            go.transform.SetParent(parent, false);
            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = dotMaterial;
            renderer.sortingOrder = DotsSortingOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            batch = new DotBatch(filter);

            courses = new FleetCourse[fleets];
            labels = new TextMeshPro[fleets];
            shown = new bool[fleets];
            for (int i = 0; i < fleets; i++)
            {
                courses[i] = new FleetCourse(parent, lineMaterial);
                if (labelTemplate != null) labels[i] = Object.Instantiate(labelTemplate, parent);
            }
        }

        public void Begin()
        {
            batch.Begin();
            System.Array.Clear(shown, 0, shown.Length);
        }

        /// <summary>
        /// The Darts at a world in <paramref name="state"/>: on its orbit or, in a battle, in the stationed
        /// front facing the attackers' front across the gap, where <paramref name="frontAngle"/> points.
        /// </summary>
        public void Garrison(WorldView world, WorldState state, float frontAngle, DartDisplay display, FleetStyle style)
        {
            Vector4 stationed = style.StationedTint(Palette.Of(state.Occupant));
            int count = DartFormations.DotCount(state.Darts, display);
            for (int i = 0; i < count; i++)
            {
                float angle, radius;
                if (state.Fighting)
                    DartFormations.FormationPlace(world, i, count, frontAngle, true, style.FrontGap, out angle, out radius);
                else
                    DartFormations.OrbitPlace(world, i, Moment, display.OrbitSpeed, out angle, out radius);
                batch.Add(world.Position + DartFormations.Polar(angle, radius), style.DotSize, stationed);
            }

            if (!state.Fighting) return;

            Vector4 arriving = style.MovingTint(Palette.Of(state.Arriving));
            int attackers = DartFormations.DotCount(state.ArrivingDarts, display);
            for (int i = 0; i < attackers; i++)
            {
                DartFormations.FormationPlace(world, i, attackers, frontAngle, false, style.FrontGap,
                    out float angle, out float radius);
                batch.Add(world.Position + DartFormations.Polar(angle, radius), style.DotSize, arriving);
            }
        }

        /// <summary>
        /// Fleet number <paramref name="slot"/>, of <paramref name="darts"/> Darts of <paramref name="faction"/>,
        /// <paramref name="progress"/> of the way from <paramref name="source"/> to <paramref name="target"/>
        /// on a flight of <paramref name="duration"/> seconds. It stops short of the target's orbit when it
        /// would form up outside it, as the target's <paramref name="targetState"/> decides.
        /// </summary>
        public void Transit(int slot, WorldView source, WorldView target, WorldState targetState, Faction faction,
            int darts, float progress, double duration, DartDisplay display, FleetStyle style, RouteStyle routeStyle)
        {
            int dots = DartFormations.DotCount(darts, display);

            // Seeded like the match's fleets, whose ids count from one.
            var swarm = new DartSwarm(slot + 1, dots, source, target, style)
            {
                Standoff = DartSwarm.FormsUpOutside(faction, targetState) ? 1f : 0f,
            };

            FleetCourse course = courses[slot];
            course.Paint(faction, routeStyle);
            double elapsed = progress * duration;
            swarm.Place(progress, elapsed, duration - elapsed, Moment, style, course, out Vector2 center,
                out float formation);
            swarm.Draw(batch, faction, style);

            TextMeshPro label = labels[slot];
            if (label != null)
            {
                FleetLayer.ShowCount(label, faction, darts);
                label.transform.position = swarm.LabelPosition(center, formation, style);
                if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);
            }

            shown[slot] = true;
        }

        /// <summary>Finishes the frame: the lines and labels of fleets not drawn in it are hidden.</summary>
        public void End()
        {
            batch.End();
            for (int i = 0; i < shown.Length; i++)
            {
                if (shown[i]) continue;
                courses[i].Hide();
                if (labels[i] != null && labels[i].gameObject.activeSelf) labels[i].gameObject.SetActive(false);
            }
        }

        public void Dispose()
        {
            batch.Dispose();
        }
    }
}
