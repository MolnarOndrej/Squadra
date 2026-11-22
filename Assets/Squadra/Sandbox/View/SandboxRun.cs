using System.Collections.Generic;
using Squadra.Pause;
using Squadra.Sandbox;
using Squadra.Simulation;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// Plays the Sandbox scene: from the moment its <see cref="SandboxPreview"/> lays down, fleets fly on,
    /// captures and battles carry on and worlds produce, by the match's rules, for as long as the user
    /// likes. There is no AI and no victory or defeat. The user orders any player's fleets and edits worlds,
    /// tuning, players' colors and the game speed through the <see cref="SandboxPanel"/>, pauses, and resets
    /// the battlefield or prepares an example capture or battle; the <see cref="SandboxSession"/> carries
    /// each command out. Everything a run changes is a copy: the scene, the shared looks, the game's
    /// defaults and the saved settings are never touched, and the next run starts from the scene again.
    ///
    /// It is drawn by the mission's own parts: the worlds' <see cref="WorldView"/>s, the
    /// <see cref="FleetLayer"/>, <see cref="RouteLanes"/>, the effects and sounds through
    /// <see cref="MatchFeedback"/>. Gameplay runs on the shared <see cref="GameClock"/>, at the saved game
    /// speed unless the panel picks another for this run; paused, neither the match nor anything drawn
    /// with it moves, while the camera, the panel and its edits keep working.
    ///
    /// Escape opens the shared <see cref="PauseMenu"/> over the run. It freezes the battlefield whether or
    /// not the run was paused already, dims it and keeps every key and click away from the battlefield and
    /// the panel until it closes; Resume gives back the run as it was, paused or not. Its Quit to Menu
    /// throws the run away and returns to the main menu. A run counts for no campaign mission and never
    /// ends, so nothing of it reaches the campaign.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SandboxRun : MonoBehaviour, IPausableRun
    {
        // The part of the screen's width the panel covers on the right. The battlefield is framed beside
        // it, and the camera stays put while the panel slides away and back.
        const float PanelShare = 0.23f;

        [SerializeField] SandboxPreview preview;
        [SerializeField] FleetLayer fleets;
        [SerializeField] EffectsLayer effects;
        [SerializeField] SoundLayer sounds;
        [SerializeField] SandboxPanel panel;

        readonly List<SandboxWorld> worlds = new List<SandboxWorld>();
        WorldView[] views;
        float[] fronts;
        SandboxSession session;
        SandboxOrders orders;
        SandboxDartTuning darts;
        RouteLanes lanes;
        Rect battlefield;
        bool paused;
        bool menuOpen;
        bool running;
        bool leaving;
        int inputHeldFrame = -1;

        /// <summary>The run's session: its match, and every command the sandbox gives.</summary>
        public SandboxSession Session => session;

        /// <summary>How this run draws its Darts. Changing it is seen at once.</summary>
        public SandboxDartTuning Darts => darts;

        /// <summary>The worlds, by match index.</summary>
        public IReadOnlyList<WorldView> Worlds => views;

        /// <summary>True while the run is paused by its own Pause button or Space; the pause menu aside.</summary>
        public bool Paused => paused;

        /// <summary>True while the pause menu holds the run: frozen, dimmed and out of reach of input.</summary>
        public bool MenuOpen => menuOpen;

        /// <summary>True while gameplay is frozen, by the run's own pause or under the pause menu.</summary>
        public bool Frozen => paused || menuOpen;

        /// <summary>False once the run is on its way out: there is nothing left to pause.</summary>
        public bool CanPause => running && !leaving;

        /// <summary>The index of the world the panel edits, or -1.</summary>
        public int InspectedWorld => orders.Inspected;

        /// <summary>True in Order mode, where clicks on worlds send fleets.</summary>
        public bool OrderMode => orders.OrderMode;

        void Awake()
        {
            if (preview == null || fleets == null || effects == null || sounds == null)
            {
                Debug.LogError("Squadra: the sandbox run is missing its preview, fleets, effects or sounds. "
                    + "Run Squadra > Build Sandbox Scene.", this);
                enabled = false;
                return;
            }

            // A run starts in the players' own colors, whatever an earlier run showed them in.
            Palette.ResetColors();

            session = new SandboxSession(preview.Setup(worlds));
            preview.Report(session.Problems);
            views = new WorldView[worlds.Count];
            for (int i = 0; i < views.Length; i++)
            {
                views[i] = worlds[i].View;
                worlds[i].Dress(preview.Looks, session.Match.Nodes[i].Kind);
            }

            fronts = SandboxPreview.FrontAngles(worlds);
            preview.ShowBackdrop();
            battlefield = Battlefield.Frame(views);
            FitBattlefield();

            darts = preview.Behavior.DartTuning();
            darts.Changed += () => fleets.SetDisplay(darts.Display());
            lanes = new RouteLanes(session.Match.Routes, views, transform, preview.LineMaterial, preview.RouteStyle);
            orders = new SandboxOrders(session, views);
            Show(session.Match);
            session.Started += OnRestarted;

            // From here the match gets time, at the saved game speed.
            GameSettings.Clock.BeginRun();
            running = true;
            if (panel != null) panel.Bind(this);
        }

        // Draws a newly set up match: its worlds, Darts and fleets as they stand, and what it does next.
        void Show(Match match)
        {
            fleets.Bind(match, views, preview.LineMaterial, preview.RouteStyle, sounds, darts.Display(), fronts);
            new MatchFeedback(views, fleets, effects, sounds, preview.Rig).Attach(match);
        }

        void OnRestarted()
        {
            orders.Restart(session);
            Show(session.Match);
        }

        void Update()
        {
            Match match = session.Match;

            // Paused, game time stands still and the views below draw the same moment every frame. The
            // user can still select and edit worlds; a fleet cannot be sent until the run carries on.
            // Under the pause menu nothing can be selected or edited either: its keys and clicks are its own.
            float deltaTime = Frozen ? 0f : Time.deltaTime;

            // The frame the run resumes on still belongs to the button that resumed it. What the pointer
            // does over the panel is the panel's, and keys are the panel's while one of its controls has
            // the keyboard, so typing a value never gives an order.
            if (menuOpen)
            {
                orders.ReadPointer(preview.Rig.Camera, true, false); // over no world: nothing hovered
            }
            else if (Time.frameCount != inputHeldFrame)
            {
                bool overPanel = panel != null && panel.IsPointerOver();
                if (panel != null && !overPanel && PointerPressed()) panel.Blur();
                orders.ReadPointer(preview.Rig.Camera, overPanel, !paused);
                if (panel != null && !panel.HasFocus) panel.ReadShortcuts();
            }

            if (!Frozen) session.Advance(deltaTime);
            orders.DropUnusableSelection();

            // Paused, a world still answers an edit or a selection on real time: its ring takes a new owner's
            // color and its outline follows the selection. Nothing of the match moves.
            float worldBlend = Frozen ? Time.unscaledDeltaTime : deltaTime;
            int selected = orders.Outlined;
            int hovered = orders.Hovered;
            for (int i = 0; i < views.Length; i++)
            {
                Node node = match.Nodes[i];
                worlds[i].DressIfChanged(preview.Looks, node.Kind);
                bool highlight = i == hovered && (selected >= 0 ? i != selected : orders.IsSelectable(i));
                views[i].Refresh(node, worldBlend, i == selected, highlight);
            }

            fleets.Tick(deltaTime);
            Faction issuer = selected >= 0 ? match.Nodes[selected].Owner : Faction.Neutral;
            lanes.Refresh(match, issuer, orders.OrderMode ? selected : -1, hovered);
            if (panel != null) panel.Refresh();
        }

        // Frames the battlefield in the part of the screen the panel leaves free.
        void FitBattlefield()
        {
            Rect frame = battlefield;
            if (panel != null) frame.width /= 1f - PanelShare;
            preview.Rig.Fit(frame);
        }

        static bool PointerPressed()
        {
            UnityEngine.InputSystem.Pointer pointer = UnityEngine.InputSystem.Pointer.current;
            return pointer != null && pointer.press.wasPressedThisFrame;
        }

        void OnDestroy()
        {
            // However the scene goes, the next one gets normal time, the music at its own level and the
            // players' own colors.
            if (running) EndRun();
            Palette.ResetColors();
        }

        // Leaving the scene ends the run, its pause, its speed and its pause menu for whatever comes next.
        // The game speed preference stays as it is.
        void EndRun()
        {
            paused = false;
            menuOpen = false;
            running = false;
            GameSettings.Clock.EndRun();
            MusicPlayer.SetPaused(false);
        }

        // ------------------------------------------------------------------ commands

        /// <summary>
        /// Freezes the run, or lets it carry on at the game speed selected by then. Paused, the match gets
        /// no time and nothing drawn with it moves; the time spent paused is never made up afterwards.
        /// </summary>
        public bool SetPaused(bool value)
        {
            if (value == paused || !running || menuOpen) return false;

            paused = value;
            ApplyFreeze();
            if (!value) inputHeldFrame = Time.frameCount;
            return true;
        }

        // The pause menu's hold, on top of the run's own pause: Resume gives the run back as it was.
        bool IPausableRun.SetPaused(bool value)
        {
            if (value == menuOpen) return false;
            if (value && !CanPause) return false;

            menuOpen = value;
            ApplyFreeze();
            MusicPlayer.SetPaused(value);
            if (panel != null) panel.SetHeld(value);
            inputHeldFrame = Time.frameCount;
            return true;
        }

        void ApplyFreeze()
        {
            GameSettings.Clock.SetPaused(Frozen);
            sounds.SetPaused(Frozen);
        }

        /// <summary>Plays the rest of this run at <paramref name="speed"/>; the saved preference is not touched.</summary>
        public bool SetSpeed(GameSpeed speed)
        {
            return GameSettings.Clock.SetOverride(speed);
        }

        /// <summary>Switches Order mode on, where clicks send fleets, or off, where they select worlds to edit.</summary>
        public void SetOrderMode(bool value)
        {
            orders.SetOrderMode(value);
        }

        /// <summary>Applies one click on a world, or on empty space when <paramref name="index"/> is negative.</summary>
        public void Click(int index)
        {
            if (!menuOpen) orders.Click(index, !paused);
        }

        /// <summary>
        /// Makes a world the one the panel edits. It switches to Inspect mode, where a click never sends a
        /// fleet; a negative index changes nothing.
        /// </summary>
        public void Select(int index)
        {
            if (menuOpen) return;
            if (orders.OrderMode) orders.SetOrderMode(false);
            orders.Click(index, false);
        }

        /// <summary>
        /// Lays the authored moment down again, with nothing of this run's orders or edits; the run's tuning,
        /// colors, speed and pause stay as they are.
        /// </summary>
        public void ResetSetup()
        {
            session.Reset();
        }

        // ------------------------------------------------------------------ leaving

        /// <summary>The pause menu's quit confirmation: a sandbox run is thrown away, wherever the player goes.</summary>
        public QuitPrompt QuitPromptFor(QuitDestination destination)
        {
            return QuitPrompt.ForSandbox(destination);
        }

        /// <summary>
        /// Throws the run away and returns to the main menu's main screen, not the mission map. The
        /// campaign is left as it was: a run is no campaign mission and never has a result.
        /// </summary>
        public void BackToMenu()
        {
            if (leaving) return;

            leaving = true;
            EndRun();
            MainMenu.Open();
        }

        /// <summary>Closes the game from the sandbox. Settings are already saved; the run is not kept.</summary>
        public void QuitToDesktop()
        {
            leaving = true;
            MainMenu.QuitApplication();
        }
    }
}
