using System;

namespace Squadra.Simulation
{
    /// <summary>A position on the map, in map units.</summary>
    public readonly struct MapPoint : IEquatable<MapPoint>
    {
        public readonly float X;
        public readonly float Y;

        public MapPoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static double Distance(MapPoint a, MapPoint b)
        {
            double dx = (double)a.X - b.X;
            double dy = (double)a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public bool Equals(MapPoint other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is MapPoint other && Equals(other);
        public override int GetHashCode() => unchecked((X.GetHashCode() * 397) ^ Y.GetHashCode());
        public override string ToString() => $"({X}, {Y})";
    }
}
