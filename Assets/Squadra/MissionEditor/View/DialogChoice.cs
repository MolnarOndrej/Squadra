using System;

namespace Squadra
{
    /// <summary>One answer a mission editor dialog offers.</summary>
    public readonly struct DialogChoice
    {
        public DialogChoice(string label, Action chosen)
        {
            Label = label;
            Chosen = chosen;
        }

        public string Label { get; }
        public Action Chosen { get; }
    }
}
