using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using static Squadra.EditorTools.SceneWiring;

namespace Squadra.EditorTools
{
    /// <summary>
    /// Assembles the shared mission scene from code: backdrop, the world template, fleet and effect
    /// layers, HUD, results screen, pause menu and the game controller. The scene has no worlds of its
    /// own: every mission builds its battlefield from its mission file when it is played. Run it again
    /// from <c>Squadra &gt; Rebuild Mission Scene</c> to regenerate the scene; lasting changes to its
    /// structure belong here. How the scene looks does not: the materials, the world, route and fleet
    /// styles, the post-processing profile and the world looks are shared assets, created when missing
    /// and otherwise kept as edited in the Inspector, and the rebuilt scene points at the same ones. The
    /// layout and look of the results screen and the pause menu live in <c>Assets/Squadra/Results/UI</c>
    /// and <c>Assets/Squadra/Pause/UI</c>, and the world name dictionaries in
    /// <c>Assets/Squadra/Visuals/WorldNames</c>; those are left alone too.
    ///
    /// The shared assets come from <see cref="SquadraAssetBuilder"/>, and serialized fields are set
    /// through <see cref="SceneWiring"/>. What is here is the composition of the scene itself.
    /// </summary>
    public static class SquadraSceneBuilder
    {
        internal const string ScenePath = ProjectFolders.Root + "/Match/Scenes/Mission.unity";

        /// <summary>First Contact: Mission 1, and the mission the scene plays when opened on its own.</summary>
        internal const string FirstMissionPath = ProjectFolders.Root + "/Match/Scenes/mission-01.json";

        const string ResultsLayoutPath = ProjectFolders.Root + "/Results/UI/MissionResults.uxml";
        const string PauseLayoutPath = ProjectFolders.Root + "/Pause/UI/PauseMenu.uxml";

        // The pause menu's document is drawn over the results document on the panel they share.
        const float PauseDocumentOrder = 10f;

        // Sorting orders, back to front.
        const int OrderBackdrop = -100;
        const int OrderAura = -20;
        const int OrderSurface = 0;
        const int OrderOwnerRing = 5;
        const int OrderCaptureRing = 6;
        const int OrderSelectionRing = 7;
        const int OrderDots = 10;
        const int OrderWaves = 15;
        const int OrderText = 20;
        const int OrderScrim = 50;

        [MenuItem("Squadra/Rebuild Mission Scene")]
        public static void Build()
        {
            if (TMP_Settings.defaultFontAsset == null)
            {
                Debug.LogError("Squadra: import TMP Essential Resources first (Window > TextMeshPro).");
                return;
            }

            Directory.CreateDirectory(SquadraAssetBuilder.MaterialFolder);
            Directory.CreateDirectory(SquadraAssetBuilder.SettingsFolder);
            AssetDatabase.Refresh();

            SceneMaterials materials = SquadraAssetBuilder.EnsureMaterials();
            SquadraAssetBuilder.BuildWorldLooks();
            SquadraAssetBuilder.EnsureStyles();
            VolumeProfile profile = SquadraAssetBuilder.EnsureVolumeProfile();
            AssetDatabase.SaveAssets();
            SquadraMenuBuilder.EnsurePanel(); // before the new scene: opening it drops assets created just now

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Loaded only now: a style created a moment ago would otherwise be wired as nothing.
            SceneStyles styles = SquadraAssetBuilder.LoadStyles();

            CameraRig rig = BuildCamera();
            BuildVolume(profile);
            Backdrop backdrop = BuildBackdrop(materials, rig);

            // The mission's worlds are copies of this template, made when the mission starts.
            var worldsRoot = new GameObject("Worlds").transform;
            WorldView template = BuildWorld(worldsRoot, "World Template", materials.Thalassa, materials, styles);
            template.gameObject.SetActive(false);

            FleetLayer fleets = BuildFleets(materials, styles);
            EffectsLayer effects = BuildEffects(materials);
            HudView hud = BuildHud(materials, rig);
            ResultsScreen results = BuildResults();
            SoundLayer sounds = BuildSounds(rig);
            BuildMusic();

            var game = new GameObject("Game").AddComponent<GameController>();
            Wire(game, "worldTemplate", template);
            Wire(game, "backdrop", backdrop);
            Wire(game, "standaloneMission", AssetDatabase.LoadAssetAtPath<TextAsset>(FirstMissionPath));
            Wire(game, "fleets", fleets);
            Wire(game, "effects", effects);
            Wire(game, "sounds", sounds);
            Wire(game, "hud", hud);
            Wire(game, "results", results);
            Wire(game, "rig", rig);
            Wire(game, "lineMaterial", materials.Line);
            Wire(game, "routeStyle", styles.Routes);
            BuildPause(game);

            EditorSceneManager.SaveScene(scene, ScenePath);

            // The catalog plays its missions in this scene, so a missing catalog can only be created now.
            MissionCatalog catalog = SquadraMenuBuilder.EnsureCatalog();
            // Loaded again here: opening the new scene leaves a reference taken before it stale.
            SquadraMenuBuilder.WireCatalog(catalog,
                AssetDatabase.LoadAssetAtPath<WorldLooks>(SquadraAssetBuilder.WorldLooksPath));
            Wire(game, "catalog", catalog);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Squadra: rebuilt " + ScenePath);
        }

