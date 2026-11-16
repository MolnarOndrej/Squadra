using System;
using System.Collections.Generic;
using Squadra.Campaign;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Squadra
{
    /// <summary>
    /// The ordered list of campaign missions. A mission's place in the list gives its "Mission N" label;
    /// its id is what progress is tracked by, so keep ids stable when reordering. Each mission is a Mission
    /// Editor file assigned here explicitly, named like its id, and every mission is played in the one
    /// shared gameplay scene. A mission without a file appears on the map as a locked preview; one whose
    /// file cannot be prepared is shown unavailable, with the reason.
    /// </summary>
    [CreateAssetMenu(menuName = "Squadra/Mission Catalog", fileName = "Missions")]
    public sealed class MissionCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Stable id that progress is tracked by. Never reuse or change it once shipped.")]
            public string id;

            [Tooltip("The mission file from the Mission Editor, named like the id (mission-01.json for mission-01). "
                + "Leave empty while the mission is not written yet.")]
            public TextAsset definition;
        }

        [SerializeField] List<Entry> missions = new List<Entry>();

        [Tooltip("The surfaces and names the missions' worlds are dressed in.")]
        [SerializeField] WorldLooks looks;

#if UNITY_EDITOR
        [Tooltip("The scene every mission is played in. It must be in the build.")]
        [SerializeField] UnityEditor.SceneAsset gameplayScene;
#endif

        [Tooltip("Project path of the gameplay scene. Filled in from Gameplay Scene.")]
        [SerializeField] string gameplayScenePath;

        public int Count => missions.Count;

        public string IdAt(int index) => missions[index].id;

        /// <summary>The mission's file, or null while it has none.</summary>
        public TextAsset DefinitionAt(int index) => missions[index].definition;

        public WorldLooks Looks => looks;

        /// <summary>The scene every mission is played in.</summary>
        public string GameplayScenePath => gameplayScenePath;

        /// <summary>"Mission 1" for the first entry, and so on.</summary>
        public static string LabelFor(int index) => "Mission " + (index + 1);

        /// <summary>The catalog index of a mission id, or -1.</summary>
        public int IndexOf(string missionId)
        {
            for (int i = 0; i < missions.Count; i++)
            {
                if (missions[i].id == missionId) return i;
            }

            return -1;
        }

        /// <summary>
        /// The catalog as the campaign rules see it, in order: a mission without a file is a preview, one
        /// that <see cref="MissionLibrary"/> could not prepare has failed with its reason, and one that was
        /// prepared is ready when the gameplay scene is in the build.
        /// </summary>
        public MissionSlot[] ToSlots()
        {
            bool sceneInBuild = !string.IsNullOrEmpty(gameplayScenePath)
                && SceneUtility.GetBuildIndexByScenePath(gameplayScenePath) >= 0;

            var slots = new MissionSlot[missions.Count];
            for (int i = 0; i < slots.Length; i++)
            {
                string id = missions[i].id;
                string problem = MissionLibrary.ProblemOf(id);
                if (missions[i].definition == null)
                    slots[i] = new MissionSlot(id, false);
                else if (problem != null)
                    slots[i] = MissionSlot.Failed(id, problem);
                else if (!sceneInBuild)
                    slots[i] = MissionSlot.Failed(id, LabelFor(i) + " cannot be played: the gameplay scene \""
                        + gameplayScenePath + "\" is not in the build.");
                else
                    slots[i] = new MissionSlot(id, MissionLibrary.TryGet(id, out _));
            }

            return slots;
        }

        /// <summary>
        /// Builds a catalog in memory, for example a longer one to try out paging. Pass a null definition
        /// for a mission that is not written yet.
        /// </summary>
        public static MissionCatalog Create(string gameplayScenePath, WorldLooks looks,
            params (string id, TextAsset definition)[] entries)
        {
            var catalog = CreateInstance<MissionCatalog>();
            catalog.looks = looks;
            catalog.gameplayScenePath = gameplayScenePath ?? string.Empty;
#if UNITY_EDITOR
            if (catalog.gameplayScenePath.Length > 0)
                catalog.gameplayScene = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(gameplayScenePath);
#endif
            foreach ((string id, TextAsset definition) in entries)
                catalog.missions.Add(new Entry { id = id, definition = definition });

            return catalog;
        }

#if UNITY_EDITOR
        // The Gameplay Scene field is what the designer edits; the path the game reads follows it, also
        // when the scene asset is moved or renamed.
        void OnValidate()
        {
            gameplayScenePath = gameplayScene != null ? UnityEditor.AssetDatabase.GetAssetPath(gameplayScene) : string.Empty;
        }
#endif
    }
}
