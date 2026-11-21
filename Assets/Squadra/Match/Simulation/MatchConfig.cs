using System.Collections.Generic;

namespace Squadra.Simulation
{
    public sealed class MatchConfig
    {
        public const int StartingDarts = 30;

        /// <summary>The most players one mission can have.</summary>
        public const int MaxPlayers = 6;

        public readonly List<NodeConfig> Nodes = new List<NodeConfig>();

        /// <summary>
        /// The routes fleets may travel, by index into <see cref="Nodes"/>. Only these: nothing is inferred
        /// from distance and no order is routed through another object.
        /// </summary>
        public readonly List<Route> Routes = new List<Route>();

        /// <summary>
        /// The mission's roster, in the order results list the players. Leave it empty to take the
        /// roster from the starting owners of the map objects, in object order. A starting owner that
        /// is missing from the roster is added after the listed players.
        /// </summary>
        public readonly List<MatchPlayer> Players = new List<MatchPlayer>();

        /// <summary>The player at the controls. Results list this player first.</summary>
        public Faction Human = Faction.Blue;

        /// <summary>Common fleet speed in map units per second. Travel time is distance divided by this.</summary>
        public double FleetSpeed = 2.4;

        /// <summary>Seconds a fleet must hold an object before ownership changes.</summary>
        public double CaptureDuration = 5.0;

        /// <summary>How long battles last: one second when decisive, five between equal forces.</summary>
        public BattleTiming BattleTiming = new BattleTiming();

        /// <summary>
        /// True for a mission: it ends once the human or every opponent is out. A sandbox sets it false and
        /// plays on whoever is left, with no victory or defeat.
        /// </summary>
        public bool EndsOnElimination = true;

        /// <summary>
        /// What a fleet does that reaches a battle between two other sides. A battle has two sides and no
        /// rule for a third was agreed, so a mission refuses it with an error. A sandbox, with three players
        /// to order, sets this true: the fleet waits just outside until the battle is over and then arrives
        /// as usual, in the same instant.
        /// </summary>
        public bool ThirdSideWaits;

        /// <summary>Adds the route between two objects, by index into <see cref="Nodes"/>.</summary>
        public void Connect(int a, int b)
        {
            Routes.Add(new Route(a, b));
        }

        /// <summary>Adds a route between every pair of objects in <see cref="Nodes"/>.</summary>
        public void ConnectAll()
        {
            for (int a = 0; a < Nodes.Count; a++)
                for (int b = a + 1; b < Nodes.Count; b++)
                    Connect(a, b);
        }

        /// <summary>
        /// The layout of First Contact: Blue and Red each hold one planet with 30 Darts, and a neutral, empty
        /// moon sits between them, every world connected to the others. Node ids are 0 (Blue planet), 1 (Red
        /// planet), 2 (moon).
        /// </summary>
        public static MatchConfig FirstPlayable(MapPoint bluePlanet, MapPoint redPlanet, MapPoint moon)
        {
            var config = new MatchConfig();
            config.Players.Add(new MatchPlayer(Faction.Blue));
            config.Players.Add(new MatchPlayer(Faction.Red));
            config.Nodes.Add(NodeConfig.Planet("Blue Planet", bluePlanet, Faction.Blue, StartingDarts));
            config.Nodes.Add(NodeConfig.Planet("Red Planet", redPlanet, Faction.Red, StartingDarts));
            config.Nodes.Add(NodeConfig.Moon("Moon", moon));
            config.ConnectAll();
            return config;
        }
    }
}