        /// <summary>
        /// Gives the open scene the results screen: adds the Results object if it is missing, removes the
        /// result texts of the old click-to-play-again overlay, and connects the game controller to the
        /// screen and to the mission catalog. Use it on a mission scene made before the results screen
        /// existed; unlike a full rebuild it leaves the rest of the scene alone.
        /// </summary>
        [MenuItem("Squadra/Rewire Results Screen In Open Scene")]
        public static void RewireResults()
        {
            GameController game = Object.FindAnyObjectByType<GameController>();
            HudView hud = Object.FindAnyObjectByType<HudView>();
            if (game == null || hud == null)
            {
                Debug.LogError("Squadra: open the playable scene first.");
                return;
            }

            foreach (string obsolete in new[] { "Result Title", "Result Subtitle", "Result Prompt" })
            {
                Transform text = hud.transform.Find("Result/" + obsolete);
                if (text != null) Object.DestroyImmediate(text.gameObject);
            }

            ResultsScreen results = Object.FindAnyObjectByType<ResultsScreen>();
            if (results == null) results = BuildResults();

            Wire(game, "results", results);
            Wire(game, "catalog", SquadraMenuBuilder.EnsureCatalog());
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            Debug.Log("Squadra: rewired the results screen in " + game.gameObject.scene.name
                + " (save the scene to keep it)");
        }

        /// <summary>
        /// Gives the open scene the pause menu: adds the Pause object if it is missing and connects it
        /// to the game controller. Use it on a mission scene made before the pause menu existed; unlike
        /// a full rebuild it leaves the rest of the scene alone.
        /// </summary>
        [MenuItem("Squadra/Rewire Pause Menu In Open Scene")]
        public static void RewirePause()
        {
            GameController game = Object.FindAnyObjectByType<GameController>();
            if (game == null)
            {
                Debug.LogError("Squadra: open the playable scene first.");
                return;
            }

            PauseMenu pause = Object.FindAnyObjectByType<PauseMenu>();
            if (pause == null) BuildPause(game);
            else Wire(pause, "game", game);

            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            Debug.Log("Squadra: rewired the pause menu in " + game.gameObject.scene.name
                + " (save the scene to keep it)");
        }

        /// <summary>
        /// Re-reads the sound takes and the music track from the audio folders into the open scene, adding
        /// the Sounds and Music objects if they are missing. Use it after adding or removing takes; unlike a
        /// full rebuild it leaves the rest of the scene, and the volumes set in the Inspector, alone.
        /// </summary>
        [MenuItem("Squadra/Rewire Sounds In Open Scene")]
        public static void RewireSounds()
        {
            GameController game = Object.FindAnyObjectByType<GameController>();
            CameraRig rig = Object.FindAnyObjectByType<CameraRig>();
            if (game == null || rig == null)
            {
                Debug.LogError("Squadra: open the playable scene first.");
                return;
            }

            SoundLayer sounds = Object.FindAnyObjectByType<SoundLayer>();
            if (sounds == null) sounds = BuildSounds(rig);
            else WireSounds(sounds, rig);

            MusicPlayer music = Object.FindAnyObjectByType<MusicPlayer>();
            if (music == null) BuildMusic();
            else WireMusic(music);

            Wire(game, "sounds", sounds);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            Debug.Log("Squadra: rewired sounds in " + game.gameObject.scene.name + " (save the scene to keep them)");
        }

        // ------------------------------------------------------------------ scene objects

