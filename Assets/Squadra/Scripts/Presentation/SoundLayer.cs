using System.Collections.Generic;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// One sound event with several interchangeable takes. Each play picks a take at random (never the
    /// same one twice in a row when there are three or more) and nudges its pitch and level a little.
    /// </summary>
    [System.Serializable]
    public sealed class SoundCue
    {
        [Tooltip("Takes of this sound. One is picked at random each time it plays.")]
        [SerializeField] AudioClip[] clips = System.Array.Empty<AudioClip>();
        [Range(0f, 1f)] [SerializeField] float volume = 0.7f;
        [Tooltip("Random pitch change per play, as a fraction either way.")]
        [Range(0f, 0.25f)] [SerializeField] float pitchJitter = 0.04f;

        int last = -1;

        public SoundCue()
        {
        }

        public SoundCue(float volume, float pitchJitter)
        {
            this.volume = volume;
            this.pitchJitter = pitchJitter;
        }

        public float Volume => volume;
        public float PitchJitter => pitchJitter;

        public AudioClip Next()
        {
            if (clips == null || clips.Length == 0) return null;

            int index;
            if (clips.Length < 3 || last < 0)
            {
                index = Random.Range(0, clips.Length);
            }
            else
            {
                index = Random.Range(0, clips.Length - 1);
                if (index >= last) index++;
            }

            last = index;
            return clips[index];
        }
    }

    /// <summary>
    /// Plays the match's sound effects: fleets leaving and arriving, battles, captures and the result.
    /// Sounds are panned by where they happen on the map. The game controller says what happened;
    /// this layer picks the take and the timing. While the mission is paused every effect holds where
    /// it is and no new one starts.
    /// </summary>
    public sealed class SoundLayer : MonoBehaviour
    {
        [SerializeField] CameraRig rig;
        [Range(0f, 1f)] [SerializeField] float volume = 0.85f;
        [Tooltip("How far a sound at the edge of the view is panned to that side.")]
        [Range(0f, 1f)] [SerializeField] float panWidth = 0.6f;
        [Tooltip("How many effects can sound at once before the oldest is cut off.")]
        [SerializeField] int voiceCount = 20;

        [Header("Fleets")]
        [SerializeField] SoundCue departure = new SoundCue(0.55f, 0.04f);
        [SerializeField] SoundCue arrival = new SoundCue(0.5f, 0.03f);

        [Header("Battle")]
        [SerializeField] SoundCue battleStart = new SoundCue(0.65f, 0.03f);
        [SerializeField] SoundCue shot = new SoundCue(0.3f, 0.1f);
        [SerializeField] SoundCue shipDestroyed = new SoundCue(0.55f, 0.08f);

        [Header("Worlds")]
        [SerializeField] SoundCue capture = new SoundCue(0.7f, 0f);

        [Header("Match result")]
        [SerializeField] AudioClip victory;
        [SerializeField] AudioClip defeat;
        [Range(0f, 1f)] [SerializeField] float resultVolume = 0.9f;
        [Tooltip("Seconds between the match ending and its result sound, while the other effects fade.")]
        [SerializeField] float resultDelay = 0.25f;

        struct Pending
        {
            public float At;
            public SoundCue Cue;
            public Vector2 Position;
            public float Gain;
        }

        readonly List<Pending> pending = new List<Pending>();
        AudioSource[] voices;
        float[] voiceGains;
        AudioSource resultVoice;
        int nextVoice;
        float clock;
        float resultIn = -1f;
        bool matchOver;
        bool paused;

        /// <summary>True while the effects are held by a pause.</summary>
        public bool Paused => paused;

        /// <summary>How many effects are sounding right now, the result included. Held effects do not count.</summary>
        public int PlayingCount
        {
            get
            {
                int count = resultVoice != null && resultVoice.isPlaying ? 1 : 0;
                if (voices == null) return count;
                foreach (AudioSource voice in voices)
                    if (voice.isPlaying) count++;
                return count;
            }
        }

        // ------------------------------------------------------------------ events

        public void FleetDeparted(Vector2 position) => Play(departure, position);

        /// <summary>A fleet reached a neutral or allied world without a fight.</summary>
        public void FleetArrived(Vector2 position) => Play(arrival, position);

        public void BattleStarted(Vector2 position) => Play(battleStart, position);

        public void Shot(Vector2 position) => Play(shot, position);

        public void ShipDestroyed(Vector2 position) => Play(shipDestroyed, position);

        public void WorldCaptured(Vector2 position) => Play(capture, position);

        /// <summary>
        /// Fills <paramref name="duration"/> seconds with the weapon fire and explosions of a battle in
        /// which each side lost <paramref name="shipsLost"/> ships. Bigger battles are denser.
        /// </summary>
        public void Skirmish(Vector2 position, int shipsLost, float duration)
        {
            float intensity = Mathf.Clamp01(shipsLost / 40f);
            int shots = Mathf.Clamp(Mathf.RoundToInt(duration * Mathf.Lerp(5f, 10f, intensity)), 3, 60);
            int kills = Mathf.Clamp(1 + shipsLost / 5, 1, Mathf.Max(2, Mathf.RoundToInt(duration * 4f)));

            for (int i = 0; i < shots; i++)
                Schedule(shot, Random.Range(0.08f, duration), Scatter(position), Random.Range(0.6f, 1f));

            // Explosions start once the shooting is under way; the last one closes the battle.
            for (int i = 0; i < kills; i++)
            {
                float spread = kills == 1 ? 0.6f : Mathf.Lerp(0.3f, 1f, i / (float)(kills - 1));
                float at = Mathf.Max(0.05f, spread * duration + Random.Range(-0.06f, 0.06f));
                Schedule(shipDestroyed, at, Scatter(position), Random.Range(0.7f, 1f));
            }
        }

        /// <summary>Fades out whatever is still sounding and plays the result over the ducked music.</summary>
        public void MatchEnded(bool won)
        {
            if (matchOver) return;

            matchOver = true;
            pending.Clear();
            MusicPlayer.Duck(true);

            AudioClip clip = won ? victory : defeat;
            if (clip == null) return;

            // The delay is counted here rather than scheduled, so that a pause can hold it.
            resultVoice.clip = clip;
            resultVoice.volume = volume * resultVolume * GameSettings.SfxVolume;
            resultIn = Mathf.Max(0f, resultDelay);
        }

        /// <summary>
        /// Holds every effect where it is while the mission is paused, the result sound included, and
        /// lets them carry on afterwards at the Sound Effects volume set by then. Nothing new starts
        /// while paused.
        /// </summary>
        public void SetPaused(bool value)
        {
            if (value == paused) return;

            paused = value;
            if (value)
            {
                foreach (AudioSource voice in voices)
                    voice.Pause();
                resultVoice.Pause();
                return;
            }

            // Effects fading out after the match keep fading from where they were.
            float sfx = GameSettings.SfxVolume;
            for (int i = 0; i < voices.Length; i++)
            {
                if (!matchOver) voices[i].volume = voiceGains[i] * sfx;
                voices[i].UnPause();
            }

            resultVoice.volume = volume * resultVolume * sfx;
            resultVoice.UnPause();
        }

        // ------------------------------------------------------------------ playback

        void Awake()
        {
            voices = new AudioSource[Mathf.Max(1, voiceCount)];
            voiceGains = new float[voices.Length];
            for (int i = 0; i < voices.Length; i++)
                voices[i] = CreateVoice();
            resultVoice = CreateVoice();
            resultVoice.priority = 0;
        }

        AudioSource CreateVoice()
        {
            var voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            voice.dopplerLevel = 0f;
            return voice;
        }

        void Update()
        {
            if (paused) return;

            if (matchOver)
            {
                if (resultIn >= 0f)
                {
                    resultIn -= Time.unscaledDeltaTime;
                    if (resultIn <= 0f)
                    {
                        resultIn = -1f;
                        resultVoice.Play();
                    }
                }

                float fade = Mathf.Exp(-Time.unscaledDeltaTime / 0.1f);
                foreach (AudioSource voice in voices)
                {
                    if (!voice.isPlaying) continue;
                    voice.volume *= fade;
                    if (voice.volume < 0.002f) voice.Stop();
                }

                return;
            }

            clock += Time.deltaTime;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                Pending due = pending[i];
                if (due.At > clock) continue;

                pending.RemoveAt(i);
                Play(due.Cue, due.Position, due.Gain);
            }
        }

        void Schedule(SoundCue cue, float delay, Vector2 position, float gain)
        {
            pending.Add(new Pending { At = clock + delay, Cue = cue, Position = position, Gain = gain });
        }

        void Play(SoundCue cue, Vector2 position, float gain = 1f)
        {
            if (matchOver || paused) return;

            AudioClip clip = cue.Next();
            if (clip == null) return;

            AudioSource voice = TakeVoice(out int index);
            voiceGains[index] = volume * cue.Volume * gain * Random.Range(0.9f, 1f);
            voice.clip = clip;
            voice.volume = voiceGains[index] * GameSettings.SfxVolume;
            voice.pitch = 1f + Random.Range(-cue.PitchJitter, cue.PitchJitter);
            voice.panStereo = Pan(position);
            voice.Play();
        }

        // A silent voice if there is one; otherwise the next in line is cut off and reused.
        AudioSource TakeVoice(out int index)
        {
            index = nextVoice;
            for (int i = 0; i < voices.Length; i++)
            {
                int candidate = (nextVoice + i) % voices.Length;
                if (voices[candidate].isPlaying) continue;

                index = candidate;
                break;
            }

            nextVoice = (index + 1) % voices.Length;
            return voices[index];
        }

        float Pan(Vector2 position)
        {
            if (rig == null) return 0f;

            Rect view = rig.View;
            return Mathf.Clamp((position.x - view.center.x) / (view.width * 0.5f), -1f, 1f) * panWidth;
        }

        static Vector2 Scatter(Vector2 position)
        {
            return position + Random.insideUnitCircle * 0.8f;
        }
    }
}
