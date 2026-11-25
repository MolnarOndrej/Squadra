using System.IO;
using Squadra.Missions;
using Squadra.Simulation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using static Squadra.EditorTools.SceneWiring;

namespace Squadra.EditorTools
{
    /// <summary>
    /// Makes the Sandbox scene: camera, post-processing, space backdrop, the sandbox's worlds and its
    /// preview, the fleet, effect and sound layers, the control panel its run plays with, the pause
    /// menu and a music player without a track, built from the same parts as the mission scene. Run from <c>Squadra &gt; Build Sandbox Scene</c>. The first
    /// run lays down the starting setup: three planets, one per player, at the corners of a triangle and a
    /// neutral moon in its middle, with routes along the edges and to the moon, a capture and a battle
    /// under way and two fleets on their way. From then on the scene belongs to its author: a later run
    /// only adds what is missing of the scene's structure and points it at the shared looks again, and
    /// leaves the worlds, the starting activity and the overrides as they were saved. To start over,
    /// delete the scene and run it again. The scene is put in the game's build once, after the menu and the
    /// mission scene, and stays there.
    /// </summary>
    public static class SquadraSandboxBuilder
    {
        internal const string ScenePath = ProjectFolders.Root + "/Sandbox/Scenes/Sandbox.unity";

        const string WorldsName = "Worlds";
        const string PanelLayoutPath = ProjectFolders.Root + "/Sandbox/UI/SandboxPanel.uxml";

