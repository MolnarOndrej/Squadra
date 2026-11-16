using System;
using System.Collections.Generic;
using Squadra.Missions;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// Works out the outlines the mission canvas draws for a node, as closed polygons in canvas
    /// pixels: a disc for a planet, a crescent for a moon, a rock for an asteroid, and plain circles
    /// for the rings around them. Every symbol reaches exactly as far as the body's radius.
    ///
    /// A body is drawn at its real size at any zoom, so its outline can be far larger than the canvas.
    /// The outline is therefore worked out in double precision, made fine only where it comes near
    /// the canvas, and cut to a rectangle a little larger than the canvas before it becomes
    /// drawable floats. What shows of a huge body is exact; nothing is scaled down to fit.
    ///
    /// One instance serves one repaint. The list a method returns is reused by the next call.
    /// </summary>
    sealed class NodeOutline
    {
        // How far outside the canvas outlines are cut. Wider than any line drawn along an outline,
        // so the cut itself never shows.
        const double Margin = 16d;

        // The most a drawn outline strays from the true curve, in pixels.
        const double Tolerance = 0.1;
        const int MaxDepth = 40;

        // Past this radius a pixel is too small a part of the body for arithmetic to place its
        // edge. The canvas is then a single point of the body: all inside it or all outside.
        const double Enormous = 1e15;

        // The bite out of a moon: another disc, placed and sized in units of the moon's radius.
        // Canvas Y runs down, so the bite is toward the upper right.
        const double BiteX = 0.42;
        const double BiteY = -0.3;
        const double BiteRadius = 0.72;

        static readonly double BiteDistance = Math.Sqrt(BiteX * BiteX + BiteY * BiteY);
        static readonly double BiteDirection = Math.Atan2(BiteY, BiteX);

        // Half the angle of the moon's edge that the bite removes, seen from the moon's middle, and
        // half the angle of the bite's edge that lies inside the moon, seen from the bite's middle.
        static readonly double BittenHalf =
            Math.Acos((BiteDistance * BiteDistance + 1d - BiteRadius * BiteRadius) / (2d * BiteDistance));

        static readonly double BiteHalf =
            Math.Acos((BiteDistance * BiteDistance + BiteRadius * BiteRadius - 1d) / (2d * BiteDistance * BiteRadius));

        // An asteroid: corners at uneven angles (degrees) and distances (in radii) from the middle.
        // The farthest corners are exactly one radius out, so a rock is as large as its size says.
        static readonly double[] RockAngles = { 8d, 47d, 78d, 116d, 148d, 189d, 224d, 261d, 293d, 322d, 344d };
        static readonly double[] RockReach = { 1d, 0.76, 0.95, 0.72, 1d, 0.8, 0.97, 0.68, 0.94, 0.74, 0.9 };

        readonly struct Point
        {
            public Point(double x, double y)
            {
                X = x;
                Y = y;
            }

            public readonly double X;
            public readonly double Y;
        }

        readonly double left;
        readonly double top;
        readonly double right;
        readonly double bottom;
        readonly List<Vector2> outline = new List<Vector2>();

        // The path being made, and the list a cut writes into; the two change places with each cut.
        List<Point> path = new List<Point>();
        List<Point> spare = new List<Point>();

        /// <param name="canvas">The canvas's content rectangle; only its size is used.</param>
        public NodeOutline(Rect canvas)
        {
            left = -Margin;
            top = -Margin;
            right = canvas.width + Margin;
            bottom = canvas.height + Margin;
        }

        /// <summary>
        /// The outline of a node's symbol with its middle at a canvas position. Empty when none of
        /// it is on the canvas.
        /// </summary>
        public List<Vector2> Symbol(MissionNodeType type, double x, double y, double radius)
        {
            path.Clear();
            if (!(radius > 0d)) return Cut();
            if (!(radius < Enormous)) return AllOrNothing(Holds(type, (MiddleX - x) / radius, (MiddleY - y) / radius));

            switch (type)
            {
                case MissionNodeType.Moon:
                    // Around the moon's edge the long way, from one end of the bite to the other, and
                    // back along the bite's edge.
                    AddArc(x, y, radius, BiteDirection + BittenHalf, BiteDirection - BittenHalf + 2d * Math.PI);
                    AddArc(x + BiteX * radius, y + BiteY * radius, BiteRadius * radius,
                        BiteDirection + Math.PI + BiteHalf, BiteDirection + Math.PI - BiteHalf);
                    break;
                case MissionNodeType.Asteroid:
                    for (int i = 0; i < RockAngles.Length; i++) path.Add(RockCorner(i, x, y, radius));
                    break;
                default:
                    AddArc(x, y, radius, 0d, 2d * Math.PI);
                    break;
            }

            return Cut();
        }

        /// <summary>A circle around a canvas position. Empty when none of it is on the canvas.</summary>
        public List<Vector2> Circle(double x, double y, double radius)
        {
            return Symbol(MissionNodeType.Planet, x, y, radius);
        }

        double MiddleX => (left + right) * 0.5;
        double MiddleY => (top + bottom) * 0.5;

        // ------------------------------------------------------------------ curves

        // Adds the arc between two angles, in the direction from the first to the second, without
        // its last point: the next piece of the outline starts there.
        void AddArc(double x, double y, double radius, double from, double to)
        {
            int pieces = Math.Max(1, (int)Math.Ceiling(Math.Abs(to - from) / (Math.PI / 4d)));
            double step = (to - from) / pieces;
            for (int i = 0; i < pieces; i++)
            {
                double angle = from + step * i;
                path.Add(OnCircle(x, y, radius, angle));
                Refine(x, y, radius, angle, angle + step, 0);
            }
        }

        // Adds the points strictly between two points of an arc, as many as it takes for straight
        // lines to pass for the curve. An arc that stays clear of the canvas gets none: there a
        // straight line and the curve cover the same part of the canvas, which is nothing.
        void Refine(double x, double y, double radius, double from, double to, int depth)
        {
            double quarter = Math.Sin((to - from) * 0.25);
            double bulge = 2d * radius * quarter * quarter; // how far the arc rises above its chord
            if (bulge <= Tolerance || depth >= MaxDepth) return;

            Point a = OnCircle(x, y, radius, from);
            Point b = OnCircle(x, y, radius, to);
            if (Math.Max(a.X, b.X) + bulge < left || Math.Min(a.X, b.X) - bulge > right
                || Math.Max(a.Y, b.Y) + bulge < top || Math.Min(a.Y, b.Y) - bulge > bottom)
                return;

            double middle = (from + to) * 0.5;
            Refine(x, y, radius, from, middle, depth + 1);
            path.Add(OnCircle(x, y, radius, middle));
            Refine(x, y, radius, middle, to, depth + 1);
        }

        static Point OnCircle(double x, double y, double radius, double angle)
        {
            return new Point(x + radius * Math.Cos(angle), y + radius * Math.Sin(angle));
        }

        static Point RockCorner(int index, double x, double y, double radius)
        {
            return OnCircle(x, y, radius * RockReach[index], RockAngles[index] * Math.PI / 180d);
        }

        // ------------------------------------------------------------------ cutting to the canvas

        // Cuts the path to the rectangle around the canvas, one side at a time, and hands it over as
        // floats. A path that surrounds the canvas comes out as the rectangle itself.
        List<Vector2> Cut()
        {
            CutSide(true, left, true);
            CutSide(true, right, false);
            CutSide(false, top, true);
            CutSide(false, bottom, false);

            outline.Clear();
            foreach (Point point in path)
            {
                var next = new Vector2((float)point.X, (float)point.Y);
                if (outline.Count == 0 || (outline[outline.Count - 1] - next).sqrMagnitude > 1e-6f) outline.Add(next);
            }

            if (outline.Count < 3) outline.Clear();
            return outline;
        }

        // Keeps what lies on the inner side of one side of the rectangle: a vertical side at an X,
        // or a horizontal one at a Y.
        void CutSide(bool vertical, double at, bool keepGreater)
        {
            spare.Clear();
            int count = path.Count;
            for (int i = 0; i < count; i++)
            {
                Point a = path[i];
                Point b = path[(i + 1) % count];
                double inA = (vertical ? a.X : a.Y) - at;
                double inB = (vertical ? b.X : b.Y) - at;
                if (!keepGreater)
                {
                    inA = -inA;
                    inB = -inB;
                }

                if (inA >= 0d) spare.Add(a);
                if ((inA >= 0d) == (inB >= 0d)) continue;

                // Where the line from a to b crosses the side. That coordinate is set outright, so
                // that the crossing lies on the side exactly.
                double along = inA / (inA - inB);
                spare.Add(vertical
                    ? new Point(at, a.Y + (b.Y - a.Y) * along)
                    : new Point(a.X + (b.X - a.X) * along, at));
            }

            (path, spare) = (spare, path);
        }

        List<Vector2> AllOrNothing(bool all)
        {
            outline.Clear();
            if (!all) return outline;

            outline.Add(new Vector2((float)left, (float)top));
            outline.Add(new Vector2((float)right, (float)top));
            outline.Add(new Vector2((float)right, (float)bottom));
            outline.Add(new Vector2((float)left, (float)bottom));
            return outline;
        }

        // True when a symbol of radius one around the origin holds a point.
        static bool Holds(MissionNodeType type, double x, double y)
        {
            if (!(x * x + y * y < 1d)) return false;

            switch (type)
            {
                case MissionNodeType.Moon:
                    double dx = x - BiteX;
                    double dy = y - BiteY;
                    return dx * dx + dy * dy > BiteRadius * BiteRadius;
                case MissionNodeType.Asteroid:
                    bool inside = false;
                    for (int i = 0, last = RockAngles.Length - 1; i < RockAngles.Length; last = i++)
                    {
                        Point a = RockCorner(i, 0d, 0d, 1d);
                        Point b = RockCorner(last, 0d, 0d, 1d);
                        if ((a.Y > y) != (b.Y > y) && x < a.X + (b.X - a.X) * (y - a.Y) / (b.Y - a.Y)) inside = !inside;
                    }

                    return inside;
                default:
                    return true;
            }
        }
    }
}
