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
        [SerializeField, Range(0f, 1f)] float volume = 0.7f;
        [Tooltip("Random pitch change per play, as a fraction either way.")]
        [SerializeField, Range(0f, 0.25f)] float pitchJitter = 0.04f;

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
}
