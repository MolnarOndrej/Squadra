using Squadra.Simulation;

namespace Squadra.Sandbox
{
    /// <summary>
    /// A capture the sandbox starts with under way: the Darts stationed at the world are the captor's,
    /// and the world stays with its owner until the capture completes.
    /// </summary>
    public sealed class StartingCapture
    {
        /// <summary>The world being captured, by index into the setup's worlds.</summary>
        public int World;

        public Faction Captor = Faction.Blue;

        /// <summary>How far the capture has got: 0 as it begins, 1 as it completes.</summary>
        public double Progress;
    }
}
