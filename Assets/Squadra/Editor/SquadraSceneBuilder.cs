using System.IO;
using Squadra.Simulation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Squadra.EditorTools
{
    /// <summary>
    /// Assembles the first playable scene from code: materials, post-processing profile, backdrop, the two
    /// planets and the moon, fleet and effect layers, HUD and the game controller. Run it again from
    /// <c>Squadra &gt; Rebuild First Playable Scene</c> to regenerate everything; it overwrites the scene and
    /// the generated materials, so tweak values here rather than in the generated assets if they must survive.
    /// </summary>
    public static class SquadraSceneBuilder
    {
        const string Root = "Assets/Squadra";
        const string MaterialFolder = Root + "/Materials";
        const string SettingsFolder = Root + "/Settings";
        const string ScenePath = "Assets/Scenes/SampleScene.unity";

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
        const int OrderResult = 60;

        // The sun sits just outside the top-left of the frame; planets are lit from there.
        static readonly Vector2 SunPosition = new Vector2(-12f, 7.5f);
        static readonly Vector4 LightDirection = new Vector4(-0.62f, 0.45f, 0.5f, 0f);

        sealed class Materials
        {
            public Material Space, Thalassa, Ember, Lune, Ring, Glow, Dot, Line, Scrim;
        }

        [MenuItem("Squadra/Rebuild First Playable Scene")]
        public static void Build()
        {
            if (TMP_Settings.defaultFontAsset == null)
            {
                Debug.LogError("Squadra: import TMP Essential Resources first (Window > TextMeshPro).");
                return;
            }

            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory(SettingsFolder);
            AssetDatabase.Refresh();

            Materials materials = BuildMaterials();
            VolumeProfile profile = BuildVolumeProfile();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CameraRig rig = BuildCamera();
            BuildVolume(profile);
            BuildBackdrop(materials, rig);

            var worldsRoot = new GameObject("Worlds").transform;
            WorldView thalassa = BuildWorld(worldsRoot, "Thalassa", NodeKind.Planet, Faction.Blue,
                MatchConfig.StartingShips, NodeConfig.PlanetProductionInterval, new Vector2(-5.5f, -0.75f), 1.15f,
                materials.Thalassa, materials);
            WorldView ember = BuildWorld(worldsRoot, "Ember", NodeKind.Planet, Faction.Red,
                MatchConfig.StartingShips, NodeConfig.PlanetProductionInterval, new Vector2(5.5f, 0.45f), 1.2f,
                materials.Ember, materials);
            WorldView lune = BuildWorld(worldsRoot, "Lune", NodeKind.Moon, Faction.Neutral, 0,
                NodeConfig.MoonProductionInterval, new Vector2(-1f, 1.6f), 0.55f, materials.Lune, materials);

            FleetLayer fleets = BuildFleets(materials);
            EffectsLayer effects = BuildEffects(materials);
            HudView hud = BuildHud(materials, rig);

            var game = new GameObject("Game").AddComponent<GameController>();
            Wire(game, "worlds", new Object[] { thalassa, ember, lune });
            Wire(game, "fleets", fleets);
            Wire(game, "effects", effects);
            Wire(game, "hud", hud);
            Wire(game, "rig", rig);
            Wire(game, "lineMaterial", materials.Line);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Squadra: rebuilt " + ScenePath);
        }

        // ------------------------------------------------------------------ assets

        static Materials BuildMaterials()
        {
            var m = new Materials
            {
                Space = LoadMaterial("Space", "Squadra/Space"),
                Thalassa = LoadMaterial("Thalassa", "Squadra/Planet"),
                Ember = LoadMaterial("Ember", "Squadra/Planet"),
                Lune = LoadMaterial("Lune", "Squadra/Planet"),
                Ring = LoadMaterial("Ring", "Squadra/Ring"),
                Glow = LoadMaterial("Glow", "Squadra/Glow"),
                Dot = LoadMaterial("Dot", "Squadra/Dot"),
                Line = LoadMaterial("Line", "Squadra/Line"),
                Scrim = LoadMaterial("Scrim", "Squadra/Scrim"),
            };

            // Backdrop
            m.Space.SetColor("_VoidColor", Palette.Void);
            m.Space.SetColor("_NebulaA", Palette.NebulaIndigo);
            m.Space.SetColor("_NebulaB", Palette.NebulaViolet);
            m.Space.SetColor("_NebulaC", Palette.NebulaTeal);
            m.Space.SetFloat("_NebulaStrength", 1.05f);
            m.Space.SetFloat("_NebulaScale", 0.11f);
            m.Space.SetColor("_StarColor", Palette.Starlight);
            m.Space.SetColor("_StarWarm", Palette.Sunlight);
            m.Space.SetFloat("_StarDensity", 0.5f);
            m.Space.SetFloat("_StarBrightness", 1.3f);
            m.Space.SetColor("_SunColor", Palette.Sunlight);
            m.Space.SetVector("_SunPosition", SunPosition);
            m.Space.SetFloat("_SunStrength", 0.7f);
            m.Space.SetFloat("_Drift", 0.012f);

            // Thalassa: Blue's ocean homeworld. Seas, green continents, ice caps, cloud bands, city lights.
            PlanetDefaults(m.Thalassa, seed: 3.7f, spin: 0.045f, tilt: 0.35f, noiseScale: 2.1f, warp: 0.7f);
            m.Thalassa.SetFloat("_SeaLevel", 0.52f);
            m.Thalassa.SetColor("_ColorDeep", Hex(0x0A2A5E));
            m.Thalassa.SetColor("_ColorShallow", Hex(0x1E7FA6));
            m.Thalassa.SetColor("_ColorLow", Hex(0x2F6B3A));
            m.Thalassa.SetColor("_ColorMid", Hex(0x7A7A45));
            m.Thalassa.SetColor("_ColorHigh", Hex(0xC9C2B0));
            m.Thalassa.SetColor("_IceColor", Hex(0xEAF4FF));
            m.Thalassa.SetFloat("_IceAmount", 0.16f);
            m.Thalassa.SetColor("_CloudColor", Hex(0xF4F8FF));
            m.Thalassa.SetFloat("_CloudAmount", 0.5f);
            m.Thalassa.SetFloat("_CloudScale", 3.2f);
            m.Thalassa.SetFloat("_CloudSpeed", 0.02f);
            m.Thalassa.SetColor("_CityColor", Hex(0xFFC870));
            m.Thalassa.SetFloat("_CityAmount", 1.6f);
            m.Thalassa.SetFloat("_Specular", 0.7f);
            m.Thalassa.SetColor("_AtmoColor", Hex(0x5AB4FF));
            m.Thalassa.SetFloat("_AtmoRim", 1.1f);
            m.Thalassa.SetFloat("_AtmoHalo", 0.9f);

            // Ember: Red's volcanic desert homeworld. Banded rust and basalt, dust clouds, glowing lava seams.
            PlanetDefaults(m.Ember, seed: 8.2f, spin: 0.035f, tilt: -0.25f, noiseScale: 2.6f, warp: 0.9f);
            m.Ember.SetFloat("_SeaLevel", 0f);
            m.Ember.SetFloat("_Bands", 0.25f);
            m.Ember.SetFloat("_BandFrequency", 7f);
            m.Ember.SetColor("_ColorLow", Hex(0x3A1C17));
            m.Ember.SetColor("_ColorMid", Hex(0x9A4B2A));
            m.Ember.SetColor("_ColorHigh", Hex(0xE0A56A));
            m.Ember.SetFloat("_IceAmount", 0f);
            m.Ember.SetColor("_CloudColor", Hex(0xE8C9A8));
            m.Ember.SetFloat("_CloudAmount", 0.6f);
            m.Ember.SetFloat("_CloudScale", 2.4f);
            m.Ember.SetFloat("_CloudSpeed", 0.015f);
            m.Ember.SetColor("_LavaColor", Hex(0xFF5A1E));
            m.Ember.SetFloat("_LavaAmount", 3f);
            m.Ember.SetFloat("_LavaLevel", 0.36f);
            m.Ember.SetFloat("_Specular", 0f);
            m.Ember.SetColor("_AtmoColor", Hex(0xFF9A5A));
            m.Ember.SetFloat("_AtmoRim", 0.8f);
            m.Ember.SetFloat("_AtmoHalo", 0.6f);

            // Lune: the neutral moon. Airless grey regolith with craters.
            PlanetDefaults(m.Lune, seed: 5.1f, spin: 0.02f, tilt: 0.1f, noiseScale: 3.2f, warp: 0.4f);
            m.Lune.SetFloat("_SeaLevel", 0f);
            m.Lune.SetColor("_ColorLow", Hex(0x4B4E5C));
            m.Lune.SetColor("_ColorMid", Hex(0x8C8F9E));
            m.Lune.SetColor("_ColorHigh", Hex(0xC5C7D4));
            m.Lune.SetFloat("_IceAmount", 0f);
            m.Lune.SetFloat("_CloudAmount", 0f);
            m.Lune.SetFloat("_CraterAmount", 0.9f);
            m.Lune.SetFloat("_CraterScale", 4.5f);
            m.Lune.SetFloat("_Specular", 0f);
            m.Lune.SetColor("_AtmoColor", Hex(0xAAB4D0));
            m.Lune.SetFloat("_AtmoRim", 0.25f);
            m.Lune.SetFloat("_AtmoHalo", 0.18f);

            // Shared effect materials: per-renderer values come from property blocks at runtime.
            m.Glow.SetFloat("_Power", 2.4f);
            m.Dot.SetFloat("_Core", 0.5f);
            m.Dot.SetFloat("_Halo", 0.55f);
            m.Dot.SetFloat("_Additive", 0.7f);

            foreach (Material material in new[] { m.Space, m.Thalassa, m.Ember, m.Lune, m.Ring, m.Glow, m.Dot, m.Line, m.Scrim })
                EditorUtility.SetDirty(material);

            return m;
        }

        static void PlanetDefaults(Material material, float seed, float spin, float tilt, float noiseScale, float warp)
        {
            material.SetFloat("_BodyRadius", 0.7f);
            material.SetFloat("_Seed", seed);
            material.SetFloat("_SpinSpeed", spin);
            material.SetFloat("_Tilt", tilt);
            material.SetFloat("_NoiseScale", noiseScale);
            material.SetFloat("_Warp", warp);
            material.SetVector("_LightDir", LightDirection);
            material.SetFloat("_Ambient", 0.035f);
            material.SetFloat("_Bands", 0f);
            material.SetFloat("_CraterAmount", 0f);
            material.SetFloat("_CityAmount", 0f);
            material.SetFloat("_LavaAmount", 0f);
        }

        static Material LoadMaterial(string name, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new System.InvalidOperationException("Missing shader " + shaderName);

            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            return material;
        }

        static VolumeProfile BuildVolumeProfile()
        {
            string path = SettingsFolder + "/SpaceVolume.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var bloom = Add<Bloom>(profile);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.85f);
            bloom.scatter.Override(0.68f);
            bloom.tint.Override(Color.Lerp(Color.white, Palette.Starlight, 0.5f));
            bloom.highQualityFiltering.Override(true);

            var vignette = Add<Vignette>(profile);
            vignette.color.Override(Palette.Void);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.55f);

            var tonemapping = Add<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            var color = Add<ColorAdjustments>(profile);
            color.contrast.Override(8f);
            color.saturation.Override(6f);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            T component = profile.Add<T>(false);
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        // ------------------------------------------------------------------ scene objects

        static CameraRig BuildCamera()
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

        static void BuildVolume(VolumeProfile profile)
        {
            var volume = new GameObject("Post Processing").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        static void BuildBackdrop(Materials materials, CameraRig rig)
        {
            MeshRenderer quad = Shapes.CreateQuad("Space Backdrop", null, materials.Space, OrderBackdrop);
            quad.transform.localScale = new Vector3(21f, 13f, 1f);
            var backdrop = quad.gameObject.AddComponent<Backdrop>();
            Wire(backdrop, "rig", rig);
            Relayout(backdrop);
        }

        static WorldView BuildWorld(Transform parent, string name, NodeKind kind, Faction owner, int ships,
            double productionInterval, Vector2 position, float radius, Material surfaceMaterial, Materials materials)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            MeshRenderer aura = Shapes.CreateQuad("Aura", go.transform, materials.Glow, OrderAura);
            MeshRenderer surface = Shapes.CreateQuad("Surface", go.transform, surfaceMaterial, OrderSurface);
            MeshRenderer ownerRing = Shapes.CreateQuad("Ownership Ring", go.transform, materials.Ring, OrderOwnerRing);
            MeshRenderer captureRing = Shapes.CreateQuad("Capture Ring", go.transform, materials.Ring, OrderCaptureRing);
            MeshRenderer selectionRing = Shapes.CreateQuad("Selection Ring", go.transform, materials.Ring, OrderSelectionRing);

            TextMeshPro count = Text("Fleet Count", go.transform, ships.ToString(), 5.4f, Palette.Text,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(4f, 1f), OrderText, FontStyles.Bold);
            TextMeshPro caption = Text("Caption", go.transform, name.ToUpperInvariant(), 1.75f, Palette.TextDim,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(6f, 0.5f), OrderText,
                FontStyles.Normal, 14f);

            var view = go.AddComponent<WorldView>();
            Wire(view, "displayName", name);
            WireEnum(view, "kind", (int)kind);
            WireEnum(view, "startingOwner", (int)owner);
            WireInt(view, "startingShips", ships);
            WireFloat(view, "productionInterval", (float)productionInterval);
            WireFloat(view, "bodyRadius", radius);
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

        static FleetLayer BuildFleets(Materials materials)
        {
            var go = new GameObject("Fleets");
            go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materials.Dot;
            renderer.sortingOrder = OrderDots;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            TextMeshPro label = Text("Fleet Label Template", go.transform, "30", 3.2f, Palette.Text,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(3f, 0.6f), OrderText, FontStyles.Bold);
            label.gameObject.SetActive(false);

            var fleets = go.AddComponent<FleetLayer>();
            Wire(fleets, "fleetLabelTemplate", label);
            return fleets;
        }

        static EffectsLayer BuildEffects(Materials materials)
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

        static HudView BuildHud(Materials materials, CameraRig rig)
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

            var result = new GameObject("Result");
            result.transform.SetParent(root, false);
            MeshRenderer scrim = Shapes.CreateQuad("Scrim", result.transform, materials.Scrim, OrderScrim);
            scrim.transform.localScale = new Vector3(22f, 14f, 1f);
            var center = new Vector2(0.5f, 0.5f);
            TextMeshPro resultTitle = Text("Result Title", result.transform, "VICTORY", 15f, Palette.Accent,
                TextAlignmentOptions.Center, center, new Vector2(16f, 2f), OrderResult, FontStyles.Bold, 30f);
            TextMeshPro resultSubtitle = Text("Result Subtitle", result.transform, "Both planets secured", 2.8f,
                Palette.Text, TextAlignmentOptions.Center, center, new Vector2(16f, 0.6f), OrderResult,
                FontStyles.Normal, 6f);
            TextMeshPro resultPrompt = Text("Result Prompt", result.transform, "CLICK TO PLAY AGAIN", 1.9f,
                Palette.TextDim, TextAlignmentOptions.Center, center, new Vector2(16f, 0.5f), OrderResult,
                FontStyles.Normal, 26f);
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
            Wire(hud, "resultTitle", resultTitle);
            Wire(hud, "resultSubtitle", resultSubtitle);
            Wire(hud, "resultPrompt", resultPrompt);
            Relayout(hud);
            return hud;
        }

        // ------------------------------------------------------------------ helpers

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

        /// <summary>Re-runs OnEnable now that the references are wired, so the edit-time layout is applied.</summary>
        static void Relayout(Behaviour behaviour)
        {
            behaviour.enabled = false;
            behaviour.enabled = true;
        }

        static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        static SerializedProperty Property(SerializedObject serialized, string field)
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
                throw new System.InvalidOperationException(serialized.targetObject.GetType().Name + " has no field " + field);
            return property;
        }

        static void Wire(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            Property(serialized, field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Wire(Object target, string field, Object[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = Property(serialized, field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Wire(Object target, string field, string value)
        {
            var serialized = new SerializedObject(target);
            Property(serialized, field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireInt(Object target, string field, int value)
        {
            var serialized = new SerializedObject(target);
            Property(serialized, field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireEnum(Object target, string field, int value)
        {
            var serialized = new SerializedObject(target);
            Property(serialized, field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireFloat(Object target, string field, float value)
        {
            var serialized = new SerializedObject(target);
            Property(serialized, field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireRect(Object target, string field, Rect value)
        {
            var serialized = new SerializedObject(target);
            Property(serialized, field).rectValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
