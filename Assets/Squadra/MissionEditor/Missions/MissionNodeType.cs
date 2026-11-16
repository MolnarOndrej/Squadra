namespace Squadra.Missions
{
    /// <summary>What a map node is. The words are the game's own; the file spells them in lower case.</summary>
    public enum MissionNodeType
    {
        Planet,
        Moon,

        /// <summary>A fleet location like the others that never produces Darts.</summary>
        Asteroid,
    }
}
