using System;
using System.Collections.Generic;
using System.Globalization;
using Squadra.Missions;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// Which part of the map the mission canvas shows: the map point in the middle and how many
    /// pixels a map unit takes. It converts between map and canvas coordinates, projects a node's
    /// body onto the canvas and does the arithmetic of panning, zooming and fitting. Map X runs to
    /// the right and map Y up, so the conversion flips Y on the way to the screen. It changes only
    /// the view, never the mission.
    /// </summary>
    sealed class MapViewport
    {
        /// <summary>Pixels per map unit at 100%.</summary>
        public const double DefaultZoom = 40d;

        public const double MinZoom = 1e-7;
        public const double MaxZoom = 1e5;

        const double MaxFitZoom = DefaultZoom * 4d;
        const float FitMargin = 90f;

        // The most of a body, in map units, that Fit to Map tries to frame. A larger one cannot be
        // shown whole even at the smallest zoom. This limits the view only, never a stored size.
        const double MaxFittedBody = 1e12;

        // The canvas's content rectangle. It is asked for each time, because layout can change it
        // between two uses.
        readonly Func<Rect> canvasBox;

        public MapViewport(Func<Rect> canvasBox)
        {
            this.canvasBox = canvasBox;
        }

        /// <summary>The map position shown in the middle of the canvas.</summary>
        public double CenterX { get; private set; }

        public double CenterY { get; private set; }

        /// <summary>Pixels per map unit.</summary>
        public double Zoom { get; private set; } = DefaultZoom;

        /// <summary>The zoom as the toolbar shows it: 100% is the view a new mission opens with.</summary>
        public string ZoomText
        {
            get
            {
                double percent = Zoom / DefaultZoom * 100d;
                string format = percent >= 10d ? "0" : percent >= 1d ? "0.#" : "0.#######";
                return percent.ToString(format, CultureInfo.InvariantCulture) + "%";
            }
        }

        public Vector2 MapToCanvas(double x, double y)
        {
            Rect box = canvasBox();
            return new Vector2(
                (float)(box.width * 0.5 + (x - CenterX) * Zoom),
                (float)(box.height * 0.5 - (y - CenterY) * Zoom));
        }

        /// <summary>
        /// Where a node's body is on the canvas and how large. The node's size is a radius in game
        /// world units; dividing by the mission's distance scale gives map units and the zoom gives
        /// pixels. So a body is drawn to the same measure as the distances between nodes: twice the
        /// zoom doubles it and twice the distance scale halves it, and neither touches the stored size.
        /// </summary>
        public NodeFootprint FootprintOf(MissionNode node, double distanceScale)
        {
            Rect box = canvasBox();
            return new NodeFootprint(
                box.width * 0.5 + (node.X - CenterX) * Zoom,
                box.height * 0.5 - (node.Y - CenterY) * Zoom,
                node.Size / distanceScale * Zoom);
        }

        public void CanvasToMap(Vector2 point, out double x, out double y)
        {
            Rect box = canvasBox();
            x = CenterX + (point.x - box.width * 0.5) / Zoom;
            y = CenterY - (point.y - box.height * 0.5) / Zoom;
        }

        // A position picked with the pointer is only as exact as a pixel. It is stored to a hundredth
        // of a pixel in round decimals, far finer than can be seen, so the mission gets -3.475 and not
        // the pointer's arithmetic noise. Typed coordinates are never touched.
        public double Tidy(double value)
        {
            double digits = Math.Ceiling(-Math.Log10(0.01 / Zoom));
            if (digits >= 0d) return Math.Round(value, (int)Math.Min(15d, digits));

            double step = Math.Pow(10d, -digits);
            return Math.Round(value / step) * step;
        }

        /// <summary>
        /// Changes the zoom by a factor while the map point under <paramref name="anchor"/> stays put.
        /// False when the zoom was already at its limit.
        /// </summary>
        public bool ZoomAround(Vector2 anchor, double factor)
        {
            double target = Math.Min(MaxZoom, Math.Max(MinZoom, Zoom * factor));
            if (target == Zoom) return false;

            CanvasToMap(anchor, out double x, out double y);
            Zoom = target;
            Rect box = canvasBox();
            CenterX = x - (anchor.x - box.width * 0.5) / Zoom;
            CenterY = y + (anchor.y - box.height * 0.5) / Zoom;
            return true;
        }

        /// <summary>Brings a map position to the middle of the canvas without changing the zoom.</summary>
        public void CenterOn(double x, double y)
        {
            CenterX = x;
            CenterY = y;
        }

        /// <summary>Moves the view with the pointer: the map follows a drag of that many pixels.</summary>
        public void Pan(Vector2 pixels)
        {
            CenterX -= pixels.x / Zoom;
            CenterY += pixels.y / Zoom;
        }

        /// <summary>
        /// Frames every node, the whole of its body included, with a margin around them. A mission
        /// without nodes gets the view a new mission opens with: the origin in the middle at 100%.
        /// </summary>
        public void Fit(List<MissionNode> nodes, double distanceScale)
        {
            if (nodes.Count == 0)
            {
                CenterX = 0d;
                CenterY = 0d;
                Zoom = DefaultZoom;
                return;
            }

            double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
            foreach (MissionNode node in nodes)
            {
                double body = node.Size / distanceScale;
                body = body > 0d ? Math.Min(body, MaxFittedBody) : 0d;
                minX = Math.Min(minX, node.X - body);
                maxX = Math.Max(maxX, node.X + body);
                minY = Math.Min(minY, node.Y - body);
                maxY = Math.Max(maxY, node.Y + body);
            }

            CenterX = (minX + maxX) * 0.5;
            CenterY = (minY + maxY) * 0.5;

            Rect box = canvasBox();
            double roomX = Math.Max(40d, box.width - 2d * FitMargin);
            double roomY = Math.Max(40d, box.height - 2d * FitMargin);
            double fit = MaxFitZoom;
            if (maxX > minX) fit = Math.Min(fit, roomX / (maxX - minX));
            if (maxY > minY) fit = Math.Min(fit, roomY / (maxY - minY));
            Zoom = Math.Min(MaxZoom, Math.Max(MinZoom, fit));
        }

        /// <summary>
        /// The distance between grid lines: a round number of map units, never closer than a
        /// comfortable gap on screen.
        /// </summary>
        public double GridStep()
        {
            const double minimumGap = 28d;
            double wanted = minimumGap / Zoom;
            double power = Math.Pow(10d, Math.Floor(Math.Log10(wanted)));
            foreach (double multiple in new[] { 1d, 2d, 5d, 10d })
                if (power * multiple >= wanted) return power * multiple;
            return power * 10d;
        }
    }
}
