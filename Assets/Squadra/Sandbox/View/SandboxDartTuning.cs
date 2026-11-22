using System;

namespace Squadra
{
    /// <summary>
    /// How a sandbox run draws its Darts, as it can be tuned while the run goes on: each value the game's
    /// default as the code has it now, or an override. It starts from the overrides saved with the scene
    /// and is only ever a copy: changing it changes neither the scene nor the game's defaults, and a new
    /// run starts from the scene again. A value out of range is refused and nothing changes.
    /// </summary>
    public sealed class SandboxDartTuning
    {
        /// <summary>The fastest orbit, in radians per second either way, that the sandbox can be tuned to.</summary>
        public const float FastestOrbit = 20f;

        public SandboxDartTuning(float? orbitSpeed, int? oneDotPerDartUpTo, int? mostStationedDots,
            int? mostTravellingDots)
        {
            OrbitSpeed = orbitSpeed;
            OneDotPerDartUpTo = oneDotPerDartUpTo;
            MostStationedDots = mostStationedDots;
            MostTravellingDots = mostTravellingDots;
        }

        /// <summary>Each value: the override, or null for the game's default.</summary>
        public float? OrbitSpeed { get; private set; }
        public int? OneDotPerDartUpTo { get; private set; }
        public int? MostStationedDots { get; private set; }
        public int? MostTravellingDots { get; private set; }

        /// <summary>Raised after any value has changed.</summary>
        public event Action Changed;

        /// <summary>The Dart display these values give.</summary>
        public DartDisplay Display()
        {
            return new DartDisplay(OrbitSpeed, OneDotPerDartUpTo, MostStationedDots, MostTravellingDots);
        }

        public bool TrySetOrbitSpeed(float? speed, out string problem)
        {
            problem = speed.HasValue && !(Math.Abs(speed.Value) <= FastestOrbit)
                ? "Orbit speed must be from -" + FastestOrbit + " to " + FastestOrbit + " radians per second."
                : null;
            if (problem != null) return false;

            OrbitSpeed = speed;
            Changed?.Invoke();
            return true;
        }

        public bool TrySetOneDotPerDartUpTo(int? count, out string problem)
        {
            if (!InRange(count, 0, out problem)) return false;

            OneDotPerDartUpTo = count;
            Changed?.Invoke();
            return true;
        }

        public bool TrySetMostStationedDots(int? count, out string problem)
        {
            if (!InRange(count, 1, out problem)) return false;

            MostStationedDots = count;
            Changed?.Invoke();
            return true;
        }

        public bool TrySetMostTravellingDots(int? count, out string problem)
        {
            if (!InRange(count, 1, out problem)) return false;

            MostTravellingDots = count;
            Changed?.Invoke();
            return true;
        }

        static bool InRange(int? count, int least, out string problem)
        {
            problem = count.HasValue && (count.Value < least || count.Value > DartDisplay.MostDotsAllowed)
                ? "It must be a whole number from " + least + " to " + DartDisplay.MostDotsAllowed + "."
                : null;
            return problem == null;
        }
    }
}
