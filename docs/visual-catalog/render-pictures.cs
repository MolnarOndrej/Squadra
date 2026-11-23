// Renders the pictures of docs/visual-catalog.md, and gameplay-scale comparisons for judging new looks.
//
// Run it in the open editor, out of Play Mode, with no unsaved scene:
//
//   unity command script eval --project-path . -- --file docs/visual-catalog/render-pictures.cs
//
// It draws with the game's renderer: the Sandbox scene's camera, space backdrop and post-processing,
// world surfaces on the same quad a world is drawn on and Darts with the game's DartBatch, in the owner
// tint and size the FleetStyle gives them. Everything else in the scene is hidden while it renders.
// Nothing is saved: the scenes that were open are opened again from disk at the end, and no asset is
// changed (a Dart that is not the FleetStyle's is drawn with a temporary copy of it).
//
// The looks of the game are the surfaces and backdrops listed in WorldLooks (Squadra > Rebuild Mission
// Scene lists the materials put in Materials/Planets, Moons, Asteroids and Backdrops) and every material
// in Materials/Darts, drawn with the mesh of the same name in Visuals/Meshes, or else the FleetStyle's.
//
// This file lives outside Assets, so Unity never compiles it or puts it in a build.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Squadra;
using Squadra.Missions;
using Squadra.Preparation;
using Squadra.Simulation;

// ---------------------------------------------------------------------------------- settings

// The catalog pictures, docs/visual-catalog/<type>-<id>.png, of every look of the game.
bool catalogPictures = true;

// Gameplay-scale strips, Recordings/VisualCatalog/compare-<type>.png (ignored by git): every look of a
// type side by side, left to right in the order logged, at the type's default size and at the scale the
// Sandbox battlefield is shown at in a Game view 1080 pixels high; Darts as a Blue, a Red and a Green one.
// Candidates are included, and their close-ups are written there too. For judging new looks, not for the
// catalog; check them in the Sandbox Game view as well, next to rings, labels, routes and fleets.
bool comparison = false;

// Looks still being worked on, by asset path: drawn in the comparison only, never into docs. The type
// is the nearest folder in the path named Planets, Moons, Asteroids, Backdrops or Darts, so a candidate
// can sit in a subfolder the scene builder does not list, such as Materials/Planets/Work. The ID is the
// file name in lower case. A Dart is "material" or "material;mesh"; without a mesh it is drawn with the
// FleetStyle's. The FleetStyle's mesh rotation and Dot Radius apply to every Dart.
string[] candidates =
{
    // "Assets/Squadra/Visuals/Materials/Planets/Work/Aurora.mat",
    // "Assets/Squadra/Visuals/Materials/Darts/Work/Arrow.mat;Assets/Squadra/Visuals/Meshes/Arrow.asset",
};

// ---------------------------------------------------------------------------------- the run

const string SandboxScene = "Assets/Squadra/Sandbox/Scenes/Sandbox.unity";
const string LooksPath = "Assets/Squadra/Visuals/WorldLooks.asset";
const string FleetStylePath = "Assets/Squadra/Visuals/Styles/FleetStyle.asset";
const string DartFolder = "Assets/Squadra/Visuals/Materials/Darts";
const string MeshFolder = "Assets/Squadra/Visuals/Meshes";
string catalogDir = Path.GetFullPath("docs/visual-catalog");
string comparisonDir = Path.GetFullPath("Recordings/VisualCatalog");

if (EditorApplication.isPlayingOrWillChangePlaymode) return "Stop Play Mode first.";
for (int i = 0; i < EditorSceneManager.sceneCount; i++)
{
    var open = EditorSceneManager.GetSceneAt(i);
    if (open.isDirty || string.IsNullOrEmpty(open.path))
        return $"Save or close '{(string.IsNullOrEmpty(open.path) ? "Untitled" : open.path)}' first; nothing was rendered.";
}

var log = new StringBuilder();
var looks = AssetDatabase.LoadAssetAtPath<WorldLooks>(LooksPath);
var fleetStyle = AssetDatabase.LoadAssetAtPath<FleetStyle>(FleetStylePath);
if (looks == null || fleetStyle == null) return $"Missing {LooksPath} or {FleetStylePath}.";

// The world types, with the name of the folder their materials sit in.
var bodyTypes = new[]
{
    (type: "planet", folder: "Planets", kind: NodeKind.Planet, missionType: MissionNodeType.Planet),
    (type: "moon", folder: "Moons", kind: NodeKind.Moon, missionType: MissionNodeType.Moon),
    (type: "asteroid", folder: "Asteroids", kind: NodeKind.Asteroid, missionType: MissionNodeType.Asteroid),
};
const string BackdropTypeFolder = "Backdrops";
const string DartTypeFolder = "Darts";

