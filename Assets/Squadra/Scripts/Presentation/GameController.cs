using System;
using System.Collections.Generic;
using Squadra.Campaign;
using Squadra.Pause;
using Squadra.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Squadra
{
    /// <summary>
    /// Runs one match in the scene. Builds the <see cref="Match"/> from the worlds placed in the scene,
    /// turns clicks into fleet orders for Blue, advances time and tells the views what to show: fleets
    /// leaving and arriving, battles beginning, their casualties as they happen and how they end. When
    /// the match ends it hands the results to the results screen and carries out the action picked there.
    /// All game rules live in the match; nothing here decides an outcome. It also owns the pause: while
    /// paused the match gets no time and no orders, the scene's animation and effects stand still, and
    /// the match, the selection and any result are kept exactly as they were.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        const Faction Player = Faction.Blue;

        /// <summary>One player of the mission, as listed on the results screen.</summary>
        [Serializable]
        public struct RosterEntry
        {
            public Faction faction;
            [Tooltip("Shown on the results screen. Leave empty to use the faction's name.")]
            public string displayName;
        }

        [Header("Rules")]
        [Tooltip("Common fleet speed in world units per second.")]
        [SerializeField] float fleetSpeed = 2.4f;
        [SerializeField] float captureSeconds = 5f;

        [Header("Battles")]
        [Tooltip("Seconds a battle lasts when one side outnumbers the other by the decisive ratio or more.")]
        [SerializeField] float shortestBattleSeconds = 1f;
        [Tooltip("Seconds a battle between equal forces lasts.")]
        [SerializeField] float longestBattleSeconds = 5f;
        [Tooltip("How many times larger one side must be for the battle to be as short as it gets.")]
        [SerializeField] float decisiveRatio = 10f;
        [Tooltip("Battle length between the two ends: balance of the forces from 0 (decisive) to 1 (equal) "
            + "along the bottom, 0 (shortest) to 1 (longest) up the side.")]
        [SerializeField] AnimationCurve battleLengthCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Mission")]
        [Tooltip("Who takes part, two to six players. Results list the human player (Blue) first and "
            + "the others in this order.")]
        [SerializeField] RosterEntry[] players =
        {
            new RosterEntry { faction = Faction.Blue, displayName = "Blue" },
            new RosterEntry { faction = Faction.Red, displayName = "Red" },
        };
        [Tooltip("The campaign this mission belongs to. It decides what Next Mission offers on the results screen.")]
        [SerializeField] MissionCatalog catalog;

        [Header("Scene")]
        [Tooltip("Node ids follow this order.")]
        [SerializeField] WorldView[] worlds;
        [SerializeField] FleetLayer fleets;
        [SerializeField] EffectsLayer effects;
        [SerializeField] SoundLayer sounds;
        [SerializeField] HudView hud;
        [SerializeField] ResultsScreen results;
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
        bool paused;
        bool leaving;
        int inputHeldFrame = -1;

        public Match Match => match;
        public int SelectedWorld => selected;
        public ResultsScreen Results => results;
        public HudView Hud => hud;
        public SoundLayer Sounds => sounds;

        /// <summary>True while the mission is frozen under the pause menu.</summary>
        public bool Paused => paused;

        /// <summary>
        /// False once the mission is on its way out, through a results action or a confirmed quit:
        /// there is nothing left to pause.
        /// </summary>
        public bool CanPause => !leaving && (results == null || !results.Leaving);

        /// <summary>How the mission stands, for a quit confirmation: still being played, won or lost.</summary>
        public MissionStanding Standing
        {
            get
            {
                switch (match.Status)
                {
                    case MatchStatus.InProgress: return MissionStanding.Unfinished;
                    case MatchStatus.BlueVictory: return MissionStanding.Won;
                    default: return MissionStanding.Lost;
                }
            }
        }

        void Awake()
        {
            var config = new MatchConfig { FleetSpeed = fleetSpeed, CaptureDuration = captureSeconds, Human = Player };
            config.BattleTiming.ShortestDuration = shortestBattleSeconds;
            config.BattleTiming.LongestDuration = longestBattleSeconds;
            config.BattleTiming.DecisiveFraction = 1.0 / Mathf.Max(1f, decisiveRatio);
            if (battleLengthCurve != null && battleLengthCurve.length > 0)
                config.BattleTiming.Curve = balance => battleLengthCurve.Evaluate((float)balance);
            foreach (RosterEntry entry in players)
                config.Players.Add(new MatchPlayer(entry.faction, entry.displayName));
            foreach (WorldView world in worlds)
                config.Nodes.Add(world.ToConfig());

            match = new Match(config);
            match.FleetLaunched += OnFleetLaunched;
            match.FleetArrived += OnFleetArrived;
            match.DartProduced += OnDartProduced;
            match.CaptureCompleted += OnCaptureCompleted;
            match.BattleStarted += OnBattleStarted;
            match.BattleCasualties += OnBattleCasualties;
            match.BattleEnded += OnBattleEnded;
            match.StatusChanged += OnStatusChanged;

            fleets.Bind(match, worlds, lineMaterial, sounds);
            BuildLanes();
        }

        void Update()
        {
            // Paused, game time stands still: the views below redraw the same moment every frame.
            float deltaTime = paused ? 0f : Time.deltaTime;

            if (paused)
            {
                // No orders and no time. The selection and the hovered world stay as they were.
            }
            else if (match.Status == MatchStatus.InProgress)
            {
                // The frame the pause menu closes still belongs to the menu: its input is not an order.
                if (Time.frameCount != inputHeldFrame) ReadInput();
                match.Advance(deltaTime);
                if (selected >= 0 && !IsSelectable(selected)) selected = -1;
            }
            else
            {
                // The match no longer advances; the views below keep their idle animation running.
                selected = -1;
                hovered = -1;
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

        void OnDestroy()
        {
            // However the scene goes, the next one gets normal time and the music at its own level.
            if (paused) ReleasePause();
        }

        // ------------------------------------------------------------------ pause

        /// <summary>
        /// Freezes the mission for the pause menu, or lets it carry on. Paused, the match is not
        /// advanced and takes no orders, the scene's animation and gameplay effects stand still, the
        /// battlefield is dimmed and the mission music drops to its paused level; a results panel is put
        /// away until the pause ends. The time spent paused is never made up afterwards. Returns false
        /// when nothing changed: already in that state, or the mission is on its way out.
        /// </summary>
        public bool SetPaused(bool value)
        {
            if (value == paused) return false;
            if (value && !CanPause) return false;

            paused = value;
            Time.timeScale = value ? 0f : 1f;
            MusicPlayer.SetPaused(value);
            sounds.SetPaused(value);
            hud.SetDimmed(value);
            if (results != null) results.SetSuspended(value);
            if (!value) inputHeldFrame = Time.frameCount;
            return true;
        }

        // Leaving the scene ends the pause for whatever comes next, without showing the mission again.
        void ReleasePause()
        {
            paused = false;
            Time.timeScale = 1f;
            MusicPlayer.SetPaused(false);
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

            // Escape belongs to the pause menu; a right-click, or a click on the selected world, cancels.
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame) selected = -1;

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

        // Darts that are capturing or fighting are committed where they are.
        bool IsSelectable(int index)
        {
            Node node = match.Nodes[index];
            return node.Owner == Player && !node.IsBeingCaptured && !node.IsContested;
        }

        /// <summary>
        /// Applies one click on a world (or on empty space when <paramref name="index"/> is negative):
        /// select an owned world, cancel by clicking it again, or send its whole fleet to another world.
        /// </summary>
        public void Click(int index)
        {
            if (paused || match.Status != MatchStatus.InProgress) return;

            if (index < 0 || index >= worlds.Length)
            {
                selected = -1;
                return;
            }

            if (selected < 0)
            {
                if (IsSelectable(index)) selected = index;
                else if (match.Nodes[index].Owner == Player && match.Nodes[index].IsContested)
                    Notify("The Darts at " + worlds[index].DisplayName + " are fighting and cannot leave");
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
                Notify("No Darts stationed at " + worlds[selected].DisplayName + " yet");
            }
        }

        void Notify(string text)
        {
            notice = text;
            noticeUntil = Time.time + 1.8f; // game time: a notice does not run out under the pause menu
        }

        string CurrentHint()
        {
            if (notice != null && Time.time < noticeUntil) return notice;

            if (selected < 0) return "Click one of your worlds to select its fleet";

            int darts = match.Nodes[selected].Darts;
            if (darts == 0) return "No Darts stationed here yet  -  click again to cancel";
            return "Click a destination to send all " + darts + (darts == 1 ? " Dart" : " Darts") + "  -  click "
                + worlds[selected].DisplayName + " again to cancel";
        }

        // ------------------------------------------------------------------ after the match

        /// <summary>Starts a fresh attempt at this mission. It stays the campaign's active mission.</summary>
        public void Restart()
        {
            ReleasePause();
            SceneManager.LoadScene(gameObject.scene.name);
        }

        /// <summary>Launches the mission that follows this one in the campaign, if it can be played.</summary>
        public void StartNextMission(int missionIndex)
        {
            ReleasePause();
            if (CampaignSession.TryBeginMission(catalog, missionIndex, out string scenePath))
                SceneManager.LoadScene(scenePath);
            else
                BackToMenu(); // never offered when it cannot be launched; the map is the safe way out
        }

        /// <summary>
        /// Leaves the mission for the menu's mission map, paused or not. An unfinished match is simply
        /// dropped: only a victory that was already recorded counts for the campaign.
        /// </summary>
        public void BackToMenu()
        {
            leaving = true;
            ReleasePause();
            MainMenu.OpenCampaignMap();
        }

        /// <summary>Closes the game from the mission. Settings are already saved; the campaign is not kept.</summary>
        public void QuitToDesktop()
        {
            leaving = true;
            MainMenu.QuitApplication();
        }

        void OnResultsAction(ResultsAction action, int nextMissionIndex)
        {
            switch (action)
            {
                case ResultsAction.NextMission:
                    StartNextMission(nextMissionIndex);
                    break;
                case ResultsAction.Restart:
                    Restart();
                    break;
                default:
                    BackToMenu();
                    break;
            }
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

        void OnDartProduced(Node node)
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

        // The match has taken its final results by now, the deciding battle included.
        void OnStatusChanged(MatchStatus status)
        {
            if (status == MatchStatus.InProgress) return;

            // A win completes the campaign mission this match was launched as, if any, before the
            // campaign decides what the results screen offers.
            bool victory = status == MatchStatus.BlueVictory;
            ResultsPlan plan = CampaignSession.ConcludeMission(catalog, victory);
            sounds.MatchEnded(victory);
            hud.ShowResult();
            results.Show(match.Results, plan, action => OnResultsAction(action, plan.NextMissionIndex));
        }

        void OnFleetArrived(ArrivalReport report)
        {
            // The fleet's dots stay on screen: they join whoever they have reached.
            fleets.Arrive(report);

            WorldView world = worlds[report.NodeId];
            Color arriving = Palette.Of(report.Arriving);
            Vector2 above = world.Position + Vector2.up * (world.OrbitRadius + 0.3f);
            world.Punch(1f);

            switch (report.Outcome)
            {
                case ArrivalOutcome.BattleStarted:
                    return; // announced by the battle itself

                case ArrivalOutcome.BattleJoined:
                    // Reinforcements: the battle carries on with more Darts on one side.
                    effects.Shockwave(world.Position, arriving, world.OrbitRadius * 1.5f, world.OrbitRadius * 1.1f,
                        0.4f, 1.6f);
                    effects.FloatText(above, "+" + report.ArrivingDarts, Palette.Bright(arriving, 0.3f), 3.6f);
                    sounds.FleetArrived(world.Position);
                    return;
            }

            // Peaceful: a fleet joining its own side, or an unopposed landing.
            effects.Shockwave(world.Position, arriving, world.OrbitRadius * 1.25f, world.OrbitRadius * 0.95f,
                0.45f, 1.5f);
            bool landing = report.Outcome == ArrivalOutcome.Landed;
            if (landing) fleets.Burst(world.Position, world.OrbitRadius, arriving, 12, 1.2f);
            effects.FloatText(above, landing ? "LANDING" : "+" + report.ArrivingDarts,
                Palette.Bright(arriving, 0.3f), landing ? 2.6f : 3.6f);
            sounds.FleetArrived(world.Position);
        }

        // Nobody has lost a Dart yet. The fleet layer shows the fight from here on: shots, and an
        // explosion for the casualties the match reports.
        void OnBattleStarted(Battle battle)
        {
            WorldView world = worlds[battle.NodeId];
            effects.Shockwave(world.Position, Palette.Starlight, world.OrbitRadius * 0.8f, world.OrbitRadius * 1.6f,
                0.5f, 2.2f);
            // One line higher than an arrival's text, which may still be on screen.
            effects.FloatText(world.Position + Vector2.up * (world.OrbitRadius + 0.72f), "BATTLE",
                Palette.Bright(Palette.Accent, 0.2f), 2.6f, 0f, 1.2f);
            world.Flash(0.4f);
            rig.Shake(0.07f);
            sounds.BattleStarted(world.Position);
        }

        void OnBattleCasualties(Battle battle, int pairs)
        {
            fleets.BattleCasualties(battle, pairs);

            WorldView world = worlds[battle.NodeId];
            world.Punch(0.3f);
            world.Flash(0.12f);
            rig.Shake(0.015f);
        }

        void OnBattleEnded(BattleReport report)
        {
            fleets.BattleEnded(report);

            WorldView world = worlds[report.NodeId];
            Color winner = Palette.Of(report.Winner);
            Vector2 above = world.Position + Vector2.up * (world.OrbitRadius + 0.3f);
            effects.Shockwave(world.Position, winner, world.OrbitRadius * 0.9f, world.OrbitRadius * 1.7f, 0.7f, 2.4f);
            world.Flash(0.6f);
            world.Punch(1f);
            rig.Shake(Mathf.Min(0.16f, 0.05f + report.LossesPerSide * 0.003f));

            effects.FloatText(above + Vector2.up * 0.42f, "-" + report.LossesPerSide, Palette.Text, 4.2f);
            effects.FloatText(above, OutcomeTag(report.Outcome), Palette.Bright(winner, 0.3f), 2.4f, 0.15f, 1.3f);
        }

        static string OutcomeTag(BattleOutcome outcome)
        {
            switch (outcome)
            {
                case BattleOutcome.AttackWon: return "DEFENSES BROKEN";
                case BattleOutcome.Repelled: return "ATTACK REPELLED";
                case BattleOutcome.ControlRestored: return "CONTROL RESTORED";
                default: return "MUTUAL DESTRUCTION";
            }
        }
    }
}
