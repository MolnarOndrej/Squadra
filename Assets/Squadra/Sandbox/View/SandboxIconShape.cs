namespace Squadra
{
    /// <summary>What a <see cref="SandboxIcon"/> shows.</summary>
    public enum SandboxIconShape
    {
        /// <summary>A pointing hand: Order mode, where clicks send fleets.</summary>
        Hand,

        /// <summary>An arrow pointer: Inspect mode, where clicks select worlds to edit.</summary>
        Cursor,

        /// <summary>Two bars: the battlefield is running, and pressing pauses it.</summary>
        Pause,

        /// <summary>A triangle: the battlefield is paused, and pressing resumes it.</summary>
        Play,

        /// <summary>A circling arrow: lay the starting setup down again, or set a value back to its default.</summary>
        Reset,

        /// <summary>An arrow to the right: slide the panel away.</summary>
        Collapse,

        /// <summary>An arrow to the left: slide the panel back in.</summary>
        Expand,
    }
}
