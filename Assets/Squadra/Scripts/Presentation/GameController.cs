using System;
using Squadra.Campaign;
using Squadra.Pause;
using Squadra.Simulation;
using UnityEngine;
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
    ///
    /// The controller sets the match up, runs the frame, owns the pause and leaves the scene. The
    /// player's selection and clicks are <see cref="PlayerOrders"/>, the lines between worlds are
    /// <see cref="RouteLanes"/>, and what each match event looks and sounds like is
    /// <see cref="MatchFeedback"/>.
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
        [SerializeField]
        RosterEntry[] players =
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
        PlayerOrders orders;
        RouteLanes lanes;
        bool paused;
        bool leaving;
        int inputHeldFrame = -1;

        public Match Match => match;
        public int SelectedWorld => orders.Selected;
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
            match = new Match(BuildConfig());
            new MatchFeedback(worlds, fleets, effects, sounds, rig).Attach(match);
            match.StatusChanged += OnStatusChanged;

            fleets.Bind(match, worlds, lineMaterial, sounds);
            orders = new PlayerOrders(match, worlds, Player);
            lanes = new RouteLanes(worlds, Player, transform, lineMaterial);
        }

        // The match's rules as set in the Inspector, and the worlds as placed in the scene.
        MatchConfig BuildConfig()
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
            return config;
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
                if (Time.frameCount != inputHeldFrame) orders.ReadPointer(rig.Camera);
                match.Advance(deltaTime);
                orders.DropUnusableSelection();
            }
            else
            {
                // The match no longer advances; the views below keep their idle animation running.
                orders.Clear();
            }

            int selected = orders.Selected;
            int hovered = orders.Hovered;
            for (int i = 0; i < worlds.Length; i++)
            {
                bool highlight = i == hovered && (selected >= 0 ? i != selected : orders.IsSelectable(i));
                worlds[i].Refresh(match.Nodes[i], deltaTime, i == selected, highlight);
            }

            fleets.Tick(deltaTime);
            lanes.Refresh(match, selected, hovered);
            hud.SetStatus(match);
            if (match.Status == MatchStatus.InProgress) hud.SetHint(orders.Hint);
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

        // ------------------------------------------------------------------ orders

        /// <summary>
        /// Applies one click on a world (or on empty space when <paramref name="index"/> is negative):
        /// select an owned world, cancel by clicking it again, or send its whole fleet to another world.
        /// Ignored while paused and once the match has ended.
        /// </summary>
        public void Click(int index)
        {
            if (!paused) orders.Click(index);
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
    }
}
