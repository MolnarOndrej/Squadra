using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace Squadra.EditorTools
{
    /// <summary>
    /// Assembles the mission editor's scene and builds the standalone Windows mission editor from it.
    ///
    /// <c>Squadra &gt; Rebuild Mission Editor Scene</c> writes the scene from code: a camera that only
    /// clears the window, and one object carrying the editor's UI document, its view and the
    /// standalone shell. The layout and look live in the <c>UI</c> folder beside this one and are left alone.
    ///
    /// <c>Squadra &gt; Build Mission Editor (Windows)</c> builds <c>SquadraMissionEditor.exe</c> with
    /// that scene as its only scene. The game's own scene list in the build settings is never read or
    /// changed, so the game still starts in its menu. The player settings a windowed desktop tool
    /// needs are applied for the duration of the build and put back afterwards.
    /// </summary>
    public static class SquadraMissionEditorBuilder
    {
        public const string ScenePath = Folder + "/Scenes/MissionEditor.unity";
        public const string BuildFolder = "Builds/MissionEditor";
        public const string ExecutableName = "SquadraMissionEditor.exe";

        const string Folder = SquadraSceneBuilder.Root + "/MissionEditor";
        const string UiFolder = Folder + "/UI";
        const string LayoutPath = UiFolder + "/MissionEditor.uxml";
        const string ThemePath = UiFolder + "/MissionEditorTheme.tss";
        const string PanelPath = UiFolder + "/MissionEditorPanel.asset";

        // Its own product name gives the editor its own window title, saved window size and settings.
        const string ProductName = "Squadra Mission Editor";
        const int WindowWidth = 1600;
        const int WindowHeight = 900;

        [MenuItem("Squadra/Rebuild Mission Editor Scene")]
        public static void BuildScene()
        {
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
            if (layout == null || EnsurePanel() == null)
            {
                Debug.LogError("Squadra: the mission editor's layout or theme is missing from " + UiFolder);
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Loaded only now: opening the new scene drops assets that were created a moment ago.
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);

            // Nothing of the game is drawn here: the camera clears the window and the interface covers it.
            var cameraObject = new GameObject("Camera") { tag = "MainCamera" };
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.Void;
            camera.cullingMask = 0;
            camera.allowHDR = false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;

            var editorObject = new GameObject("Mission Editor");
            var document = editorObject.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = layout;
            var shell = editorObject.AddComponent<StandaloneMissionEditorShell>();
            var view = editorObject.AddComponent<MissionEditorView>();
            SquadraSceneBuilder.Wire(view, "document", document);
            SquadraSceneBuilder.Wire(view, "shell", shell);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Squadra: rebuilt " + ScenePath);
        }

        /// <summary>
        /// The editor's panel settings: one interface pixel is one screen pixel at 100% display scaling,
        /// and grows with the display's scaling, so a larger window shows more map rather than larger
        /// controls. Created when missing; null without the theme.
        /// </summary>
        static PanelSettings EnsurePanel()
        {
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme == null) return null;

            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelPath);
            }

            panel.themeStyleSheet = theme;
            panel.scaleMode = PanelScaleMode.ConstantPhysicalSize;
            panel.referenceDpi = 96f;
            panel.fallbackDpi = 96f;
            EditorUtility.SetDirty(panel);
            AssetDatabase.SaveAssets();
            return panel;
        }

        [MenuItem("Squadra/Build Mission Editor (Windows)")]
        public static void BuildPlayer()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError("Squadra: run Squadra > Rebuild Mission Editor Scene first; " + ScenePath + " is missing.");
                return;
            }

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string product = PlayerSettings.productName;
            FullScreenMode fullScreen = PlayerSettings.fullScreenMode;
            bool resizable = PlayerSettings.resizableWindow;
            int width = PlayerSettings.defaultScreenWidth;
            int height = PlayerSettings.defaultScreenHeight;
            try
            {
                PlayerSettings.productName = ProductName;
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.resizableWindow = true;
                PlayerSettings.defaultScreenWidth = WindowWidth;
                PlayerSettings.defaultScreenHeight = WindowHeight;

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = BuildFolder + "/" + ExecutableName,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                };

                BuildSummary summary = BuildPipeline.BuildPlayer(options).summary;
                if (summary.result == BuildResult.Succeeded)
                    Debug.Log("Squadra: built the mission editor at " + Path.GetFullPath(options.locationPathName));
                else
                    Debug.LogError("Squadra: the mission editor build ended as " + summary.result + " with "
                        + summary.totalErrors + " error(s).");
            }
            finally
            {
                PlayerSettings.productName = product;
                PlayerSettings.fullScreenMode = fullScreen;
                PlayerSettings.resizableWindow = resizable;
                PlayerSettings.defaultScreenWidth = width;
                PlayerSettings.defaultScreenHeight = height;
                AssetDatabase.SaveAssets();
            }
        }
    }
}
