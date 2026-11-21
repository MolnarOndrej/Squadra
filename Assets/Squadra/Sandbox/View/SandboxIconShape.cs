namespace Squadra
{
    /// <summary>What a <see cref="SandboxIcon"/> shows.</summary>
    public enum SandboxIconShape
    {
        /// <summary>A pointing hand: Order mode, where clicks send fleets.</summary>
        Hand,

        /// <summary>Two bars: the battlefield is running, and pressing pauses it.</summary>
        Pause,

        /// <summary>A triangle: the battlefield is paused, and pressing resumes it.</summary>
        Play,

        /// <summary>A row of arrows, one for each step of game speed.</summary>
        Speed,

        /// <summary>A circling arrow: lay the starting setup down again.</summary>
        Reset,

        /// <summary>An arrow to the right: slide the panel away.</summary>
        Collapse,

        /// <summary>An arrow to the left: slide the panel back in.</summary>
        Expand,
    }
}
