using System;

namespace Squadra.MissionEditing
{
    /// <summary>A save that did not happen. The message is written for the author.</summary>
    public sealed class MissionFileException : Exception
    {
        public MissionFileException(string message, Exception inner = null) : base(message, inner)
        {
        }
    }
}
