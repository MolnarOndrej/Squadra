using Unity.Scripting.LifecycleManagement;

namespace Squadra
{
    /// <summary>
    /// The behavior of the Darts' dots, as opposed to their looks: how fast a stationed fleet orbits its
    /// world and how many dots stand for a fleet. The game's values are the defaults here, in code, and
    /// changing them is a deliberate change to the game. A sandbox can try others: each value it leaves
    /// out is the default as the code has it now, so an experiment never keeps an outdated copy.
    /// </summary>
    // The default instance never changes, so there is nothing to reset between play sessions.
    [NoAutoStaticsCleanup]
    public sealed class DartDisplay
    {
        /// <summary>Radians per second of the innermost of a world's three orbit lanes.</summary>
        public const float DefaultOrbitSpeed = 0.42f;

        /// <summary>Up to this many Darts, every Dart has a dot of its own.</summary>
        public const int DefaultOneDotPerDartUpTo = 24;

        /// <summary>The most dots one fleet is ever drawn with, however large it is.</summary>
        public const int DefaultMostDots = 64;

        /// <summary>The game's own values.</summary>
        public static readonly DartDisplay Default = new DartDisplay();

        /// <param name="orbitSpeed">Null for <see cref="DefaultOrbitSpeed"/>.</param>
        /// <param name="oneDotPerDartUpTo">Null for <see cref="DefaultOneDotPerDartUpTo"/>.</param>
        /// <param name="mostDots">Null for <see cref="DefaultMostDots"/>.</param>
        public DartDisplay(float? orbitSpeed = null, int? oneDotPerDartUpTo = null, int? mostDots = null)
        {
            OrbitSpeed = orbitSpeed ?? DefaultOrbitSpeed;
            OneDotPerDartUpTo = oneDotPerDartUpTo ?? DefaultOneDotPerDartUpTo;
            MostDots = mostDots ?? DefaultMostDots;
        }

        public float OrbitSpeed { get; }
        public int OneDotPerDartUpTo { get; }
        public int MostDots { get; }
    }
}
