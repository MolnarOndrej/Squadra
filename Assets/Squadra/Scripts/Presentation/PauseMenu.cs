using System;
using Squadra.Pause;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Squadra
{
    /// <summary>
    /// The mission's pause menu: Paused above Resume, Settings, Quit to Menu and Quit to Desktop, the
    /// shared Settings screen, and the confirmation dialog over them. Escape opens it at any point of a
    /// mission, a result included, and backs out of it one step at a time. It asks the game controller
    /// to freeze and release the mission and to carry out a confirmed quit; <see cref="PauseFlow"/>
    /// decides where each press leads. Everything it shows sits above the results panel and over the
    /// battlefield the controller has dimmed, so it adds no dimming of its own.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour, IDialogHost
    {
        const string HiddenClass = "hidden";
        const string ActiveScreenClass = "screen--active";
        const string FadeClass = "fade--on";
        const string PointerModeClass = "menu-root--pointer";
        const string KeyboardModeClass = "menu-root--keys";
        const string PopupClass = "unity-base-dropdown";
        const long LeaveFadeMilliseconds = 350;

        // Indexed by PauseAction.
        static readonly string[] ButtonNames =
        {
            "resumeButton", "pauseSettingsButton", "quitToMenuButton", "quitToDesktopButton",
        };

        [SerializeField] UIDocument document;
        [SerializeField] MenuAudio sounds;
        [SerializeField] GameController game;

        readonly PauseFlow flow = new PauseFlow();

        VisualElement root;
        VisualElement menuScreen;
        VisualElement settingsScreen;
        VisualElement fade;
        Label heading;
        Button[] buttons;
        SettingsScreen settings;
        MenuDialog dialog;

        bool popupWasOpen;
        bool keyboardMode;
        Vector3 lastPointer = new Vector3(float.NaN, float.NaN);

        /// <summary>The pause screen in front, or <see cref="PauseScreen.Closed"/> while the mission runs.</summary>
        public PauseScreen Screen => flow.Screen;

        /// <summary>True from the moment the menu opens until the mission is resumed.</summary>
        public bool IsOpen => flow.IsOpen;

        /// <summary>True once a quit has been confirmed and the mission is on its way out.</summary>
        public bool Leaving => flow.Leaving;

        public bool DialogOpen => dialog != null && dialog.IsOpen;
        public string DialogTitle => dialog != null ? dialog.Title : string.Empty;
        public string DialogMessage => dialog != null ? dialog.Message : string.Empty;
        public string HeadingText => heading != null ? heading.text : string.Empty;

        /// <summary>
        /// True while the keyboard is the input in use: the focused control is highlighted. False while
        /// the mouse is: only the control under the pointer is.
        /// </summary>
        public bool KeyboardMode => keyboardMode;

        /// <summary>The root menu action that has the keyboard focus, or null when none has.</summary>
        public PauseAction? FocusedAction
        {
            get
            {
                Focusable focused = root?.focusController?.focusedElement;
                if (focused == null || buttons == null) return null;
                for (int i = 0; i < buttons.Length; i++)
                    if (focused == buttons[i]) return (PauseAction)i;
                return null;
            }
        }

        /// <summary>True while the dialog's Cancel button has the keyboard focus.</summary>
        public bool CancelFocused => DialogOpen && root.focusController?.focusedElement == dialog.CancelButton;

        /// <summary>True when the screen can be seen right now. A screen under an open dialog cannot.</summary>
        public bool IsShowing(PauseScreen screen)
        {
            VisualElement element = screen == PauseScreen.Menu ? menuScreen
                : screen == PauseScreen.Settings ? settingsScreen
                : null;
            return element != null && IsOpen && !element.ClassListContains(HiddenClass);
        }

        // ------------------------------------------------------------------ player actions

        /// <summary>
        /// What one press of Escape does: opens the pause menu, or takes one step back out of it. Returns
        /// the step taken. <paramref name="usedByControl"/> is true when a control such as an open
        /// dropdown list has already used the press.
        /// </summary>
        public EscapeStep PressEscape(bool usedByControl = false)
        {
            if (root == null) return EscapeStep.None;
            if (!flow.IsOpen && (game == null || !game.CanPause)) return EscapeStep.None;

            EscapeStep step = flow.Escape(usedByControl, DialogOpen);
            switch (step)
            {
                case EscapeStep.Open:
                    game.SetPaused(true);
                    SetKeyboardMode(true);
                    Refresh();
                    FocusSoon(ButtonOf(PauseFlow.InitialFocus));
                    break;
                case EscapeStep.Resume:
                    sounds.Back();
                    Close();
                    break;
                case EscapeStep.LeaveSettings:
                    sounds.Back();
                    Refresh();
                    FocusSoon(ButtonOf(PauseAction.Settings));
                    break;
                case EscapeStep.CancelDialog:
                    dialog.Cancel(true);
                    break;
            }

            return step;
        }

        /// <summary>
        /// What activating one of the root menu's buttons does. Returns false, and does nothing, unless
        /// the root menu is the screen in use.
        /// </summary>
        public bool Activate(PauseAction action)
        {
            if (root == null || flow.Leaving || DialogOpen || flow.Screen != PauseScreen.Menu) return false;

            switch (action)
            {
                case PauseAction.Resume:
                    if (!flow.Resume()) return false;
                    sounds.Select();
                    Close();
                    return true;
                case PauseAction.Settings:
                    if (!flow.OpenSettings()) return false;
                    sounds.Select();
                    settings.Open();
                    Refresh();
                    FocusSoon(settings.DefaultFocus);
                    return true;
                case PauseAction.QuitToMenu:
                    sounds.Select();
                    AskToQuit(QuitDestination.Menu);
                    return true;
                default:
                    sounds.Select();
                    AskToQuit(QuitDestination.Desktop);
                    return true;
            }
        }

        /// <summary>
        /// What the Settings screen's Back button does: returns to the root pause menu. The mission
        /// stays paused. With a dialog open it cancels the dialog instead, like Escape.
        /// </summary>
        public void Back()
        {
            if (root == null || flow.Leaving) return;

            if (DialogOpen)
            {
                dialog.Cancel(true);
                return;
            }

            if (!flow.LeaveSettings()) return;

            sounds.Back();
            Refresh();
            FocusSoon(ButtonOf(PauseAction.Settings));
        }

        /// <summary>What a dialog's Confirm button does.</summary>
        public void ConfirmDialog()
        {
            if (DialogOpen) dialog.Confirm();
        }

        /// <summary>What a dialog's Cancel button does.</summary>
        public void CancelDialog()
        {
            if (DialogOpen) dialog.Cancel(true);
        }

        /// <summary>Shows one of the Settings categories: 0 General, 1 Graphics, 2 Sound.</summary>
        public void ShowSettingsTab(int index)
        {
            if (flow.Screen == PauseScreen.Settings && !DialogOpen && !flow.Leaving) settings.ShowTab(index);
        }

        // ------------------------------------------------------------------ dialog and quitting

        /// <summary>
        /// Opens the confirmation dialog in place of the current pause screen: the screen is put away
        /// until the dialog closes, so only the dialog can be used and nothing shows through it. Cancel
        /// has the focus.
        /// </summary>
        public void ShowDialog(string title, Func<int, string> message, Action onConfirm, Action onCancel,
            float timeout = 0f)
        {
            if (root == null || !flow.IsOpen || flow.Leaving) return;

            dialog.Show(title, message, onConfirm, onCancel, timeout);
        }

        void AskToQuit(QuitDestination destination)
        {
            QuitPrompt prompt = QuitPrompt.For(destination, game.Standing);
            ShowDialog(prompt.Title, _ => prompt.Message, () => Quit(destination), null);
        }

        // Runs once per mission at most: the flow refuses a second confirmation.
        void Quit(QuitDestination destination)
        {
            if (!flow.ConfirmQuit(destination)) return;

            Refresh();
            if (destination == QuitDestination.Desktop)
            {
                game.QuitToDesktop();
                return;
            }

            // Fade out under the selection cue; the cover also keeps the pointer off the menu.
            fade.pickingMode = PickingMode.Position;
            fade.AddToClassList(FadeClass);
            root.schedule.Execute(game.BackToMenu).StartingIn(LeaveFadeMilliseconds);
        }

        void OnDialogOpened()
        {
            Refresh();
            FocusSoon(dialog.CancelButton);
        }

        void OnDialogClosed(Focusable returnTo)
        {
            Refresh();
            if (!flow.IsOpen || flow.Leaving) return;

            // Back to whatever opened the dialog, unless that control is gone (a closed dropdown list).
            VisualElement screen = flow.Screen == PauseScreen.Settings ? settingsScreen : menuScreen;
            var opener = returnTo as VisualElement;
            if (opener != null && opener.panel != null && screen.Contains(opener)) FocusSoon(opener);
            else if (flow.Screen == PauseScreen.Settings) FocusSoon(settings.DefaultFocus);
            else FocusSoon(ButtonOf(PauseFlow.InitialFocus));
        }

        // ------------------------------------------------------------------ screens

        void Close()
        {
            Refresh();
            game.SetPaused(false);
        }

        // Shows what the flow says is in front. A dialog replaces the screen it was opened from.
        void Refresh()
        {
            bool open = flow.IsOpen;
            bool screens = open && !flow.Leaving && !DialogOpen;
            root.EnableInClassList(HiddenClass, !open);
            ShowScreen(menuScreen, screens && flow.Screen == PauseScreen.Menu);
            ShowScreen(settingsScreen, screens && flow.Screen == PauseScreen.Settings);
        }

        static void ShowScreen(VisualElement screen, bool show)
        {
            if (show != screen.ClassListContains(HiddenClass)) return; // already as asked

            screen.EnableInClassList(HiddenClass, !show);
            screen.RemoveFromClassList(ActiveScreenClass);
            if (!show) return;

            // The fade-in starts a frame later, once the screen has been laid out transparent.
            screen.schedule.Execute(() =>
            {
                if (!screen.ClassListContains(HiddenClass)) screen.AddToClassList(ActiveScreenClass);
            }).StartingIn(20);
        }

        Button ButtonOf(PauseAction action)
        {
            return buttons[(int)action];
        }

        // Focus only takes once the element is displayed, which is a frame after its class changes.
        // By then the target may have been disabled or hidden again; asking such an element to take the
        // focus would clear it altogether, so it is left where it is.
        void FocusSoon(Focusable target)
        {
            if (target == null) return;
            root.schedule.Execute(() =>
            {
                if (target.canGrabFocus) target.Focus();
            }).StartingIn(20);
        }

        // ------------------------------------------------------------------ input in use

        // As in the main menu: the style sheet highlights hovered controls in pointer mode and the
        // focused one in keyboard mode.
        void SetKeyboardMode(bool keys)
        {
            keyboardMode = keys;
            sounds.PointerActive = !keys;
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

        // ------------------------------------------------------------------ lifecycle

        void Start()
        {
            VisualElement tree = document != null ? document.rootVisualElement : null;
            VisualElement pauseRoot = tree?.Q("pauseRoot");
            if (pauseRoot == null || sounds == null || game == null)
            {
                Debug.LogError("Squadra: the pause menu needs its document, its sounds and the game controller.", this);
                return;
            }

            // This document lies over the whole panel, on top of the results document, without taking
            // any room in the panel's layout.
            tree.pickingMode = PickingMode.Ignore;
            tree.style.position = Position.Absolute;
            tree.style.left = 0f;
            tree.style.top = 0f;
            tree.style.right = 0f;
            tree.style.bottom = 0f;

            root = pauseRoot;
            menuScreen = root.Q("pauseScreen");
            settingsScreen = root.Q("settingsScreen");
            fade = root.Q("pauseFade");
            heading = root.Q<Label>("pauseHeading");

            buttons = new Button[ButtonNames.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                var action = (PauseAction)i;
                buttons[i] = root.Q<Button>(ButtonNames[i]);
                buttons[i].clicked += () => Activate(action);
            }

            root.Q<Button>("settingsBackButton").clicked += Back;
            dialog = new MenuDialog(root.Q("dialog"), sounds, OnDialogOpened, OnDialogClosed);
            settings = new SettingsScreen(settingsScreen, this, sounds);
            root.Query<Button>().ForEach(sounds.AttachHover);

            SetKeyboardMode(false);
            root.RegisterCallback<PointerMoveEvent>(OnPointerMoved, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerDownEvent>(_ => SetKeyboardMode(false), TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationMoveEvent>(_ => SetKeyboardMode(true), TrickleDown.TrickleDown);
            Refresh();
        }

        void Update()
        {
            if (root == null) return;

            // Escape also closes an open dropdown list; that press must not leave the screen as well.
            bool popupOpen = root.panel != null && root.panel.visualTree.Q(className: PopupClass) != null;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (flow.IsOpen && !keyboardMode && keyboard.anyKey.wasPressedThisFrame) SetKeyboardMode(true);
                if (keyboard.escapeKey.wasPressedThisFrame) PressEscape(popupOpen || popupWasOpen);
            }

            popupWasOpen = popupOpen;
            dialog.Tick();
        }
    }
}
