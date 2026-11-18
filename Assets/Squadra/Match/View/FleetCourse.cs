using Squadra.Simulation;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// The two lines of a fleet in transit, in its owner's color: its course ahead, with dashes scrolling
    /// toward the target, and its wake behind. How they look comes from the shared <see cref="RouteStyle"/>.
    /// </summary>
    sealed class FleetCourse
    {
        // How fast the dashes of a fleet's course scroll toward its target, in world units per second.
        const float CourseScroll = 0.9f;

        readonly LineStyle lead;
        readonly LineStyle wake;

        /// <summary>Creates the two lines as children of <paramref name="parent"/>.</summary>
        public FleetCourse(Transform parent, Material lineMaterial)
        {
            lead = new LineStyle(Shapes.CreateQuad("Fleet Course", parent, lineMaterial, -42)) { Scroll = CourseScroll };
            wake = new LineStyle(Shapes.CreateQuad("Fleet Wake", parent, lineMaterial, -44));
        }

        /// <summary>
        /// Gives the lines the color of a fleet of <paramref name="faction"/>, taken from the style as it is
        /// now. Takes effect with the next <see cref="Follow"/>.
        /// </summary>
        public void Paint(Faction faction, RouteStyle style)
        {
            Color tint = Palette.Of(faction);
            LineTuning course = style.Course;
            LineTuning trail = style.Wake;
            lead.Shape(course);
            lead.Tint = Palette.Hdr(Palette.Bright(tint, style.CourseWhiten), course.Brightness, course.Opacity);
            wake.Shape(trail);
            wake.Tint = Palette.Hdr(Palette.Bright(tint, style.WakeWhiten), trail.Brightness, trail.Opacity);
        }

        /// <summary>Lays the wake from where the fleet set out to where it is, and the course on to its end.</summary>
        public void Follow(Vector2 start, Vector2 position, Vector2 end)
        {
            lead.Set(position, end);
            wake.Set(start, position);
        }

        public void Hide()
        {
            lead.Visible = false;
            wake.Visible = false;
        }
    }
}
