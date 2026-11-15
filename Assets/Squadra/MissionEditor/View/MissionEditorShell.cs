using System;
using Squadra.MissionEditing;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// The application around the mission editor, as a scene component: where mission files are kept,
    /// how the author picks one, and how the editor closes. The editor view asks its shell for these
    /// and for nothing else, so the same editor scene content can sit in the standalone editor or be
    /// brought into the game with a shell of the game's own.
    /// </summary>
    public abstract class MissionEditorShell : MonoBehaviour, IMissionEditorShell
    {
        IMissionFileStore files;

        /// <summary>Where mission files are read and written. The local disk unless a shell says otherwise.</summary>
        public virtual IMissionFileStore Files => files ?? (files = new LocalMissionFiles());

        /// <summary>Called once by the editor view when its session exists.</summary>
        public virtual void Attach(MissionEditorSession session)
        {
        }

        public abstract void ChooseFileToOpen(Action<string> chosen);

        public abstract void ChooseFileToSave(string suggestedFileName, Action<SaveDestination> chosen);

        public abstract void Exit();
    }
}
