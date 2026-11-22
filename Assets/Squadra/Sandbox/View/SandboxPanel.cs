using System;
using System.Collections.Generic;
using System.Globalization;
using Squadra.Pause;
using Squadra.Sandbox;
using Squadra.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Squadra
{
    /// <summary>
    /// The sandbox's controls. A menu in the top right corner switches between Inspect and Order mode,
    /// pauses and resumes, steps the game speed and resets the setup. The panel below it
    /// edits the inspected world's Darts, owner, type and production, each player's color, and the run's
    /// tuning; its arrow slides it off to the right and back. It shows the live state every frame and hands
    /// every edit to the <see cref="SandboxRun"/> or its session, which carries it out or says why not; the
    /// reason is shown and nothing changes. Nothing here is saved, and nothing here decides a rule.
    ///
    /// The pointer over the menu or the panel and the keys while one of their controls has the keyboard are
    /// theirs, never the battlefield's. Keys while nothing in them has the keyboard: Space pauses or
    /// resumes and M switches Order mode on and off; Esc is the pause menu's. While the pause menu is open
    /// the panel is dimmed with the battlefield and takes no pointer or keys at all. The Set Default
    /// icons explain themselves beside the pointer, drawn by a <see cref="SandboxTooltip"/>.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class SandboxPanel : MonoBehaviour
    {
        const string OnClass = "sandbox-menu-button--on";
        const string CollapsedClass = "sandbox-panel--collapsed";
        const string OpenSwatchClass = "sandbox-swatch--open";
        const string OverriddenClass = "sandbox-tuning__name--overridden";
        const string ScrimOnClass = "sandbox-scrim--on";

        static readonly NodeKind[] Kinds = { NodeKind.Planet, NodeKind.Moon, NodeKind.Asteroid };

        // The popup list of a dropdown is laid over the whole screen, outside the panel; it is the panel's too.
        const string DropdownClass = "unity-base-dropdown";
        const string StyleSheetName = "SandboxPanel";

        [SerializeField] UIDocument document;

        // One tuning value: what it is now, whether it is overridden, how to set it and how to reset it.
        sealed class TuningRow
        {
            public Func<string> Shown;
            public Func<bool> Overridden;
            public Func<string> Default;
            public Func<string, string> Apply; // the text typed; returns why it was refused, or null
            public Func<string> Reset;
            public Label Name;
            public TextField Field;
            public Button ResetButton;
            public Label Problem;
        }

        // One player: its Darts and its color.
        sealed class PlayerRow
        {
            public Faction Faction;
            public Button Swatch;
            public Label Count;
            public Button ResetColor;
            public VisualElement PickerBox;
            public SandboxColorPicker Picker;
            public Label Hex;
        }

        readonly List<TuningRow> tuning = new List<TuningRow>();
        readonly List<PlayerRow> players = new List<PlayerRow>();
        readonly List<Faction> owners = new List<Faction>();

        SandboxRun run;
        VisualElement sideElement;
        VisualElement scrimElement;
        VisualElement panelElement;
        VisualElement menuElement;
        Button orderButton;
        Button pauseButton;
        Button speedButton;
        SandboxIcon orderIcon;
        SandboxIcon pauseIcon;
        SandboxIcon toggleIcon;
        Label noWorldLabel;
        VisualElement worldSection;
        Label worldNameLabel;
        Label worldStateLabel;
        TextField dartsField;
        DropdownField ownerField;
        DropdownField kindField;
        Toggle productionToggle;
        Label productionNote;
        Label worldProblemLabel;
        Label setupProblemsLabel;
        PlayerRow openPicker;
        VisualElement sharedTree;
        StyleSheet sharedSheet;
        int shownWorld = -2;
        bool expanded = true;

        SandboxSession Session => run.Session;

        /// <summary>Connects the panel to the run it controls and lays out its rows.</summary>
        public void Bind(SandboxRun boundRun)
        {
            run = boundRun;
            if (document == null) document = GetComponent<UIDocument>();
            VisualElement root = document.rootVisualElement;

            sideElement = root.Q("sandboxSide");
            scrimElement = root.Q("sandboxScrim");
            panelElement = root.Q("sandboxPanel");
            menuElement = root.Q("sandboxMenu");
            orderButton = root.Q<Button>("orderModeButton");
            pauseButton = root.Q<Button>("pauseButton");
            speedButton = root.Q<Button>("speedButton");
            noWorldLabel = root.Q<Label>("noWorldLabel");
            worldSection = root.Q("worldSection");
            worldNameLabel = root.Q<Label>("worldNameLabel");
            worldStateLabel = root.Q<Label>("worldStateLabel");
            dartsField = root.Q<TextField>("dartsField");
            ownerField = root.Q<DropdownField>("ownerField");
            kindField = root.Q<DropdownField>("kindField");
            productionToggle = root.Q<Toggle>("productionToggle");
            productionNote = root.Q<Label>("productionNote");
            worldProblemLabel = root.Q<Label>("worldProblemLabel");
            setupProblemsLabel = root.Q<Label>("setupProblemsLabel");

            // The icon itself says the mode: the hand in Order mode, the arrow pointer in Inspect mode.
            orderIcon = new SandboxIcon(SandboxIconShape.Hand);
            orderButton.Add(orderIcon);
            pauseIcon = new SandboxIcon(SandboxIconShape.Pause);
            pauseButton.Add(pauseIcon);
            Button resetButton = root.Q<Button>("resetButton");
            resetButton.Add(new SandboxIcon(SandboxIconShape.Reset));
            Button toggleButton = root.Q<Button>("panelToggleButton");
            toggleIcon = new SandboxIcon(SandboxIconShape.Collapse);
            toggleButton.Add(toggleIcon);

            orderButton.clicked += () => run.SetOrderMode(!run.OrderMode);
            pauseButton.clicked += () => run.SetPaused(!run.Paused);
            speedButton.clicked += NextSpeed;
            resetButton.clicked += run.ResetSetup;
            toggleButton.clicked += () => SetExpanded(!expanded);
            dartsField.RegisterValueChangedCallback(e => SetDarts(e.newValue));
            productionToggle.RegisterValueChangedCallback(e => SetProducing(e.newValue));

            BuildOwners();
            BuildKinds();
            BuildPlayers(root.Q("playerRows"));
            BuildTuning(root.Q("tuningRows"));
            new SandboxTooltip(root);
            ShareStyleWithDropdowns(root);

            // A run starts with the panel slid away, the battlefield in full view.
            SetExpanded(false);

            Session.Started += ShowSetupProblems;
            ShowSetupProblems();
            GameSettings.Clock.Changed += ShowSpeed;
            ShowSpeed();
            Refresh();
        }

        void OnDestroy()
        {
            // The run may already be destroyed, as when play mode ends, and then compares equal to null; so
            // this asks only whether the panel was bound. Every menu shares the panel's root: a sheet left
            // hung there would give the main menu's dropdown lists the sandbox's sizes.
            if (sharedTree != null) sharedTree.styleSheets.Remove(sharedSheet);
            sharedTree = null;
            if (ReferenceEquals(run, null)) return;
            GameSettings.Clock.Changed -= ShowSpeed;
        }

        // The list a dropdown opens is laid over the whole panel, outside this document, so it only takes
        // the sandbox's look from this document's style sheet hung on the panel's root as well. Its rules
        // name the sandbox's own classes and the dropdown list's, so nothing else on the panel changes.
        void ShareStyleWithDropdowns(VisualElement root)
        {
            VisualElement tree = root.panel?.visualTree;
            if (tree == null) return;

            for (int i = 0; i < root.styleSheets.count; i++)
            {
                if (root.styleSheets[i].name != StyleSheetName) continue;
                sharedSheet = root.styleSheets[i];
                sharedTree = tree;
                tree.styleSheets.Add(sharedSheet);
                return;
            }
        }

        // ------------------------------------------------------------------ input

        /// <summary>True while the pointer is over the menu or the panel: its clicks are theirs.</summary>
        public bool IsPointerOver()
        {
            if (panelElement == null || panelElement.panel == null) return false;
            Pointer pointer = Pointer.current;
            if (pointer == null) return false;

            Vector2 screen = pointer.position.ReadValue();
            Vector2 local = RuntimePanelUtils.ScreenToPanel(panelElement.panel,
                new Vector2(screen.x, Screen.height - screen.y));
            return IsOurs(panelElement.panel.Pick(local));
        }

        /// <summary>True while one of the menu's or the panel's controls has the keyboard.</summary>
        public bool HasFocus => IsOurs(panelElement?.panel?.focusController?.focusedElement as VisualElement);

        bool IsOurs(VisualElement element)
        {
            if (element == null) return false;
            if (element == panelElement || panelElement.Contains(element)
                || element == menuElement || menuElement.Contains(element)) return true;

            for (VisualElement at = element; at != null; at = at.parent)
            {
                if (at.ClassListContains(DropdownClass)) return true;
            }

            return false;
        }

        /// <summary>Slides the panel out, or away to the right so that only its arrow shows.</summary>
        public void SetExpanded(bool value)
        {
            if (expanded == value) return;
            expanded = value;
            if (!value) Blur();
            panelElement.EnableInClassList(CollapsedClass, !value);
            toggleIcon.Shape = value ? SandboxIconShape.Collapse : SandboxIconShape.Expand;
        }

        /// <summary>
        /// Dims the battlefield and the controls under the pause menu, or lifts the dimming. Held, the menu
        /// and the panel take neither the pointer nor the keyboard; a value being typed is applied first.
        /// </summary>
        public void SetHeld(bool value)
        {
            if (sideElement == null) return;

            if (value) Blur();
            sideElement.SetEnabled(!value);

            // The pause menu shares this panel. Its Settings and their dropdown lists keep the menu's own
            // sizes only while the sandbox's sheet is off the panel's root; no sandbox list is open now.
            if (sharedTree != null)
            {
                if (value) sharedTree.styleSheets.Remove(sharedSheet);
                else if (!sharedTree.styleSheets.Contains(sharedSheet)) sharedTree.styleSheets.Add(sharedSheet);
            }

            scrimElement.pickingMode = value ? PickingMode.Position : PickingMode.Ignore;
            scrimElement.EnableInClassList(ScrimOnClass, value);
        }

        /// <summary>Takes the keyboard away from the panel. A value being typed is applied first.</summary>
        public void Blur()
        {
            panelElement?.panel?.focusController?.focusedElement?.Blur();
        }

        /// <summary>The panel's keys, read while nothing in it has the keyboard.</summary>
        public void ReadShortcuts()
        {
            Keyboard keys = Keyboard.current;
            if (keys == null) return;

            if (keys.spaceKey.wasPressedThisFrame) run.SetPaused(!run.Paused);
            if (keys.mKey.wasPressedThisFrame) run.SetOrderMode(!run.OrderMode);
        }

        // ------------------------------------------------------------------ showing the state

        /// <summary>Shows the run as it is now. Called every frame by the run.</summary>
        public void Refresh()
        {
            if (run == null) return;

            orderIcon.Shape = run.OrderMode ? SandboxIconShape.Hand : SandboxIconShape.Cursor;
            pauseIcon.Shape = run.Paused ? SandboxIconShape.Play : SandboxIconShape.Pause;
            pauseButton.EnableInClassList(OnClass, run.Paused);

            ShowWorld();
            foreach (PlayerRow row in players)
                ShowPlayer(row);
            foreach (TuningRow row in tuning)
                ShowTuning(row);
        }

        void ShowWorld()
        {
            int selected = run.InspectedWorld;
            if (selected != shownWorld)
            {
                shownWorld = selected;
                worldProblemLabel.text = string.Empty;
            }

            worldProblemLabel.style.display = worldProblemLabel.text.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;

            bool any = selected >= 0;
            worldSection.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
            noWorldLabel.style.display = any ? DisplayStyle.None : DisplayStyle.Flex;
            if (!any) return;

            Node node = Session.Match.Nodes[selected];
            worldNameLabel.text = node.Name;
            worldNameLabel.style.color = Palette.Bright(Palette.Of(node.Owner), 0.3f);
            worldStateLabel.text = Describe(node);
            if (!IsFocused(dartsField)) dartsField.SetValueWithoutNotify(node.Darts.ToString(CultureInfo.InvariantCulture));

            ownerField.SetValueWithoutNotify(ownerField.choices[owners.IndexOf(node.Owner)]);
            kindField.SetValueWithoutNotify(kindField.choices[Array.IndexOf(Kinds, node.Kind)]);

            bool asteroid = node.Kind == NodeKind.Asteroid;
            productionToggle.SetEnabled(!asteroid);
            productionToggle.SetValueWithoutNotify(!asteroid && !node.ProductionStopped);
            productionNote.text = asteroid
                ? "Asteroids never produce Darts."
                : node.CanProduce
                    ? Rate(node.ProductionInterval) + " Darts per second while owned and at peace."
                    : "Production is switched off.";
        }

        static string Describe(Node node)
        {
            string owner = node.Owner == Faction.Neutral ? "Neutral" : "Owned by " + node.Owner;
            Battle battle = node.Battle;
            if (battle != null)
            {
                string fight = "Fighting: " + battle.Stationed + " " + battle.StationedDarts + " vs "
                    + battle.Arriving + " " + battle.ArrivingDarts;
                if (node.IsBeingCaptured)
                    fight += ", " + node.Captor + " capture held at " + Mathf.RoundToInt(node.CaptureProgress * 100f) + "%";
                return owner + ". " + fight + ".";
            }

            if (node.IsBeingCaptured)
                return owner + ". " + node.Captor + " capturing, " + Mathf.RoundToInt(node.CaptureProgress * 100f)
                    + "% done; the Darts here are " + node.Captor + "'s.";
            return owner + ".";
        }

        void ShowSpeed()
        {
            GameSpeed speed = GameSettings.Clock.Speed;
            speedButton.text = "×" + GameClock.Multiplier(speed).ToString("0.##", CultureInfo.InvariantCulture);
        }

        static int SpeedStep(GameSpeed speed)
        {
            IReadOnlyList<GameSpeed> speeds = GameClock.Speeds;
            for (int i = 0; i < speeds.Count; i++)
            {
                if (speeds[i] == speed) return i;
            }

            return 0;
        }

        // From Slow to Turbo one step at a time, and from Turbo back to Slow.
        void NextSpeed()
        {
            IReadOnlyList<GameSpeed> speeds = GameClock.Speeds;
            GameSpeed next = speeds[(SpeedStep(GameSettings.Clock.Speed) + 1) % speeds.Count];
            run.SetSpeed(next);
        }

        void ShowSetupProblems()
        {
            IReadOnlyList<string> problems = Session.Problems;
            setupProblemsLabel.text = problems.Count == 0
                ? string.Empty
                : "Left out of the setup:\n" + string.Join("\n", problems);
            setupProblemsLabel.style.display = problems.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }

        static bool IsFocused(VisualElement field)
        {
            Focusable focused = field.panel?.focusController?.focusedElement;
            return focused is VisualElement element && (element == field || field.Contains(element));
        }

        // Darts per second for a production of one Dart every `seconds`.
        static string Rate(double seconds)
        {
            return (1.0 / seconds).ToString("0.####", CultureInfo.InvariantCulture);
        }

        // ------------------------------------------------------------------ the selected world

        void SetDarts(string text)
        {
            int selected = run.InspectedWorld;
            if (selected < 0) return;

            string problem = "Darts must be a whole number from 0 to " + SandboxSession.MostDarts + ".";
            if (int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int darts))
                Session.TrySetDarts(selected, darts, out problem);
            worldProblemLabel.text = problem ?? string.Empty;
        }

        void SetOwner(Faction owner)
        {
            int selected = run.InspectedWorld;
            if (selected < 0) return;

            Session.TrySetOwner(selected, owner, out string problem);
            worldProblemLabel.text = problem ?? string.Empty;
        }

        void SetKind(NodeKind kind)
        {
            int selected = run.InspectedWorld;
            if (selected < 0) return;

            Session.TrySetKind(selected, kind, out string problem);
            worldProblemLabel.text = problem ?? string.Empty;
        }

        void SetProducing(bool producing)
        {
            int selected = run.InspectedWorld;
            if (selected < 0) return;

            Session.TrySetProducing(selected, producing, out string problem);
            worldProblemLabel.text = problem ?? string.Empty;
        }

        // ------------------------------------------------------------------ building the rows

        void BuildOwners()
        {
            owners.Add(Faction.Neutral);
            foreach (MatchPlayer player in Session.Players)
                owners.Add(player.Faction);

            var names = new List<string>();
            foreach (Faction owner in owners)
                names.Add(owner.ToString());
            ownerField.choices = names;
            ownerField.RegisterValueChangedCallback(e =>
            {
                int index = names.IndexOf(e.newValue);
                if (index >= 0) SetOwner(owners[index]);
            });
        }

        void BuildKinds()
        {
            var names = new List<string>();
            foreach (NodeKind kind in Kinds)
                names.Add(kind.ToString());
            kindField.choices = names;
            kindField.RegisterValueChangedCallback(e =>
            {
                int index = names.IndexOf(e.newValue);
                if (index >= 0) SetKind(Kinds[index]);
            });
        }

        void BuildPlayers(VisualElement parent)
        {
            foreach (MatchPlayer player in Session.Players)
            {
                var row = new PlayerRow { Faction = player.Faction };
                var box = new VisualElement();
                box.AddToClassList("sandbox-player");

                var top = new VisualElement();
                top.AddToClassList("sandbox-row");
                row.Swatch = new Button(() => TogglePicker(row));
                row.Swatch.AddToClassList("sandbox-swatch");
                var name = new Label(player.Name);
                name.AddToClassList("sandbox-player__name");
                row.Count = new Label();
                row.Count.AddToClassList("sandbox-player__count");
                row.ResetColor = ResetButton(() => ResetColor(row));
                top.Add(row.Swatch);
                top.Add(name);
                top.Add(row.Count);
                top.Add(row.ResetColor);

                row.PickerBox = new VisualElement();
                row.PickerBox.AddToClassList("sandbox-player__picker");
                row.PickerBox.style.display = DisplayStyle.None;
                row.Picker = new SandboxColorPicker();
                row.Picker.Picked += color => Palette.Recolor(row.Faction, color);
                var under = new VisualElement();
                under.AddToClassList("sandbox-row");
                row.Hex = new Label();
                row.Hex.AddToClassList("sandbox-picker__hex");
                under.Add(row.Hex);
                row.PickerBox.Add(row.Picker);
                row.PickerBox.Add(under);

                box.Add(top);
                box.Add(row.PickerBox);
                parent.Add(box);
                players.Add(row);
            }
        }

        void ShowPlayer(PlayerRow row)
        {
            Color color = Palette.Of(row.Faction);
            row.Swatch.style.backgroundColor = color;
            bool recolored = Palette.IsRecolored(row.Faction);
            row.Count.text = Session.Match.TotalDarts(row.Faction) + " Darts"
                + (recolored ? "  -  recolored" : string.Empty);
            row.ResetColor.style.display = recolored ? DisplayStyle.Flex : DisplayStyle.None;
            row.Hex.text = "#" + ColorUtility.ToHtmlStringRGB(color);
        }

        // One picker is open at a time; opening another closes it.
        void TogglePicker(PlayerRow row)
        {
            PlayerRow closing = openPicker;
            if (closing != null)
            {
                closing.PickerBox.style.display = DisplayStyle.None;
                closing.Swatch.RemoveFromClassList(OpenSwatchClass);
                openPicker = null;
            }

            if (closing == row) return;
            openPicker = row;
            row.PickerBox.style.display = DisplayStyle.Flex;
            row.Swatch.AddToClassList(OpenSwatchClass);
            row.Picker.Show(Palette.Of(row.Faction));
        }

        // Only what is drawn changes: the player stays who it is, and no material is touched.
        void ResetColor(PlayerRow row)
        {
            Palette.ResetColor(row.Faction);
            row.Picker.Show(Palette.Of(row.Faction));
        }

        void BuildTuning(VisualElement parent)
        {
            SandboxDartTuning darts = run.Darts;
            AddProduction(parent, "Planet production, Darts/s", NodeKind.Planet);
            AddProduction(parent, "Moon production, Darts/s", NodeKind.Moon);
            AddTuning(parent, "Fleet speed, units/s", new TuningRow
            {
                Shown = () => Number(Session.FleetSpeed),
                Overridden = () => Session.FleetSpeedOverride.HasValue,
                Default = () => Number(SandboxSession.DefaultFleetSpeed),
                Apply = text => TryNumber(text, out double value)
                    ? (Session.TrySetFleetSpeed(value, out string problem) ? null : problem)
                    : "Type a number, such as 2.4.",
                Reset = () => Session.TrySetFleetSpeed(null, out string problem) ? null : problem,
            });
            AddTuning(parent, "Orbit speed, radians/s", new TuningRow
            {
                Shown = () => Number(darts.OrbitSpeed ?? DartDisplay.DefaultOrbitSpeed),
                Overridden = () => darts.OrbitSpeed.HasValue,
                Default = () => Number(DartDisplay.DefaultOrbitSpeed),
                Apply = text => TryNumber(text, out double value)
                    ? (darts.TrySetOrbitSpeed((float)value, out string problem) ? null : problem)
                    : "Type a number, such as 0.42.",
                Reset = () => darts.TrySetOrbitSpeed(null, out string problem) ? null : problem,
            });
            AddCount(parent, "Every Dart drawn up to", () => darts.OneDotPerDartUpTo, DartDisplay.DefaultOneDotPerDartUpTo,
                (int? count, out string problem) => darts.TrySetOneDotPerDartUpTo(count, out problem));
            AddCount(parent, "Most drawn at a world", () => darts.MostStationedDots, DartDisplay.DefaultMostStationedDots,
                (int? count, out string problem) => darts.TrySetMostStationedDots(count, out problem));
            AddCount(parent, "Most drawn in transit", () => darts.MostTravellingDots, DartDisplay.DefaultMostTravellingDots,
                (int? count, out string problem) => darts.TrySetMostTravellingDots(count, out problem));
        }

        delegate bool CountSetter(int? count, out string problem);

        // Shown and typed as Darts per second; the session keeps seconds per Dart, as the match does.
        void AddProduction(VisualElement parent, string name, NodeKind kind)
        {
            AddTuning(parent, name, new TuningRow
            {
                Shown = () => Rate(Session.ProductionInterval(kind)),
                Overridden = () => Session.ProductionOverride(kind).HasValue,
                Default = () => Rate(SandboxSession.DefaultProductionInterval(kind)),
                Apply = text =>
                {
                    if (!TryNumber(text, out double rate)) return "Type a number of Darts per second, such as 2.";
                    double seconds = rate > 0 ? 1.0 / rate : double.PositiveInfinity;
                    if (!(seconds >= SandboxSession.FastestProduction && seconds <= SandboxSession.SlowestProduction))
                        return "Production must be from " + Rate(SandboxSession.SlowestProduction) + " to "
                            + Rate(SandboxSession.FastestProduction) + " Darts per second.";
                    return Session.TrySetProductionInterval(kind, seconds, out string problem) ? null : problem;
                },
                Reset = () => Session.TrySetProductionInterval(kind, null, out string problem) ? null : problem,
            });
        }

        void AddCount(VisualElement parent, string name, Func<int?> current, int fallback, CountSetter set)
        {
            AddTuning(parent, name, new TuningRow
            {
                Shown = () => (current() ?? fallback).ToString(CultureInfo.InvariantCulture),
                Overridden = () => current().HasValue,
                Default = () => fallback.ToString(CultureInfo.InvariantCulture),
                Apply = text =>
                {
                    if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
                        return "Type a whole number.";
                    return set(count, out string problem) ? null : problem;
                },
                Reset = () => set(null, out string problem) ? null : problem,
            });
        }

        // One line per value: its name, the value and the way back to the game's. A refusal shows under it
        // until the value is set again.
        void AddTuning(VisualElement parent, string name, TuningRow row)
        {
            var line = new VisualElement();
            line.AddToClassList("sandbox-tuning");
            row.Name = new Label(name);
            row.Name.AddToClassList("sandbox-tuning__name");
            row.Field = new TextField { isDelayed = true };
            row.Field.AddToClassList("sandbox-field");
            row.Field.AddToClassList("sandbox-tuning__field");
            row.Field.RegisterValueChangedCallback(e => ShowProblem(row, Set(row, e.newValue)));
            row.ResetButton = ResetButton(() => ShowProblem(row, row.Reset()));
            line.Add(row.Name);
            line.Add(row.Field);
            line.Add(row.ResetButton);

            row.Problem = new Label();
            row.Problem.AddToClassList("sandbox-problem");
            row.Problem.AddToClassList("sandbox-tuning__problem");
            ShowProblem(row, null);
            parent.Add(line);
            parent.Add(row.Problem);
            tuning.Add(row);
        }

        // A value typed in as the game's own is no override: the row goes back to following the game.
        static string Set(TuningRow row, string text)
        {
            string problem = row.Apply(text);
            if (problem == null && row.Overridden() && row.Shown() == row.Default()) problem = row.Reset();
            return problem;
        }

        // The circling arrow that sets a value back to the game's, as the menu's reset icon draws it.
        static Button ResetButton(Action clicked)
        {
            var button = new Button(clicked) { tooltip = "Set Default" };
            button.AddToClassList("sandbox-reset");
            button.Add(new SandboxIcon(SandboxIconShape.Reset));
            return button;
        }

        static void ShowProblem(TuningRow row, string problem)
        {
            row.Problem.text = problem ?? string.Empty;
            row.Problem.style.display = problem == null ? DisplayStyle.None : DisplayStyle.Flex;
        }

        void ShowTuning(TuningRow row)
        {
            if (!IsFocused(row.Field)) row.Field.SetValueWithoutNotify(row.Shown());
            bool overridden = row.Overridden();
            row.Name.EnableInClassList(OverriddenClass, overridden);
            row.ResetButton.SetEnabled(overridden);
        }

        static string Number(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        static bool TryNumber(string text, out double value)
        {
            return double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
