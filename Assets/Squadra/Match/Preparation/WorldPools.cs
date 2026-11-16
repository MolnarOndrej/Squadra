using System;
using System.Collections.Generic;
using Squadra.Missions;

namespace Squadra.Preparation
{
    /// <summary>
    /// What a mission can be dressed in: the world pools of every node type, where a world only ever draws
    /// from its own type's pool, and the space backdrops the mission as a whole draws one from.
    /// </summary>
    public sealed class WorldPools
    {
        readonly WorldPool planets;
        readonly WorldPool moons;
        readonly WorldPool asteroids;

        public WorldPools(WorldPool planets, WorldPool moons, WorldPool asteroids, IEnumerable<string> backdrops)
        {
            this.planets = planets ?? WorldPool.Empty;
            this.moons = moons ?? WorldPool.Empty;
            this.asteroids = asteroids ?? WorldPool.Empty;

            // A backdrop has no name, only a stable ID; the world pool keeps the IDs sorted and without repeats.
            Backdrops = new WorldPool(backdrops, null).VariantIds;
        }

        /// <summary>Space backdrop IDs, sorted ordinally.</summary>
        public IReadOnlyList<string> Backdrops { get; }

        public WorldPool For(MissionNodeType type)
        {
            switch (type)
            {
                case MissionNodeType.Planet: return planets;
                case MissionNodeType.Moon: return moons;
                case MissionNodeType.Asteroid: return asteroids;
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        public bool HasBackdrop(string backdropId)
        {
            foreach (string id in Backdrops)
                if (string.Equals(id, backdropId, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
