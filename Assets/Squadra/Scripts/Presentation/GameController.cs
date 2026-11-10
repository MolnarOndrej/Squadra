using System.Collections.Generic;
using Squadra.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Squadra
{
    /// <summary>
    /// Runs one match in the scene. Builds the <see cref="Match"/> from the worlds placed in the scene,
    /// turns clicks into fleet orders for Blue, advances time and tells the views what to show.
    /// All game rules live in the match; nothing here decides an outcome.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        const Faction Player = Faction.Blue;

        [Header("Rules")]
        [Tooltip("Common fleet speed in world units per second.")]
        [SerializeField] float fleetSpeed = 2.4f;
        [SerializeField] float captureSeconds = 5f;

        [Header("Scene")]
        [Tooltip("Node ids follow this order.")]
        [SerializeField] WorldView[] worlds;
        [SerializeField] FleetLayer fleets;
        [SerializeField] EffectsLayer effects;
        [SerializeField] SoundLayer sounds;
        [SerializeField] HudView hud;
        [SerializeField] CameraRig rig;
        [SerializeField] Material lineMaterial;

        Match match;
        int selected = -1;
        int hovered = -1;
        LineStyle orderPreview;
        LineStyle[,] idleLanes;
        bool[,] activeLanes;
        float noticeUntil;
        string notice;

        public Match Match => match;
        public int SelectedWorld => selected;

        void Awake()
        {
            var config = new MatchConfig { FleetSpeed = fleetSpeed, CaptureDuration = captureSeconds };
            foreach (WorldView world in worlds)
                config.Nodes.Add(world.ToConfig());

            match = new Match(config);
            match.FleetLaunched += OnFleetLaunched;
            match.FleetArrived += OnFleetArrived;
            match.ShipProduced += OnShipProduced;
            match.CaptureCompleted += OnCaptureCompleted;
            match.StatusChanged += OnStatusChanged;

            fleets.Bind(match, worlds, lineMaterial);
            BuildLanes();
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;

            if (match.Status == MatchStatus.InProgress)
            {
                ReadInput();
                match.Advance(deltaTime);
                if (selected >= 0 && !IsSelectable(selected)) selected = -1;
            }
            else
            {
                selected = -1;
                hovered = -1;
                hud.ShowResult(match.Status, match.Elapsed);
                if (hud.ResultReady && RestartRequested()) Restart();
            }

            for (int i = 0; i < worlds.Length; i++)
            {
                bool highlight = i == hovered && (selected >= 0 ? i != selected : IsSelectable(i));
                worlds[i].Refresh(match.Nodes[i], deltaTime, i == selected, highlight);
            }

            fleets.Tick(deltaTime);
            UpdateRoutes();
            hud.SetStatus(match);
            if (match.Status == MatchStatus.InProgress) hud.SetHint(CurrentHint());
        }

        // ------------------------------------------------------------------ input and orders

        void ReadInput()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null)
            {
                hovered = -1;
                return;
            }

            Vector2 screen = pointer.position.ReadValue();
            Camera cam = rig.Camera;
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            hovered = WorldAt(world);

            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            bool cancel = (mouse != null && mouse.rightButton.wasPressedThisFrame)
                || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame);
            if (cancel) selected = -1;

            if (pointer.press.wasPressedThisFrame) Click(hovered);
        }

        int WorldAt(Vector2 point)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < worlds.Length; i++)
            {
                float distance = Vector2.Distance(point, worlds[i].Position);
                if (distance <= worlds[i].PickRadius && distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }

        bool IsSelectable(int index)
        {
            Node node = match.Nodes[index];
            return node.Owner == Player && !node.IsBeingCaptured;
        }

        /// <summary>
        /// Applies one click on a world (or on empty space when <paramref name="index"/> is negative):
        /// select an owned world, cancel by clicking it again, or send its whole fleet to another world.
        /// </summary>
        public void Click(int index)
        {
            if (match.Status != MatchStatus.InProgress) return;

            if (index < 0 || index >= worlds.Length)
            {
                selected = -1;
                return;
            }

            if (selected < 0)
            {
                if (IsSelectable(index)) selected = index;
                else Notify("Select one of your own worlds first");
                return;
            }

            if (index == selected)
            {
                selected = -1;
                return;
            }

            if (match.TryLaunch(Player, selected, index))
            {
                selected = -1;
            }
            else if (IsSelectable(index))
            {
                selected = index; // the first world had nothing to send: treat the click as a new selection
            }
            else
            {
                Notify("No ships stationed at " + worlds[selected].DisplayName + " yet");
            }
        }

        void Notify(string text)
        {
            notice = text;
            noticeUntil = Time.unscaledTime + 1.8f;
        }

        string CurrentHint()
        {
            if (notice != null && Time.unscaledTime < noticeUntil) return notice;

            if (selected < 0) return "Click one of your worlds to select its fleet";

            int ships = match.Nodes[selected].Ships;
            if (ships == 0) return "No ships stationed here yet  -  click again to cancel";
            return "Click a destination to send all " + ships + " ships  -  click "
                + worlds[selected].DisplayName + " again to cancel";
        }

        static bool RestartRequested()
        {
            Pointer pointer = Pointer.current;
            Keyboard keyboard = Keyboard.current;
            return (pointer != null && pointer.press.wasPressedThisFrame)
                || (keyboard != null && (keyboard.rKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame
                    || keyboard.spaceKey.wasPressedThisFrame));
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(gameObject.scene.name);
        }

        // ------------------------------------------------------------------ lanes and order preview

        void BuildLanes()
        {
            // Every pair of worlds has a direct route. Keep the idle lanes readable under active orders.
            idleLanes = new LineStyle[worlds.Length, worlds.Length];
            activeLanes = new bool[worlds.Length, worlds.Length];
            for (int a = 0; a < worlds.Length; a++)
            {
                for (int b = a + 1; b < worlds.Length; b++)
                {
                    GetRoute(a, b, out Vector2 from, out Vector2 to);
                    var lane = new LineStyle(Shapes.CreateQuad("Lane", transform, lineMaterial, -50))
                    {
                        Tint = Palette.Hdr(Palette.Starlight, 0.66f, 0.53f),
                        Width = 0.10f,
                        Dash = 0.13f,
                        Gap = 0.16f,
                        EndFade = 0.04f,
                    };
                    lane.Set(from, to);
                    idleLanes[a, b] = lane;
                }
            }

            orderPreview = new LineStyle(Shapes.CreateQuad("Order Preview", transform, lineMaterial, 8))
            {
                Width = 0.16f,
                Dash = 0.26f,
                Gap = 0.16f,
                Scroll = 1.15f,
                EndFade = 0.05f,
            };
            orderPreview.Visible = false;
        }

        void GetRoute(int source, int target, out Vector2 from, out Vector2 to)
        {
            Vector2 delta = worlds[target].Position - worlds[source].Position;
            Vector2 direction = delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector2.right;
            from = worlds[source].Position + direction * worlds[source].OrbitRadius;
            to = worlds[target].Position - direction * worlds[target].OrbitRadius;
        }

        void UpdateRoutes()
        {
            bool show = selected >= 0 && hovered >= 0 && hovered != selected
                && match.CanLaunch(Player, selected, hovered);
            orderPreview.Visible = show;
            if (show)
            {
                GetRoute(selected, hovered, out Vector2 from, out Vector2 to);
                orderPreview.Tint = Palette.Hdr(Palette.Accent, 1.55f, 0.95f);
                orderPreview.Set(from, to);
            }

            // An idle lane under an animated dash would fill its gaps.
            System.Array.Clear(activeLanes, 0, activeLanes.Length);
            if (show) MarkActiveLane(selected, hovered);
            foreach (Fleet fleet in match.Fleets)
                MarkActiveLane(fleet.SourceId, fleet.TargetId);

            for (int a = 0; a < worlds.Length; a++)
                for (int b = a + 1; b < worlds.Length; b++)
                    idleLanes[a, b].Visible = !activeLanes[a, b];
        }

        void MarkActiveLane(int source, int target)
        {
            activeLanes[Mathf.Min(source, target), Mathf.Max(source, target)] = true;
        }

        // ------------------------------------------------------------------ match events

        void OnFleetLaunched(Fleet fleet)
        {
            fleets.BeginTransit(fleet);
            WorldView source = worlds[fleet.SourceId];
            effects.Shockwave(source.Position, Palette.Of(fleet.Faction), source.OrbitRadius * 0.85f,
                source.OrbitRadius * 1.3f, 0.45f, 1.6f);
            sounds.FleetDeparted(source.Position);
        }

        void OnShipProduced(Node node)
        {
            worlds[node.Id].Punch(0.25f);
        }

        void OnCaptureCompleted(Node node, Faction previousOwner)
        {
            WorldView world = worlds[node.Id];
            Color color = Palette.Of(node.Owner);
            world.Flash(1f);
            world.Punch(1f);
            effects.Shockwave(world.Position, color, world.BodyRadius, world.OrbitRadius * 2.4f, 0.9f, 2.6f);
            effects.Shockwave(world.Position, color, world.BodyRadius * 0.6f, world.OrbitRadius * 1.7f, 0.7f, 1.8f);
            fleets.Burst(world.Position, world.OrbitRadius, color, 28, 2.6f, 0.8f);
            effects.FloatText(world.Position + Vector2.up * (world.OrbitRadius + 0.3f), "CAPTURED",
                Palette.Bright(color, 0.3f), 3.2f, 0f, 1.5f);
            sounds.WorldCaptured(world.Position);
        }

        void OnStatusChanged(MatchStatus status)
        {
            if (status != MatchStatus.InProgress) sounds.MatchEnded(status == MatchStatus.BlueVictory);
        }

        void OnFleetArrived(ArrivalReport report)
        {
            fleets.EndTransit(report.FleetId);

            WorldView world = worlds[report.NodeId];
            Color arriving = Palette.Of(report.Arriving);
            Vector2 above = world.Position + Vector2.up * (world.OrbitRadius + 0.3f);
            world.Punch(1f);

            if (!report.WasBattle)
            {
                // Peaceful: reinforcements joining, or an unopposed landing.
                effects.Shockwave(world.Position, arriving, world.OrbitRadius * 1.25f, world.OrbitRadius * 0.95f,
                    0.45f, 1.5f);
                bool landing = report.Outcome == ArrivalOutcome.Landed;
                if (landing) fleets.Burst(world.Position, world.OrbitRadius, arriving, 12, 1.2f);
                effects.FloatText(above, landing ? "LANDING" : "+" + report.ArrivingShips,
                    Palette.Bright(arriving, 0.3f), landing ? 2.6f : 3.6f);
                sounds.FleetArrived(world.Position);
                return;
            }

            // Battle: both sides lose the same number of ships in a flash of sparks.
            Color stationed = Palette.Of(report.Stationed);
            int sparks = Mathf.Clamp(report.LossesPerSide, 8, 40);
            fleets.Burst(world.Position, world.OrbitRadius, arriving, sparks, 3.2f);
            fleets.Burst(world.Position, world.OrbitRadius, stationed, sparks, 2.6f);
            effects.Shockwave(world.Position, Palette.Starlight, world.OrbitRadius * 0.7f, world.OrbitRadius * 1.9f,
                0.55f, 2.8f);
            effects.Shockwave(world.Position, arriving, world.OrbitRadius * 0.9f, world.OrbitRadius * 1.5f,
                0.75f, 2.2f);
            world.Flash(0.6f);
            rig.Shake(Mathf.Min(0.24f, 0.06f + report.LossesPerSide * 0.004f));

            // Combat resolves in an instant, so the fight is heard as a short burst sized to the losses.
            sounds.BattleStarted(world.Position);
            sounds.Skirmish(world.Position, report.LossesPerSide,
                Mathf.Lerp(0.8f, 1.5f, Mathf.Clamp01(report.LossesPerSide / 30f)));

            effects.FloatText(above + Vector2.up * 0.42f, "-" + report.LossesPerSide, Palette.Text, 4.2f);
            effects.FloatText(above, OutcomeTag(report.Outcome), Palette.Bright(OutcomeColor(report), 0.3f), 2.4f,
                0.15f, 1.3f);
        }

        static string OutcomeTag(ArrivalOutcome outcome)
        {
            switch (outcome)
            {
                case ArrivalOutcome.AttackWon: return "DEFENSES BROKEN";
                case ArrivalOutcome.Repelled: return "ATTACK REPELLED";
                case ArrivalOutcome.ControlRestored: return "CONTROL RESTORED";
                default: return "MUTUAL DESTRUCTION";
            }
        }

        static Color OutcomeColor(ArrivalReport report)
        {
            switch (report.Outcome)
            {
                case ArrivalOutcome.AttackWon:
                case ArrivalOutcome.ControlRestored:
                    return Palette.Of(report.Arriving);
                case ArrivalOutcome.Repelled:
                    return Palette.Of(report.Stationed);
                default:
                    return Palette.Neutral;
            }
        }
    }
}
