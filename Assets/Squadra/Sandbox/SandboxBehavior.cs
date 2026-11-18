using System;
using Squadra.Simulation;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// The behavior the sandbox plays by where it does not take the game's own: how Darts are drawn and
    /// the rules a sandbox match would use. Each value left unticked follows the default in code, so a
    /// later change to the game reaches the sandbox; normal missions never see any of these.
    /// </summary>
    [Serializable]
    public sealed class SandboxBehavior
    {
        // A fleet must move, or a match cannot be set up.
        const float SlowestFleet = 0.01f;

        [Header("Darts")]
        [Tooltip("Radians per second of the innermost orbit lane. The preview stands still, so this shows "
            + "only once the sandbox is played.")]
        [SerializeField] SandboxOverride orbitSpeed = new SandboxOverride(DartDisplay.DefaultOrbitSpeed);
        [Tooltip("Up to this many Darts, every Dart has a dot of its own.")]
        [SerializeField] SandboxOverride oneDotPerDartUpTo = new SandboxOverride(DartDisplay.DefaultOneDotPerDartUpTo);
        [Tooltip("The most dots one fleet is ever drawn with.")]
        [SerializeField] SandboxOverride mostDots = new SandboxOverride(DartDisplay.DefaultMostDots);

        [Header("Rules")]
        [Tooltip("Fleet speed in map units per second. The preview shows it only in how far a fleet near "
            + "either end of its course has fanned out.")]
        [SerializeField] SandboxOverride fleetSpeed = new SandboxOverride((float)new MatchConfig().FleetSpeed);
        [Tooltip("Seconds per Dart built on a planet, as its caption shows.")]
        [SerializeField]
        SandboxOverride planetProductionInterval = new SandboxOverride((float)NodeConfig.PlanetProductionInterval);
        [Tooltip("Seconds per Dart built on a moon, as its caption shows.")]
        [SerializeField]
        SandboxOverride moonProductionInterval = new SandboxOverride((float)NodeConfig.MoonProductionInterval);

        /// <summary>How the sandbox draws Darts: the game's way, but for what is overridden.</summary>
        public DartDisplay Darts()
        {
            return new DartDisplay(orbitSpeed.Value, oneDotPerDartUpTo.WholeValue, mostDots.WholeValue);
        }

        /// <summary>The rules the sandbox replaces; null in each for the game's default.</summary>
        public MatchOverrides Rules()
        {
            float? speed = fleetSpeed.Value;
            return new MatchOverrides
            {
                FleetSpeed = speed.HasValue ? Mathf.Max(SlowestFleet, speed.Value) : (double?)null,
                PlanetProductionInterval = planetProductionInterval.Value,
                MoonProductionInterval = moonProductionInterval.Value,
            };
        }
    }
}
