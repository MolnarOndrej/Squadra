using System;
using Squadra.Campaign;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Squadra
{
    /// <summary>
    /// Runs the menu scene: the main menu, the mission map, Settings and the confirmation dialog over
    /// them. It moves between screens, keeps keyboard focus on something sensible, turns Escape and the
    /// page keys into the same actions as the on-screen buttons, and loads a mission when the map
    /// launches one.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        /// <summary>Name of the menu scene in the build.</summary>
        public const string SceneName = "MainMenu";

        const string HiddenClass = "hidden";
        const string ActiveScreenClass = "screen--active";
        const string FadeClass = "fade--on";
        const string PointerModeClass = "menu-root--pointer";
        const string KeyboardModeClass = "menu-root--keys";
        const string PopupClass = "unity-base-dropdown";
        const long LaunchFadeMilliseconds = 350;

        public enum MenuScreen
        {
            Main,
            Map,
            Settings,
        }

        [SerializeField] UIDocument document;
        [SerializeField] MissionMap map;
        [SerializeField] MenuAudio sounds;

        VisualElement root;
        VisualElement menuRoot;
        VisualElement fade;
        VisualElement dialog;
        VisualElement[] screens;
        Button singlePlayerButton;
        Button settingsButton;
        Button quitButton;
        Button mapBackButton;
        Button dialogCancelButton;
        Label dialogTitle;
        Label dialogMessage;
        SettingsScreen settings;

        MenuScreen screen;
        bool loading;
        bool popupWasOpen;
        bool keyboardMode;
        Vector3 lastPointer = new Vector3(float.NaN, float.NaN);

        bool dialogOpen;
        Action dialogConfirmed;
        Action dialogCancelled;
        Func<int, string> dialogText;
        float dialogDeadline;
        int dialogSeconds;
        Focusable dialogReturnFocus;

        public MenuScreen CurrentScreen => screen;

        /// <summary>
        /// True while the keyboard is the input in use: the focused control is highlighted. False while
        /// the mouse is: only the control under the pointer is, and it stops when the pointer leaves.
        /// </summary>
        public bool KeyboardMode => keyboardMode;
        public bool DialogOpen => dialogOpen;

        /// <summary>True from the moment a mission is launched until its scene replaces the menu.</summary>
        public bool IsLoading => loading;

        /// <summary>
        /// Loads the menu straight onto the mission map. This is the entry point for other scenes, such
        /// as a results screen's Continue button.
        /// </summary>
        public static void OpenCampaignMap()
        {
            CampaignSession.RequestMap();
            SceneManager.LoadScene(SceneName);
        }

        // ------------------------------------------------------------------ player actions

        public void OpenMap() => Show(MenuScreen.Map);

        public void OpenSettings() => Show(MenuScreen.Settings);

        public void AskToQuit()
        {
            ShowDialog("QUIT SQUADRA?", _ => "Campaign progress is kept only while the game is running.", Quit, null);
        }

        /// <summary>
        /// What Escape, a Back button and a dialog's Cancel do: dismiss the dialog if one is open,
        /// otherwise return to the main menu. Plays the back cue when it has an effect.
        /// </summary>
        public void Back()
        {
            if (loading) return;

            if (dialogOpen)
            {
                CancelDialog(true);
                return;
            }

            if (screen == MenuScreen.Main) return;

            sounds.Back();
            Show(MenuScreen.Main);
        }

        /// <summary>What a dialog's Confirm button does.</summary>
        public void ConfirmDialog()
        {
            if (!dialogOpen) return;

            Action confirmed = dialogConfirmed;
            sounds.Select();
            CloseDialog();
            confirmed?.Invoke();
        }

        /// <summary>
        /// Launches a mission by its place in the catalog, if the campaign allows it. Further calls are
        /// ignored while the scene loads.
        /// </summary>
        public void Launch(int missionIndex)
        {
            MissionCatalog catalog = map.Catalog;
            if (loading || dialogOpen || catalog == null || missionIndex < 0 || missionIndex >= catalog.Count) return;
            if (!CampaignRules.CanLaunch(catalog.ToSlots(), CampaignSession.Progress, missionIndex)) return;

            loading = true;
            sounds.Select();
            CampaignSession.BeginMission(catalog.IdAt(missionIndex));

            // Fade out under the selection cue; the cover also keeps the pointer off the menu.
            string scenePath = catalog.ScenePathAt(missionIndex);
            fade.pickingMode = PickingMode.Position;
            fade.AddToClassList(FadeClass);
            root.schedule.Execute(() => SceneManager.LoadSceneAsync(scenePath)).StartingIn(LaunchFadeMilliseconds);
        }

        // ------------------------------------------------------------------ dialog

        /// <summary>
        /// Opens the confirmation dialog over the current screen. Only the dialog can be used until it
        /// closes, and Cancel has the focus. <paramref name="message"/> gets the seconds left when
        /// <paramref name="timeout"/> is set; running out of time cancels.
        /// </summary>
        internal void ShowDialog(string title, Func<int, string> message, Action onConfirm, Action onCancel,
            float timeout = 0f)
        {
            if (dialogOpen) CancelDialog(false);

            dialogOpen = true;
            dialogConfirmed = onConfirm;
            dialogCancelled = onCancel;
            dialogText = message;
            dialogDeadline = timeout > 0f ? Time.unscaledTime + timeout : 0f;
            dialogSeconds = Mathf.CeilToInt(timeout);
            dialogReturnFocus = root.focusController?.focusedElement;

            dialogTitle.text = title;
            dialogMessage.text = message(dialogSeconds);
            screens[(int)screen].SetEnabled(false);
            dialog.RemoveFromClassList(HiddenClass);
            FocusSoon(dialogCancelButton);
        }

        void CancelDialog(bool playCue)
        {
            if (!dialogOpen) return;

            Action cancelled = dialogCancelled;
            if (playCue) sounds.Back();
            CloseDialog();
            cancelled?.Invoke();
        }

        void CloseDialog()
        {
            dialogOpen = false;
            dialogConfirmed = null;
            dialogCancelled = null;
            dialog.AddToClassList(HiddenClass);
            screens[(int)screen].SetEnabled(true);

            // Back to whatever opened the dialog, unless that control is gone (a closed dropdown list).
            var opener = dialogReturnFocus as VisualElement;
            FocusSoon(opener != null && opener.panel != null && screens[(int)screen].Contains(opener)
                ? opener
                : DefaultFocus(screen));
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ screens

        void Show(MenuScreen target)
        {
            MenuScreen previous = screen;
            if (previous == MenuScreen.Map && target != MenuScreen.Map) map.Close();

            screen = target;
            for (int i = 0; i < screens.Length; i++)
            {
                screens[i].RemoveFromClassList(ActiveScreenClass);
                screens[i].EnableInClassList(HiddenClass, i != (int)target);
            }

            if (target == MenuScreen.Map) map.Open();
            if (target == MenuScreen.Settings) settings.Open();

            // The fade-in starts a frame later, once the screen has been laid out transparent.
            VisualElement shown = screens[(int)target];
            shown.schedule.Execute(() =>
            {
                if (screen == target) shown.AddToClassList(ActiveScreenClass);
            }).StartingIn(20);

            // Returning to the main menu puts the focus back on the button that was used to leave it.
            Focusable focus = target != MenuScreen.Main ? DefaultFocus(target)
                : previous == MenuScreen.Settings ? settingsButton
                : singlePlayerButton;
            FocusSoon(focus);
        }

        Focusable DefaultFocus(MenuScreen target)
        {
            switch (target)
            {
                case MenuScreen.Map: return map.DefaultFocus() ?? mapBackButton;
                case MenuScreen.Settings: return settings.DefaultFocus;
                default: return singlePlayerButton;
            }
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

        // The style sheet highlights hovered controls in pointer mode and the focused one in keyboard mode.
        void SetKeyboardMode(bool keys)
        {
            keyboardMode = keys;
            sounds.PointerActive = !keys;
            menuRoot.EnableInClassList(KeyboardModeClass, keys);
            menuRoot.EnableInClassList(PointerModeClass, !keys);
        }

        // The panel also reports the pointer when the layout changes under it; only real movement counts.
        void OnPointerMoved(PointerMoveEvent moved)
        {
            if (moved.position == lastPointer) return;

            lastPointer = moved.position;
            if (keyboardMode) SetKeyboardMode(false);
        }

        // ------------------------------------------------------------------ lifecycle

        void Awake()
        {
            Time.timeScale = 1f;
            CampaignSession.EndMission();
        }

        void Start()
        {
            root = document.rootVisualElement;
            menuRoot = root.Q("menuRoot");
            fade = root.Q("fade");
            dialog = root.Q("dialog");
            dialogTitle = root.Q<Label>("dialogTitle");
            dialogMessage = root.Q<Label>("dialogMessage");
            dialogCancelButton = root.Q<Button>("dialogCancelButton");
            screens = new[] { root.Q("mainScreen"), root.Q("mapScreen"), root.Q("settingsScreen") };

            singlePlayerButton = root.Q<Button>("singlePlayerButton");
            settingsButton = root.Q<Button>("settingsButton");
            quitButton = root.Q<Button>("quitButton");
            mapBackButton = root.Q<Button>("mapBackButton");
            root.Q<Button>("multiPlayerButton").SetEnabled(false);

            singlePlayerButton.clicked += () =>
            {
                sounds.Select();
                OpenMap();
            };
            settingsButton.clicked += () =>
            {
                sounds.Select();
                OpenSettings();
            };
            quitButton.clicked += () =>
            {
                sounds.Select();
                AskToQuit();
            };
            mapBackButton.clicked += Back;
            root.Q<Button>("settingsBackButton").clicked += Back;
            dialogCancelButton.clicked += Back;
            root.Q<Button>("dialogConfirmButton").clicked += ConfirmDialog;

            // The map adds the hover cue to its own dots as it builds them.
            root.Query<Button>().ForEach(sounds.AttachHover);
            map.Bind(screens[(int)MenuScreen.Map], sounds, Launch);
            settings = new SettingsScreen(screens[(int)MenuScreen.Settings], this, sounds);

            SetKeyboardMode(false);
            root.RegisterCallback<PointerMoveEvent>(OnPointerMoved, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerDownEvent>(_ => SetKeyboardMode(false), TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationMoveEvent>(_ => SetKeyboardMode(true), TrickleDown.TrickleDown);

            Show(CampaignSession.ConsumeMapRequest() ? MenuScreen.Map : MenuScreen.Main);
            root.schedule.Execute(() => fade.RemoveFromClassList(FadeClass)).StartingIn(50);
        }

        void Update()
        {
            if (root == null) return;

            // Escape also closes an open dropdown list; that press must not leave the screen as well.
            bool popupOpen = root.panel != null && root.panel.visualTree.Q(className: PopupClass) != null;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame && !keyboardMode) SetKeyboardMode(true);
            if (keyboard != null && !loading)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    if (!popupOpen && !popupWasOpen) Back();
                }
                else if (screen == MenuScreen.Map && !dialogOpen)
                {
                    if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
                        map.PreviousPage();
                    else if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
                        map.NextPage();
                }
            }

            popupWasOpen = popupOpen;

            if (dialogOpen && dialogDeadline > 0f)
            {
                int seconds = Mathf.CeilToInt(dialogDeadline - Time.unscaledTime);
                if (seconds <= 0)
                {
                    CancelDialog(false);
                }
                else if (seconds != dialogSeconds)
                {
                    dialogSeconds = seconds;
                    dialogMessage.text = dialogText(seconds);
                }
            }
        }
    }
}
