using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// How every world is drawn around its surface: the orbit its fleet circles on, the ownership ring,
    /// the aura, the capture ring and the selection ring. One asset is shared by every mission, so a
    /// change here reaches all of them. The owner's and captor's colors come from <see cref="Palette"/>;
    /// how fast rings turn and pulse is timing and stays in code. <c>Squadra &gt; Rebuild Mission Scene</c>
    /// creates the asset with these defaults when it is missing and otherwise leaves it as edited.
    /// </summary>
    [CreateAssetMenu(menuName = "Squadra/World Style", fileName = "WorldStyle")]
    public sealed class WorldStyle : ScriptableObject
    {
        [Header("Orbit")]
        [Tooltip("Distance from a world's surface to the orbit its fleet circles on, which the ownership ring "
            + "marks. Routes and fleets in transit start and end on it.")]
        [SerializeField, Min(0f)] float orbitGap = 0.42f;

        [Header("Ownership ring")]
        [Tooltip("A neutral world: a faint dashed circle.")]
        [SerializeField]
        RingTuning neutralRing = new RingTuning(
            thickness: 0.018f, softness: 0.05f, glow: 0.1f, dashes: 44f, dashFill: 0.5f, track: 0f,
            additive: 0.7f, brightness: 0.9f, opacity: 0.55f);
        [Tooltip("An owned world: a solid glowing band. A world changing hands blends between the two.")]
        [SerializeField]
        RingTuning ownedRing = new RingTuning(
            thickness: 0.03f, softness: 0.075f, glow: 0.25f, dashes: 0f, dashFill: 0.5f, track: 0f,
            additive: 0.7f, brightness: 1.65f, opacity: 1f);
        [Tooltip("Extra brightness of the ring at the height of the flash when a world changes hands.")]
        [SerializeField, Min(0f)] float flashBrightness = 2f;

        [Header("Aura")]
        [Tooltip("How far the aura reaches beyond the orbit, in world units.")]
        [SerializeField, Min(0f)] float auraReach = 1.5f;
        [SerializeField, Min(0f)] float auraBrightness = 1f;
        [SerializeField, Range(0f, 1f)] float neutralAuraOpacity = 0.03f;
        [SerializeField, Range(0f, 1f)] float ownedAuraOpacity = 0.11f;
        [Tooltip("Opacity added at the height of the flash when a world changes hands.")]
        [SerializeField, Range(0f, 1f)] float flashAuraOpacity = 0.5f;
        [Tooltip("Opacity added while the world is selected.")]
        [SerializeField, Range(0f, 1f)] float selectedAuraOpacity = 0.08f;

        [Header("Capture ring")]
        [Tooltip("Distance from the orbit to the capture ring, in world units.")]
        [SerializeField] float captureOffset = 0.2f;
        [Tooltip("A capture under way: an arc in the captor's color filling clockwise over a faint track.")]
        [SerializeField]
        RingTuning runningCapture = new RingTuning(
            thickness: 0.075f, softness: 0.1f, glow: 0.35f, dashes: 0f, dashFill: 0.55f, track: 0.1f,
            additive: 0.6f, brightness: 2.3f, opacity: 1f);
        [Tooltip("How far the brightness of a capture under way swings up and down as it pulses.")]
        [SerializeField, Min(0f)] float capturePulse = 0.4f;
        [Tooltip("A capture held while the captor fights: broken into dashes and no longer pulsing.")]
        [SerializeField]
        RingTuning heldCapture = new RingTuning(
            thickness: 0.075f, softness: 0.1f, glow: 0.15f, dashes: 40f, dashFill: 0.55f, track: 0.22f,
            additive: 0.6f, brightness: 1.3f, opacity: 1f);

        [Header("Selection ring")]
        [SerializeField] Color selectionColor = Palette.Accent;
        [Tooltip("Distance from the orbit to the selection ring, in world units.")]
        [SerializeField] float selectionOffset = 0.3f;
        [Tooltip("Distance from the orbit to the selection ring while a capture ring is shown.")]
        [SerializeField] float selectionOffsetWhileCapturing = 0.44f;
        [Tooltip("A world under the pointer. Its opacity is how strongly a hovered world is outlined.")]
        [SerializeField]
        RingTuning hoveredRing = new RingTuning(
            thickness: 0.022f, softness: 0.045f, glow: 0f, dashes: 36f, dashFill: 0.55f, track: 0f,
            additive: 0.6f, brightness: 1.6f, opacity: 0.45f);
        [Tooltip("The selected world. Selecting blends from the hovered look to this one.")]
        [SerializeField]
        RingTuning selectedRing = new RingTuning(
            thickness: 0.036f, softness: 0.045f, glow: 0.22f, dashes: 36f, dashFill: 0.55f, track: 0f,
            additive: 0.6f, brightness: 2.4f, opacity: 1f);
        [Tooltip("How far the selected ring's radius swings in and out as it pulses, in world units.")]
        [SerializeField, Min(0f)] float selectionPulse = 0.035f;

        public float OrbitGap => orbitGap;

        public RingTuning NeutralRing => neutralRing;
        public RingTuning OwnedRing => ownedRing;
        public float FlashBrightness => flashBrightness;

        public float AuraReach => auraReach;
        public float AuraBrightness => auraBrightness;
        public float NeutralAuraOpacity => neutralAuraOpacity;
        public float OwnedAuraOpacity => ownedAuraOpacity;
        public float FlashAuraOpacity => flashAuraOpacity;
        public float SelectedAuraOpacity => selectedAuraOpacity;

        public float CaptureOffset => captureOffset;
        public RingTuning RunningCapture => runningCapture;
        public float CapturePulse => capturePulse;
        public RingTuning HeldCapture => heldCapture;

        public Color SelectionColor => selectionColor;
        public float SelectionOffset => selectionOffset;
        public float SelectionOffsetWhileCapturing => selectionOffsetWhileCapturing;
        public RingTuning HoveredRing => hoveredRing;
        public RingTuning SelectedRing => selectedRing;
        public float SelectionPulse => selectionPulse;
    }
}