var bodies = new List<(string type, MissionNodeType missionType, string id, Material material, bool candidate)>();
var backdrops = new List<(string id, Material material, bool candidate)>();
var darts = new List<(string id, Material material, Mesh mesh, bool candidate)>();

// The looks of the game: the pools missions are dealt from, and the Dart materials.
WorldPools pools = looks.ToPools();
foreach (var bodyType in bodyTypes)
{
    foreach (string id in pools.For(bodyType.missionType).VariantIds)
    {
        if (looks.TryGetSurface(bodyType.kind, id, out Material material))
            bodies.Add((bodyType.type, bodyType.missionType, id, material, false));
    }
}

foreach (string id in pools.Backdrops)
{
    if (looks.TryGetBackdrop(id, out Material material)) backdrops.Add((id, material, false));
}

Mesh DartMeshFor(Material material)
{
    if (material == fleetStyle.DartMaterial) return fleetStyle.DartMesh;
    var own = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshFolder}/{material.name}.asset");
    return own != null ? own : fleetStyle.DartMesh;
}

foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { DartFolder }))
{
    string path = AssetDatabase.GUIDToAssetPath(guid);
    if (Path.GetDirectoryName(path)?.Replace('\\', '/') != DartFolder) continue; // the folder itself, not its subfolders
    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
    if (material != null) darts.Add((material.name.ToLowerInvariant(), material, DartMeshFor(material), false));
}

darts.Sort((a, b) => string.CompareOrdinal(a.id, b.id));

string[] typeFolders = bodyTypes.Select(t => t.folder).Append(BackdropTypeFolder).Append(DartTypeFolder).ToArray();
foreach (string entry in candidates)
{
    string[] parts = entry.Split(';');
    string path = parts[0].Trim();
    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
    if (material == null) return $"Candidate material not found: {path}";
    if (bodies.Any(b => b.material == material) || backdrops.Any(b => b.material == material) || darts.Any(d => d.material == material))
    {
        log.AppendLine($"{path} is already a look of the game; not drawn again as a candidate.");
        continue;
    }

    string id = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
    string folder = (Path.GetDirectoryName(path) ?? string.Empty).Replace('\\', '/').Split('/')
        .LastOrDefault(f => typeFolders.Contains(f));
    if (folder == null)
        return $"Cannot tell the type of {path}: keep it under a folder named {string.Join(", ", typeFolders)}.";

    if (folder == BackdropTypeFolder)
    {
        backdrops.Add((id, material, true));
    }
    else if (folder == DartTypeFolder)
    {
        string meshPath = parts.Length > 1 ? parts[1].Trim() : null;
        Mesh mesh = meshPath != null ? AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) : fleetStyle.DartMesh;
        if (mesh == null) return $"Candidate mesh not found: {meshPath}";
        darts.Add((id, material, mesh, true));
    }
    else
    {
        var bodyType = bodyTypes.First(t => t.folder == folder);
        bodies.Add((bodyType.type, bodyType.missionType, id, material, true));
    }
}

SceneSetup[] openScenes = EditorSceneManager.GetSceneManagerSetup();

// Everything made here, destroyed at the end even when a step fails.
var made = new List<UnityEngine.Object>();
T Made<T>(T o) where T : UnityEngine.Object
{
    made.Add(o);
    return o;
}