        internal static CameraRig BuildCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(0f, 0f, -10f);

            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.Void;
            camera.allowHDR = true;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 50f;
            go.AddComponent<AudioListener>();

            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;

            var rig = go.AddComponent<CameraRig>();
            WireRect(rig, "content", new Rect(-8.9f, -5f, 17.8f, 10f));
            Relayout(rig);
            return rig;
        }

        internal static void BuildVolume(VolumeProfile profile)
        {
            var volume = new GameObject("Post Processing").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        // The mission's own backdrop replaces this material when the mission starts.
        internal static Backdrop BuildBackdrop(SceneMaterials materials, CameraRig rig)
        {
            MeshRenderer quad = Shapes.CreateQuad("Space Backdrop", null, materials.Space, OrderBackdrop);
            quad.transform.localScale = new Vector3(21f, 13f, 1f);
            var backdrop = quad.gameObject.AddComponent<Backdrop>();
            Wire(backdrop, "rig", rig);
            Relayout(backdrop);
            return backdrop;
        }

        // Its name, place, size and surface are given when a mission, or the sandbox, sets it up.
        internal static WorldView BuildWorld(Transform parent, string name, Material surfaceMaterial,
            SceneMaterials materials, SceneStyles styles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            MeshRenderer aura = Shapes.CreateQuad("Aura", go.transform, materials.Glow, OrderAura);
            MeshRenderer surface = Shapes.CreateQuad("Surface", go.transform, surfaceMaterial, OrderSurface);
            MeshRenderer ownerRing = Shapes.CreateQuad("Ownership Ring", go.transform, materials.Ring, OrderOwnerRing);
            MeshRenderer captureRing = Shapes.CreateQuad("Capture Ring", go.transform, materials.Ring, OrderCaptureRing);
            MeshRenderer selectionRing = Shapes.CreateQuad("Selection Ring", go.transform, materials.Ring, OrderSelectionRing);

            TextMeshPro count = Text("Fleet Count", go.transform, string.Empty, 5.4f, Palette.Text,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(4f, 1f), OrderText, FontStyles.Bold);
            TextMeshPro caption = Text("Caption", go.transform, name.ToUpperInvariant(), 1.75f, Palette.TextDim,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(6f, 0.5f), OrderText,
                FontStyles.Normal, 14f);

            var view = go.AddComponent<WorldView>();
            Wire(view, "style", styles.World);
            Wire(view, "surface", surface);
            Wire(view, "aura", aura);
            Wire(view, "ownerRing", ownerRing);
            Wire(view, "captureRing", captureRing);
            Wire(view, "selectionRing", selectionRing);
            Wire(view, "countLabel", count);
            Wire(view, "captionLabel", caption);

            Relayout(view);
            return view;
        }

        internal static FleetLayer BuildFleets(SceneMaterials materials, SceneStyles styles)
        {
            var go = new GameObject("Fleets");
            go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materials.Dot;
            renderer.sortingOrder = OrderDots;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            TextMeshPro label = BuildFleetLabelTemplate(go.transform);

            var fleets = go.AddComponent<FleetLayer>();
            Wire(fleets, "style", styles.Fleets);
            Wire(fleets, "fleetLabelTemplate", label);
            return fleets;
        }

        /// <summary>The inactive text copied for the Dart count that travels with each fleet.</summary>
        internal static TextMeshPro BuildFleetLabelTemplate(Transform parent)
        {
            TextMeshPro label = Text("Fleet Label Template", parent, "30", 3.2f, Palette.Text,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(3f, 0.6f), OrderText, FontStyles.Bold);
            label.gameObject.SetActive(false);
            return label;
        }

        internal static EffectsLayer BuildEffects(SceneMaterials materials)
        {
            var go = new GameObject("Effects");
            TextMeshPro floating = Text("Floating Text Template", go.transform, "-20", 4f, Palette.Text,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(8f, 0.8f), OrderText + 2,
                FontStyles.Bold, 6f);
            floating.gameObject.SetActive(false);

            var effects = go.AddComponent<EffectsLayer>();
            Wire(effects, "ringMaterial", materials.Ring);
            Wire(effects, "floatingTextTemplate", floating);
            WireInt(effects, "waveSortingOrder", OrderWaves);
            return effects;
        }

        static HudView BuildHud(SceneMaterials materials, CameraRig rig)
        {
            var root = new GameObject("HUD").transform;
            var topLeft = new Vector2(0f, 1f);
            var topRight = new Vector2(1f, 1f);
            var topCenter = new Vector2(0.5f, 1f);

            TextMeshPro title = Text("Title", root, "SQUADRA", 4.6f, Palette.Text, TextAlignmentOptions.TopLeft,
                topLeft, new Vector2(8f, 0.7f), OrderText, FontStyles.Bold, 38f);
            TextMeshPro subtitle = Text("Subtitle", root, "SECTOR 01  -  FIRST CONTACT", 1.6f, Palette.TextDim,
                TextAlignmentOptions.TopLeft, topLeft, new Vector2(8f, 0.4f), OrderText, FontStyles.Normal, 22f);

            TextMeshPro blueTally = Text("Blue Tally", root, "30\n<size=34%><cspace=0.12em>BLUE FLEET</cspace></size>",
                5.6f, Palette.Bright(Palette.Blue, 0.25f), TextAlignmentOptions.TopRight, topRight,
                new Vector2(5f, 1.2f), OrderText, FontStyles.Bold);
            blueTally.lineSpacing = 8f;
            TextMeshPro redTally = Text("Red Tally", root, "30\n<size=34%><cspace=0.12em>RED FLEET</cspace></size>",
                5.6f, Palette.Bright(Palette.Red, 0.25f), TextAlignmentOptions.TopLeft, topLeft,
                new Vector2(5f, 1.2f), OrderText, FontStyles.Bold);
            redTally.lineSpacing = 8f;
            TextMeshPro versus = Text("Versus", root, "VS", 1.6f, Palette.TextDim, TextAlignmentOptions.Top,
                topCenter, new Vector2(1f, 0.4f), OrderText, FontStyles.Normal, 10f);

            TextMeshPro clock = Text("Clock", root, "00:00", 4.2f, Palette.Text, TextAlignmentOptions.TopRight,
                topRight, new Vector2(5f, 0.6f), OrderText, FontStyles.Bold, 6f);
            TextMeshPro clockCaption = Text("Clock Caption", root, "ELAPSED", 1.6f, Palette.TextDim,
                TextAlignmentOptions.TopRight, topRight, new Vector2(5f, 0.4f), OrderText, FontStyles.Normal, 22f);

            TextMeshPro hint = Text("Hint", root, "Click one of your worlds to select its fleet", 2.2f,
                Palette.Bright(Palette.TextDim, 0.35f), TextAlignmentOptions.Bottom, new Vector2(0.5f, 0f),
                new Vector2(16f, 0.5f), OrderText, FontStyles.Normal, 6f);

            // The scrim fades the battlefield when the match ends; the results panel is drawn over it.
            var result = new GameObject("Result");
            result.transform.SetParent(root, false);
            MeshRenderer scrim = Shapes.CreateQuad("Scrim", result.transform, materials.Scrim, OrderScrim);
            scrim.transform.localScale = new Vector3(22f, 14f, 1f);
            result.SetActive(false);

            var hud = root.gameObject.AddComponent<HudView>();
            Wire(hud, "rig", rig);
            Wire(hud, "title", title);
            Wire(hud, "subtitle", subtitle);
            Wire(hud, "blueTally", blueTally);
            Wire(hud, "redTally", redTally);
            Wire(hud, "versus", versus);
            Wire(hud, "clock", clock);
            Wire(hud, "clockCaption", clockCaption);
            Wire(hud, "hint", hint);
            Wire(hud, "resultRoot", result);
            Wire(hud, "scrim", scrim);
            Relayout(hud);
            return hud;
        }

        // The results panel is a UI document over the scene, like the menu, and borrows the menu's
        // panel settings and interface cues.
        static ResultsScreen BuildResults()
        {
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ResultsLayoutPath);
            PanelSettings panel = SquadraMenuBuilder.EnsurePanel();
            if (layout == null || panel == null)
                Debug.LogError("Squadra: the results layout (" + ResultsLayoutPath + ") or the menu panel is missing.");
            if (!SquadraSoundBuilder.HasMenuSounds) SquadraSoundBuilder.Build();

            var go = new GameObject("Results");
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = layout;

            var cues = go.AddComponent<MenuAudio>();
            Wire(cues, "hover", SquadraSoundBuilder.LoadClip(SquadraSoundBuilder.MenuHover));
            Wire(cues, "select", SquadraSoundBuilder.LoadClip(SquadraSoundBuilder.MenuSelect));
            Wire(cues, "back", SquadraSoundBuilder.LoadClip(SquadraSoundBuilder.MenuBack));

            var results = go.AddComponent<ResultsScreen>();
            Wire(results, "document", document);
            Wire(results, "sounds", cues);
            return results;
        }

