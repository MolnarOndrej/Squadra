using Squadra.Pause;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Squadra
{
    /// <summary>
    /// Loops the background track. The first player to wake keeps playing across scene loads. A later
    /// copy with the same track removes itself, so restarting a match or moving around the menu does not
    /// restart the music; a later copy with a different track hands its track over first, which is how
    /// the menu music gives way to the mission music. A paused mission keeps its track playing, quieter.
    /// </summary>
    [AutoStaticsCleanup]
    public sealed partial class MusicPlayer : MonoBehaviour
    {
        static MusicPlayer current;

        [SerializeField] AudioClip track;
        [Range(0f, 1f)] [SerializeField] float volume = 0.3f;
        [SerializeField] float fadeInSeconds = 3f;
        [Tooltip("Music level while a match result is on screen, as a fraction of the normal volume.")]
        [Range(0f, 1f)] [SerializeField] float duckedLevel = 0.3f;
        [SerializeField] float duckSeconds = 0.5f;
        [Tooltip("Seconds the playing track takes to fade out when another scene brings a different one.")]
        [SerializeField] float switchSeconds = 0.6f;
        [Tooltip("Seconds the music takes to drop when the mission is paused, and to come back.")]
        [SerializeField] float pauseSeconds = 0.25f;

        AudioSource source;
        float fadeIn;
        float level = 1f;
        float pauseLevel = 1f;
        bool ducked;
        bool paused;
        MusicPlayer next;

        /// <summary>The track that is playing or about to play, or null when there is no music.</summary>
        public static AudioClip CurrentTrack => current != null ? current.track : null;

        /// <summary>Lowers the music under something important, or brings it back.</summary>
        public static void Duck(bool ducked)
        {
            if (current != null) current.ducked = ducked;
        }

        /// <summary>
        /// Holds the music at its paused level while the mission is paused, on top of the volume settings
        /// and of <see cref="Duck"/>. The track keeps playing from where it is. The level is fixed, so
        /// pausing again never lowers it further, and nothing of it is saved.
        /// </summary>
        public static void SetPaused(bool paused)
        {
            if (current != null) current.paused = paused;
        }

        /// <summary>True while the music is held at its paused level.</summary>
        public static bool Paused => current != null && current.paused;

        /// <summary>
        /// The level the music is playing at before the master volume, or 0 with no music. Read by play
        /// tests to compare the mix.
        /// </summary>
        public static float Level => current != null && current.source != null ? current.source.volume : 0f;

        /// <summary>How far into its track the music is, in seconds. It only goes back when the track loops or changes.</summary>
        public static float Position => current != null && current.source != null ? current.source.time : 0f;

        void Awake()
        {
            if (current != null)
            {
                if (track != null && track != current.track) current.SwitchTo(this);
                else Destroy(gameObject);
                return;
            }

            current = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;

            source = gameObject.AddComponent<AudioSource>();
            source.clip = track;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.priority = 0;
            source.volume = 0f;
            if (track != null) source.Play();
        }

        void OnDestroy()
        {
            if (current != this) return;

            current = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // A freshly loaded scene is a new match, or the menu: bring the music back up.
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ducked = false;
            paused = false;
        }

        // Takes over another player's track and levels: fade out, swap, fade in. The other player only
        // carries the settings and is removed once they are copied.
        void SwitchTo(MusicPlayer other)
        {
            if (next != null) Destroy(next.gameObject);

            next = other;
            track = other.track;
            other.enabled = false;
            DontDestroyOnLoad(other.gameObject); // keep it until the fade-out is done
        }

        void Update()
        {
            if (current != this) return; // a duplicate waiting to be destroyed

            float deltaTime = Time.unscaledDeltaTime;
            if (next != null)
            {
                fadeIn = switchSeconds > 0f ? Mathf.MoveTowards(fadeIn, 0f, deltaTime / switchSeconds) : 0f;
                if (fadeIn <= 0f)
                {
                    volume = next.volume;
                    fadeInSeconds = next.fadeInSeconds;
                    duckedLevel = next.duckedLevel;
                    duckSeconds = next.duckSeconds;
                    switchSeconds = next.switchSeconds;
                    Destroy(next.gameObject);
                    next = null;

                    source.clip = track;
                    source.Play();
                }
            }
            else
            {
                fadeIn = fadeInSeconds > 0f ? Mathf.Clamp01(fadeIn + deltaTime / fadeInSeconds) : 1f;
            }

            float target = ducked ? duckedLevel : 1f;
            level = duckSeconds > 0f ? Mathf.MoveTowards(level, target, deltaTime / duckSeconds) : target;

            float pauseTarget = AudioMix.MusicFactor(paused);
            pauseLevel = pauseSeconds > 0f
                ? Mathf.MoveTowards(pauseLevel, pauseTarget, deltaTime / pauseSeconds)
                : pauseTarget;
            source.volume = volume * fadeIn * level * pauseLevel * GameSettings.MusicVolume;
        }
    }
}
