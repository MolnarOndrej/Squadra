using System;
using System.Collections.Generic;
using Squadra.Preparation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Squadra
{
    /// <summary>
    /// Builds a prepared mission's worlds in the scene: one copy of the world template per world, at its
    /// game position and radius, with the surface and name its look was given, in front of the space
    /// backdrop the mission was given. It also works out the area
    /// the camera must show so that every world, with what is drawn around it, is in view. It decides no
    /// rule and rolls no look of its own.
    /// </summary>
    static class Battlefield
    {
        // Room around a world for what is drawn beside it: the Darts on its orbit, the texts that float up
        // above it, and the count and caption below it, which are wider than a small world.
        const float SideRoom = 0.4f;
        const float CaptionHalfWidth = 2.6f;
        const float TopRoom = 1.0f;
        const float BottomRoom = 1.5f;

        // Shares of the view's height kept clear for the HUD's top bar and its hint line.
        const float TopBand = 0.16f;
        const float BottomBand = 0.09f;

        // A small map is shown at the scale the battlefield was designed at rather than blown up.
        static readonly Vector2 MinimumArea = new Vector2(17.8f, 10f);

        /// <summary>
        /// The mission's worlds, by match index. Throws when the looks have no surface for a world, which
        /// preparation rules out: an unknown variant is never quietly replaced by another.
        /// </summary>
        public static WorldView[] Build(PreparedMission mission, WorldView template, WorldLooks looks)
        {
            var worlds = new WorldView[mission.Worlds.Count];
            foreach (PreparedWorld world in mission.Worlds)
            {
                if (looks == null || !looks.TryGetSurface(world.Kind, world.Look.VariantId, out Material surface))
                {
                    throw new InvalidOperationException("There is no " + world.Kind + " surface \"" + world.Look.VariantId
                        + "\" for node " + world.NodeId + " of " + mission.Id + ".");
                }

                WorldView view = Object.Instantiate(template, template.transform.parent);
                view.name = world.Look.Name + " (" + world.NodeId + ")";
                view.Setup(world.Look.Name, new Vector2(world.Position.X, world.Position.Y), world.Radius, surface);
                view.gameObject.SetActive(true);
                worlds[world.Index] = view;
            }

            return worlds;
        }

        /// <summary>
        /// Puts the mission's space backdrop behind the battlefield. Throws when the looks have no such
        /// backdrop, which preparation rules out.
        /// </summary>
        public static void ShowBackdrop(PreparedMission mission, Backdrop backdrop, WorldLooks looks)
        {
            string id = mission.Assignment.BackdropId;
            if (looks == null || !looks.TryGetBackdrop(id, out Material material))
                throw new InvalidOperationException("There is no space backdrop \"" + id + "\" for " + mission.Id + ".");

            backdrop.Show(material);
        }

        /// <summary>
        /// The area to show: every world with room for what is drawn around it, and bands above and below
        /// for the HUD. Worlds stay where the mission put them; only the view is fitted to them.
        /// </summary>
        public static Rect Frame(IReadOnlyList<WorldView> worlds)
        {
            if (worlds.Count == 0) return new Rect(-MinimumArea / 2f, MinimumArea);

            float xMin = float.MaxValue, xMax = float.MinValue, yMin = float.MaxValue, yMax = float.MinValue;
            foreach (WorldView world in worlds)
            {
                Vector2 center = world.Position;
                float orbit = world.OrbitRadius;
                float side = Mathf.Max(orbit + SideRoom, CaptionHalfWidth);
                xMin = Mathf.Min(xMin, center.x - side);
                xMax = Mathf.Max(xMax, center.x + side);
                yMin = Mathf.Min(yMin, center.y - orbit - BottomRoom);
                yMax = Mathf.Max(yMax, center.y + orbit + TopRoom);
            }

            float height = (yMax - yMin) / (1f - TopBand - BottomBand);
            Vector2 middle = new Rect(xMin, yMin - height * BottomBand, xMax - xMin, height).center;
            Vector2 size = Vector2.Max(new Vector2(xMax - xMin, height), MinimumArea);
            return new Rect(middle - size / 2f, size);
        }
    }
}
