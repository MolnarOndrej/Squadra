using System.Collections.Generic;
using Squadra.Pause;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Squadra
{
    /// <summary>How the game uses the display. Which modes exist depends on the platform.</summary>
    public enum DisplayMode
    {
        Windowed,
        BorderlessFullscreen,
        ExclusiveFullscreen,
    }

    /// <summary>
    /// The player's preferences: audio volumes, Mute in Background, VSync and the display. Setting a value
    /// applies it at once; <see cref="Save"/> writes everything to the local player preferences, and the
    /// saved values are applied again at every launch before the first scene loads. Volumes default to 1,
    /// which is the mix the scenes were balanced at. The game keeps running while another application is
    /// in front; Mute in Background only decides whether it can be heard then.
    /// </summary>
    [AutoStaticsCleanup]
    public static partial class GameSettings
    {
        const string MasterKey = "settings.volume.master";
        const string MusicKey = "settings.volume.music";
        const string SfxKey = "settings.volume.sfx";
        const string MuteInBackgroundKey = "settings.audio.muteInBackground";
        const string VSyncKey = "settings.vsync";
        const string DisplayModeKey = "settings.display.mode";
        const string DisplayWidthKey = "settings.display.width";
        const string DisplayHeightKey = "settings.display.height";

        // Below this the menu and the map stop being readable.
        const int MinWidth = 1024;
        const int MinHeight = 576;

        static bool loaded;
        static float master;
        static float music;
        static float sfx;
        static bool muteInBackground;
        static bool focused = true;
        static DisplayMode displayMode;
        static Vector2Int resolution;
        static bool displayChosen;

        // ------------------------------------------------------------------ audio

        /// <summary>Scales everything the player hears, 0 to 1.</summary>
        public static float MasterVolume
        {
            get
            {
                EnsureLoaded();
                return master;
            }
            set
            {
                EnsureLoaded();
                master = Mathf.Clamp01(value);
                ApplyOutput();
            }
        }

        /// <summary>Scales the menu and mission music, 0 to 1. The music players read it every frame.</summary>
        public static float MusicVolume
        {
            get
            {
                EnsureLoaded();
                return music;
            }
            set
            {
                EnsureLoaded();
                music = Mathf.Clamp01(value);
            }
        }

        /// <summary>Scales menu cues and gameplay effects, 0 to 1. Applies to sounds as they start.</summary>
        public static float SfxVolume
        {
            get
            {
                EnsureLoaded();
                return sfx;
            }
            set
            {
                EnsureLoaded();
                sfx = Mathf.Clamp01(value);
            }
        }

        /// <summary>
        /// When set, nothing is heard while another application is in front: music, effects, result cues
        /// and menu feedback alike. It only silences the output. Volumes, a pause and the music's place
        /// in its track are left alone, so everything is as it should be when the player comes back.
        /// </summary>
        public static bool MuteInBackground
        {
            get
            {
                EnsureLoaded();
                return muteInBackground;
            }
            set
            {
                EnsureLoaded();
                muteInBackground = value;
                ApplyOutput();
            }
        }

        /// <summary>
        /// Whether the game is the application in front. It follows the system: Alt-Tab and minimizing
        /// both take the focus away. Play tests set it to stand in for switching applications.
        /// </summary>
        public static bool Focused
        {
            get => focused;
            set
            {
                focused = value;
                ApplyOutput();
            }
        }

        /// <summary>The level everything is heard at right now: the master volume, or silence while muted in the background.</summary>
        public static float OutputVolume
        {
            get
            {
                EnsureLoaded();
                return AudioMix.Output(master, muteInBackground, focused);
            }
        }

        // The master volume and the background mute share the listener, so both go through here and
        // neither can overwrite the other.
        static void ApplyOutput()
        {
            AudioListener.volume = OutputVolume;
        }

        static void OnFocusChanged(bool hasFocus)
        {
            Focused = hasFocus;
        }

        // ------------------------------------------------------------------ graphics

        /// <summary>False on platforms where the game cannot turn vertical sync on or off.</summary>
        public static bool VSyncSupported => IsDesktop;

        public static bool VSync
        {
            get => QualitySettings.vSyncCount > 0;
            set => QualitySettings.vSyncCount = value ? 1 : 0;
        }

        /// <summary>False on platforms where the player cannot choose a window mode or resolution.</summary>
        public static bool DisplaySupported => IsDesktop;

        public static DisplayMode DisplayMode
        {
            get
            {
                EnsureLoaded();
                return displayMode;
            }
        }

        public static Vector2Int Resolution
        {
            get
            {
                EnsureLoaded();
                return resolution;
            }
        }

        /// <summary>The display modes this platform offers, in menu order.</summary>
        public static List<DisplayMode> SupportedDisplayModes()
        {
            var modes = new List<DisplayMode> { DisplayMode.Windowed, DisplayMode.BorderlessFullscreen };
            if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
                modes.Add(DisplayMode.ExclusiveFullscreen);
            return modes;
        }

        /// <summary>
        /// The resolutions the display supports in the given mode, smallest first. A window cannot be
        /// larger than the desktop; the fullscreen modes offer everything the display reports.
        /// </summary>
        public static List<Vector2Int> SupportedResolutions(DisplayMode mode)
        {
            Vector2Int desktop = DesktopSize;
            var sizes = new List<Vector2Int>();
            foreach (UnityEngine.Resolution candidate in Screen.resolutions)
            {
                var size = new Vector2Int(candidate.width, candidate.height);
                if (size.x < MinWidth || size.y < MinHeight) continue;
                if (mode == DisplayMode.Windowed && (size.x > desktop.x || size.y > desktop.y)) continue;
                if (!sizes.Contains(size)) sizes.Add(size); // the list repeats each size per refresh rate
            }

            if (sizes.Count == 0) sizes.Add(mode == DisplayMode.Windowed ? Resolution : desktop);
            sizes.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return sizes;
        }

        /// <summary>
        /// The resolution to use when switching to <paramref name="mode"/>: the current one when the mode
        /// supports it, otherwise the desktop's own for fullscreen or the largest window that fits.
        /// </summary>
        public static Vector2Int PreferredResolution(DisplayMode mode)
        {
            List<Vector2Int> sizes = SupportedResolutions(mode);
            if (sizes.Contains(Resolution)) return Resolution;
            if (mode != DisplayMode.Windowed && sizes.Contains(DesktopSize)) return DesktopSize;
            return sizes[sizes.Count - 1];
        }

        /// <summary>Switches the display now without saving, so a change can be previewed and reverted.</summary>
        public static void ApplyDisplay(DisplayMode mode, Vector2Int size)
        {
            EnsureLoaded();
            displayMode = mode;
            resolution = size;
            if (!Application.isEditor) Screen.SetResolution(size.x, size.y, ToFullScreenMode(mode));
        }

        // ------------------------------------------------------------------ persistence

        /// <summary>
        /// Writes the current preferences to disk. The display is included only once the player has
        /// confirmed one with <see cref="SaveDisplay"/>; a previewed display is never saved by accident.
        /// </summary>
        public static void Save()
        {
            EnsureLoaded();
            PlayerPrefs.SetFloat(MasterKey, master);
            PlayerPrefs.SetFloat(MusicKey, music);
            PlayerPrefs.SetFloat(SfxKey, sfx);
            PlayerPrefs.SetInt(MuteInBackgroundKey, muteInBackground ? 1 : 0);
            PlayerPrefs.SetInt(VSyncKey, VSync ? 1 : 0);
            if (displayChosen)
            {
                PlayerPrefs.SetInt(DisplayModeKey, (int)displayMode);
                PlayerPrefs.SetInt(DisplayWidthKey, resolution.x);
                PlayerPrefs.SetInt(DisplayHeightKey, resolution.y);
            }

            PlayerPrefs.Save();
        }

        /// <summary>Keeps the display that is showing now as the player's choice and saves it.</summary>
        public static void SaveDisplay()
        {
            EnsureLoaded();
            displayChosen = true;
            Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplySaved()
        {
            loaded = false;
            EnsureLoaded();

            // A mission is never paused by switching away, and a display confirmation keeps counting.
            Application.runInBackground = true;
            Application.focusChanged -= OnFocusChanged;
            Application.focusChanged += OnFocusChanged;
            focused = Application.isFocused;
            ApplyOutput();
            if (VSyncSupported && PlayerPrefs.HasKey(VSyncKey)) VSync = PlayerPrefs.GetInt(VSyncKey) != 0;

            if (DisplaySupported && !Application.isEditor && displayChosen
                && (Screen.width != resolution.x || Screen.height != resolution.y
                    || Screen.fullScreenMode != ToFullScreenMode(displayMode)))
                Screen.SetResolution(resolution.x, resolution.y, ToFullScreenMode(displayMode));
        }

        static void EnsureLoaded()
        {
            if (loaded) return;

            loaded = true;
            master = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterKey, 1f));
            music = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 1f));
            sfx = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, 1f));
            muteInBackground = PlayerPrefs.GetInt(MuteInBackgroundKey, 0) != 0;

            // Without a saved choice, describe the display as the player finds it.
            displayMode = ToDisplayMode(Screen.fullScreenMode);
            resolution = new Vector2Int(Screen.width, Screen.height);
            displayChosen = false;
            if (PlayerPrefs.HasKey(DisplayModeKey))
            {
                var saved = (DisplayMode)PlayerPrefs.GetInt(DisplayModeKey);
                if (SupportedDisplayModes().Contains(saved))
                {
                    displayChosen = true;
                    displayMode = saved;
                    resolution = new Vector2Int(PlayerPrefs.GetInt(DisplayWidthKey, resolution.x),
                        PlayerPrefs.GetInt(DisplayHeightKey, resolution.y));
                }
            }
        }

        // ------------------------------------------------------------------ helpers

        static bool IsDesktop
        {
            get
            {
                switch (Application.platform)
                {
                    case RuntimePlatform.WindowsPlayer:
                    case RuntimePlatform.WindowsEditor:
                    case RuntimePlatform.OSXPlayer:
                    case RuntimePlatform.OSXEditor:
                    case RuntimePlatform.LinuxPlayer:
                    case RuntimePlatform.LinuxEditor:
                        return true;
                    default:
                        return false;
                }
            }
        }

        static Vector2Int DesktopSize
        {
            get
            {
                Display display = Display.main;
                return new Vector2Int(display.systemWidth, display.systemHeight);
            }
        }

        static FullScreenMode ToFullScreenMode(DisplayMode mode)
        {
            switch (mode)
            {
                case DisplayMode.BorderlessFullscreen: return FullScreenMode.FullScreenWindow;
                case DisplayMode.ExclusiveFullscreen: return FullScreenMode.ExclusiveFullScreen;
                default: return FullScreenMode.Windowed;
            }
        }

        static DisplayMode ToDisplayMode(FullScreenMode mode)
        {
            switch (mode)
            {
                case FullScreenMode.Windowed: return DisplayMode.Windowed;
                case FullScreenMode.ExclusiveFullScreen:
                    return SupportedDisplayModes().Contains(DisplayMode.ExclusiveFullscreen)
                        ? DisplayMode.ExclusiveFullscreen
                        : DisplayMode.BorderlessFullscreen;
                default: return DisplayMode.BorderlessFullscreen;
            }
        }
    }
}
