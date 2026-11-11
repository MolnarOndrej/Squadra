using UnityEngine;
using UnityEngine.UIElements;

namespace Squadra
{
    /// <summary>
    /// Plays the menu's interface cues: one when the pointer enters a button, one when a button or tab is
    /// activated or the map page changes, and a different one for going back or cancelling a dialog.
    /// The screens decide which cue an action gets; this component only plays it.
    /// </summary>
    public sealed class MenuAudio : MonoBehaviour
    {
        [SerializeField] AudioClip hover;
        [SerializeField] AudioClip select;
        [SerializeField] AudioClip back;
        [Range(0f, 1f)] [SerializeField] float volume = 0.6f;
        [Tooltip("Hover level as a fraction of the other cues, so sweeping across buttons stays quiet.")]
        [Range(0f, 1f)] [SerializeField] float hoverLevel = 0.5f;

        AudioSource source;

        /// <summary>How many cues of each kind have played since the scene loaded. Read by play tests.</summary>
        public int HoverCount { get; private set; }
        public int SelectCount { get; private set; }
        public int BackCount { get; private set; }

        public void Hover()
        {
            HoverCount++;
            Play(hover, hoverLevel);
        }

        public void Select()
        {
            SelectCount++;
            Play(select, 1f);
        }

        public void Back()
        {
            BackCount++;
            Play(back, 1f);
        }

        /// <summary>
        /// Makes a button sound and take keyboard focus when the pointer enters it, so the mouse and the
        /// keyboard always agree on which button is highlighted. Disabled buttons stay silent.
        /// </summary>
        public void AttachHover(Button button)
        {
            button.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
        }

        void OnPointerEnter(PointerEnterEvent pointer)
        {
            var element = (VisualElement)pointer.currentTarget;
            if (!element.enabledInHierarchy) return;

            Hover();
            element.Focus();
        }

        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        void Play(AudioClip clip, float level)
        {
            if (clip == null || source == null) return;
            source.PlayOneShot(clip, volume * level * GameSettings.SfxVolume);
        }
    }
}
