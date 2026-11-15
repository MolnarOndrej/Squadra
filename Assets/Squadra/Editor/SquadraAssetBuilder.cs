using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Squadra.EditorTools
{
    /// <summary>
    /// Generates the assets the scenes are built from: the materials of the backdrop, the worlds and
    /// the effects, and the post-processing profile. The look of each world is decided here.
    /// </summary>
    static class SquadraAssetBuilder
    {
        public const string MaterialFolder = SquadraSceneBuilder.Root + "/Materials";
        public const string SettingsFolder = SquadraSceneBuilder.Root + "/Settings";

        // The sun sits just outside the top-left of the frame; planets are lit from there.
        static readonly Vector2 SunPosition = new Vector2(-12f, 7.5f);
        static readonly Vector4 LightDirection = new Vector4(-0.62f, 0.45f, 0.5f, 0f);

        /// <summary>
        /// Loads or creates every generated material and gives it the look set here. Values changed in
        /// a generated material are overwritten the next time a scene is rebuilt.
        /// </summary>
        public static SceneMaterials BuildMaterials()
        {
            var m = new SceneMaterials
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

        /// <summary>Recreates the post-processing profile the scenes share.</summary>
        public static VolumeProfile BuildVolumeProfile()
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

        static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }
    }
}
