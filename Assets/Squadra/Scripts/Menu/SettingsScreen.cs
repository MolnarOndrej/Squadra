using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Squadra
{
    /// <summary>
    /// The Settings screen: General (language), Graphics (display mode, resolution, VSync) and Sound
    /// (master, music and effects volume). Every control applies its change at once and saves it when the
    /// player finishes with the control. A display change is only previewed until the player confirms it.
    /// </summary>
    sealed class SettingsScreen
    {
        const string SelectedTabClass = "tab--selected";
        const float DisplayConfirmSeconds = 12f;

        readonly MainMenu menu;
        readonly MenuAudio sounds;
        readonly Button[] tabs;
        readonly VisualElement[] panels;
        readonly DropdownField displayModeField;
        readonly DropdownField resolutionField;
        readonly Toggle vsyncToggle;
        readonly Slider masterSlider;
        readonly Slider musicSlider;
        readonly Slider sfxSlider;

        List<DisplayMode> modes = new List<DisplayMode>();
        List<Vector2Int> sizes = new List<Vector2Int>();
        int tab;

        public SettingsScreen(VisualElement screen, MainMenu menu, MenuAudio sounds)
        {
            this.menu = menu;
            this.sounds = sounds;

            tabs = new[]
            {
                screen.Q<Button>("generalTab"), screen.Q<Button>("graphicsTab"), screen.Q<Button>("soundTab"),
            };
            panels = new[] { screen.Q("generalPanel"), screen.Q("graphicsPanel"), screen.Q("soundPanel") };
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                tabs[i].clicked += () =>
                {
                    sounds.Select();
                    SelectTab(index);
                };
            }

            // General: English is the only language so far.
            var language = screen.Q<DropdownField>("languageField");
            language.choices = new List<string> { "English" };
            language.index = 0;

            displayModeField = screen.Q<DropdownField>("displayModeField");
            resolutionField = screen.Q<DropdownField>("resolutionField");
            vsyncToggle = screen.Q<Toggle>("vsyncToggle");
            displayModeField.RegisterValueChangedCallback(_ => OnDisplayModePicked());
            resolutionField.RegisterValueChangedCallback(_ => OnResolutionPicked());
            vsyncToggle.RegisterValueChangedCallback(change =>
            {
                GameSettings.VSync = change.newValue;
                GameSettings.Save();
            });
            screen.Q("displayModeRow").EnableInClassList("hidden", !GameSettings.DisplaySupported);
            screen.Q("resolutionRow").EnableInClassList("hidden", !GameSettings.DisplaySupported);
            screen.Q("vsyncRow").EnableInClassList("hidden", !GameSettings.VSyncSupported);

            masterSlider = BindVolume(screen, "master", value => GameSettings.MasterVolume = value);
            musicSlider = BindVolume(screen, "music", value => GameSettings.MusicVolume = value);
            sfxSlider = BindVolume(screen, "sfx", value => GameSettings.SfxVolume = value);

            SelectTab(0);
        }

        /// <summary>The tab that should get keyboard focus when the screen opens.</summary>
        public Focusable DefaultFocus => tabs[tab];

        /// <summary>Call when the screen appears: shows the current preferences.</summary>
        public void Open()
        {
            ShowDisplay();
            vsyncToggle.SetValueWithoutNotify(GameSettings.VSync);
            ShowVolume(masterSlider, GameSettings.MasterVolume);
            ShowVolume(musicSlider, GameSettings.MusicVolume);
            ShowVolume(sfxSlider, GameSettings.SfxVolume);
        }

        void SelectTab(int index)
        {
            tab = index;
            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i].EnableInClassList(SelectedTabClass, i == index);
                panels[i].EnableInClassList("hidden", i != index);
            }
        }

        // ------------------------------------------------------------------ display

        void ShowDisplay()
        {
            modes = GameSettings.SupportedDisplayModes();
            var modeNames = new List<string>();
            foreach (DisplayMode mode in modes)
                modeNames.Add(NameOf(mode));
            displayModeField.choices = modeNames;
            displayModeField.SetValueWithoutNotify(NameOf(GameSettings.DisplayMode));

            sizes = GameSettings.SupportedResolutions(GameSettings.DisplayMode);
            if (!sizes.Contains(GameSettings.Resolution)) sizes.Add(GameSettings.Resolution);
            var sizeNames = new List<string>();
            foreach (Vector2Int size in sizes)
                sizeNames.Add(NameOf(size));
            resolutionField.choices = sizeNames;
            resolutionField.SetValueWithoutNotify(NameOf(GameSettings.Resolution));
        }

        void OnDisplayModePicked()
        {
            int index = displayModeField.index;
            if (index < 0 || index >= modes.Count) return;
            Preview(modes[index], GameSettings.PreferredResolution(modes[index]));
        }

        void OnResolutionPicked()
        {
            int index = resolutionField.index;
            if (index < 0 || index >= sizes.Count) return;
            Preview(GameSettings.DisplayMode, sizes[index]);
        }

        // Switches the display, then asks the player to keep it. Without a confirmation in time the
        // previous display comes back, so a mode the monitor cannot show never traps the player.
        void Preview(DisplayMode mode, Vector2Int size)
        {
            DisplayMode previousMode = GameSettings.DisplayMode;
            Vector2Int previousSize = GameSettings.Resolution;
            if (mode == previousMode && size == previousSize) return;

            GameSettings.ApplyDisplay(mode, size);
            ShowDisplay();
            menu.ShowDialog("KEEP THIS DISPLAY?",
                seconds => NameOf(mode) + ", " + NameOf(size) + "\nReverting in " + seconds
                    + (seconds == 1 ? " second" : " seconds"),
                GameSettings.SaveDisplay,
                () =>
                {
                    GameSettings.ApplyDisplay(previousMode, previousSize);
                    ShowDisplay();
                },
                DisplayConfirmSeconds);
        }

        static string NameOf(DisplayMode mode)
        {
            switch (mode)
            {
                case DisplayMode.BorderlessFullscreen: return "Borderless Fullscreen";
                case DisplayMode.ExclusiveFullscreen: return "Exclusive Fullscreen";
                default: return "Windowed";
            }
        }

        static string NameOf(Vector2Int size)
        {
            return size.x + " x " + size.y;
        }

        // ------------------------------------------------------------------ volume

        // A drag is heard while it moves and saved once, on release. Keyboard steps save as they happen.
        Slider BindVolume(VisualElement screen, string name, System.Action<float> apply)
        {
            var slider = screen.Q<Slider>(name + "Slider");
            var readout = screen.Q<Label>(name + "Value");
            bool dragging = false;

            slider.RegisterValueChangedCallback(change =>
            {
                apply(change.newValue / 100f);
                readout.text = Mathf.RoundToInt(change.newValue) + "%";
                if (!dragging) GameSettings.Save();
            });
            slider.RegisterCallback<PointerDownEvent>(_ => dragging = true, TrickleDown.TrickleDown);
            slider.RegisterCallback<PointerCaptureOutEvent>(_ => Release(), TrickleDown.TrickleDown);
            slider.RegisterCallback<PointerUpEvent>(_ => Release(), TrickleDown.TrickleDown);
            slider.userData = readout;
            return slider;

            void Release()
            {
                if (!dragging) return;

                dragging = false;
                GameSettings.Save();
            }
        }

        static void ShowVolume(Slider slider, float volume)
        {
            slider.SetValueWithoutNotify(volume * 100f);
            ((Label)slider.userData).text = Mathf.RoundToInt(volume * 100f) + "%";
        }
    }
}
