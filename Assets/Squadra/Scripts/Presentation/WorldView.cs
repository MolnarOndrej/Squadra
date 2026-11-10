using System.Globalization;
using Squadra.Simulation;
using TMPro;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// One planet or moon in the scene. Holds the object's starting rules (owner, ships, production rate)
    /// and shows its live state: ownership ring and aura, selection outline, fleet count and capture progress.
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
        [SerializeField] int startingShips;
        [Tooltip("Seconds per produced ship while owned and outside capture.")]
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
        int shownShips = int.MinValue;
        Faction shownCountFaction = (Faction)(-1);
        int shownCaptionKey = int.MinValue;

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
                Ships = startingShips,
                ProductionInterval = productionInterval,
            };
        }

        /// <summary>Scale-pops the fleet count, e.g. when a ship is built or a fleet arrives.</summary>
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
            Render(node.Owner, node.Occupant, node.Ships, node.Captor, node.CaptureProgress,
                (float)node.CaptureRemaining, (float)node.ProductionInterval, deltaTime, selected, hovered);
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
            Render(startingOwner, startingOwner, startingShips, Faction.Neutral, 0f, 0f, productionInterval,
                1000f, false, false);
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

        void Render(Faction ownerFaction, Faction occupant, int ships, Faction captor, float captureProgress,
            float captureRemaining, float interval, float deltaTime, bool selected, bool hovered)
        {
            if (owner == null) return;

            float blend = 1f - Mathf.Exp(-deltaTime * 6f);
            float fast = 1f - Mathf.Exp(-deltaTime * 14f);
            bool owned = ownerFaction != Faction.Neutral;

            shownColor = Color.Lerp(shownColor, Palette.Of(ownerFaction), blend);
            ownedAmount = Mathf.Lerp(ownedAmount, owned ? 1f : 0f, blend);
            selectAmount = Mathf.Lerp(selectAmount, selected ? 1f : 0f, fast);
            hoverAmount = Mathf.Lerp(hoverAmount, hovered ? 1f : 0f, fast);
            punch = Mathf.MoveTowards(punch, 0f, deltaTime * 2.2f);
            flash = Mathf.MoveTowards(flash, 0f, deltaTime * 1.6f);

            float time = Application.isPlaying ? Time.time : 0f;

            // Ownership ring: a solid glowing band when owned, a faint dashed circle when neutral.
            owner.Radius = OrbitRadius;
            owner.Thickness = Mathf.Lerp(0.018f, 0.03f, ownedAmount);
            owner.Softness = Mathf.Lerp(0.05f, 0.12f, ownedAmount);
            owner.Glow = Mathf.Lerp(0.1f, 0.42f, ownedAmount);
            owner.Dashes = owned ? 0f : 44f;
            owner.DashFill = 0.5f;
            owner.Spin = 0.01f;
            owner.Additive = 0.7f;
            owner.Tint = Palette.Hdr(shownColor, Mathf.Lerp(0.9f, 1.9f, ownedAmount) + flash * 2f,
                Mathf.Lerp(0.55f, 1f, ownedAmount));
            owner.Apply();

            if (aura != null)
            {
                float alpha = Mathf.Lerp(0.03f, 0.2f, ownedAmount) + flash * 0.5f + selectAmount * 0.08f;
                auraBlock.SetVector(TintId, Palette.Hdr(shownColor, 1f, alpha));
                aura.SetPropertyBlock(auraBlock);
            }

            // Capture: an arc in the captor's color filling clockwise over a faint full track.
            bool capturing = captor != Faction.Neutral;
            capture.Visible = capturing;
            if (capturing)
            {
                capture.Radius = OrbitRadius + 0.2f;
                capture.Thickness = 0.075f;
                capture.Softness = 0.1f;
                capture.Glow = 0.35f;
                capture.Arc = captureProgress;
                capture.Track = 0.1f;
                capture.Additive = 0.6f;
                capture.Tint = Palette.Hdr(Palette.Of(captor), 2.3f + 0.4f * Mathf.Sin(time * 6f), 1f);
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

            RenderLabels(ownerFaction, occupant, ships, captor, captureRemaining, interval);
        }

        void RenderLabels(Faction ownerFaction, Faction occupant, int ships, Faction captor, float captureRemaining,
            float interval)
        {
            if (countLabel != null)
            {
                bool show = occupant != Faction.Neutral;
                if (ships != shownShips || occupant != shownCountFaction)
                {
                    shownShips = ships;
                    shownCountFaction = occupant;
                    if (show) countLabel.SetText("{0}", ships);
                    else countLabel.SetText(string.Empty);
                    countLabel.color = Palette.Bright(Palette.Of(occupant), 0.3f);
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
            bool capturing = captor != Faction.Neutral;
            int tenths = capturing ? Mathf.CeilToInt(captureRemaining * 10f) : 0;
            int key = capturing
                ? 1000000 + (int)captor * 100000 + tenths
                : (int)ownerFaction * 10000 + Mathf.RoundToInt(interval * 100f);
            if (key == shownCaptionKey) return;
            shownCaptionKey = key;

            if (capturing)
            {
                captionLabel.text = Palette.NameOf(captor) + " CAPTURING  " + (tenths / 10f).ToString("0.0", CultureInfo.InvariantCulture) + "s";
                captionLabel.color = Palette.Bright(Palette.Of(captor), 0.2f);
            }
            else if (ownerFaction == Faction.Neutral)
            {
                captionLabel.text = displayName.ToUpperInvariant() + "  -  NEUTRAL";
                captionLabel.color = Palette.TextDim;
            }
            else
            {
                string rate = Mathf.Approximately(interval, 1f)
                    ? "+1 / s"
                    : "+1 / " + interval.ToString("0.#", CultureInfo.InvariantCulture) + "s";
                captionLabel.text = displayName.ToUpperInvariant() + "  -  " + rate;
                captionLabel.color = Palette.TextDim;
            }
        }
    }
}
