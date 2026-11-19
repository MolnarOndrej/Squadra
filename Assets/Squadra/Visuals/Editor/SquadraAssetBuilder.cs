using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Squadra.EditorTools
{
    /// <summary>
    /// Makes sure the assets the scenes are built from exist: the materials of the backdrop, the world
    /// surfaces, the Darts and the effects, the Dart mesh, the styles of the worlds, routes and fleets,
    /// the post-processing profile, and the world looks that list which surfaces each type of world can
    /// be given and which space backdrops a mission can be shown against.
    ///
    /// A missing asset is created with the look written here, which is where the game's look started.
    /// An existing one is left exactly as it is: its values are edited in the Inspector and survive every
    /// rebuild, and so do its identity and every reference to it. Nothing here puts an asset back to these
    /// values; to start one over, delete it and rebuild.
    ///
    /// World surfaces live in their type's folder (Planets, Moons, Asteroids) and backdrops in Backdrops,
    /// so a material added there is offered the next time the looks are built. Dart materials live in
    /// Darts and the Dart mesh in <c>Visuals/Meshes</c>; the fleet style is given the folder's first
    /// material and that mesh only when it has none.
    /// </summary>
    static class SquadraAssetBuilder
    {
        public const string MaterialFolder = ProjectFolders.Root + "/Visuals/Materials";
        public const string PlanetFolder = MaterialFolder + "/Planets";
        public const string MoonFolder = MaterialFolder + "/Moons";
        public const string AsteroidFolder = MaterialFolder + "/Asteroids";
        public const string BackdropFolder = MaterialFolder + "/Backdrops";
        public const string DartFolder = MaterialFolder + "/Darts";
        public const string MeshFolder = ProjectFolders.Root + "/Visuals/Meshes";
        public const string DartMeshPath = MeshFolder + "/Dart.asset";
        public const string SettingsFolder = ProjectFolders.Root + "/Visuals/PostProcessing";
        public const string NamesFolder = ProjectFolders.Root + "/Visuals/WorldNames";
        public const string WorldLooksPath = ProjectFolders.Root + "/Visuals/WorldLooks.asset";
        public const string StylesFolder = ProjectFolders.Root + "/Visuals/Styles";
        public const string WorldStylePath = StylesFolder + "/WorldStyle.asset";
        public const string RouteStylePath = StylesFolder + "/RouteStyle.asset";
        public const string FleetStylePath = StylesFolder + "/FleetStyle.asset";

        // The sun sits just outside the top-left of the frame; planets are lit from there.
        static readonly Vector2 SunOffset = new Vector2(-3.1f, 2.5f);
        static readonly Vector4 LightDirection = new Vector4(-0.62f, 0.45f, 0.5f, 0f);

        /// <summary>
        /// Every generated material, each created with its first look when it is missing. Existing
        /// materials are returned as they are.
        /// </summary>
        public static SceneMaterials EnsureMaterials()
        {
            foreach (string folder in new[] { MaterialFolder, PlanetFolder, MoonFolder, AsteroidFolder, BackdropFolder, DartFolder })
                Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();

            // Ring, line and scrim materials have nothing of their own: their renderers set every value.
            var materials = new SceneMaterials
            {
                Space = EnsureMaterial(BackdropFolder, "Space", "Squadra/Space", FirstSpaceLook),
                Thalassa = EnsureMaterial(PlanetFolder, "Thalassa", "Squadra/Planet", FirstThalassaLook),
                Ember = EnsureMaterial(PlanetFolder, "Ember", "Squadra/Planet", FirstEmberLook),
                Lune = EnsureMaterial(MoonFolder, "Lune", "Squadra/Planet", FirstLuneLook),
                Cinder = EnsureMaterial(AsteroidFolder, "Cinder", "Squadra/Planet", FirstCinderLook),
                Ferrite = EnsureMaterial(AsteroidFolder, "Ferrite", "Squadra/Planet", FirstFerriteLook),
                Ring = EnsureMaterial(MaterialFolder, "Ring", "Squadra/Ring", null),
                Glow = EnsureMaterial(MaterialFolder, "Glow", "Squadra/Glow", glow => glow.SetFloat("_Power", 2.4f)),
                Dot = EnsureMaterial(MaterialFolder, "Dot", "Squadra/Dot", FirstDotLook),
                Line = EnsureMaterial(MaterialFolder, "Line", "Squadra/Line", null),
                Scrim = EnsureMaterial(MaterialFolder, "Scrim", "Squadra/Scrim", null),
            };
            EnsureDartMaterial(materials.Dot);
            return materials;
        }

        // The Darts were drawn with the dot material until they had one of their own, so the first Dart
        // material starts as a copy of it, edits included. Any material in the folder will do.
        static void EnsureDartMaterial(Material dot)
        {
            if (DartMaterials().Count > 0) return;

            var dart = new Material(dot) { name = "Dart" };
            AssetDatabase.CreateAsset(dart, DartFolder + "/Dart.mat");
        }

        // The materials of the Darts folder itself, by name.
        static List<Material> DartMaterials()
        {
            var found = new List<Material>();
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { DartFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetDirectoryName(path)?.Replace('\\', '/') != DartFolder) continue;
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null) found.Add(material);
            }

            found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return found;
        }

        // The round dot: a quad twice as wide as the dot's core, which the Squadra/Dot shader draws as a
        // soft disc. At the style's dot width it is the dot the fleets were always drawn with.
        static Mesh EnsureDartMesh()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(DartMeshPath);
            if (mesh != null) return mesh;

            Directory.CreateDirectory(MeshFolder);
            AssetDatabase.Refresh();
            mesh = new Mesh { name = "Dart" };
            mesh.SetVertices(new[]
            {
                new Vector3(-1f, -1f, 0f), new Vector3(-1f, 1f, 0f), new Vector3(1f, 1f, 0f), new Vector3(1f, -1f, 0f),
            });
            mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, DartMeshPath);
            return mesh;
        }

        static Material EnsureMaterial(string folder, string name, string shaderName, System.Action<Material> firstLook)
        {
            string path = folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new System.InvalidOperationException("Missing shader " + shaderName);

            material = new Material(shader);
            firstLook?.Invoke(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // ------------------------------------------------------------------ first looks

        static void FirstSpaceLook(Material space)
        {
            space.SetColor("_VoidColor", Palette.Void);
            space.SetColor("_NebulaA", Palette.NebulaIndigo);
            space.SetColor("_NebulaB", Palette.NebulaViolet);
            space.SetColor("_NebulaC", Palette.NebulaTeal);
            space.SetFloat("_NebulaStrength", 1.05f);
            space.SetFloat("_NebulaScale", 0.11f);
            space.SetColor("_StarColor", Palette.Starlight);
            space.SetColor("_StarWarm", Palette.Sunlight);
            space.SetFloat("_StarDensity", 0.5f);
            space.SetFloat("_StarBrightness", 1.3f);
            space.SetColor("_SunColor", Palette.Sunlight);
            space.SetVector("_SunOffset", SunOffset);
            space.SetFloat("_SunStrength", 0.7f);
            space.SetFloat("_Drift", 0.012f);
        }

        // Thalassa: Blue's ocean homeworld. Seas, green continents, ice caps, cloud bands, city lights.
        static void FirstThalassaLook(Material m)
        {
            PlanetDefaults(m, seed: 3.7f, spin: 0.045f, tilt: 0.35f, noiseScale: 2.1f, warp: 0.7f);
            m.SetFloat("_SeaLevel", 0.52f);
            m.SetColor("_ColorDeep", Hex(0x0A2A5E));
            m.SetColor("_ColorShallow", Hex(0x1E7FA6));
            m.SetColor("_ColorLow", Hex(0x2F6B3A));
            m.SetColor("_ColorMid", Hex(0x7A7A45));
            m.SetColor("_ColorHigh", Hex(0xC9C2B0));
            m.SetColor("_IceColor", Hex(0xEAF4FF));
            m.SetFloat("_IceAmount", 0.16f);
            m.SetColor("_CloudColor", Hex(0xF4F8FF));
            m.SetFloat("_CloudAmount", 0.5f);
            m.SetFloat("_CloudScale", 3.2f);
            m.SetFloat("_CloudSpeed", 0.02f);
            m.SetColor("_CityColor", Hex(0xFFC870));
            m.SetFloat("_CityAmount", 1.6f);
            m.SetFloat("_Specular", 0.7f);
            m.SetColor("_AtmoColor", Hex(0x5AB4FF));
            m.SetFloat("_AtmoRim", 1.1f);
            m.SetFloat("_AtmoHalo", 0.9f);
        }

        // Ember: Red's volcanic desert homeworld. Banded rust and basalt, dust clouds, glowing lava seams.
        static void FirstEmberLook(Material m)
        {
            PlanetDefaults(m, seed: 8.2f, spin: 0.035f, tilt: -0.25f, noiseScale: 2.6f, warp: 0.9f);
            m.SetFloat("_SeaLevel", 0f);
            m.SetFloat("_Bands", 0.25f);
            m.SetFloat("_BandFrequency", 7f);
            m.SetColor("_ColorLow", Hex(0x3A1C17));
            m.SetColor("_ColorMid", Hex(0x9A4B2A));
            m.SetColor("_ColorHigh", Hex(0xE0A56A));
            m.SetFloat("_IceAmount", 0f);
            m.SetColor("_CloudColor", Hex(0xE8C9A8));
            m.SetFloat("_CloudAmount", 0.6f);
            m.SetFloat("_CloudScale", 2.4f);
            m.SetFloat("_CloudSpeed", 0.015f);
            m.SetColor("_LavaColor", Hex(0xFF5A1E));
            m.SetFloat("_LavaAmount", 3f);
            m.SetFloat("_LavaLevel", 0.36f);
            m.SetFloat("_Specular", 0f);
            m.SetColor("_AtmoColor", Hex(0xFF9A5A));
            m.SetFloat("_AtmoRim", 0.8f);
            m.SetFloat("_AtmoHalo", 0.6f);
        }

        // Lune: the neutral moon. Airless grey regolith with craters.
        static void FirstLuneLook(Material m)
        {
            PlanetDefaults(m, seed: 5.1f, spin: 0.02f, tilt: 0.1f, noiseScale: 3.2f, warp: 0.4f);
            m.SetFloat("_SeaLevel", 0f);
            m.SetColor("_ColorLow", Hex(0x4B4E5C));
            m.SetColor("_ColorMid", Hex(0x8C8F9E));
            m.SetColor("_ColorHigh", Hex(0xC5C7D4));
            m.SetFloat("_IceAmount", 0f);
            m.SetFloat("_CloudAmount", 0f);
            m.SetFloat("_CraterAmount", 0.9f);
            m.SetFloat("_CraterScale", 4.5f);
            m.SetFloat("_Specular", 0f);
            m.SetColor("_AtmoColor", Hex(0xAAB4D0));
            m.SetFloat("_AtmoRim", 0.25f);
            m.SetFloat("_AtmoHalo", 0.18f);
        }

        // Cinder: a dark basalt rock, pocked with craters, its outline uneven.
        static void FirstCinderLook(Material m)
        {
            PlanetDefaults(m, seed: 2.3f, spin: 0.012f, tilt: 0.5f, noiseScale: 3.8f, warp: 1.1f);
            m.SetFloat("_Lumps", 0.22f);
            m.SetFloat("_SeaLevel", 0f);
            m.SetColor("_ColorLow", Hex(0x2B2723));
            m.SetColor("_ColorMid", Hex(0x5E554B));
            m.SetColor("_ColorHigh", Hex(0x9A8F80));
            m.SetFloat("_IceAmount", 0f);
            m.SetFloat("_CloudAmount", 0f);
            m.SetFloat("_CraterAmount", 1f);
            m.SetFloat("_CraterScale", 6f);
            m.SetFloat("_Specular", 0f);
            m.SetColor("_AtmoColor", Hex(0x8A8070));
            m.SetFloat("_AtmoRim", 0.1f);
            m.SetFloat("_AtmoHalo", 0f);
        }

        // Ferrite: a rust-brown iron rock, more lopsided than Cinder.
        static void FirstFerriteLook(Material m)
        {
            PlanetDefaults(m, seed: 6.6f, spin: 0.016f, tilt: -0.4f, noiseScale: 4.2f, warp: 0.9f);
            m.SetFloat("_Lumps", 0.28f);
            m.SetFloat("_SeaLevel", 0f);
            m.SetColor("_ColorLow", Hex(0x3A2418));
            m.SetColor("_ColorMid", Hex(0x7A4A2E));
            m.SetColor("_ColorHigh", Hex(0xB88A62));
            m.SetFloat("_IceAmount", 0f);
            m.SetFloat("_CloudAmount", 0f);
            m.SetFloat("_CraterAmount", 0.8f);
            m.SetFloat("_CraterScale", 5f);
            m.SetFloat("_Specular", 0f);
            m.SetColor("_AtmoColor", Hex(0x9A7A60));
            m.SetFloat("_AtmoRim", 0.08f);
            m.SetFloat("_AtmoHalo", 0f);
        }

        static void FirstDotLook(Material dot)
        {
            dot.SetFloat("_Core", 0.5f);
            dot.SetFloat("_Halo", 0.55f);
            dot.SetFloat("_Additive", 0.7f);
        }

        static void PlanetDefaults(Material material, float seed, float spin, float tilt, float noiseScale, float warp)
        {
            material.SetFloat("_BodyRadius", 0.7f);
            material.SetFloat("_Seed", seed);
            material.SetFloat("_SpinSpeed", spin);
            material.SetFloat("_Tilt", tilt);
            material.SetFloat("_Lumps", 0f);
            material.SetFloat("_NoiseScale", noiseScale);
            material.SetFloat("_Warp", warp);
            material.SetVector("_LightDir", LightDirection);
            material.SetFloat("_Ambient", 0.035f);
            material.SetFloat("_Bands", 0f);
            material.SetFloat("_CraterAmount", 0f);
            material.SetFloat("_CityAmount", 0f);
            material.SetFloat("_LavaAmount", 0f);
        }

        // ------------------------------------------------------------------ styles

        /// <summary>
        /// Creates the world, route and fleet styles that are missing, with the defaults their types give
        /// them. Load them with <see cref="LoadStyles"/>, after any new scene has been opened.
        /// </summary>
        public static void EnsureStyles()
        {
            Directory.CreateDirectory(StylesFolder);
            AssetDatabase.Refresh();
            EnsureAsset<WorldStyle>(WorldStylePath);
            EnsureAsset<RouteStyle>(RouteStylePath);
            EnsureAsset<FleetStyle>(FleetStylePath);
            KeepOrAssignDartLook(AssetDatabase.LoadAssetAtPath<FleetStyle>(FleetStylePath));
        }

        // A mesh or material already chosen is kept, whatever it is; only an empty one is filled.
        static void KeepOrAssignDartLook(FleetStyle style)
        {
            var serialized = new SerializedObject(style);
            SerializedProperty mesh = SceneWiring.Property(serialized, "dartMesh");
            SerializedProperty material = SceneWiring.Property(serialized, "dartMaterial");
            if (mesh.objectReferenceValue == null) mesh.objectReferenceValue = EnsureDartMesh();
            if (material.objectReferenceValue == null)
            {
                List<Material> found = DartMaterials();
                if (found.Count > 0) material.objectReferenceValue = found[0];
                else Debug.LogError("Squadra: there is no Dart material in " + DartFolder + ".");
            }

            if (serialized.ApplyModifiedPropertiesWithoutUndo()) EditorUtility.SetDirty(style);
        }

        public static SceneStyles LoadStyles()
        {
            return new SceneStyles
            {
                World = AssetDatabase.LoadAssetAtPath<WorldStyle>(WorldStylePath),
                Routes = AssetDatabase.LoadAssetAtPath<RouteStyle>(RouteStylePath),
                Fleets = AssetDatabase.LoadAssetAtPath<FleetStyle>(FleetStylePath),
            };
        }

        static void EnsureAsset<T>(string path) where T : ScriptableObject
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null) return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<T>(), path);
        }

        // ------------------------------------------------------------------ world looks

        /// <summary>
        /// Loads or creates the world looks and adds to their surface lists the materials of the three
        /// surface folders, and to the backdrop list those of the backdrop folder, that are not listed yet.
        /// What is listed stays as it is. The name dictionaries are the text files in the names folder,
        /// which are edited by hand; a dictionary already chosen is kept.
        /// </summary>
        public static WorldLooks BuildWorldLooks()
        {
            var looks = AssetDatabase.LoadAssetAtPath<WorldLooks>(WorldLooksPath);
            if (looks == null)
            {
                looks = ScriptableObject.CreateInstance<WorldLooks>();
                AssetDatabase.CreateAsset(looks, WorldLooksPath);
            }

            var serialized = new SerializedObject(looks);
            FillSurfaces(SceneWiring.Property(serialized, "planets"), PlanetFolder);
            FillSurfaces(SceneWiring.Property(serialized, "moons"), MoonFolder);
            FillSurfaces(SceneWiring.Property(serialized, "asteroids"), AsteroidFolder);
            FillSurfaces(SceneWiring.Property(serialized, "backdrops"), BackdropFolder);
            KeepOrLoadNames(SceneWiring.Property(serialized, "planetNames"), "Planets");
            KeepOrLoadNames(SceneWiring.Property(serialized, "moonNames"), "Moons");
            KeepOrLoadNames(SceneWiring.Property(serialized, "asteroidNames"), "Asteroids");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(looks);
            return looks;
        }

        // Every listed material keeps its place and ID, one added by hand from another folder included;
        // only an entry whose material no longer exists is dropped. Materials in the folder that are not
        // listed yet are added after them by name, each with its file name in lower case as its ID.
        static void FillSurfaces(SerializedProperty list, string folder)
        {
            var listed = new HashSet<Material>();
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                var material = entry.FindPropertyRelative("material").objectReferenceValue as Material;
                if (material == null)
                {
                    list.DeleteArrayElementAtIndex(i);
                    continue;
                }

                listed.Add(material);
                SerializedProperty id = entry.FindPropertyRelative("id");
                if (string.IsNullOrEmpty(id.stringValue)) id.stringValue = material.name.ToLowerInvariant();
            }

            // Only the folder itself: a subfolder is not part of the pool.
            var added = new List<Material>();
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetDirectoryName(path)?.Replace('\\', '/') != folder) continue;
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null && !listed.Contains(material)) added.Add(material);
            }

            added.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            foreach (Material material in added)
            {
                list.arraySize++;
                SerializedProperty entry = list.GetArrayElementAtIndex(list.arraySize - 1);
                entry.FindPropertyRelative("id").stringValue = material.name.ToLowerInvariant();
                entry.FindPropertyRelative("material").objectReferenceValue = material;
            }
        }

        static void KeepOrLoadNames(SerializedProperty names, string type)
        {
            if (names.objectReferenceValue != null) return;

            string path = NamesFolder + "/" + type + ".txt";
            var dictionary = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (dictionary == null) Debug.LogWarning("Squadra: the name dictionary " + path + " is missing.");
            names.objectReferenceValue = dictionary;
        }

        // ------------------------------------------------------------------ post-processing

        /// <summary>
        /// The post-processing profile the scenes share, created with the game's first grade when it is
        /// missing. An existing profile is returned as it is.
        /// </summary>
        public static VolumeProfile EnsureVolumeProfile()
        {
            string path = SettingsFolder + "/SpaceVolume.asset";
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (existing != null) return existing;

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
