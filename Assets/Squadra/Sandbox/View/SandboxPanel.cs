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
    /// The sandbox's controls. A menu in the top right corner switches Order mode on and off, pauses and
    /// resumes, steps the game speed and resets the setup, with the hint line under it. The panel below it
    /// edits the selected world's Darts, owner, type and production, each player's color, and the run's
    /// tuning; its arrow slides it off to the right and back. It shows the live state every frame and hands
    /// every edit to the <see cref="SandboxRun"/> or its session, which carries it out or says why not; the
    /// reason is shown and nothing changes. Nothing here is saved, and nothing here decides a rule.
    ///
    /// The pointer over the menu or the panel and the keys while one of their controls has the keyboard are
    /// theirs, never the battlefield's. Keys while nothing in them has the keyboard: Space pauses or
    /// resumes, M switches Order mode on and off, Esc puts the selection away.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class SandboxPanel : MonoBehaviour
    {
        const string ChosenClass = "sandbox-button--chosen";
        const string OnClass = "sandbox-menu-button--on";
        const string OffClass = "sandbox-menu-button--off";
        const string CollapsedClass = "sandbox-panel--collapsed";
        const string OpenSwatchClass = "sandbox-swatch--open";
        const string OverriddenClass = "sandbox-tuning__name--overridden";

        static readonly NodeKind[] Kinds = { NodeKind.Planet, NodeKind.Moon, NodeKind.Asteroid };

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

        // One player: its Darts, its color and handing it the selected world.
        sealed class PlayerRow
        {
            public Faction Faction;
            public Button Swatch;
            public Label Count;
            public Button GiveWorld;
            public VisualElement PickerBox;
            public SandboxColorPicker Picker;
            public Label Hex;
        }

        readonly List<TuningRow> tuning = new List<TuningRow>();
        readonly List<PlayerRow> players = new List<PlayerRow>();
        readonly Dictionary<Faction, Button> ownerButtons = new Dictionary<Faction, Button>();
        readonly Dictionary<NodeKind, Button> kindButtons = new Dictionary<NodeKind, Button>();

        SandboxRun run;
        VisualElement panelElement;
        VisualElement menuElement;
        Button orderButton;
        Button pauseButton;
        Button speedButton;
        SandboxIcon pauseIcon;
        SandboxIcon speedIcon;
        SandboxIcon toggleIcon;
        Label hintLabel;
        Label noWorldLabel;
        VisualElement worldSection;
        Label worldNameLabel;
        Label worldStateLabel;
        TextField dartsField;
        Toggle productionToggle;
        Label productionNote;
        Label worldProblemLabel;
        Label setupProblemsLabel;
        PlayerRow openPicker;
        int shownWorld = -2;
        bool expanded = true;

        SandboxSession Session => run.Session;

        /// <summary>True while the panel is out; false while it is slid away and only its arrow shows.</summary>
        public bool Expanded => expanded;

        /// <summary>Connects the panel to the run it controls and lays out its rows.</summary>
        public void Bind(SandboxRun boundRun)
        {
            run = boundRun;
            if (document == null) document = GetComponent<UIDocument>();
            VisualElement root = document.rootVisualElement;

            panelElement = root.Q("sandboxPanel");
            menuElement = root.Q("sandboxMenu");
            orderButton = root.Q<Button>("orderModeButton");
            pauseButton = root.Q<Button>("pauseButton");
            speedButton = root.Q<Button>("speedButton");
            hintLabel = root.Q<Label>("hintLabel");
            noWorldLabel = root.Q<Label>("noWorldLabel");
            worldSection = root.Q("worldSection");
            worldNameLabel = root.Q<Label>("worldNameLabel");
            worldStateLabel = root.Q<Label>("worldStateLabel");
            dartsField = root.Q<TextField>("dartsField");
            productionToggle = root.Q<Toggle>("productionToggle");
            productionNote = root.Q<Label>("productionNote");
            worldProblemLabel = root.Q<Label>("worldProblemLabel");
            setupProblemsLabel = root.Q<Label>("setupProblemsLabel");

            orderButton.Add(new SandboxIcon(SandboxIconShape.Hand));
            pauseIcon = new SandboxIcon(SandboxIconShape.Pause);
            pauseButton.Add(pauseIcon);
            speedIcon = new SandboxIcon(SandboxIconShape.Speed);
            speedButton.Add(speedIcon);
            Button resetButton = root.Q<Button>("resetButton");
            resetButton.Add(new SandboxIcon(SandboxIconShape.Reset));
            Button toggleButton = root.Q<Button>("panelToggleButton");
            toggleIcon = new SandboxIcon(SandboxIconShape.Collapse);
            toggleButton.Add(toggleIcon);

            orderButton.clicked += () => run.SetOrderMode(!run.OrderMode);
            pauseButton.clicked += () => run.SetPaused(!run.Paused);
            speedButton.clicked += NextSpeed;
            resetButton.clicked += ResetSetup;
            toggleButton.clicked += () => SetExpanded(!expanded);
            dartsField.RegisterValueChangedCallback(e => SetDarts(e.newValue));
            productionToggle.RegisterValueChangedCallback(e => SetProducing(e.newValue));

            BuildOwners(root.Q("ownerChoices"));
            BuildKinds(root.Q("kindChoices"));
            BuildPlayers(root.Q("playerRows"));
            BuildTuning(root.Q("tuningRows"));

            Session.Started += ShowSetupProblems;
            ShowSetupProblems();
            GameSettings.Clock.Changed += ShowSpeed;
            ShowSpeed();
            Refresh();
        }

        void OnDestroy()
        {
            if (run == null) return;
            GameSettings.Clock.Changed -= ShowSpeed;
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
            return element == panelElement || panelElement.Contains(element)
                || element == menuElement || menuElement.Contains(element);
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
            if (keys.escapeKey.wasPressedThisFrame) run.Click(-1);
        }

        // ------------------------------------------------------------------ showing the state

        /// <summary>Shows the run as it is now. Called every frame by the run.</summary>
        public void Refresh()
        {
            if (run == null) return;

            orderButton.EnableInClassList(OnClass, run.OrderMode);
            orderButton.EnableInClassList(OffClass, !run.OrderMode);
            pauseIcon.Shape = run.Paused ? SandboxIconShape.Play : SandboxIconShape.Pause;
            pauseButton.EnableInClassList(OnClass, run.Paused);
            hintLabel.text = run.Hint;

            ShowWorld();
            foreach (PlayerRow row in players)
                ShowPlayer(row);
            foreach (TuningRow row in tuning)
                ShowTuning(row);
        }

        void ShowWorld()
        {
            int selected = run.SelectedWorld;
            if (selected != shownWorld)
            {
                shownWorld = selected;
                worldProblemLabel.text = string.Empty;
            }

            bool any = selected >= 0;
            worldSection.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
            noWorldLabel.style.display = any ? DisplayStyle.None : DisplayStyle.Flex;
            if (!any) return;

            Node node = Session.Match.Nodes[selected];
            worldNameLabel.text = node.Name;
            worldNameLabel.style.color = Palette.Bright(Palette.Of(node.Owner), 0.3f);
            worldStateLabel.text = Describe(node);
            if (!IsFocused(dartsField)) dartsField.SetValueWithoutNotify(node.Darts.ToString(CultureInfo.InvariantCulture));

            foreach (KeyValuePair<Faction, Button> owner in ownerButtons)
                SetChosen(owner.Value, owner.Key == node.Owner);
            foreach (KeyValuePair<NodeKind, Button> kind in kindButtons)
                SetChosen(kind.Value, kind.Key == node.Kind);

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
            speedIcon.Arrows = SpeedStep(speed) + 1;
            speedButton.tooltip = "Game speed: " + GameClock.NameOf(speed)
                + ". Press for the next; Settings keeps its own choice";
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
            run.Notify("Game speed: " + GameClock.NameOf(next));
        }

        void ShowSetupProblems()
        {
            IReadOnlyList<string> problems = Session.Problems;
            setupProblemsLabel.text = problems.Count == 0
                ? string.Empty
                : "Left out of the setup:\n" + string.Join("\n", problems);
        }

        static void SetChosen(VisualElement element, bool chosen)
        {
            element.EnableInClassList(ChosenClass, chosen);
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
            int selected = run.SelectedWorld;
            if (selected < 0) return;

            string problem = "Darts must be a whole number from 0 to " + SandboxSession.MostDarts + ".";
            if (int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int darts))
                Session.TrySetDarts(selected, darts, out problem);
            worldProblemLabel.text = problem ?? string.Empty;
        }

        void SetOwner(Faction owner)
        {
            int selected = run.SelectedWorld;
            if (selected < 0) return;

            Session.TrySetOwner(selected, owner, out string problem);
            worldProblemLabel.text = problem ?? string.Empty;
        }

        void SetKind(NodeKind kind)
        {
            int selected = run.SelectedWorld;
            if (selected < 0) return;

            Session.TrySetKind(selected, kind, out string problem);
            worldProblemLabel.text = problem ?? string.Empty;
        }

        void SetProducing(bool producing)
        {
            int selected = run.SelectedWorld;
            if (selected < 0) return;

            Session.TrySetProducing(selected, producing, out string problem);
            worldProblemLabel.text = problem ?? string.Empty;
        }

        // ------------------------------------------------------------------ the menu

        void ResetSetup()
        {
            run.ResetSetup();
            run.Notify("The starting setup is laid down again");
        }

        // ------------------------------------------------------------------ building the rows

        void BuildOwners(VisualElement parent)
        {
            AddOwner(parent, Faction.Neutral);
            foreach (MatchPlayer player in Session.Players)
                AddOwner(parent, player.Faction);
        }

        void AddOwner(VisualElement parent, Faction owner)
        {
            var button = new Button(() => SetOwner(owner)) { text = Palette.NameOf(owner) };
            button.AddToClassList("sandbox-button");
            button.AddToClassList("sandbox-button--small");
            parent.Add(button);
            ownerButtons[owner] = button;
        }

        void BuildKinds(VisualElement parent)
        {
            foreach (NodeKind kind in Kinds)
            {
                NodeKind chosen = kind;
                var button = new Button(() => SetKind(chosen)) { text = kind.ToString().ToUpperInvariant() };
                button.AddToClassList("sandbox-button");
                parent.Add(button);
                kindButtons[kind] = button;
            }
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
                row.Swatch = new Button(() => TogglePicker(row)) { tooltip = "Pick another color for " + player.Name };
                row.Swatch.AddToClassList("sandbox-swatch");
                var name = new Label(player.Name);
                name.AddToClassList("sandbox-player__name");
                row.Count = new Label();
                row.Count.AddToClassList("sandbox-player__count");
                row.GiveWorld = new Button(() => SetOwner(row.Faction))
                {
                    text = "GIVE WORLD",
                    tooltip = "Hand the selected world to " + player.Name,
                };
                row.GiveWorld.AddToClassList("sandbox-button");
                row.GiveWorld.AddToClassList("sandbox-button--small");
                top.Add(row.Swatch);
                top.Add(name);
                top.Add(row.Count);
                top.Add(row.GiveWorld);

                row.PickerBox = new VisualElement();
                row.PickerBox.AddToClassList("sandbox-player__picker");
                row.PickerBox.style.display = DisplayStyle.None;
                row.Picker = new SandboxColorPicker();
                row.Picker.Picked += color => Palette.Recolor(row.Faction, color);
                var under = new VisualElement();
                under.AddToClassList("sandbox-row");
                row.Hex = new Label();
                row.Hex.AddToClassList("sandbox-picker__hex");
                var reset = new Button(() => ResetColor(row))
                {
                    text = "RESET COLOR",
                    tooltip = "Back to " + player.Name + "'s own color",
                };
                reset.AddToClassList("sandbox-button");
                reset.AddToClassList("sandbox-button--small");
                under.Add(row.Hex);
                under.Add(reset);
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
            row.Count.text = Session.Match.TotalDarts(row.Faction) + " Darts"
                + (Palette.IsRecolored(row.Faction) ? "  -  recolored" : string.Empty);
            row.Hex.text = "#" + ColorUtility.ToHtmlStringRGB(color);

            int selected = run.SelectedWorld;
            row.GiveWorld.SetEnabled(selected >= 0 && Session.Match.Nodes[selected].Owner != row.Faction);
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
            row.Field.RegisterValueChangedCallback(e => ShowProblem(row, row.Apply(e.newValue)));
            row.ResetButton = new Button(() => ShowProblem(row, row.Reset()))
            {
                text = "DEFAULT",
                tooltip = "Back to the game's value as the code has it now",
            };
            row.ResetButton.AddToClassList("sandbox-button");
            row.ResetButton.AddToClassList("sandbox-button--small");
            row.ResetButton.AddToClassList("sandbox-button--tight");
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
            row.Name.tooltip = overridden ? "Overridden; the game plays " + row.Default() : "The game's value";
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
