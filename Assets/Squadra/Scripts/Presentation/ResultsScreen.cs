using System;
using System.Globalization;
using Squadra.Campaign;
using Squadra.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Squadra
{
    /// <summary>
    /// The mission results panel, shown over the faded battlefield when a match ends: the outcome, the
    /// mission time, one row of combat totals per player, and the Next Mission, Restart and Back to Menu
    /// actions. The campaign decides what is offered and the match supplies the numbers; this screen
    /// shows them, keeps keyboard focus on something sensible and reports the action the player picks.
    /// It never acts on the background, on Escape, or before it has been on screen for a moment. While
    /// the pause menu is open over it the panel is put away, with nothing on it usable, and comes back
    /// exactly as it was.
    /// </summary>
    public sealed class ResultsScreen : MonoBehaviour
    {
        const string HiddenClass = "hidden";
        const string ShownClass = "results--shown";
        const string FadeClass = "fade--on";
        const string PointerModeClass = "menu-root--pointer";
        const string KeyboardModeClass = "menu-root--keys";
        const string TooltipOnClass = "results__tooltip--on";

        // Indexed by ResultsHeading.
        static readonly string[] HeadingClasses = { "results--victory", "results--defeat", "results--complete" };
        static readonly string[] HeadingTexts = { "VICTORY", "DEFEAT", "CAMPAIGN COMPLETE!" };

        /// <summary>
        /// Seconds the results are on screen before they accept input, so that a click or key press
        /// meant for the battle cannot pick an action.
        /// </summary>
        public const float ReadySeconds = 0.9f;

        /// <summary>Seconds the screen fades to black before the chosen action is carried out.</summary>
        public const float LeaveSeconds = 0.35f;

        /// <summary>
        /// Seconds the results ignore input after coming back from a pause, so that the click or key
        /// press that resumed cannot also pick an action.
        /// </summary>
        public const float ResumeGuardSeconds = 0.25f;

        [SerializeField] UIDocument document;
        [Tooltip("Optional. Plays the menu's hover, select and back cues for the result actions.")]
        [SerializeField] MenuAudio sounds;

        VisualElement root;
        VisualElement panel;
        VisualElement rows;
        VisualElement nextSlot;
        VisualElement fade;
        ResultsCelebration celebration;
        Label heading;
        Label message;
        Label time;
        Label nextTooltip;
        Button nextButton;
        Button restartButton;
        Button backButton;

        ResultsPlan plan;
        Action<ResultsAction> chosen;
        ResultsAction leavingFor;
        float age;
        float leaveAge;
        bool visible;
        bool entered;
        bool ready;
        bool leaving;
        bool suspended;
        float guard;
        ResultsAction? focusBeforeSuspend;
        bool keyboardMode;
        Vector3 lastPointer = new Vector3(float.NaN, float.NaN);

        /// <summary>True once <see cref="Show"/> has put the results on screen.</summary>
        public bool Visible => visible;

        /// <summary>True once the results accept input.</summary>
        public bool Ready => ready;

        /// <summary>True while the panel is put away under the pause menu. The results themselves are kept.</summary>
        public bool Suspended => suspended;

        /// <summary>True when the panel can be seen: shown, and not put away under the pause menu.</summary>
        public bool PanelShown => visible && !suspended && root != null && !root.ClassListContains(HiddenClass);

        /// <summary>True from the moment an action is picked until its scene replaces this one.</summary>
        public bool Leaving => leaving;

        /// <summary>True while the keyboard is the input in use and the focused action is highlighted.</summary>
        public bool KeyboardMode => keyboardMode;

        /// <summary>What the campaign offers on this screen.</summary>
        public ResultsPlan Plan => plan;

        public string HeadingText => heading != null ? heading.text : string.Empty;
        public string TimeText => time != null ? time.text : string.Empty;
        public int RowCount => rows != null ? rows.childCount : 0;

        /// <summary>The disabled Next Mission action's explanation, or an empty string when it has none.</summary>
        public string NextMissionTooltip => nextTooltip != null ? nextTooltip.text : string.Empty;

        /// <summary>The action that has the keyboard focus, or null when none has.</summary>
        public ResultsAction? FocusedAction
        {
            get
            {
                Focusable focused = root?.focusController?.focusedElement;
                if (focused == null) return null;
                if (focused == nextButton) return ResultsAction.NextMission;
                if (focused == restartButton) return ResultsAction.Restart;
                if (focused == backButton) return ResultsAction.BackToMenu;
                return null;
            }
        }

        /// <summary>True when the action's button is on the panel, enabled or not.</summary>
        public bool IsOffered(ResultsAction action)
        {
            return visible && (action != ResultsAction.NextMission || plan.ShowsNextMission);
        }

        /// <summary>True when the action's button is on the panel and can be activated.</summary>
        public bool IsEnabled(ResultsAction action)
        {
            return visible && (action != ResultsAction.NextMission || plan.CanStartNextMission);
        }

        // ------------------------------------------------------------------ showing

        /// <summary>
        /// Puts the results on screen. <paramref name="onChosen"/> is called once, after a short fade,
        /// with the action the player picked; it is never called with a disabled or hidden action.
        /// </summary>
        public void Show(MatchResults results, ResultsPlan offered, Action<ResultsAction> onChosen)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (!Bind()) return;

            plan = offered;
            chosen = onChosen;
            age = 0f;
            visible = true;
            entered = false;
            ready = false;
            leaving = false;
            guard = 0f;
            focusBeforeSuspend = null;

            for (int i = 0; i < HeadingClasses.Length; i++)
                panel.EnableInClassList(HeadingClasses[i], i == (int)offered.Heading);
            heading.text = HeadingTexts[(int)offered.Heading];
            message.EnableInClassList(HiddenClass, !offered.Celebrates);
            celebration.EnableInClassList(HiddenClass, !offered.Celebrates);
            celebration.Playing = offered.Celebrates;
            time.text = MatchClock.Format(results.Duration);

            rows.Clear();
            foreach (PlayerResult player in results.Players)
                rows.Add(BuildRow(player));

            string reason = offered.NextMissionReason;
            nextSlot.EnableInClassList(HiddenClass, !offered.ShowsNextMission);
            nextButton.SetEnabled(offered.CanStartNextMission);
            nextTooltip.text = reason ?? string.Empty;
            nextTooltip.EnableInClassList(TooltipOnClass, reason != null);

            fade.pickingMode = PickingMode.Ignore;
            fade.RemoveFromClassList(FadeClass);
            panel.RemoveFromClassList(ShownClass);
            panel.SetEnabled(!suspended);
            root.EnableInClassList(HiddenClass, suspended);
            SetKeyboardMode(false);
        }

        /// <summary>
        /// Puts the panel away while the pause menu is open, or brings it back. Away, it is hidden, none
        /// of its actions can be used and its timers stand still. Back, it shows the same result with
        /// the same action focused, and ignores input for a moment. Nothing is restarted either way.
        /// </summary>
        public void SetSuspended(bool value)
        {
            if (value == suspended) return;

            suspended = value;
            if (!visible || root == null) return;

            if (value)
            {
                focusBeforeSuspend = FocusedAction;
                panel.SetEnabled(false);
                root.AddToClassList(HiddenClass);
                return;
            }

            guard = ResumeGuardSeconds;
            panel.SetEnabled(true);
            root.RemoveFromClassList(HiddenClass);
            if (sounds != null) sounds.PointerActive = !keyboardMode;
            if (!ready) return;

            // Focus only takes once the panel is displayed again, a frame after its class changes.
            ResultsAction? restore = focusBeforeSuspend;
            root.schedule.Execute(() =>
            {
                if (suspended || !ready || leaving) return;

                Button target = restore.HasValue ? ButtonOf(restore.Value) : null;
                if (target != null && target.canGrabFocus) target.Focus();
                else FocusDefault();
            }).StartingIn(20);
        }

        static VisualElement BuildRow(PlayerResult player)
        {
            Color color = Palette.Of(player.Faction);

            var row = new VisualElement();
            row.AddToClassList("results__row");
            row.EnableInClassList("results__row--human", player.IsHuman);

            var who = new VisualElement();
            who.AddToClassList("results__cell--player");
            var swatch = new VisualElement();
            swatch.AddToClassList("results__swatch");
            swatch.style.backgroundColor = color;
            who.Add(swatch);

            // Names are shown as written: a mission may name a player anything.
            var name = new Label(player.Name) { enableRichText = false };
            name.AddToClassList("results__name");
            name.style.color = Palette.Bright(color, 0.3f);
            who.Add(name);
            if (player.IsHuman)
            {
                var you = new Label("YOU");
                you.AddToClassList("results__you");
                who.Add(you);
            }

            row.Add(who);
            row.Add(Number(player.DartsDestroyed));
            row.Add(Number(player.DartsLost));
            return row;
        }

        static Label Number(int value)
        {
            var label = new Label(value.ToString(CultureInfo.InvariantCulture));
            label.AddToClassList("results__cell");
            label.AddToClassList("results__cell--number");
            label.AddToClassList("results__value");
            return label;
        }

        // ------------------------------------------------------------------ actions

        /// <summary>
        /// What clicking an action's button does, or pressing Enter or Space with it focused. Returns
        /// false, and does nothing, while the results are not ready yet, once an action has already been
        /// picked, and for a Next Mission that is disabled or hidden.
        /// </summary>
        public bool Activate(ResultsAction action)
        {
            if (!ready || leaving || suspended || guard > 0f || !IsEnabled(action)) return false;

            leaving = true;
            leavingFor = action;
            leaveAge = 0f;
            if (sounds != null)
            {
                if (action == ResultsAction.BackToMenu) sounds.Back();
                else sounds.Select();
            }

            // Fade out under the cue; the cover also keeps the pointer off the buttons.
            fade.pickingMode = PickingMode.Position;
            fade.AddToClassList(FadeClass);
            return true;
        }

        Button ButtonOf(ResultsAction action)
        {
            switch (action)
            {
                case ResultsAction.NextMission: return nextButton;
                case ResultsAction.Restart: return restartButton;
                default: return backButton;
            }
        }

        void FocusDefault()
        {
            Button target = ButtonOf(plan.InitialFocus);
            if (!target.canGrabFocus) target = restartButton;
            if (target.canGrabFocus) target.Focus();
        }

        // ------------------------------------------------------------------ input in use

        // As in the menu: the style sheet highlights the hovered action in pointer mode and the focused
        // one in keyboard mode.
        void SetKeyboardMode(bool keys)
        {
            keyboardMode = keys;
            if (sounds != null) sounds.PointerActive = !keys;
            root.EnableInClassList(KeyboardModeClass, keys);
            root.EnableInClassList(PointerModeClass, !keys);
        }

        // The panel also reports the pointer when the layout changes under it; only real movement counts.
        void OnPointerMoved(PointerMoveEvent moved)
        {
            if (moved.position == lastPointer) return;

            lastPointer = moved.position;
            if (keyboardMode) SetKeyboardMode(false);
        }

        // A click beside the buttons does nothing at all: it must not even take the focus away from the
        // action the keyboard would activate.
        void OnPointerDown(PointerDownEvent pressed)
        {
            if (keyboardMode) SetKeyboardMode(false);
            if (!(pressed.target is Button)) root.focusController?.IgnoreEvent(pressed);
        }

        // Pointing at an action gives it the focus, so switching to the keyboard carries on from there.
        void OnPointerEnter(PointerEnterEvent pointer)
        {
            var button = (Button)pointer.currentTarget;
            if (!ready || leaving || suspended || keyboardMode || !button.enabledInHierarchy) return;

            if (sounds != null) sounds.Hover();
            button.Focus();
        }

        // Should the focus ever be lost, the first arrow or Tab press brings it back to the default action.
        void OnNavigationMove(NavigationMoveEvent move)
        {
            if (!keyboardMode) SetKeyboardMode(true);
            if (!ready || leaving || FocusedAction != null) return;

            move.StopPropagation();
            root.focusController?.IgnoreEvent(move);
            FocusDefault();
        }

        // ------------------------------------------------------------------ lifecycle

        // The document builds a new tree whenever it is enabled again, so a tree that has left its panel
        // is looked up afresh.
        bool Bind()
        {
            if (root != null && root.panel != null) return true;
            if (document == null || document.rootVisualElement == null) return false;

            VisualElement tree = document.rootVisualElement;
            root = tree.Q("resultsRoot");
            if (root == null)
            {
                Debug.LogError("Squadra: the results document has no resultsRoot element.", this);
                return false;
            }

            panel = root.Q("resultsPanel");
            rows = root.Q("resultsRows");
            nextSlot = root.Q("nextMissionSlot");
            fade = root.Q("resultsFade");
            celebration = root.Q<ResultsCelebration>("resultsCelebration");
            heading = root.Q<Label>("resultsHeading");
            message = root.Q<Label>("resultsMessage");
            time = root.Q<Label>("resultsTime");
            nextTooltip = root.Q<Label>("nextMissionTooltip");
            nextButton = root.Q<Button>("nextMissionButton");
            restartButton = root.Q<Button>("restartButton");
            backButton = root.Q<Button>("backToMenuButton");

            nextButton.clicked += () => Activate(ResultsAction.NextMission);
            restartButton.clicked += () => Activate(ResultsAction.Restart);
            backButton.clicked += () => Activate(ResultsAction.BackToMenu);
            foreach (Button button in new[] { nextButton, restartButton, backButton })
                button.RegisterCallback<PointerEnterEvent>(OnPointerEnter);

            root.RegisterCallback<PointerMoveEvent>(OnPointerMoved, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);
            return true;
        }

        void Start()
        {
            Bind();
        }

        void Update()
        {
            if (!visible || suspended) return;

            if (guard > 0f) guard -= Time.unscaledDeltaTime;

            // The entrance starts a frame after the panel has been laid out transparent.
            if (!entered && age > 0f)
            {
                entered = true;
                panel.AddToClassList(ShownClass);
            }

            age += Time.unscaledDeltaTime;
            if (!ready && age >= ReadySeconds)
            {
                // The default action starts out highlighted, so it is plain what Enter would do. Moving
                // the mouse hands the highlight over to whatever is under the pointer.
                ready = true;
                SetKeyboardMode(true);
                FocusDefault();
            }

            if (leaving)
            {
                leaveAge += Time.unscaledDeltaTime;
                if (leaveAge >= LeaveSeconds && chosen != null)
                {
                    Action<ResultsAction> act = chosen;
                    chosen = null;
                    act(leavingFor);
                }

                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (ready && !keyboardMode && keyboard != null && keyboard.anyKey.wasPressedThisFrame)
                SetKeyboardMode(true);
        }
    }
}
