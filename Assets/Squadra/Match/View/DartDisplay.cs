using System;
using Unity.Scripting.LifecycleManagement;

namespace Squadra
{
    /// <summary>
    /// The behavior of the drawn Darts, as opposed to their looks: how fast a stationed fleet orbits its
    /// world and how many Darts are drawn for a fleet, at a world and in transit. A Dart drawn may stand
    /// for several real ones; the limits change only what is drawn, never a count, a battle or a result.
    /// The game's values are the defaults here, in code, and changing them is a deliberate change to the
    /// game. A sandbox can try others: each value it leaves out is the default as the code has it now, so
    /// an experiment never keeps an outdated copy.
    /// </summary>
    // The default instance never changes, so there is nothing to reset between play sessions.
    [NoAutoStaticsCleanup]
    public sealed class DartDisplay
    {
        /// <summary>Radians per second of the innermost of a world's three orbit lanes.</summary>
        public const float DefaultOrbitSpeed = 0.42f;

        /// <summary>Up to this many Darts, every Dart is drawn.</summary>
        public const int DefaultOneDotPerDartUpTo = 24;

        /// <summary>
        /// The most Darts drawn for the fleet stationed at a world, however large it is. Both sides of a
        /// battle stand at the world, so each is held to it too.
        /// </summary>
        public const int DefaultMostStationedDots = 64;

        /// <summary>The most Darts drawn for one fleet in transit, however large it is.</summary>
        public const int DefaultMostTravellingDots = 64;

        /// <summary>The highest either limit may be, so that no setting draws without bound.</summary>
        public const int MostDotsAllowed = 1000;

        /// <summary>The game's own values.</summary>
        public static readonly DartDisplay Default = new DartDisplay();

        /// <param name="orbitSpeed">Null for <see cref="DefaultOrbitSpeed"/>.</param>
        /// <param name="oneDotPerDartUpTo">Null for <see cref="DefaultOneDotPerDartUpTo"/>; not below zero.</param>
        /// <param name="mostStationedDots">Null for <see cref="DefaultMostStationedDots"/>; from 1 to <see cref="MostDotsAllowed"/>.</param>
        /// <param name="mostTravellingDots">Null for <see cref="DefaultMostTravellingDots"/>; from 1 to <see cref="MostDotsAllowed"/>.</param>
        public DartDisplay(float? orbitSpeed = null, int? oneDotPerDartUpTo = null, int? mostStationedDots = null,
            int? mostTravellingDots = null)
        {
            OrbitSpeed = orbitSpeed ?? DefaultOrbitSpeed;
            OneDotPerDartUpTo = oneDotPerDartUpTo ?? DefaultOneDotPerDartUpTo;
            MostStationedDots = mostStationedDots ?? DefaultMostStationedDots;
            MostTravellingDots = mostTravellingDots ?? DefaultMostTravellingDots;

            if (OneDotPerDartUpTo < 0)
                throw new ArgumentOutOfRangeException(nameof(oneDotPerDartUpTo), OneDotPerDartUpTo, "Cannot be below zero.");
            if (MostStationedDots < 1 || MostStationedDots > MostDotsAllowed)
                throw new ArgumentOutOfRangeException(nameof(mostStationedDots), MostStationedDots,
                    "Must be from 1 to " + MostDotsAllowed + ".");
            if (MostTravellingDots < 1 || MostTravellingDots > MostDotsAllowed)
                throw new ArgumentOutOfRangeException(nameof(mostTravellingDots), MostTravellingDots,
                    "Must be from 1 to " + MostDotsAllowed + ".");
        }

        public float OrbitSpeed { get; }
        public int OneDotPerDartUpTo { get; }
        public int MostStationedDots { get; }
        public int MostTravellingDots { get; }
    }
}
