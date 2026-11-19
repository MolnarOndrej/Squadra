using System;
using System.Collections.Generic;

namespace Squadra.Pause
{
    /// <summary>
    /// How fast gameplay runs right now. It keeps three things apart: the saved game speed preference,
    /// a sandbox run's temporary speed and the pause. While paused gameplay gets no time; otherwise it
    /// runs at the sandbox's speed when one is chosen, or at the preference. Menus are not gameplay and
    /// always run at normal speed. Neither the pause nor the sandbox's speed ever changes the
    /// preference, so saving it never stores either of them.
    ///
    /// The result is one factor on elapsed time, applied once to everything that moves with the game:
    /// production, travel, capture, battles, the match clock, orbits and effects. The camera, the menus,
    /// the music and the display confirmation keep real time.
    /// </summary>
    public sealed class GameClock
    {
        /// <summary>The speed of a new profile, and of a saved value the game does not know.</summary>
        public const GameSpeed DefaultSpeed = GameSpeed.Normal;

        static readonly GameSpeed[] Order = { GameSpeed.Slow, GameSpeed.Normal, GameSpeed.Fast, GameSpeed.Turbo };

        /// <summary>Every speed the player can choose, in menu order.</summary>
        public static IReadOnlyList<GameSpeed> Speeds => Order;

        public GameClock(GameSpeed preference = DefaultSpeed)
        {
            Preference = IsDefined(preference) ? preference : DefaultSpeed;
        }

        /// <summary>The player's saved choice. Only <see cref="Choose"/> changes it.</summary>
        public GameSpeed Preference { get; private set; }

        /// <summary>The sandbox run's temporary speed, or null when the run follows the preference.</summary>
        public GameSpeed? Override { get; private set; }

        /// <summary>True from the start of a mission or sandbox run until it is left.</summary>
        public bool Running { get; private set; }

        /// <summary>True while a run is frozen under the pause menu.</summary>
        public bool Paused { get; private set; }

        /// <summary>The pace selected for gameplay, paused or not: the sandbox's speed, else the preference.</summary>
        public GameSpeed Speed => Override ?? Preference;

        /// <summary>
        /// The factor on elapsed time right now: 1 outside a run, 0 while paused, otherwise the selected
        /// speed's multiplier.
        /// </summary>
        public float Scale
        {
            get
            {
                if (!Running) return 1f;
                return Paused ? 0f : Multiplier(Speed);
            }
        }

        /// <summary>Raised after anything here changes, and after every <see cref="Choose"/>.</summary>
        public event Action Changed;

        /// <summary>
        /// The player's choice of speed in Settings. It becomes the preference, to be saved, and replaces
        /// any sandbox speed, even when it is the speed already saved. Inside a run it takes effect at
        /// once, or on resuming when paused.
        /// </summary>
        public void Choose(GameSpeed speed)
        {
            if (!IsDefined(speed)) return;

            Preference = speed;
            Override = null;
            Changed?.Invoke(); // also for the same speed: a sandbox selector must show the change back
        }

        /// <summary>
        /// The sandbox's on-screen choice for the rest of the run. It is never saved and does not touch
        /// the preference. Ignored outside a run, since leaving one discards it anyway.
        /// </summary>
        public bool SetOverride(GameSpeed speed)
        {
            if (!Running || !IsDefined(speed)) return false;
            if (Override == speed) return false;

            Override = speed;
            Changed?.Invoke();
            return true;
        }

        /// <summary>A mission or sandbox run starts: unpaused, at the preference, with no sandbox speed.</summary>
        public void BeginRun()
        {
            Running = true;
            Paused = false;
            Override = null;
            Changed?.Invoke();
        }

        /// <summary>
        /// The run is left: the pause and any sandbox speed go with it, and what follows runs at normal
        /// speed. The preference stays as it is.
        /// </summary>
        public void EndRun()
        {
            Running = false;
            Paused = false;
            Override = null;
            Changed?.Invoke();
        }

        /// <summary>
        /// Freezes the run, or lets it carry on at the speed selected now. Returns false when nothing
        /// changed: already in that state, or there is no run.
        /// </summary>
        public bool SetPaused(bool value)
        {
            if (!Running || value == Paused) return false;

            Paused = value;
            Changed?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ the choices

        /// <summary>How much faster than normal gameplay runs at <paramref name="speed"/>.</summary>
        public static float Multiplier(GameSpeed speed)
        {
            switch (speed)
            {
                case GameSpeed.Slow: return 0.75f;
                case GameSpeed.Fast: return 1.5f;
                case GameSpeed.Turbo: return 2f;
                default: return 1f;
            }
        }

        /// <summary>The name the Settings screen shows.</summary>
        public static string NameOf(GameSpeed speed)
        {
            switch (speed)
            {
                case GameSpeed.Slow: return "Slow";
                case GameSpeed.Fast: return "Fast";
                case GameSpeed.Turbo: return "Turbo";
                default: return "Normal";
            }
        }

        /// <summary>
        /// The text the preference is saved as. It names the choice rather than its position or
        /// multiplier, so reordering or retuning the speeds never turns a saved choice into another.
        /// </summary>
        public static string KeyOf(GameSpeed speed)
        {
            switch (speed)
            {
                case GameSpeed.Slow: return "slow";
                case GameSpeed.Fast: return "fast";
                case GameSpeed.Turbo: return "turbo";
                default: return "normal";
            }
        }

        /// <summary>The speed a saved text stands for: Normal when nothing was saved or the text is unknown.</summary>
        public static GameSpeed FromKey(string key)
        {
            foreach (GameSpeed speed in Speeds)
                if (KeyOf(speed) == key)
                    return speed;
            return DefaultSpeed;
        }

        static bool IsDefined(GameSpeed speed)
        {
            foreach (GameSpeed known in Speeds)
                if (known == speed)
                    return true;
            return false;
        }
    }
}