try
{
    var sandbox = EditorSceneManager.OpenScene(SandboxScene, OpenSceneMode.Single);
    var preview = UnityEngine.Object.FindAnyObjectByType<SandboxPreview>();
    var backdrop = UnityEngine.Object.FindAnyObjectByType<Backdrop>();
    if (preview == null || backdrop == null || preview.Rig == null) return "The Sandbox scene has no SandboxPreview, Backdrop or camera rig.";
    Camera cam = preview.Rig.Camera;
    var backdropRenderer = backdrop.GetComponent<MeshRenderer>();
    Material sceneBackdrop = backdropRenderer.sharedMaterial;
    int stageOrder = backdropRenderer.sortingOrder + 10;

    // Frame the battlefield as a 16:9 Game view shows it. The backdrop stays where this puts it, sun
    // included, for every picture: a close-up shows the sky that is really behind the middle of the field.
    cam.aspect = 16f / 9f;
    preview.Redraw();
    Vector2 middle = cam.transform.position;
    float fieldHalfHeight = cam.orthographicSize;
    float pixelsPerUnit = 1080f / (2f * fieldHalfHeight);

    var stage = Made(new GameObject("Visual Catalog Stage") { hideFlags = HideFlags.DontSave });

    // Hidden for good: the scene is opened again from disk at the end. FindObjectsOfTypeAll, as the
    // SandboxPreview's worlds and fleets are DontSave objects, which FindObjectsByType leaves out.
    foreach (Renderer r in Resources.FindObjectsOfTypeAll<Renderer>())
    {
        if (r.gameObject.scene == sandbox && r != backdropRenderer) r.enabled = false;
    }

    Texture2D Render(Vector2 center, float halfHeight, int width, int height)
    {
        cam.aspect = width / (float)height;
        cam.transform.position = new Vector3(center.x, center.y, cam.transform.position.z);
        cam.orthographicSize = halfHeight;
        const int Supersample = 2;
        var desc = new RenderTextureDescriptor(width * Supersample, height * Supersample, RenderTextureFormat.ARGB32, 24)
        {
            sRGB = true,
            msaaSamples = 4,
        };
        // Taken before rendering, which can leave the picture's own texture active.
        RenderTexture previous = RenderTexture.active;
        RenderTexture full = RenderTexture.GetTemporary(desc);
        RenderTexture small = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        try
        {
            var request = new RenderPipeline.StandardRequest { destination = full };
            if (!RenderPipeline.SupportsRenderRequest(cam, request)) throw new InvalidOperationException("The render pipeline refused the render request.");
            RenderPipeline.SubmitRenderRequest(cam, request);

            Graphics.Blit(full, small);
            RenderTexture.active = small;
            var picture = Made(new Texture2D(width, height, TextureFormat.RGB24, false));
            picture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            picture.Apply();
            return picture;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(small);
            RenderTexture.ReleaseTemporary(full);
        }
    }

    void Save(Texture2D picture, string directory, string file, string note)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, file), picture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(picture);
        log.AppendLine($"{Path.GetFileName(directory)}/{file}{note}");
    }

    Texture2D Strip(List<Texture2D> cells)
    {
        int cellWidth = cells[0].width, cellHeight = cells[0].height;
        var strip = Made(new Texture2D(cellWidth * cells.Count, cellHeight, TextureFormat.RGB24, false));
        for (int i = 0; i < cells.Count; i++)
        {
            strip.SetPixels(i * cellWidth, 0, cellWidth, cellHeight, cells[i].GetPixels());
            UnityEngine.Object.DestroyImmediate(cells[i]);
        }

        strip.Apply();
        return strip;
    }

    string Mark(bool candidate) => candidate ? " (candidate)" : "";

    // A world's body: the surface quad, sized by the game's own rule.
    MeshRenderer body = Shapes.CreateQuad("Body", stage.transform, null, stageOrder);
    body.transform.position = new Vector3(middle.x, middle.y, 0f);
    body.enabled = false;
    void ShowBody(Material material, float radius)
    {
        float side = Shapes.BodyQuadSide(material, radius);
        body.sharedMaterial = material;
        body.transform.localScale = new Vector3(side, side, 1f);
        body.enabled = true;
    }

    // Darts: a batch of the game's own, drawn with a temporary copy of the FleetStyle for another look.
    var dartsObject = Made(new GameObject("Darts") { hideFlags = HideFlags.DontSave });
    dartsObject.transform.SetParent(stage.transform, false);
    var dartRenderer = dartsObject.AddComponent<MeshRenderer>();
    dartRenderer.sortingOrder = stageOrder;
    dartRenderer.shadowCastingMode = ShadowCastingMode.Off;
    dartRenderer.receiveShadows = false;
    var dartFilter = dartsObject.AddComponent<MeshFilter>();
    var dartBatch = new DartBatch(dartFilter, dartRenderer);
    Made(dartFilter.sharedMesh);
    FleetStyle StyleFor(Material material, Mesh mesh)
    {
        if (material == fleetStyle.DartMaterial && mesh == fleetStyle.DartMesh) return fleetStyle;
        FleetStyle copy = Made(UnityEngine.Object.Instantiate(fleetStyle));
        copy.hideFlags = HideFlags.DontSave;
        // FleetStyle has no setters: these are its serialized field names, which SquadraAssetBuilder sets too.
        var so = new SerializedObject(copy);
        SerializedProperty materialProperty = so.FindProperty("dartMaterial");
        SerializedProperty meshProperty = so.FindProperty("dartMesh");
        if (materialProperty == null || meshProperty == null)
            throw new InvalidOperationException("FleetStyle has no dartMaterial or dartMesh field; update this script.");
        materialProperty.objectReferenceValue = material;
        meshProperty.objectReferenceValue = mesh;
        so.ApplyModifiedPropertiesWithoutUndo();
        return copy;
    }

    // Null, with the reason logged, for a Dart look the game could not draw either.
    FleetStyle DrawableStyle(string id, Material material, Mesh mesh)
    {
        FleetStyle style = StyleFor(material, mesh);
        string problem = style.DartProblem();
        if (problem == null) return style;
        log.AppendLine($"dart-{id} not drawn: {problem}");
        return null;
    }

    void ShowDarts(FleetStyle style, IEnumerable<(Vector2 at, Faction owner)> placed)
    {
        dartBatch.Begin(style);
        foreach (var (at, owner) in placed)
            dartBatch.Add(at, 0f, style.DotSize, style.StationedTint(Palette.Of(owner))); // nose to the right
        dartBatch.End();
    }

    // The battlefield framing measured above. Backdrop.Show sizes the backdrop and places its sun for the
    // camera's current view, so it must be back before every Show: a pass of close-ups leaves the camera
    // on its last Dart.
    void FrameField()
    {
        cam.aspect = 16f / 9f;
        cam.orthographicSize = fieldHalfHeight;
        cam.transform.position = new Vector3(middle.x, middle.y, cam.transform.position.z);
    }

    // The close-ups, as the catalog shows them: the game's looks, or the candidates.
    void CloseUps(string directory, bool ofCandidates)
    {
        FrameField();

        // Backdrop: the whole battlefield framing with nothing in front of it.
        foreach (var (id, material, candidate) in backdrops.Where(b => b.candidate == ofCandidates))
        {
            backdrop.Show(material);
            Save(Render(middle, fieldHalfHeight, 480, 270), directory, $"backdrop-{id}.png", Mark(candidate));
        }

        backdrop.Show(sceneBackdrop);

        // Bodies: every type at radius 1 in a view 3 units high, to compare surfaces, not sizes.
        foreach (var b in bodies.Where(b => b.candidate == ofCandidates))
        {
            ShowBody(b.material, 1f);
            Save(Render(middle, 1.5f, 256, 256), directory, $"{b.type}-{b.id}.png", Mark(b.candidate));
        }

        body.enabled = false;

        // Dart: one Blue Dart, nose to the right, filling about half the picture.
        foreach (var (id, material, mesh, candidate) in darts.Where(d => d.candidate == ofCandidates))
        {
            FleetStyle style = DrawableStyle(id, material, mesh);
            if (style == null) continue;
            ShowDarts(style, new[] { (middle, Faction.Blue) });
            Save(Render(middle, style.DotSize, 256, 256), directory, $"dart-{id}.png", Mark(candidate));
        }

        ShowDarts(fleetStyle, Array.Empty<(Vector2, Faction)>());
    }

    if (catalogPictures) CloseUps(catalogDir, false);

    if (comparison)
    {
        CloseUps(comparisonDir, true);

        const float Cell = 3f; // world units per look, wide and high
        int cellPixels = Mathf.RoundToInt(Cell * pixelsPerUnit);
        foreach (var group in bodies.GroupBy(b => b.type))
        {
            var cells = new List<Texture2D>();
            foreach (var b in group)
            {
                ShowBody(b.material, (float)MissionRules.DefaultSize(b.missionType));
                cells.Add(Render(middle, Cell / 2f, cellPixels, cellPixels));
            }

            Save(Strip(cells), comparisonDir, $"compare-{group.Key}.png",
                ": " + string.Join(", ", group.Select(b => b.id + Mark(b.candidate))));
        }

        body.enabled = false;

        // Darts: a Blue, a Red and a Green one per look, in a cell 1 unit wide.
        var dartCells = new List<Texture2D>();
        var drawn = new List<string>();
        foreach (var (id, material, mesh, candidate) in darts)
        {
            FleetStyle style = DrawableStyle(id, material, mesh);
            if (style == null) continue;
            float step = style.DotSize * 2.5f;
            ShowDarts(style, new[]
            {
                (middle + new Vector2(0f, step), Faction.Blue),
                (middle, Faction.Red),
                (middle - new Vector2(0f, step), Faction.Green),
            });
            dartCells.Add(Render(middle, 0.5f, Mathf.RoundToInt(pixelsPerUnit), Mathf.RoundToInt(pixelsPerUnit)));
            drawn.Add(id + Mark(candidate));
        }

        ShowDarts(fleetStyle, Array.Empty<(Vector2, Faction)>());
        if (dartCells.Count > 0) Save(Strip(dartCells), comparisonDir, "compare-dart.png", ": " + string.Join(", ", drawn));

        log.AppendLine($"Gameplay scale: {pixelsPerUnit:0.#} pixels per world unit.");
    }
}
finally
{
    foreach (UnityEngine.Object o in made) if (o != null) UnityEngine.Object.DestroyImmediate(o);
    // An empty scene first, so restoring opens every scene from disk: a scene still loaded, such as the
    // Sandbox scene when it was open before the run, could otherwise keep the changes made here.
    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    if (openScenes.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(openScenes);
}

return log.ToString();
