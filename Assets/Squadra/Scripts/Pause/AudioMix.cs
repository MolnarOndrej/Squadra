namespace Squadra.Pause
{
    /// <summary>
    /// How the temporary audio states combine with the player's volume preferences. Each state is a
    /// factor on top of the saved volumes, so none of them changes a preference, and taking a state
    /// away brings back whatever the preferences say at that moment.
    /// </summary>
    public static class AudioMix
    {
        /// <summary>Mission music level while the game is paused, as a fraction of its unpaused level.</summary>
        public const float PausedMusicLevel = 0.5f;

        /// <summary>
        /// The level of everything the player hears. Master volume scales it; Mute in Background
        /// silences it while another application is in front, and only then.
        /// </summary>
        public static float Output(float masterVolume, bool muteInBackground, bool focused)
        {
            if (muteInBackground && !focused) return 0f;
            return Clamp01(masterVolume);
        }

        /// <summary>
        /// The factor pausing puts on the music. It is a fixed level rather than a step down, so pausing
        /// again, or moving between the pause menu's screens, never lowers the music further.
        /// </summary>
        public static float MusicFactor(bool paused)
        {
            return paused ? PausedMusicLevel : 1f;
        }

        static float Clamp01(float value)
        {
            if (!(value > 0f)) return 0f; // also catches NaN
            return value < 1f ? value : 1f;
        }
    }
}
