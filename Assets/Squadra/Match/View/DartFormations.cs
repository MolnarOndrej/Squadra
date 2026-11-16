using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// Where the dots of a fleet belong around a world: how many dots stand for it, each dot's place
    /// on its orbit, and each dot's place in a battle formation. Places are an angle and a distance
    /// from the world. Pure geometry; it keeps nothing and draws nothing.
    /// </summary>
    static class DartFormations
    {
        /// <summary>Distance between the rows of a battle formation, in world units.</summary>
        public const float RowSpacing = 0.14f;

        const float GoldenAngle = 2.39996323f;
        const float DotSpacing = 0.15f;
        const float WidestFront = 2.3f; // radians of a world's circumference a formation may cover

        /// <summary>How many dots stand for a fleet. One per Dart at first, then growing slowly.</summary>
        public static int DotCount(int darts)
        {
            if (darts <= 0) return 0;
            if (darts <= 24) return darts;
            return Mathf.Min(64, 24 + Mathf.RoundToInt(Mathf.Sqrt(darts - 24) * 2.5f));
        }

        /// <summary>The place of dot number <paramref name="index"/> on its orbit at a moment.</summary>
        public static void OrbitPlace(WorldView world, int index, float time, out float angle, out float radius)
        {
            int lane = index % 3;
            int slot = index / 3;
            float speed = 0.42f - lane * 0.075f;
            angle = slot * GoldenAngle + lane * 2.1f + time * speed;
            radius = world.OrbitRadius + (lane - 1) * 0.13f + 0.018f * Mathf.Sin(time * 1.3f + index * 1.7f);
        }

        /// <summary>
        /// The place of dot number <paramref name="index"/> of <paramref name="count"/> in a battle
        /// formation: rows on an arc centered on the front. The stationed side stands on the orbit and
        /// inside it, the arriving side outside it, across <paramref name="frontGap"/>: two fronts
        /// facing each other on one side of the world.
        /// </summary>
        public static void FormationPlace(WorldView world, int index, int count, float frontAngle, bool stationed,
            float frontGap, out float angle, out float radius)
        {
            int rows = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(count / 5f)), 1, stationed ? 3 : 4);
            int row = index % rows;
            int column = index / rows;
            int columns = (count - row + rows - 1) / rows;

            radius = stationed
                ? world.OrbitRadius - row * RowSpacing
                : world.OrbitRadius + frontGap + row * RowSpacing;
            // A large fleet at a small world closes up rather than wrapping around it.
            float spacing = Mathf.Min(DotSpacing / Mathf.Max(0.3f, radius), WidestFront / Mathf.Max(1, columns));
            angle = frontAngle + (column - (columns - 1) * 0.5f + (row % 2) * 0.5f) * spacing;
        }

        public static Vector2 Polar(float angle, float radius)
        {
            return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }
    }
}
