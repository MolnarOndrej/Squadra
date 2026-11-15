using System;
using System.Collections.Generic;
using Squadra.Campaign;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Squadra
{
    /// <summary>
    /// The ordered list of campaign missions. A mission's place in the list gives its "Mission N" label;
    /// its id is what progress is tracked by, so keep ids stable when reordering. A mission without a
    /// gameplay scene, or whose scene is not in the build, appears on the map as a locked preview.
    /// </summary>
    [CreateAssetMenu(menuName = "Squadra/Mission Catalog", fileName = "Missions")]
    public sealed class MissionCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Stable id that progress is tracked by. Never reuse or change it once shipped.")]
            public string id;

#if UNITY_EDITOR
            [Tooltip("The mission's gameplay scene. Leave empty while the scenario is not built yet.")]
            public UnityEditor.SceneAsset scene;
#endif

            [Tooltip("Project path of the gameplay scene. Filled in from Scene.")]
            public string scenePath;
        }

        [SerializeField] List<Entry> missions = new List<Entry>();

        public int Count => missions.Count;

        public string IdAt(int index) => missions[index].id;

        public string ScenePathAt(int index) => missions[index].scenePath;

        /// <summary>"Mission 1" for the first entry, and so on.</summary>
        public static string LabelFor(int index) => "Mission " + (index + 1);

        /// <summary>True when the mission has a gameplay scene and that scene is included in the build.</summary>
        public bool IsReady(int index)
        {
            string path = missions[index].scenePath;
            return !string.IsNullOrEmpty(path) && SceneUtility.GetBuildIndexByScenePath(path) >= 0;
        }

        /// <summary>The catalog as the campaign rules see it, in order.</summary>
        public MissionSlot[] ToSlots()
        {
            var slots = new MissionSlot[missions.Count];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = new MissionSlot(missions[i].id, IsReady(i));
            return slots;
        }

        /// <summary>
        /// Builds a catalog in memory, for example a longer one to try out paging. Pass a null scene path
        /// for a mission that has no scenario yet.
        /// </summary>
        public static MissionCatalog Create(params (string id, string scenePath)[] entries)
        {
            var catalog = CreateInstance<MissionCatalog>();
            foreach ((string id, string scenePath) in entries)
            {
                var entry = new Entry { id = id, scenePath = scenePath ?? string.Empty };
#if UNITY_EDITOR
                if (entry.scenePath.Length > 0)
                    entry.scene = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(entry.scenePath);
#endif
                catalog.missions.Add(entry);
            }

            return catalog;
        }

#if UNITY_EDITOR
        // The Scene field is what the designer edits; the path the game reads follows it, also when
        // the scene asset is moved or renamed.
        void OnValidate()
        {
            foreach (Entry entry in missions)
                entry.scenePath = entry.scene != null ? UnityEditor.AssetDatabase.GetAssetPath(entry.scene) : string.Empty;
        }
#endif
    }
}
