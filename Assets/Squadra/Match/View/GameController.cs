using System.Collections.Generic;
using Squadra.Campaign;
using Squadra.Pause;
using Squadra.Preparation;
using Squadra.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Squadra
{
    /// <summary>
    /// Runs one match in the shared mission scene. It builds the active campaign mission's battlefield
    /// from the mission as it was prepared at startup, frames it, and only then starts the
    /// <see cref="Match"/>. It turns clicks into fleet orders for Blue, advances time and tells the views
    /// what to show: fleets leaving and arriving, battles beginning, their casualties as they happen and
    /// how they end. When the match ends it hands the results to the results screen and carries out the
    /// action picked there. All game rules live in the match; nothing here decides an outcome. It also
    /// owns the pause: while paused the match gets no time and no orders, the scene's animation and
    /// effects stand still, and the match, the selection and any result are kept exactly as they were.
    /// The match and everything that moves with it run at the game speed the player chose, through the
    /// shared <see cref="GameClock"/>; the controller starts the clock's run and leaves it, and never
    /// sets the time scale itself.
    ///
    /// The controller sets the match up, runs the frame, owns the pause and leaves the scene. The worlds
    /// are built by <see cref="Battlefield"/>, the player's selection and clicks are
    /// <see cref="PlayerOrders"/>, the lines between worlds are <see cref="RouteLanes"/>, and what each
    /// match event looks and sounds like is <see cref="MatchFeedback"/>.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        const Faction Player = Faction.Blue;

        // The fleet speed is the match's own default (MatchConfig.FleetSpeed): changing it is a change to
        // the game's rules, made in code, not a value kept in this scene.
        [Header("Rules")]
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
        [Tooltip("The campaign: which mission is played here, how its worlds look, and what Next Mission "
            + "offers on the results screen.")]
        [SerializeField] MissionCatalog catalog;
        [Tooltip("For development only: the mission played when this scene is opened on its own rather than "
            + "launched from the mission map. It counts for no campaign mission.")]
        [SerializeField] TextAsset standaloneMission;

        [Header("Scene")]
        [Tooltip("Copied once for every world of the mission. Kept inactive.")]
        [SerializeField] WorldView worldTemplate;
        [Tooltip("Shows the space backdrop the mission was given.")]
        [SerializeField] Backdrop backdrop;
        [SerializeField] FleetLayer fleets;
        [SerializeField] EffectsLayer effects;
        [SerializeField] SoundLayer sounds;
        [SerializeField] HudView hud;
        [SerializeField] ResultsScreen results;
        [SerializeField] CameraRig rig;
        [SerializeField] Material lineMaterial;
        [Tooltip("How the lines between worlds look. Shared with every mission.")]
        [SerializeField] RouteStyle routeStyle;

        PreparedMission mission;
        WorldView[] worlds;
        Match match;
        PlayerOrders orders;
        RouteLanes lanes;
        bool paused;
        bool running;
        bool leaving;
        int inputHeldFrame = -1;

        public Match Match => match;

        /// <summary>The mission being played, as it was prepared. Null when there was none to play.</summary>
        public PreparedMission Mission => mission;

        /// <summary>The worlds built for the mission, by match index.</summary>
        public IReadOnlyList<WorldView> Worlds => worlds;
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

        // Everything is built and bound here, before the first frame: no frame ever sees a battlefield or
        // a match half set up, and the match gets no time before it is complete.
        void Awake()
        {
            mission = FindMission();
            if (mission == null)
            {
                // Nothing to play: back to the map rather than an empty battlefield.
                leaving = true;
                enabled = false;
                MainMenu.OpenCampaignMap();
                return;
            }

            WorldLooks looks = catalog != null ? catalog.Looks : null;
            worlds = Battlefield.Build(mission, worldTemplate, looks);
            Battlefield.ShowBackdrop(mission, backdrop, looks);
            rig.Fit(Battlefield.Frame(worlds));
            hud.SetSubtitle(Subtitle());

            match = new Match(BuildConfig());
            new MatchFeedback(worlds, fleets, effects, sounds, rig).Attach(match);
            match.StatusChanged += OnStatusChanged;

            fleets.Bind(match, worlds, lineMaterial, routeStyle, sounds);
            orders = new PlayerOrders(match, worlds, Player);
            lanes = new RouteLanes(match.Routes, worlds, transform, lineMaterial, routeStyle);

            // From here the match gets time, at the saved game speed.
            GameSettings.Clock.BeginRun();
            running = true;
        }

        // The mission launched from the map; a scene opened on its own plays its standalone mission,
        // outside the campaign. It never falls back to another campaign entry.
        PreparedMission FindMission()
        {
            string missionId = CampaignSession.ActiveMissionId;
            if (missionId == null) return MissionLibrary.PrepareStandalone(standaloneMission, catalog != null ? catalog.Looks : null);

            MissionLibrary.Prepare(catalog);
            if (MissionLibrary.TryGet(missionId, out PreparedMission prepared)) return prepared;

            Debug.LogError("Squadra: the mission " + missionId + " cannot be played here. "
                + (MissionLibrary.ProblemOf(missionId) ?? "It is not in the mission catalog."));
            return null;
        }

        // "SECTOR 01  -  FIRST CONTACT" for the catalog's first mission; just the name outside the campaign.
        string Subtitle()
        {
            string name = mission.Name.ToUpperInvariant();
            int index = catalog != null && CampaignSession.ActiveMissionId != null
                ? catalog.IndexOf(CampaignSession.ActiveMissionId)
                : -1;
            return index < 0 ? name : "SECTOR " + (index + 1).ToString("00") + "  -  " + name;
        }

        // The mission's players, worlds and routes, with the capture and battle rules as set in the Inspector.
        MatchConfig BuildConfig()
        {
            MatchConfig config = mission.CreateConfig();
            config.CaptureDuration = captureSeconds;
            config.BattleTiming.ShortestDuration = shortestBattleSeconds;
            config.BattleTiming.LongestDuration = longestBattleSeconds;
            config.BattleTiming.DecisiveFraction = 1.0 / Mathf.Max(1f, decisiveRatio);
            if (battleLengthCurve != null && battleLengthCurve.length > 0)
                config.BattleTiming.Curve = balance => battleLengthCurve.Evaluate((float)balance);
            return config;
        }

        void Update()
        {
            // Paused, game time stands still: the views below redraw the same moment every frame.
            // Otherwise Unity's delta time already carries the game speed, once, for the match and the views.
            float deltaTime = paused ? 0f : Time.deltaTime;

            if (paused)
            {
                // No orders and no time. The selection and the hovered world stay as they were.
            }
            else if (match.Status == MatchStatus.InProgress)
            {
                // The frame the pause menu closes still belongs to the menu: its input is not an order.
                if (Time.frameCount != inputHeldFrame) orders.ReadPointer(rig.Camera);
                orders.CountDownNotice(Time.unscaledDeltaTime);
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
            lanes.Refresh(match, Player, selected, hovered);
            hud.SetStatus(match);
            if (match.Status == MatchStatus.InProgress) hud.SetHint(orders.Hint);
        }

        void OnDestroy()
        {
            // However the scene goes, the next one gets normal time and the music at its own level.
            // A run already left (restart, next mission, menu) is not left again: the next mission's
            // run may have begun by now.
            if (running) EndRun();
        }

        // ------------------------------------------------------------------ pause

        /// <summary>
        /// Freezes the mission for the pause menu, or lets it carry on. Paused, the match is not
        /// advanced and takes no orders, the scene's animation and gameplay effects stand still, the
        /// battlefield is dimmed and the mission music drops to its paused level; a results panel is put
        /// away until the pause ends. The time spent paused is never made up afterwards, and the mission
        /// carries on at the game speed selected by then. Returns false when nothing changed: already in
        /// that state, or the mission is on its way out.
        /// </summary>
        public bool SetPaused(bool value)
        {
            if (value == paused) return false;
            if (value && !CanPause) return false;

            paused = value;
            GameSettings.Clock.SetPaused(value);
            MusicPlayer.SetPaused(value);
            sounds.SetPaused(value);
            hud.SetDimmed(value);
            if (results != null) results.SetSuspended(value);
            if (!value) inputHeldFrame = Time.frameCount;
            return true;
        }

        // Leaving the scene ends the run and its pause for whatever comes next, without showing the
        // mission again. The game speed preference stays as it is.
        void EndRun()
        {
            paused = false;
            running = false;
            GameSettings.Clock.EndRun();
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
            EndRun();
            SceneManager.LoadScene(gameObject.scene.name);
        }

        /// <summary>Launches the mission that follows this one in the campaign, if it can be played.</summary>
        public void StartNextMission(int missionIndex)
        {
            EndRun();
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
            EndRun();
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
