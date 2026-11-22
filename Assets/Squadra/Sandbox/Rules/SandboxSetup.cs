using System.Collections.Generic;
using Squadra.Simulation;

namespace Squadra.Sandbox
{
    /// <summary>
    /// The moment a sandbox starts from, as its author laid it down: the players, the worlds and routes,
    /// the captures, battles and fleets already under way, and the rules played by instead of the game's
    /// own. Edit Mode shows it frozen, and every run, and every reset of a run, starts from it. A session
    /// only reads it, so nothing done in a run ever changes it.
    /// </summary>
    public sealed class SandboxSetup
    {
        /// <summary>The players that can be given orders and own worlds, in the order they are listed.</summary>
        public readonly List<Faction> Players = new List<Faction>();

        /// <summary>The worlds, as a mission would lay them down, with the game's production for their kind.</summary>
        public readonly List<NodeConfig> Worlds = new List<NodeConfig>();

        /// <summary>Routes by index into <see cref="Worlds"/>; one that does not join two of them is skipped.</summary>
        public readonly List<Route> Routes = new List<Route>();

        public readonly List<StartingCapture> Captures = new List<StartingCapture>();
        public readonly List<StartingBattle> Battles = new List<StartingBattle>();
        public readonly List<StartingFleet> Fleets = new List<StartingFleet>();

        /// <summary>The rules saved with the scene to play by instead of the game's; null values keep the game's.</summary>
        public MatchOverrides Rules = new MatchOverrides();
    }
}
