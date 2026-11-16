using System;

namespace Squadra.Simulation
{
    /// <summary>
    /// A direct route between two map objects, by node id. Routes run both ways, so the route from 2 to 0
    /// is the route from 0 to 2: <see cref="A"/> is always the smaller id.
    /// </summary>
    public readonly struct Route : IEquatable<Route>
    {
        public readonly int A;
        public readonly int B;

        public Route(int a, int b)
        {
            A = Math.Min(a, b);
            B = Math.Max(a, b);
        }

        /// <summary>True when this route joins the two objects, in either direction.</summary>
        public bool Joins(int first, int second) => this == new Route(first, second);

        public bool Equals(Route other) => A == other.A && B == other.B;
        public override bool Equals(object obj) => obj is Route other && Equals(other);
        public override int GetHashCode() => unchecked(A * 397 ^ B);
        public override string ToString() => A + " - " + B;

        public static bool operator ==(Route left, Route right) => left.Equals(right);
        public static bool operator !=(Route left, Route right) => !left.Equals(right);
    }
}