        [MenuItem("Squadra/Build Sandbox Scene")]
        public static void Build()
        {
            if (TMP_Settings.defaultFontAsset == null)
            {
                Debug.LogError("Squadra: import TMP Essential Resources first (Window > TextMeshPro).");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Directory.CreateDirectory(SquadraAssetBuilder.SettingsFolder);
            AssetDatabase.Refresh();

            SquadraAssetBuilder.EnsureMaterials();
            SquadraAssetBuilder.BuildWorldLooks();
            SquadraAssetBuilder.EnsureStyles();
            SquadraAssetBuilder.EnsureVolumeProfile();
            AssetDatabase.SaveAssets();
            SquadraMenuBuilder.EnsurePanel(); // before the scene opens: opening it drops assets created just now

            bool exists = File.Exists(ScenePath);
            var scene = exists
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Loaded only now: an asset loaded before the scene was opened could be wired as nothing.
            SceneMaterials materials = SquadraAssetBuilder.EnsureMaterials();
            SceneStyles styles = SquadraAssetBuilder.LoadStyles();
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(SquadraAssetBuilder.SettingsFolder + "/SpaceVolume.asset");
            var looks = AssetDatabase.LoadAssetAtPath<WorldLooks>(SquadraAssetBuilder.WorldLooksPath);

            CameraRig rig = Object.FindAnyObjectByType<CameraRig>();
            if (rig == null) rig = SquadraSceneBuilder.BuildCamera();
            if (Object.FindAnyObjectByType<Volume>() == null) SquadraSceneBuilder.BuildVolume(profile);
            Backdrop backdrop = Object.FindAnyObjectByType<Backdrop>();
            if (backdrop == null) backdrop = SquadraSceneBuilder.BuildBackdrop(materials, rig);

            SandboxPreview preview = Object.FindAnyObjectByType<SandboxPreview>();
            if (preview == null) preview = BuildPreview(materials, styles);

            // The structure: shared looks and the scene's parts. Samples and overrides are the author's.
            Wire(preview, "looks", looks);
            Wire(preview, "routeStyle", styles.Routes);
            Wire(preview, "fleetStyle", styles.Fleets);
            Wire(preview, "lineMaterial", materials.Line);
            Wire(preview, "dotMaterial", materials.Dot);
            Wire(preview, "rig", rig);
            Wire(preview, "backdrop", backdrop);
            Relayout(preview);

            // What a run plays with: the mission's fleet, effect and sound layers and the sandbox's panel.
            FleetLayer fleets = Object.FindAnyObjectByType<FleetLayer>();
            if (fleets == null) fleets = SquadraSceneBuilder.BuildFleets(materials, styles);
            Wire(fleets, "style", styles.Fleets);
            EffectsLayer effects = Object.FindAnyObjectByType<EffectsLayer>();
            if (effects == null) effects = SquadraSceneBuilder.BuildEffects(materials);
            SoundLayer sounds = Object.FindAnyObjectByType<SoundLayer>();
            if (sounds == null) sounds = SquadraSceneBuilder.BuildSounds(rig);
            SandboxPanel panel = Object.FindAnyObjectByType<SandboxPanel>();
            if (panel == null) panel = BuildPanel();
            WirePanel(panel);

            SandboxRun run = preview.GetComponent<SandboxRun>();
            if (run == null) run = preview.gameObject.AddComponent<SandboxRun>();
            Wire(run, "preview", preview);
            Wire(run, "fleets", fleets);
            Wire(run, "effects", effects);
            Wire(run, "sounds", sounds);
            Wire(run, "panel", panel);

            PauseMenu pause = Object.FindAnyObjectByType<PauseMenu>();
            if (pause == null) SquadraSceneBuilder.BuildPause(run);
            else Wire(pause, "game", run);

            // A music player without a track: the sandbox is silent, so the menu music fades out on entry.
            if (Object.FindAnyObjectByType<MusicPlayer>() == null) new GameObject("Music").AddComponent<MusicPlayer>();

            if (!exists || scene.isDirty) EditorSceneManager.SaveScene(scene, ScenePath);
            SquadraMenuBuilder.ArrangeBuild();
            Debug.Log(exists
                ? "Squadra: checked the structure of " + ScenePath + "; its worlds, samples and overrides are as they were"
                : "Squadra: built " + ScenePath);
        }

        // ------------------------------------------------------------------ the panel

        static SandboxPanel BuildPanel()
        {
            var go = new GameObject("Panel");
            go.AddComponent<UIDocument>();
            return go.AddComponent<SandboxPanel>();
        }

        // The panel is a UI document on the menu's panel settings, which scale the 1920 x 1080 layout.
        static void WirePanel(SandboxPanel panel)
        {
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PanelLayoutPath);
            PanelSettings settings = SquadraMenuBuilder.EnsurePanel();
            if (layout == null || settings == null)
                Debug.LogError("Squadra: the sandbox panel layout (" + PanelLayoutPath + ") or the menu panel is missing.");

            var document = panel.GetComponent<UIDocument>();
            document.panelSettings = settings;
            document.visualTreeAsset = layout;
            EditorUtility.SetDirty(document);
            Wire(panel, "document", document);
        }

        // ------------------------------------------------------------------ the starting setup

