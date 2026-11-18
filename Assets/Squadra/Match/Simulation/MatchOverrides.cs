namespace Squadra.Simulation
{
    /// <summary>
    /// Rules a sandbox plays by instead of the game's own. The game's values are the defaults of
    /// <see cref="MatchConfig"/> and <see cref="NodeConfig"/>, in code; normal missions use no overrides.
    /// A value left null keeps the default as the code has it when the match is set up, so an override
    /// never turns into an outdated copy of a default. Only these rules are replaced: what a mission
    /// lays down, such as its worlds, their kinds, owners and starting Darts, is never touched.
    /// </summary>
    public sealed class MatchOverrides
    {
        /// <summary>Common fleet speed in map units per second, or null for the default.</summary>
        public double? FleetSpeed;

        /// <summary>Seconds per Dart produced on a planet, or null for the default.</summary>
        public double? PlanetProductionInterval;

        /// <summary>Seconds per Dart produced on a moon, or null for the default.</summary>
        public double? MoonProductionInterval;

        /// <summary>Puts the overridden rules into <paramref name="config"/>; leaves the rest as it is.</summary>
        public void ApplyTo(MatchConfig config)
        {
            if (FleetSpeed.HasValue) config.FleetSpeed = FleetSpeed.Value;

            foreach (NodeConfig node in config.Nodes)
            {
                // Asteroids never produce, whatever the rate of the other kinds.
                if (node.Kind == NodeKind.Planet && PlanetProductionInterval.HasValue)
                    node.ProductionInterval = PlanetProductionInterval.Value;
                else if (node.Kind == NodeKind.Moon && MoonProductionInterval.HasValue)
                    node.ProductionInterval = MoonProductionInterval.Value;
            }
        }
    }
}
