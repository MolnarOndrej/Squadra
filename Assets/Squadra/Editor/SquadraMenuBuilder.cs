using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Squadra.EditorTools
{
    /// <summary>
    /// Assembles the main menu scene from code: camera, space backdrop and scenery, the menu's UI
    /// document, the mission map, the interface cues and the menu music. Run it again from
    /// <c>Squadra &gt; Rebuild Main Menu Scene</c>; it overwrites the scene and puts it first in the build.
    /// The layout and look live in <c>Assets/Squadra/UI</c> and the mission list in the catalog asset;
    /// those are created when missing and otherwise left alone. Materials and the post-processing
    /// profile are shared with the first playable scene, so build that one first. The panel settings and
    /// the catalog are shared the other way: the playable scene's results screen uses both.
    /// </summary>
    public static class SquadraMenuBuilder
    {
        public const string ScenePath = "Assets/Scenes/MainMenu.unity";

        const string UiFolder = SquadraSceneBuilder.Root + "/UI";
        const string LayoutPath = UiFolder + "/MainMenu.uxml";
        const string ThemePath = UiFolder + "/MenuTheme.tss";
        const string PanelPath = UiFolder + "/MenuPanel.asset";
        const string CampaignFolder = SquadraSceneBuilder.Root + "/Campaign";
        const string CatalogPath = CampaignFolder + "/Missions.asset";

        // Sorting orders, back to front. The interface itself is drawn over everything by its panel.
        const int OrderBackdrop = -100;
        const int OrderOrbits = -60;
        const int OrderPlanet = -40;
        const int OrderMoon = -39;

        [MenuItem("Squadra/Rebuild Main Menu Scene")]
        public static void Build()
        {
            var space = Load<Material>(SquadraSceneBuilder.MaterialFolder + "/Space.mat");
            var planet = Load<Material>(SquadraSceneBuilder.MaterialFolder + "/Thalassa.mat");
            var moon = Load<Material>(SquadraSceneBuilder.MaterialFolder + "/Lune.mat");
            var ring = Load<Material>(SquadraSceneBuilder.MaterialFolder + "/Ring.mat");
            var profile = Load<VolumeProfile>(SquadraSceneBuilder.SettingsFolder + "/SpaceVolume.asset");
            var layout = Load<VisualTreeAsset>(LayoutPath);
            var theme = Load<ThemeStyleSheet>(ThemePath);
            if (space == null || planet == null || moon == null || ring == null || profile == null)
            {
                Debug.LogError("Squadra: run Squadra > Rebuild First Playable Scene first; the menu shares its materials.");
                return;
            }

            if (layout == null || theme == null)
            {
                Debug.LogError("Squadra: the menu layout or theme is missing from " + UiFolder);
                return;
            }

            if (!SquadraSoundBuilder.HasMenuSounds) SquadraSoundBuilder.Build();
            EnsurePanel();
            EnsureCatalog();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Loaded only now: opening the new scene drops assets that were created a moment ago.
            var panel = Load<PanelSettings>(PanelPath);
            var catalog = Load<MissionCatalog>(CatalogPath);
            if (panel == null || catalog == null)
            {
                Debug.LogError("Squadra: could not create the menu's panel settings or mission catalog.");
                return;
            }

            CameraRig rig = SquadraSceneBuilder.BuildCamera();
            SquadraSceneBuilder.BuildVolume(profile);

            MeshRenderer backdropQuad = Shapes.CreateQuad("Space Backdrop", null, space, OrderBackdrop);
            var backdrop = backdropQuad.gameObject.AddComponent<Backdrop>();
            SquadraSceneBuilder.Wire(backdrop, "rig", rig);
            SquadraSceneBuilder.Relayout(backdrop);

            var scenery = new GameObject("Scenery").AddComponent<MenuScenery>();
            Transform sceneryRoot = scenery.transform;
            SquadraSceneBuilder.Wire(scenery, "rig", rig);
            SquadraSceneBuilder.Wire(scenery, "innerOrbit", Shapes.CreateQuad("Inner Orbit", sceneryRoot, ring, OrderOrbits));
            SquadraSceneBuilder.Wire(scenery, "outerOrbit", Shapes.CreateQuad("Outer Orbit", sceneryRoot, ring, OrderOrbits));
            SquadraSceneBuilder.Wire(scenery, "planet", Shapes.CreateQuad("Planet", sceneryRoot, planet, OrderPlanet));
            SquadraSceneBuilder.Wire(scenery, "moon", Shapes.CreateQuad("Moon", sceneryRoot, moon, OrderMoon));
            SquadraSceneBuilder.Relayout(scenery);

            var music = new GameObject("Music").AddComponent<MusicPlayer>();
            AudioClip track = SquadraSoundBuilder.LoadMenuMusic();
            if (track == null) Debug.LogWarning("Squadra: no menu track in " + SquadraSoundBuilder.MenuMusicFolder);
            SquadraSceneBuilder.Wire(music, "track", track);

            var sounds = new GameObject("Menu Sounds").AddComponent<MenuAudio>();
            SquadraSceneBuilder.Wire(sounds, "hover", SquadraSoundBuilder.LoadClip(SquadraSoundBuilder.MenuHover));
            SquadraSceneBuilder.Wire(sounds, "select", SquadraSoundBuilder.LoadClip(SquadraSoundBuilder.MenuSelect));
            SquadraSceneBuilder.Wire(sounds, "back", SquadraSoundBuilder.LoadClip(SquadraSoundBuilder.MenuBack));

            // Missions per Page is set on this object in the Inspector.
            var map = new GameObject("Mission Map").AddComponent<MissionMap>();
            SquadraSceneBuilder.Wire(map, "catalog", catalog);

            var menuObject = new GameObject("Menu");
            var document = menuObject.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = layout;
            var menu = menuObject.AddComponent<MainMenu>();
            SquadraSceneBuilder.Wire(menu, "document", document);
            SquadraSceneBuilder.Wire(menu, "map", map);
            SquadraSceneBuilder.Wire(menu, "sounds", sounds);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            PutFirstInBuild();
            Debug.Log("Squadra: rebuilt " + ScenePath);
        }

        // ------------------------------------------------------------------ assets

        /// <summary>
        /// The panel settings that scale the 1920 x 1080 interface to the window, always fitting all of
        /// it. The menu and the results screen share them. Created when missing; null without the theme.
        /// </summary>
        internal static PanelSettings EnsurePanel()
        {
            var theme = Load<ThemeStyleSheet>(ThemePath);
            if (theme == null) return null;

            var panel = Load<PanelSettings>(PanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelPath);
            }

            panel.themeStyleSheet = theme;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.Expand;
            EditorUtility.SetDirty(panel);
            AssetDatabase.SaveAssets();
            return panel;
        }

        /// <summary>
        /// The mission catalog, created when missing: Mission 1 is the first playable scenario and
        /// Missions 2-5 are previews until their scenes exist. Mission 1 only gets its scene when that
        /// scene has been saved, so create the catalog after the first playable scene.
        /// </summary>
        internal static MissionCatalog EnsureCatalog()
        {
            var catalog = Load<MissionCatalog>(CatalogPath);
            if (catalog != null) return catalog;

            Directory.CreateDirectory(CampaignFolder);
            AssetDatabase.Refresh();
            catalog = MissionCatalog.Create(
                ("mission-01", SquadraSceneBuilder.ScenePath),
                ("mission-02", null),
                ("mission-03", null),
                ("mission-04", null),
                ("mission-05", null));
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        // The game opens on the menu, and a mission can only be launched when its scene is in the build.
        static void PutFirstInBuild()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(entry => entry.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            if (!scenes.Exists(entry => entry.path == SquadraSceneBuilder.ScenePath))
                scenes.Add(new EditorBuildSettingsScene(SquadraSceneBuilder.ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static T Load<T>(string path) where T : Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }
    }
}
