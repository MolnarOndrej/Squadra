using System;
using UnityEngine;

namespace Squadra
{
    /// <summary>A route of the sandbox: an idle lane between two of its worlds, both ways.</summary>
    [Serializable]
    public sealed class SandboxRoute
    {
        [SerializeField] SandboxWorld from;
        [SerializeField] SandboxWorld to;

        public SandboxWorld From => from;
        public SandboxWorld To => to;
    }
}
