using Squadra.Simulation;

namespace Squadra.Sandbox
{
    /// <summary>
    /// A battle the sandbox starts with: attackers facing the Darts stationed at a world, or its captor's
    /// when a capture is under way there too, with nobody lost yet.
    /// </summary>
    public sealed class StartingBattle
    {
        /// <summary>The world fought over, by index into the setup's worlds.</summary>
        public int World;

        public Faction Attacker = Faction.Red;
        public int Darts;
    }
}
