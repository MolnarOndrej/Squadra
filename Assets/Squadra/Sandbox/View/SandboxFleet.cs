using System;
using Squadra.Simulation;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// A fleet of the sandbox already on its way from one world to another when it starts. Its Darts are
    /// its own, apart from those stationed at the world it set out from. Edit Mode shows it where it is;
    /// Play Mode flies it on from there, and it arrives by the rules like any fleet.
    /// </summary>
    [Serializable]
    public sealed class SandboxFleet
    {
        [SerializeField] SandboxWorld from;
        [SerializeField] SandboxWorld to;
        [SerializeField] Faction faction = Faction.Blue;
        [SerializeField, Min(1)] int darts = 10;
        [Tooltip("How far along its course the fleet is: 0 as it sets out, 1 as it arrives.")]
        [SerializeField, Range(0f, 1f)] float progress = 0.5f;

        public SandboxWorld From => from;
        public SandboxWorld To => to;
        public Faction Faction => faction;
        public int Darts => darts;
        public float Progress => progress;
    }
}
