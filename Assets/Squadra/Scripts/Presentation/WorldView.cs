using System.Globalization;
using Squadra.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace Squadra
{
    /// <summary>
    /// One planet or moon in the scene. Holds the object's starting rules (owner, Darts, production rate)
    /// and shows its live state: ownership ring and aura, selection outline, fleet count and capture
    /// progress. While a battle is fought here it shows both sides' counts and that they are fighting,
    /// and a capture that was running is shown held at the progress it had reached.
    /// </summary>
    [ExecuteAlways]
    public sealed class WorldView : MonoBehaviour
    {
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int BodyRadiusId = Shader.PropertyToID("_BodyRadius");

        [Header("Rules")]
        [SerializeField] string displayName = "World";
        [SerializeField] NodeKind kind = NodeKind.Planet;
        [SerializeField] Faction startingOwner = Faction.Neutral;
        [FormerlySerializedAs("startingShips")]
        [SerializeField] int startingDarts;
        [Tooltip("Seconds per produced Dart while owned and outside capture.")]
        [SerializeField] float productionInterval = 1f;

        [Header("Look")]
        [Tooltip("Radius of the body in world units.")]
        [SerializeField] float bodyRadius = 1.1f;
        [SerializeField] MeshRenderer surface;
        [SerializeField] MeshRenderer aura;
        [SerializeField] MeshRenderer ownerRing;
        [SerializeField] MeshRenderer captureRing;
        [SerializeField] MeshRenderer selectionRing;
        [SerializeField] TextMeshPro countLabel;
        [SerializeField] TextMeshPro captionLabel;

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

        /// <summary>What there is to show of one map object at one moment.</summary>
        struct State
        {
            public Faction Owner;
            public Faction Occupant;
            public Faction Captor;
            public int Darts;
            public float CaptureProgress;
            public float CaptureRemaining;
            public float Interval;

            /// <summary>True while a battle is fought here. <see cref="Darts"/> is then the stationed side's count.</summary>
            public bool Fighting;
            public Faction Arriving;
            public int ArrivingDarts;
        }

        /// <summary>The Dart count as shown under the world. Both sides' counts during a battle.</summary>
        public string CountText => countLabel != null ? countLabel.GetParsedText() : string.Empty;

        /// <summary>The line under the count: production, a capture, or the battle.</summary>
        public string CaptionText => captionLabel != null ? captionLabel.text : string.Empty;

        public string DisplayName => displayName;
        public float BodyRadius => bodyRadius;

        /// <summary>Radius of the ring the stationed fleet orbits on.</summary>
        public float OrbitRadius => bodyRadius + 0.42f;

        public float PickRadius => OrbitRadius + 0.3f;
        public Vector2 Position => transform.position;

        public NodeConfig ToConfig()
        {
            return new NodeConfig
            {
                Name = displayName,
                Kind = kind,
                Position = new MapPoint(transform.position.x, transform.position.y),
                Owner = startingOwner,
                Darts = startingDarts,
                ProductionInterval = productionInterval,
            };
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
            Battle battle = node.Battle;
            Render(new State
            {
                Owner = node.Owner,
                Occupant = node.Occupant,
                Captor = node.Captor,
                Darts = node.Darts,
                CaptureProgress = node.CaptureProgress,
                CaptureRemaining = (float)node.CaptureRemaining,
                Interval = (float)node.ProductionInterval,
                Fighting = battle != null,
                Arriving = battle != null ? battle.Arriving : Faction.Neutral,
                ArrivingDarts = battle != null ? battle.ArrivingDarts : 0,
            }, deltaTime, selected, hovered);
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
            // In the editor, mirror the starting rules so the scene reads correctly without pressing Play.
            if (!Application.isPlaying) Preview();
        }

        void Preview()
        {
            if (surface == null || ownerRing == null) return;
            EnsureHandles();
            Layout();
            Render(new State
            {
                Owner = startingOwner,
                Occupant = startingOwner,
                Darts = startingDarts,
                Interval = productionInterval,
            }, 1000f, false, false);
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

            if (aura != null)
            {
                float side = (OrbitRadius + 1.5f) * 2f;
                aura.transform.localScale = new Vector3(side, side, 1f);
            }

            float labelY = -(OrbitRadius + 0.62f);
            if (countLabel != null) countLabel.transform.localPosition = new Vector3(0f, labelY, 0f);
            if (captionLabel != null) captionLabel.transform.localPosition = new Vector3(0f, labelY - 0.5f, 0f);
        }

        // ------------------------------------------------------------------ rendering

        void Render(State state, float deltaTime, bool selected, bool hovered)
        {
            if (owner == null) return;

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

            // Ownership ring: a solid glowing band when owned, a faint dashed circle when neutral.
            owner.Radius = OrbitRadius;
            owner.Thickness = Mathf.Lerp(0.018f, 0.03f, ownedAmount);
            owner.Softness = Mathf.Lerp(0.05f, 0.075f, ownedAmount);
            owner.Glow = Mathf.Lerp(0.1f, 0.25f, ownedAmount);
            owner.Dashes = owned ? 0f : 44f;
            owner.DashFill = 0.5f;
            owner.Spin = 0.01f;
            owner.Additive = 0.7f;
            owner.Tint = Palette.Hdr(shownColor, Mathf.Lerp(0.9f, 1.65f, ownedAmount) + flash * 2f,
                Mathf.Lerp(0.55f, 1f, ownedAmount));
            owner.Apply();

            if (aura != null)
            {
                float alpha = Mathf.Lerp(0.03f, 0.11f, ownedAmount) + flash * 0.5f + selectAmount * 0.08f;
                auraBlock.SetVector(TintId, Palette.Hdr(shownColor, 1f, alpha));
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
                capture.Radius = OrbitRadius + 0.2f;
                capture.Thickness = 0.075f;
                capture.Softness = 0.1f;
                capture.Glow = held ? 0.15f : 0.35f;
                capture.Arc = state.CaptureProgress;
                capture.Track = held ? 0.22f : 0.1f;
                capture.Dashes = held ? 40f : 0f;
                capture.DashFill = 0.55f;
                capture.Spin = 0f;
                capture.Additive = 0.6f;
                capture.Tint = Palette.Hdr(Palette.Of(state.Captor),
                    held ? 1.3f : 2.3f + 0.4f * Mathf.Sin(time * 6f), 1f);
                capture.Apply();
            }

            // Selection outline: a rotating dashed ring in the accent color; fainter while only hovered.
            float outline = Mathf.Max(selectAmount, hoverAmount * 0.45f);
            selection.Visible = outline > 0.01f;
            if (selection.Visible)
            {
                float pulse = selectAmount * 0.035f * Mathf.Sin(time * 4.5f);
                selection.Radius = OrbitRadius + (capturing ? 0.44f : 0.3f) + pulse;
                selection.Thickness = Mathf.Lerp(0.022f, 0.036f, selectAmount);
                selection.Softness = 0.045f;
                selection.Glow = 0.22f * selectAmount;
                selection.Dashes = 36f;
                selection.DashFill = 0.55f;
                selection.Spin = 0.035f;
                selection.Additive = 0.6f;
                selection.Tint = Palette.Hdr(Palette.Accent, 1.6f + selectAmount * 0.8f, outline);
                selection.Apply();
            }

            RenderLabels(state, time);
        }

        void RenderLabels(State state, float time)
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
