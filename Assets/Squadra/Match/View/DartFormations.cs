using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// Where the Darts of a fleet belong around a world: how many are drawn for it, each one's place on
    /// its orbit and in a battle formation, and which way it faces there. Places are an angle and a
    /// distance from the world, spaced as the shared <see cref="FleetStyle"/> says. Pure geometry; it
    /// keeps nothing and draws nothing.
    /// </summary>
    static class DartFormations
    {
        const float GoldenAngle = 2.39996323f;
        const float WidestFront = 2.3f; // radians of a world's circumference a formation may cover
        const float LaneSlowdown = 0.075f; // radians per second each orbit lane turns slower than the one inside it
        const float QuarterTurn = Mathf.PI * 0.5f;

        /// <summary>How many Darts are drawn for the fleet stationed at a world, or for either side of a battle there.</summary>
        public static int StationedDots(int darts, DartDisplay display)
        {
            return DotCount(darts, display.OneDotPerDartUpTo, display.MostStationedDots);
        }

        /// <summary>How many Darts are drawn for a fleet in transit.</summary>
        public static int TravellingDots(int darts, DartDisplay display)
        {
            return DotCount(darts, display.OneDotPerDartUpTo, display.MostTravellingDots);
        }

        /// <summary>
        /// Radians per second at which Dart number <paramref name="index"/> turns on its orbit. The innermost
        /// of the three lanes turns at <paramref name="orbitSpeed"/>, each outer one a little slower, so with
        /// a slow enough orbit an outer lane turns the other way.
        /// </summary>
        public static float LaneSpeed(int index, float orbitSpeed)
        {
            return orbitSpeed - (index % 3) * LaneSlowdown;
        }

        /// <summary>
        /// The place of Dart number <paramref name="index"/> on its orbit at a moment, turning at its
        /// <see cref="LaneSpeed"/>.
        /// </summary>
        public static void OrbitPlace(WorldView world, int index, float time, float orbitSpeed, FleetStyle style,
            out float angle, out float radius)
        {
            int lane = index % 3;
            int slot = index / 3;
            angle = slot * GoldenAngle + lane * 2.1f + time * LaneSpeed(index, orbitSpeed);
            radius = world.OrbitRadius + (lane - 1) * style.LaneSpacing + 0.018f * Mathf.Sin(time * 1.3f + index * 1.7f);
        }

        /// <summary>
        /// The place of Dart number <paramref name="index"/> of <paramref name="count"/> in a battle
        /// formation: rows on an arc centered on the front. The stationed side stands on the orbit and
        /// inside it, the arriving side outside it, across the style's front gap: two fronts facing each
        /// other on one side of the world.
        /// </summary>
        public static void FormationPlace(WorldView world, int index, int count, float frontAngle, bool stationed,
            FleetStyle style, out float angle, out float radius)
        {
            int rows = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(count / 5f)), 1, stationed ? 3 : 4);
            int row = index % rows;
            int column = index / rows;
            int columns = (count - row + rows - 1) / rows;

            radius = stationed
                ? world.OrbitRadius - row * style.RowSpacing
                : world.OrbitRadius + style.FrontGap + row * style.RowSpacing;
            // A large fleet at a small world closes up rather than wrapping around it.
            float spacing = Mathf.Min(style.FrontSpacing / Mathf.Max(0.3f, radius), WidestFront / Mathf.Max(1, columns));
            angle = frontAngle + (column - (columns - 1) * 0.5f + (row % 2) * 0.5f) * spacing;
        }

        /// <summary>
        /// Which way Dart number <paramref name="index"/>, at <paramref name="angle"/> around its world, faces,
        /// in radians: a stationed one the way its own lane turns, turned toward the enemy outside as it is
        /// <paramref name="formed"/> (0 in orbit, 1 in formation); an arriving one toward the world it attacks.
        /// </summary>
        public static float Heading(float angle, int index, bool stationed, float formed, float orbitSpeed)
        {
            if (!stationed) return angle + Mathf.PI;

            float along = angle + (LaneSpeed(index, orbitSpeed) < 0f ? -QuarterTurn : QuarterTurn);
            return along + Mathf.DeltaAngle(along * Mathf.Rad2Deg, angle * Mathf.Rad2Deg) * Mathf.Deg2Rad * formed;
        }

        public static Vector2 Polar(float angle, float radius)
        {
            return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }

        // One per Dart at first, then growing slowly, and never more than the limit, even when that is
        // below the one-per-Dart threshold.
        static int DotCount(int darts, int oneEach, int most)
        {
            if (darts <= 0) return 0;
            int dots = darts <= oneEach
                ? darts
                : oneEach + Mathf.RoundToInt(Mathf.Sqrt(darts - oneEach) * 2.5f);
            return Mathf.Min(most, dots);
        }
    }
}
