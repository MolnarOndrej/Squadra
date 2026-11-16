using System;
using System.Collections.Generic;
using Squadra.Simulation;

namespace Squadra.Preparation
{
    /// <summary>
    /// A mission ready to be played: its identity, its worlds with their starting state and looks, and the
    /// routes between them. It is read-only. Every attempt at the mission, a restart included, starts from
    /// a fresh <see cref="CreateConfig"/>, so nothing a match does can leak into the next one.
    /// </summary>
    public sealed class PreparedMission
    {
        readonly List<PreparedWorld> worlds;
        readonly List<Route> routes;
        readonly Dictionary<string, int> indices = new Dictionary<string, int>(StringComparer.Ordinal);

        internal PreparedMission(string id, string name, List<PreparedWorld> worlds, List<Route> routes,
            WorldAssignment assignment)
        {
            Id = id;
            Name = name;
            this.worlds = worlds;
            this.routes = routes;
            Assignment = assignment;
            foreach (PreparedWorld world in worlds)
                indices.Add(world.NodeId, world.Index);
        }

        /// <summary>The mission's ID: the campaign entry's ID and the file's name.</summary>
        public string Id { get; }

        /// <summary>The name the author gave the mission.</summary>
        public string Name { get; }

        /// <summary>The worlds in match index order, which is the order of the file's node list.</summary>
        public IReadOnlyList<PreparedWorld> Worlds => worlds;

        /// <summary>The routes, by match index, in the order of the file's edge list.</summary>
        public IReadOnlyList<Route> Routes => routes;

        /// <summary>The look of every world, by mission and node ID.</summary>
        public WorldAssignment Assignment { get; }

        /// <summary>The match index of a node ID, or -1 for a node this mission does not have.</summary>
        public int IndexOf(string nodeId)
        {
            return nodeId != null && indices.TryGetValue(nodeId, out int index) ? index : -1;
        }

        /// <summary>
        /// A new match configuration with this mission's players, worlds and routes. Blue is the human
        /// player. The match rules (fleet speed, capture, battles) keep their defaults for the caller to set.
        /// </summary>
        public MatchConfig CreateConfig()
        {
            var config = new MatchConfig { Human = Faction.Blue };
            config.Players.Add(new MatchPlayer(Faction.Blue));
            config.Players.Add(new MatchPlayer(Faction.Red));

            foreach (PreparedWorld world in worlds)
            {
                string name = world.Look.Name;
                switch (world.Kind)
                {
                    case NodeKind.Planet:
                        config.Nodes.Add(NodeConfig.Planet(name, world.Position, world.Owner, world.Darts));
                        break;
                    case NodeKind.Moon:
                        config.Nodes.Add(NodeConfig.Moon(name, world.Position, world.Owner, world.Darts));
                        break;
                    default:
                        config.Nodes.Add(NodeConfig.Asteroid(name, world.Position, world.Owner, world.Darts));
                        break;
                }
            }

            config.Routes.AddRange(routes);
            return config;
        }
    }
}
