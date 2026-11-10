using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Squadra
{
    /// <summary>
    /// Loops the background track. The first player to wake keeps playing across scene reloads, so
    /// restarting a match does not restart the music; later copies remove themselves.
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

        AudioSource source;
        float fadeIn;
        float level = 1f;
        bool ducked;

        /// <summary>Lowers the music under something important, or brings it back.</summary>
        public static void Duck(bool ducked)
        {
            if (current != null) current.ducked = ducked;
        }

        void Awake()
        {
            if (current != null)
            {
                Destroy(gameObject);
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

        // A freshly loaded scene is a new match: bring the music back up.
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ducked = false;
        }

        void Update()
        {
            if (current != this) return; // a duplicate waiting to be destroyed

            float deltaTime = Time.unscaledDeltaTime;
            fadeIn = fadeInSeconds > 0f ? Mathf.Clamp01(fadeIn + deltaTime / fadeInSeconds) : 1f;
            float target = ducked ? duckedLevel : 1f;
            level = duckSeconds > 0f ? Mathf.MoveTowards(level, target, deltaTime / duckSeconds) : target;
            source.volume = volume * fadeIn * level;
        }
    }
}
