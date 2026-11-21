using Squadra.Simulation;

namespace Squadra.Sandbox
{
    /// <summary>
    /// A fleet the sandbox starts with already on its way: its Darts are its own, apart from those
    /// stationed at the world it set out from, and the match flies it on from where it is.
    /// </summary>
    public sealed class StartingFleet
    {
        public Faction Faction = Faction.Blue;
        public int Darts = 10;

        /// <summary>The world it set out from, by index into the setup's worlds; negative for none.</summary>
        public int From = -1;

        /// <summary>The world it flies to, by index into the setup's worlds; negative for none.</summary>
        public int To = -1;

        /// <summary>How far along its route it is: 0 as it sets out, 1 as it arrives.</summary>
        public double Progress = 0.5;
    }
}