        // The pause menu is another UI document on the menu's panel, above the results, with its own
        // copy of the interface cues. It pauses the mission's game controller or the sandbox run.
        internal static PauseMenu BuildPause(MonoBehaviour game)
        {
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PauseLayoutPath);
            PanelSettings panel = SquadraMenuBuilder.EnsurePanel();
            if (layout == null || panel == null)
                Debug.LogError("Squadra: the pause menu layout (" + PauseLayoutPath + ") or the menu panel is missing.");
            if (!SquadraSoundBuilder.HasMenuSounds) SquadraSoundBuilder.Build();

            var go = new GameObject("Pause");
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = layout;
            document.sortingOrder = PauseDocumentOrder;

            var cues = go.AddComponent<MenuAudio>();
            Wire(cues, "hover", SquadraSoundBuilder.LoadClip(SquadraSoundBuilder.MenuHover));
            Wire(cues, "select", SquadraSoundBuilder.LoadClip(SquadraSoundBuilder.MenuSelect));
            Wire(cues, "back", SquadraSoundBuilder.LoadClip(SquadraSoundBuilder.MenuBack));

            var pause = go.AddComponent<PauseMenu>();
            Wire(pause, "document", document);
            Wire(pause, "sounds", cues);
            Wire(pause, "game", game);
            return pause;
        }

        internal static SoundLayer BuildSounds(CameraRig rig)
        {
            var sounds = new GameObject("Sounds").AddComponent<SoundLayer>();
            WireSounds(sounds, rig);
            return sounds;
        }

        // Volumes and pitch variation are defaults on SoundLayer; only the clips come from the folders.
        static void WireSounds(SoundLayer sounds, CameraRig rig)
        {
            if (!SquadraSoundBuilder.HasSounds) SquadraSoundBuilder.Build();

            Wire(sounds, "rig", rig);
            WireTakes(sounds, "departure", SquadraSoundBuilder.Departure);
            WireTakes(sounds, "arrival", SquadraSoundBuilder.Arrival);
            WireTakes(sounds, "battleStart", SquadraSoundBuilder.BattleStart);
            WireTakes(sounds, "shot", SquadraSoundBuilder.Shot);
            WireTakes(sounds, "dartDestroyed", SquadraSoundBuilder.DartDestroyed);
            WireTakes(sounds, "capture", SquadraSoundBuilder.Capture);
            Wire(sounds, "victory", SquadraSoundBuilder.LoadClip(SquadraSoundBuilder.Victory));
            Wire(sounds, "defeat", SquadraSoundBuilder.LoadClip(SquadraSoundBuilder.Defeat));
        }

        static MusicPlayer BuildMusic()
        {
            var music = new GameObject("Music").AddComponent<MusicPlayer>();
            WireMusic(music);
            return music;
        }

        static void WireMusic(MusicPlayer music)
        {
            AudioClip track = SquadraSoundBuilder.LoadMusic();
            if (track == null) Debug.LogWarning("Squadra: no music track in " + SquadraSoundBuilder.MusicFolder);
            Wire(music, "track", track);
        }

        static void WireTakes(Object target, string field, string sound)
        {
            AudioClip[] takes = SquadraSoundBuilder.LoadTakes(sound);
            if (takes.Length == 0) Debug.LogWarning("Squadra: no takes found for the " + sound + " sound");

            var serialized = new SerializedObject(target);
            SerializedProperty clips = Property(serialized, field).FindPropertyRelative("clips");
            clips.arraySize = takes.Length;
            for (int i = 0; i < takes.Length; i++)
                clips.GetArrayElementAtIndex(i).objectReferenceValue = takes[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static TextMeshPro Text(string name, Transform parent, string text, float size, Color color,
            TextAlignmentOptions alignment, Vector2 pivot, Vector2 rectSize, int sortingOrder,
            FontStyles style = FontStyles.Normal, float characterSpacing = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.rectTransform.sizeDelta = rectSize;
            tmp.rectTransform.pivot = pivot;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.fontStyle = style;
            tmp.characterSpacing = characterSpacing;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.richText = true;
            tmp.sortingOrder = sortingOrder;
            return tmp;
        }
    }
}
