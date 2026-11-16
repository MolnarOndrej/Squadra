using System;
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
    public sealed class MainMenu : MonoBehaviour, IDialogHost
    {
        /// <summary>Name of the menu scene in the build.</summary>
        public const string SceneName = "MainMenu";

        const string HiddenClass = "hidden";
        const string ActiveScreenClass = "screen--active";
        const string FadeClass = "fade--on";
        const string PopupClass = "unity-base-dropdown";
        const long LaunchFadeMilliseconds = 350;

        [SerializeField] UIDocument document;
        [SerializeField] MissionMap map;
        [SerializeField] MenuAudio sounds;

        VisualElement root;
        VisualElement menuRoot;
        VisualElement fade;
        VisualElement[] screens;
        Button singlePlayerButton;
        Button settingsButton;
        Button quitButton;
        Button mapBackButton;
        MenuDialog dialog;
        SettingsScreen settings;

        MenuScreen screen;
        bool loading;
        bool popupWasOpen;
        MenuInputMode inputMode;

        public MenuScreen CurrentScreen => screen;

        /// <summary>
        /// True while the keyboard is the input in use, false while the mouse is. The focused control is
        /// highlighted either way; the mouse moves the focus to the control it points at.
        /// </summary>
        public bool KeyboardMode => inputMode != null && inputMode.Keyboard;
        public bool DialogOpen => dialog != null && dialog.IsOpen;

        /// <summary>True from the moment a mission is launched until its scene replaces the menu.</summary>
        public bool IsLoading => loading;

        /// <summary>
        /// Loads the menu straight onto the mission map. This is the entry point for other scenes, such
        /// as the results screen's Back to Menu button.
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
            ShowDialog("QUIT SQUADRA?", _ => "Campaign progress is kept only while the game is running.",
                QuitApplication, null);
        }

        /// <summary>
        /// What Escape, a Back button and a dialog's Cancel do: dismiss the dialog if one is open,
        /// otherwise return to the main menu. Plays the back cue when it has an effect.
        /// </summary>
        public void Back()
        {
            if (loading) return;

            if (DialogOpen)
            {
                dialog.Cancel(true);
                return;
            }

            if (screen == MenuScreen.Main) return;

            sounds.Back();
            Show(MenuScreen.Main);
        }

        /// <summary>What a dialog's Confirm button does.</summary>
        public void ConfirmDialog()
        {
            if (DialogOpen) dialog.Confirm();
        }

        /// <summary>
        /// Launches a mission by its place in the catalog, if the campaign allows it. Further calls are
        /// ignored while the scene loads.
        /// </summary>
        public void Launch(int missionIndex)
        {
            if (loading || DialogOpen) return;
            if (!CampaignSession.TryBeginMission(map.Catalog, missionIndex, out string scenePath)) return;

            loading = true;
            sounds.Select();

            // Fade out under the selection cue; the cover also keeps the pointer off the menu.
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
        public void ShowDialog(string title, Func<int, string> message, Action onConfirm, Action onCancel,
            float timeout = 0f)
        {
            dialog.Show(title, message, onConfirm, onCancel, timeout);
        }

        void OnDialogOpened()
        {
            screens[(int)screen].SetEnabled(false);
            MenuFocus.Soon(root, dialog.CancelButton);
        }

        void OnDialogClosed(Focusable returnTo)
        {
            screens[(int)screen].SetEnabled(true);

            // Back to whatever opened the dialog, unless that control is gone (a closed dropdown list).
            var opener = returnTo as VisualElement;
            MenuFocus.Soon(root, opener != null && opener.panel != null && screens[(int)screen].Contains(opener)
                ? opener
                : DefaultFocus(screen));
        }

        /// <summary>Closes the game. In the editor it stops play mode instead.</summary>
        public static void QuitApplication()
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
            MenuFocus.Soon(root, focus);
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

        // ------------------------------------------------------------------ lifecycle

        void Awake()
        {
            Time.timeScale = 1f;
            CampaignSession.EndMission();

            // Every assigned mission is prepared before the menu takes input, so the map knows which can
            // be played. Missions already prepared this session are kept as they are.
            MissionLibrary.Prepare(map.Catalog);
        }

        void Start()
        {
            root = document.rootVisualElement;
            menuRoot = root.Q("menuRoot");
            fade = root.Q("fade");
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
            dialog = new MenuDialog(root.Q("dialog"), sounds, OnDialogOpened, OnDialogClosed);

            // The map adds the hover cue to its own dots as it builds them.
            root.Query<Button>().ForEach(sounds.AttachHover);
            map.Bind(screens[(int)MenuScreen.Map], sounds, Launch);
            settings = new SettingsScreen(screens[(int)MenuScreen.Settings], this, sounds);

            inputMode = new MenuInputMode(sounds);
            inputMode.Attach(menuRoot);
            inputMode.Set(false);
            root.RegisterCallback<PointerMoveEvent>(inputMode.OnPointerMoved, TrickleDown.TrickleDown);
            foreach (VisualElement field in settings.Fields)
                inputMode.FollowPointer(field);
            root.RegisterCallback<PointerDownEvent>(inputMode.OnPointerPressed, TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationMoveEvent>(_ => inputMode.Set(true), TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationMoveEvent>(inputMode.OnNavigated);

            Show(CampaignSession.ConsumeMapRequest() ? MenuScreen.Map : MenuScreen.Main);
            root.schedule.Execute(() => fade.RemoveFromClassList(FadeClass)).StartingIn(50);
        }

        void Update()
        {
            if (root == null) return;

            // Escape also closes an open dropdown list; that press must not leave the screen as well.
            bool popupOpen = root.panel != null && root.panel.visualTree.Q(className: PopupClass) != null;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame && !inputMode.Keyboard) inputMode.Set(true);
            if (keyboard != null && !loading)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    if (!popupOpen && !popupWasOpen) Back();
                }
                else if (screen == MenuScreen.Map && !DialogOpen)
                {
                    if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
                        map.PreviousPage();
                    else if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
                        map.NextPage();
                }
            }

            popupWasOpen = popupOpen;
            dialog.Tick();
        }
    }
}
