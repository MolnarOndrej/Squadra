using System;
using System.IO;
using Squadra.MissionEditing;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// The shell of the standalone Windows mission editor: native Open and Save As dialogs, and an exit
    /// that ends the process. Closing the window, Alt+F4 and a Quit command all go through the
    /// session's quit protection first, so unsaved work is offered for saving before anything closes.
    /// It cannot protect against the process being killed or the power going.
    /// </summary>
    public sealed class StandaloneMissionEditorShell : MissionEditorShell
    {
        const string LastFolderKey = "missionEditor.lastFolder";

        MissionEditorSession session;
        bool exitGranted;
        bool answeringQuit;

        void Awake()
        {
            // A desktop tool: a window beside the game, drawn at the display's pace and no faster.
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = 60;
#if !UNITY_EDITOR
            if (Screen.fullScreenMode != FullScreenMode.Windowed) Screen.fullScreenMode = FullScreenMode.Windowed;
#endif
        }

        public override void Attach(MissionEditorSession editorSession)
        {
            session = editorSession;
#if !UNITY_EDITOR
            Application.wantsToQuit -= OnWantsToQuit;
            Application.wantsToQuit += OnWantsToQuit;
#endif
        }

        void OnDestroy()
        {
            Application.wantsToQuit -= OnWantsToQuit;
        }

        // The window's close button, Alt+F4 and the system asking the program to end all arrive here.
        // The close is refused until the session has granted the exit; when nothing is unsaved the
        // session grants it within this very call and the close goes ahead.
        bool OnWantsToQuit()
        {
            if (exitGranted || session == null) return true;

            answeringQuit = true;
            session.RequestQuit();
            answeringQuit = false;
            return exitGranted;
        }

        public override void Exit()
        {
            exitGranted = true;
            if (!answeringQuit) MainMenu.QuitApplication();
        }

        public override void ChooseFileToOpen(Action<string> chosen)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            string path = WindowsFileDialogs.Open("Open Mission", LastFolder);
            Remember(path);
            chosen(path);
#else
            Debug.LogWarning("Squadra: the mission editor's file dialogs are only available on Windows.");
            chosen(null);
#endif
        }

        public override void ChooseFileToSave(string suggestedFileName, Action<SaveDestination> chosen)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            // The dialog itself asks before an existing file is picked, so its answer is already consent.
            string path = WindowsFileDialogs.Save("Save Mission As", LastFolder, suggestedFileName);
            Remember(path);
            chosen(path != null ? new SaveDestination(path, true) : null);
#else
            Debug.LogWarning("Squadra: the mission editor's file dialogs are only available on Windows.");
            chosen(null);
#endif
        }

        static string LastFolder
        {
            get
            {
                string folder = PlayerPrefs.GetString(LastFolderKey, "");
                return folder.Length > 0 && Directory.Exists(folder)
                    ? folder
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
        }

        static void Remember(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            string folder = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(folder)) return;

            PlayerPrefs.SetString(LastFolderKey, folder);
            PlayerPrefs.Save();
        }
    }
}
