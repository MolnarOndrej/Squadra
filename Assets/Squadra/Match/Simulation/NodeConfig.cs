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
        public int Darts;

        /// <summary>Seconds per produced Dart while owned and outside capture. Zero or less disables production.</summary>
        public double ProductionInterval = PlanetProductionInterval;

        public static NodeConfig Planet(string name, MapPoint position, Faction owner, int darts)
        {
            return new NodeConfig
            {
                Name = name,
                Kind = NodeKind.Planet,
                Position = position,
                Owner = owner,
                Darts = darts,
                ProductionInterval = PlanetProductionInterval,
            };
        }

        public static NodeConfig Moon(string name, MapPoint position, Faction owner = Faction.Neutral, int darts = 0)
        {
            return new NodeConfig
            {
                Name = name,
                Kind = NodeKind.Moon,
                Position = position,
                Owner = owner,
                Darts = darts,
                ProductionInterval = MoonProductionInterval,
            };
        }
    }
}
