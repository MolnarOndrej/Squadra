using System.Globalization;
using Squadra.Simulation;
using TMPro;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// One planet, moon or asteroid in the scene. It is a copy of the scene's world template, given its
    /// name, place, size and surface when the mission's battlefield is built, and shows the world's live
    /// state: ownership ring and aura in the owner's color, selection outline, fleet count and capture
    /// progress. While a battle is fought here it shows both sides' counts and that they are fighting,
    /// and a capture that was running is shown held at the progress it had reached. The surface is the
    /// shared material of the world's look and is never changed by ownership. The orbit, rings and aura
    /// look as the shared <see cref="WorldStyle"/> says, read every frame, so an edit to it shows at once.
    /// </summary>
    [ExecuteAlways]
    public sealed class WorldView : MonoBehaviour
    {
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int BodyRadiusId = Shader.PropertyToID("_BodyRadius");

        // How the rings move: timing, not looks, so it stays here rather than in the style.
        const float OwnerRingSpin = 0.01f;     // turns per second
        const float SelectionRingSpin = 0.035f;
        const float CapturePulseRate = 6f;     // radians per second
        const float SelectionPulseRate = 4.5f;

        [Header("Look")]
        [Tooltip("How the orbit, rings and aura look. Shared with every world of every mission.")]
        [SerializeField] WorldStyle style;
        [Tooltip("Radius of the body in world units.")]
        [SerializeField] float bodyRadius = 1.1f;
        [SerializeField] MeshRenderer surface;
        [SerializeField] MeshRenderer aura;
        [SerializeField] MeshRenderer ownerRing;
        [SerializeField] MeshRenderer captureRing;
        [SerializeField] MeshRenderer selectionRing;
        [SerializeField] TextMeshPro countLabel;
        [SerializeField] TextMeshPro captionLabel;

        string displayName = "World";
        RingStyle owner;
        RingStyle capture;
        RingStyle selection;
        MaterialPropertyBlock auraBlock;

        Color shownColor = Palette.Neutral;
        float ownedAmount;
        float selectAmount;
        float hoverAmount;
        float punch;
        float flash;
        long shownCountKey = long.MinValue;
        long shownCaptionKey = long.MinValue;

        /// <summary>The Dart count as shown under the world. Both sides' counts during a battle.</summary>
        public string CountText => countLabel != null ? countLabel.GetParsedText() : string.Empty;

        /// <summary>The line under the count: production, a capture, or the battle.</summary>
        public string CaptionText => captionLabel != null ? captionLabel.text : string.Empty;

        public string DisplayName => displayName;
        public float BodyRadius => bodyRadius;

        /// <summary>Radius of the ring the stationed fleet orbits on.</summary>
        public float OrbitRadius => bodyRadius + (style != null ? style.OrbitGap : 0f);

        public float PickRadius => OrbitRadius + 0.3f;
        public Vector2 Position => transform.position;

        /// <summary>
        /// Makes this copy of the template one world of the mission. Call before the copy is first enabled.
        /// </summary>
        public void Setup(string worldName, Vector2 position, float radius, Material surfaceMaterial)
        {
            displayName = worldName ?? string.Empty;
            transform.position = new Vector3(position.x, position.y, 0f);
            bodyRadius = radius;
            if (surface != null) surface.sharedMaterial = surfaceMaterial;
            Layout();
        }

        /// <summary>Scale-pops the fleet count, e.g. when a Dart is built or a fleet arrives.</summary>
        public void Punch(float amount)
        {
            punch = Mathf.Max(punch, amount);
        }

        /// <summary>Briefly brightens the aura, e.g. when ownership changes.</summary>
        public void Flash(float amount)
        {
            flash = Mathf.Max(flash, amount);
        }

        public void Refresh(Node node, float deltaTime, bool selected, bool hovered)
        {
            Render(WorldState.Of(node), deltaTime, selected, hovered);
        }

        // ------------------------------------------------------------------ lifecycle

        void OnEnable()
        {
            EnsureHandles();
            Layout();
            if (!Application.isPlaying) Preview();
        }

        void Update()
        {
            // In the editor, show the template as a neutral world so the scene reads without pressing Play.
            if (!Application.isPlaying) Preview();
        }

        void Preview()
        {
            if (surface == null || ownerRing == null || style == null) return;
            EnsureHandles();
            Layout();
            Render(new WorldState { Interval = 1f }, 1000f, false, false);
        }

        void EnsureHandles()
        {
            if (owner == null && ownerRing != null) owner = new RingStyle(ownerRing);
            if (capture == null && captureRing != null) capture = new RingStyle(captureRing);
            if (selection == null && selectionRing != null) selection = new RingStyle(selectionRing);
            if (auraBlock == null) auraBlock = new MaterialPropertyBlock();
        }

        /// <summary>Sizes and places the child visuals from <see cref="bodyRadius"/>.</summary>
        public void Layout()
        {
            if (surface != null)
            {
                Material material = surface.sharedMaterial;
                float fraction = material != null && material.HasProperty(BodyRadiusId)
                    ? Mathf.Max(0.05f, material.GetFloat(BodyRadiusId))
                    : 0.7f;
                float side = bodyRadius * 2f / fraction;
                surface.transform.localScale = new Vector3(side, side, 1f);
            }

            if (aura != null && style != null)
            {
                float side = (OrbitRadius + style.AuraReach) * 2f;
                aura.transform.localScale = new Vector3(side, side, 1f);
            }

            float labelY = -(OrbitRadius + 0.62f);
            if (countLabel != null) countLabel.transform.localPosition = new Vector3(0f, labelY, 0f);
            if (captionLabel != null) captionLabel.transform.localPosition = new Vector3(0f, labelY - 0.5f, 0f);
        }

        // ------------------------------------------------------------------ rendering

        void Render(WorldState state, float deltaTime, bool selected, bool hovered)
        {
            if (owner == null || style == null) return;

            float blend = 1f - Mathf.Exp(-deltaTime * 6f);
            float fast = 1f - Mathf.Exp(-deltaTime * 14f);
            bool owned = state.Owner != Faction.Neutral;

            shownColor = Color.Lerp(shownColor, Palette.Of(state.Owner), blend);
            ownedAmount = Mathf.Lerp(ownedAmount, owned ? 1f : 0f, blend);
            selectAmount = Mathf.Lerp(selectAmount, selected ? 1f : 0f, fast);
            hoverAmount = Mathf.Lerp(hoverAmount, hovered ? 1f : 0f, fast);
            punch = Mathf.MoveTowards(punch, 0f, deltaTime * 2.2f);
            flash = Mathf.MoveTowards(flash, 0f, deltaTime * 1.6f);

            float time = Application.isPlaying ? Time.time : 0f;

            // Ownership ring: a solid glowing band when owned, a faint dashed circle when neutral. The
            // dashes go or come at once; everything else blends.
            RingTuning ownerLook = RingTuning.Lerp(style.NeutralRing, style.OwnedRing, ownedAmount);
            owner.Radius = OrbitRadius;
            owner.Shape(ownerLook);
            owner.Dashes = owned ? style.OwnedRing.Dashes : style.NeutralRing.Dashes;
            owner.Spin = OwnerRingSpin;
            owner.Tint = Palette.Hdr(shownColor, ownerLook.Brightness + flash * style.FlashBrightness,
                ownerLook.Opacity);
            owner.Apply();

            if (aura != null)
            {
                float alpha = Mathf.Lerp(style.NeutralAuraOpacity, style.OwnedAuraOpacity, ownedAmount)
                    + flash * style.FlashAuraOpacity + selectAmount * style.SelectedAuraOpacity;
                auraBlock.SetVector(TintId, Palette.Hdr(shownColor, style.AuraBrightness, alpha));
                aura.SetPropertyBlock(auraBlock);
            }

            // Capture: an arc in the captor's color filling clockwise over a faint full track. While the
            // captor is fighting, the arc stays where it had got to, broken into dashes and no longer
            // pulsing: held, not lost.
            bool capturing = state.Captor != Faction.Neutral;
            bool held = capturing && state.Fighting;
            capture.Visible = capturing;
            if (capturing)
            {
                RingTuning captureLook = held ? style.HeldCapture : style.RunningCapture;
                capture.Radius = OrbitRadius + style.CaptureOffset;
                capture.Shape(captureLook);
                capture.Arc = state.CaptureProgress;
                capture.Spin = 0f;
                float brightness = held
                    ? captureLook.Brightness
                    : captureLook.Brightness + style.CapturePulse * Mathf.Sin(time * CapturePulseRate);
                capture.Tint = Palette.Hdr(Palette.Of(state.Captor), brightness, captureLook.Opacity);
                capture.Apply();
            }

            // Selection outline: a rotating dashed ring in the selection color; fainter while only hovered.
            float outline = Mathf.Max(selectAmount * style.SelectedRing.Opacity, hoverAmount * style.HoveredRing.Opacity);
            selection.Visible = outline > 0.01f;
            if (selection.Visible)
            {
                RingTuning selectionLook = RingTuning.Lerp(style.HoveredRing, style.SelectedRing, selectAmount);
                float pulse = selectAmount * style.SelectionPulse * Mathf.Sin(time * SelectionPulseRate);
                float offset = capturing ? style.SelectionOffsetWhileCapturing : style.SelectionOffset;
                selection.Radius = OrbitRadius + offset + pulse;
                selection.Shape(selectionLook);
                selection.Spin = SelectionRingSpin;
                selection.Tint = Palette.Hdr(style.SelectionColor, selectionLook.Brightness, outline);
                selection.Apply();
            }

            RenderLabels(state, time);
        }

        void RenderLabels(WorldState state, float time)
        {
            if (countLabel != null)
            {
                bool show = state.Fighting || state.Occupant != Faction.Neutral;
                long key = state.Darts + 100000L * (int)state.Occupant;
                if (state.Fighting) key += 1000000L * (1 + (int)state.Arriving) + 10000000L * state.ArrivingDarts;
                if (key != shownCountKey)
                {
                    shownCountKey = key;
                    if (state.Fighting)
                    {
                        // Both sides, each in its own color: the stationed side first.
                        countLabel.color = Color.white;
                        countLabel.text = Colored(state.Occupant, state.Darts)
                            + "<size=38%><color=#" + ColorUtility.ToHtmlStringRGB(Palette.TextDim) + ">  VS  </color></size>"
                            + Colored(state.Arriving, state.ArrivingDarts);
                    }
                    else
                    {
                        if (show) countLabel.SetText("{0}", state.Darts);
                        else countLabel.SetText(string.Empty);
                        countLabel.color = Palette.Bright(Palette.Of(state.Occupant), 0.3f);
                    }
                }

                float scale = 1f + punch * 0.35f;
                countLabel.transform.localScale = new Vector3(scale, scale, 1f);

                // With no count to show, the caption moves up into its place.
                if (captionLabel != null)
                {
                    float labelY = -(OrbitRadius + 0.62f);
                    captionLabel.transform.localPosition = new Vector3(0f, show ? labelY - 0.5f : labelY + 0.2f, 0f);
                }
            }

            if (captionLabel == null) return;

            // The caption only changes when its content does: one key per distinct text.
            bool capturing = state.Captor != Faction.Neutral;
            int tenths = capturing ? Mathf.CeilToInt(state.CaptureRemaining * 10f) : 0;
            long captionKey = capturing
                ? 1000000 + (int)state.Captor * 100000 + tenths
                : (int)state.Owner * 10000 + Mathf.RoundToInt(state.Interval * 100f);
            if (state.Fighting) captionKey += 100000000L * (1 + (int)state.Arriving) + 1000000000L * (int)state.Occupant;

            if (captionKey != shownCaptionKey)
            {
                shownCaptionKey = captionKey;
                if (state.Fighting && capturing)
                {
                    // No battle countdown: a reinforcement can change how long the fight lasts.
                    captionLabel.text = "FIGHTING  -  " + Palette.NameOf(state.Captor) + " CAPTURE PAUSED  "
                        + (tenths / 10f).ToString("0.0", CultureInfo.InvariantCulture) + "s";
                    captionLabel.color = Palette.Bright(Palette.Accent, 0.15f);
                }
                else if (state.Fighting)
                {
                    captionLabel.text = "FIGHTING  -  " + Palette.NameOf(state.Occupant) + " VS "
                        + Palette.NameOf(state.Arriving);
                    captionLabel.color = Palette.Bright(Palette.Accent, 0.15f);
                }
                else if (capturing)
                {
                    captionLabel.text = Palette.NameOf(state.Captor) + " CAPTURING  "
                        + (tenths / 10f).ToString("0.0", CultureInfo.InvariantCulture) + "s";
                    captionLabel.color = Palette.Bright(Palette.Of(state.Captor), 0.2f);
                }
                else if (state.Owner == Faction.Neutral)
                {
                    captionLabel.text = displayName.ToUpperInvariant() + "  -  NEUTRAL";
                    captionLabel.color = Palette.TextDim;
                }
                else if (state.Interval <= 0f)
                {
                    // An asteroid: owned, but never a Dart to count down to.
                    captionLabel.text = displayName.ToUpperInvariant() + "  -  NO PRODUCTION";
                    captionLabel.color = Palette.TextDim;
                }
                else
                {
                    string rate = Mathf.Approximately(state.Interval, 1f)
                        ? "+1 / s"
                        : "+1 / " + state.Interval.ToString("0.#", CultureInfo.InvariantCulture) + "s";
                    captionLabel.text = displayName.ToUpperInvariant() + "  -  " + rate;
                    captionLabel.color = Palette.TextDim;
                }
            }

            // The fighting caption pulses. It follows game time, so it holds still under the pause menu.
            float alpha = state.Fighting ? 0.72f + 0.28f * Mathf.Sin(time * 7f) : 1f;
            if (!Mathf.Approximately(captionLabel.alpha, alpha)) captionLabel.alpha = alpha;
        }

        static string Colored(Faction faction, int darts)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(Palette.Bright(Palette.Of(faction), 0.3f)) + ">"
                + darts.ToString(CultureInfo.InvariantCulture) + "</color>";
        }
    }
}
