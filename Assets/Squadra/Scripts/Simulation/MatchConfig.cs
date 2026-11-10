using System.Collections.Generic;

namespace Squadra.Simulation
{
    /// <summary>Starting setup of one map object. Production is configuration so later upgrades can change it.</summary>
    public sealed class NodeConfig
    {
        public const double PlanetProductionInterval = 1.0;
        public const double MoonProductionInterval = 2.0;

        public string Name = "";
        public NodeKind Kind = NodeKind.Planet;
        public MapPoint Position;
        public Faction Owner = Faction.Neutral;
        public int Ships;

        /// <summary>Seconds per produced ship while owned and outside capture. Zero or less disables production.</summary>
        public double ProductionInterval = PlanetProductionInterval;

        public static NodeConfig Planet(string name, MapPoint position, Faction owner, int ships)
        {
            return new NodeConfig
            {
                Name = name,
                Kind = NodeKind.Planet,
                Position = position,
                Owner = owner,
                Ships = ships,
                ProductionInterval = PlanetProductionInterval,
            };
        }

        public static NodeConfig Moon(string name, MapPoint position, Faction owner = Faction.Neutral, int ships = 0)
        {
            return new NodeConfig
            {
                Name = name,
                Kind = NodeKind.Moon,
                Position = position,
                Owner = owner,
                Ships = ships,
                ProductionInterval = MoonProductionInterval,
            };
        }
    }

    public sealed class MatchConfig
    {
        public const int StartingShips = 30;

        public readonly List<NodeConfig> Nodes = new List<NodeConfig>();

        /// <summary>Common fleet speed in map units per second. Travel time is distance divided by this.</summary>
        public double FleetSpeed = 2.4;

        /// <summary>Seconds a fleet must hold an object before ownership changes.</summary>
        public double CaptureDuration = 5.0;

        /// <summary>
        /// The first playable map: Blue and Red each hold one planet with 30 ships, and a neutral, empty moon
        /// sits between them. Node ids are 0 (Blue planet), 1 (Red planet), 2 (moon).
        /// </summary>
        public static MatchConfig FirstPlayable(MapPoint bluePlanet, MapPoint redPlanet, MapPoint moon)
        {
            var config = new MatchConfig();
            config.Nodes.Add(NodeConfig.Planet("Blue Planet", bluePlanet, Faction.Blue, StartingShips));
            config.Nodes.Add(NodeConfig.Planet("Red Planet", redPlanet, Faction.Red, StartingShips));
            config.Nodes.Add(NodeConfig.Moon("Moon", moon));
            return config;
        }
    }
}
