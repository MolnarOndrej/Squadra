using System;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// The disc a node's body covers on the mission canvas: its middle and its radius, in pixels.
    /// <see cref="MapViewport.FootprintOf"/> makes it from the node's size, the mission's distance
    /// scale and the zoom. Drawing, picking and label placement all go by it, so they agree, and
    /// nodes of one size have one radius whatever their type.
    ///
    /// The values are doubles and are never clamped: zoomed far in, a body is millions of pixels
    /// across and its middle far off the canvas, which a float cannot hold to the pixel. A distance
    /// scale near zero can even make the radius infinite; every user of a footprint allows for that.
    /// </summary>
    readonly struct NodeFootprint
    {
        public NodeFootprint(double x, double y, double radius)
        {
            X = x;
            Y = y;
            Radius = radius;
        }

        public double X { get; }
        public double Y { get; }
        public double Radius { get; }

        /// <summary>
        /// True when the square around the disc, grown by a margin, reaches a canvas of this size.
        /// It may say yes for a disc that just misses a corner; it never says no for one that shows.
        /// </summary>
        public bool Touches(Rect box, double margin)
        {
            double reach = Radius + margin;
            return X + reach >= 0d && X - reach <= box.width && Y + reach >= 0d && Y - reach <= box.height;
        }

        /// <summary>How far a canvas position is from the middle of the body, in pixels.</summary>
        public double DistanceTo(Vector2 point)
        {
            double dx = point.x - X;
            double dy = point.y - Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
