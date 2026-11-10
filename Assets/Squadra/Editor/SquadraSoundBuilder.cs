using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Squadra.EditorTools
{
    /// <summary>
    /// Renders the sound effects described in <see cref="SoundRecipes"/> to WAV files and sets the import
    /// settings for them and for the music. Run it from <c>Squadra &gt; Rebuild Sound Effects</c>.
    /// It overwrites the WAV files it generates and nothing else: the ship explosions are recordings kept
    /// in the same folder as <c>ShipDestroyed_N.ogg</c>, and any take you add yourself is left alone.
    /// </summary>
    public static class SquadraSoundBuilder
    {
        public const string SfxFolder = "Assets/Squadra/Audio/Sfx";
        public const string MusicFolder = "Assets/Squadra/Audio/Music";

        /// <summary>Recordings the recipes use as raw material. Nothing in the scene plays them directly.</summary>
        public const string SourceFolder = "Assets/Squadra/Audio/Sources";

        public const string Departure = "Departure";
        public const string Arrival = "Arrival";
        public const string BattleStart = "BattleStart";
        public const string Shot = "Shot";
        public const string ShipDestroyed = "ShipDestroyed";
        public const string Capture = "Capture";
        public const string Victory = "Victory";
        public const string Defeat = "Defeat";

        public static bool HasSounds => File.Exists(ClipPath(Victory));

        [MenuItem("Squadra/Rebuild Sound Effects")]
        public static void Build()
        {
            Directory.CreateDirectory(SfxFolder);
            AssetDatabase.Refresh(); // make sure the source recordings are imported before they are read

            int written = 0;
            for (int i = 0; i < SoundRecipes.DepartureTakes; i++)
            {
                if (TryLoadSource(SoundRecipes.DepartureEngines[i], out SoundSynth.Recording engine))
                    written += Write(TakeName(Departure, i), SoundRecipes.Departure(i, engine));
            }

            for (int i = 0; i < SoundRecipes.ArrivalTakes; i++)
            {
                if (TryLoadSource(SoundRecipes.ArrivalEngines[i], out SoundSynth.Recording engine)
                    && TryLoadSource(SoundRecipes.ArrivalPulses[i], out SoundSynth.Recording pulse))
                    written += Write(TakeName(Arrival, i), SoundRecipes.Arrival(i, engine, pulse));
            }

            for (int i = 0; i < SoundRecipes.BattleStartTakes; i++)
                written += Write(TakeName(BattleStart, i), SoundRecipes.BattleStart(i));
            for (int i = 0; i < SoundRecipes.ShotTakes; i++)
                written += Write(TakeName(Shot, i), SoundRecipes.Shot(i));
            written += Write(TakeName(Capture, 0), SoundRecipes.Capture()); // a single take by design
            written += Write(Victory, SoundRecipes.Victory());
            written += Write(Defeat, SoundRecipes.Defeat());

            AssetDatabase.Refresh();

            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { SfxFolder }))
                Configure(AssetDatabase.GUIDToAssetPath(guid), AudioClipLoadType.DecompressOnLoad, 0.8f);
            if (AssetDatabase.IsValidFolder(MusicFolder))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { MusicFolder }))
                    Configure(AssetDatabase.GUIDToAssetPath(guid), AudioClipLoadType.Streaming, 0.6f);
            }

            Debug.Log("Squadra: rebuilt sound effects in " + SfxFolder + " (" + written + " files changed)");
        }

        /// <summary>Every take of one sound, in name order: <c>Shot_1</c>, <c>Shot_2</c>, ...</summary>
        public static AudioClip[] LoadTakes(string sound)
        {
            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets(sound + "_ t:AudioClip", new[] { SfxFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path).StartsWith(sound + "_")) paths.Add(path);
            }

            paths.Sort(System.StringComparer.Ordinal);
            var clips = new AudioClip[paths.Count];
            for (int i = 0; i < clips.Length; i++)
                clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(paths[i]);
            return clips;
        }

        public static AudioClip LoadClip(string sound)
        {
            return AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath(sound));
        }

        /// <summary>The first track in the music folder, or null when there is none.</summary>
        public static AudioClip LoadMusic()
        {
            if (!AssetDatabase.IsValidFolder(MusicFolder)) return null;

            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { MusicFolder });
            if (guids.Length == 0) return null;

            var paths = new List<string>();
            foreach (string guid in guids)
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(System.StringComparer.Ordinal);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(paths[0]);
        }

        // Decodes a source recording to mono samples. Logs and returns false when it cannot be read.
        static bool TryLoadSource(string name, out SoundSynth.Recording recording)
        {
            recording = default;
            string path = SourceFolder + "/" + name + ".ogg";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                Debug.LogError("Squadra: missing source recording " + path);
                return false;
            }

            if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
            var interleaved = new float[clip.samples * clip.channels];
            if (!clip.GetData(interleaved, 0))
            {
                Debug.LogError("Squadra: could not read " + path + ". Its Load Type must be Decompress On Load.");
                return false;
            }

            var mono = new float[clip.samples];
            for (int i = 0; i < mono.Length; i++)
            {
                float sum = 0f;
                for (int c = 0; c < clip.channels; c++)
                    sum += interleaved[i * clip.channels + c];
                mono[i] = sum / clip.channels;
            }

            recording = new SoundSynth.Recording(mono, clip.frequency);
            return true;
        }

        static string TakeName(string sound, int index)
        {
            return sound + "_" + (index + 1);
        }

        static string ClipPath(string name)
        {
            return SfxFolder + "/" + name + ".wav";
        }

        // Returns 1 when the file changed. Unchanged sounds are left alone so they are not re-imported.
        static int Write(string name, float[][] channels)
        {
            string path = ClipPath(name);
            byte[] wav = SoundSynth.EncodeWav(channels);
            if (File.Exists(path) && Same(File.ReadAllBytes(path), wav)) return 0;

            try
            {
                File.WriteAllBytes(path, wav);
                return 1;
            }
            catch (IOException exception)
            {
                // Seen when Unity is still importing the previous version of the file.
                Debug.LogWarning("Squadra: could not write " + path + " (" + exception.Message
                    + "). It may be in use; run Rebuild Sound Effects again.");
                return 0;
            }
        }

        static bool Same(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }

            return true;
        }

        static void Configure(string path, AudioClipLoadType loadType, float quality)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) return;

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            bool streaming = loadType == AudioClipLoadType.Streaming;
            if (settings.loadType == loadType && settings.compressionFormat == AudioCompressionFormat.Vorbis
                && Mathf.Approximately(settings.quality, quality) && settings.preloadAudioData == !streaming
                && importer.loadInBackground == streaming && !importer.forceToMono)
                return;

            settings.loadType = loadType;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = quality;
            settings.preloadAudioData = !streaming;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = streaming;
            importer.forceToMono = false;
            importer.SaveAndReimport();
        }
    }
}