        static SandboxPreview BuildPreview(SceneMaterials materials, SceneStyles styles)
        {
            var root = new GameObject("Sandbox");
            var preview = root.AddComponent<SandboxPreview>();

            Transform worldsRoot = new GameObject(WorldsName).transform;
            worldsRoot.SetParent(root.transform, false);
            float planet = (float)MissionRules.DefaultSize(MissionNodeType.Planet);
            float moon = (float)MissionRules.DefaultSize(MissionNodeType.Moon);

            // A triangle of three players' planets of different sizes, a neutral moon in its middle.
            SandboxWorld blue = BuildWorld(worldsRoot, "Thalassa", NodeKind.Planet, "thalassa", planet,
                new Vector2(-5.6f, -2.6f), Faction.Blue, MatchConfig.StartingDarts, materials, styles);
            SandboxWorld red = BuildWorld(worldsRoot, "Ember", NodeKind.Planet, "ember", planet * 1.25f,
                new Vector2(0f, 4.2f), Faction.Red, 24, materials, styles);
            SandboxWorld green = BuildWorld(worldsRoot, "Verdance", NodeKind.Planet, "thalassa", planet * 0.8f,
                new Vector2(5.6f, -2.6f), Faction.Green, 20, materials, styles);
            SandboxWorld lune = BuildWorld(worldsRoot, "Lune", NodeKind.Moon, "lune", moon,
                new Vector2(0f, -0.35f), Faction.Neutral, 0, materials, styles);

            // Blue has its planet selected and points at the moon, in Edit Mode. Green fights off Red, whose
            // reinforcements are on the way; Blue is capturing Red's planet.
            WireEnum(blue, "highlight", (int)SandboxHighlight.Selected);
            WireEnum(lune, "highlight", (int)SandboxHighlight.Hovered);
            WireBool(green, "fighting", true);
            WireEnum(green, "attacker", (int)Faction.Red);
            WireInt(green, "attackingDarts", 14);
            WireFloat(green, "frontAngle", 150f);
            WireBool(red, "capturing", true);
            WireEnum(red, "captor", (int)Faction.Blue);
            WireFloat(red, "captureProgress", 0.4f);

            TextMeshPro label = SquadraSceneBuilder.BuildFleetLabelTemplate(root.transform);

            Wire(preview, "worldsRoot", worldsRoot);
            Wire(preview, "fleetLabelTemplate", label);
            Wire(preview, "orderFrom", blue);
            Wire(preview, "orderTo", lune);
            WireRoutes(preview, new[] { blue, red, red, green, green, blue, blue, lune, red, lune, green, lune });
            WireFleets(preview, new[]
            {
                (red, green, Faction.Red, 9, 0.55f),
                (green, blue, Faction.Green, 40, 0.3f),
            });
            return preview;
        }

        static SandboxWorld BuildWorld(Transform parent, string name, NodeKind kind, string surface, float size,
            Vector2 position, Faction owner, int darts, SceneMaterials materials, SceneStyles styles)
        {
            WorldView view = SquadraSceneBuilder.BuildWorld(parent, name, materials.Thalassa, materials, styles);
            view.transform.position = new Vector3(position.x, position.y, 0f);

            var world = view.gameObject.AddComponent<SandboxWorld>();
            Wire(world, "worldName", name);
            WireEnum(world, "kind", (int)kind);
            Wire(world, "surface", surface);
            WireFloat(world, "size", size);
            WireEnum(world, "owner", (int)owner);
            WireInt(world, "darts", darts);
            return world;
        }

        // Pairs of worlds, one route each.
        static void WireRoutes(SandboxPreview preview, SandboxWorld[] ends)
        {
            var serialized = new SerializedObject(preview);
            SerializedProperty routes = Property(serialized, "routes");
            routes.arraySize = ends.Length / 2;
            for (int i = 0; i < routes.arraySize; i++)
            {
                SerializedProperty route = routes.GetArrayElementAtIndex(i);
                Relative(route, "from").objectReferenceValue = ends[i * 2];
                Relative(route, "to").objectReferenceValue = ends[i * 2 + 1];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireFleets(SandboxPreview preview,
            (SandboxWorld from, SandboxWorld to, Faction faction, int darts, float progress)[] samples)
        {
            var serialized = new SerializedObject(preview);
            SerializedProperty fleets = Property(serialized, "fleets");
            fleets.arraySize = samples.Length;
            for (int i = 0; i < samples.Length; i++)
            {
                SerializedProperty fleet = fleets.GetArrayElementAtIndex(i);
                Relative(fleet, "from").objectReferenceValue = samples[i].from;
                Relative(fleet, "to").objectReferenceValue = samples[i].to;
                Relative(fleet, "faction").intValue = (int)samples[i].faction;
                Relative(fleet, "darts").intValue = samples[i].darts;
                Relative(fleet, "progress").floatValue = samples[i].progress;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static SerializedProperty Relative(SerializedProperty parent, string field)
        {
            SerializedProperty property = parent.FindPropertyRelative(field);
            if (property == null)
                throw new System.InvalidOperationException(parent.propertyPath + " has no field " + field);
            return property;
        }
    }
}
