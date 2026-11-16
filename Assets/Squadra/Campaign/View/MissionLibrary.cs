using System.Collections.Generic;
using Squadra.Preparation;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// The campaign's missions as prepared for this session. The menu prepares every assigned mission once,
    /// locked ones included, before it takes any input; afterwards a mission is only looked up. The worlds'
    /// looks are dealt then and kept, so a restart, a return to the menu and a replay all show the same
    /// named worlds. A mission that cannot be prepared keeps its reason, and the reason is logged once. Nothing
    /// here is saved: a new launch prepares, and deals, afresh.
    /// </summary>
    [AutoStaticsCleanup]
    public static partial class MissionLibrary
    {
        sealed class Prepared
        {
            public TextAsset Source;
            public PreparationOutcome Outcome;
            public string Problem;
        }

        static Dictionary<string, Prepared> missions;
        static Dictionary<TextAsset, PreparationOutcome> standalone;
        static System.Random random;

        static Dictionary<string, Prepared> Missions => missions ??= new Dictionary<string, Prepared>();
        static System.Random Random => random ??= new System.Random();

        /// <summary>
        /// Prepares every mission of the catalog that has a file and has not been prepared from that file
        /// yet. It creates no battlefield and starts no match.
        /// </summary>
        public static void Prepare(MissionCatalog catalog)
        {
            if (catalog == null) return;

            WorldPools pools = catalog.Looks != null ? catalog.Looks.ToPools() : new WorldPools(null, null, null, null);
            for (int i = 0; i < catalog.Count; i++)
            {
                TextAsset source = catalog.DefinitionAt(i);
                string id = catalog.IdAt(i);
                if (source == null || string.IsNullOrEmpty(id)) continue;
                if (Missions.TryGetValue(id, out Prepared known) && known.Source == source) continue;

                PreparationOutcome outcome = MissionPreparation.Prepare(id, source.name, source.text, pools, Random);
                string label = MissionCatalog.LabelFor(i) + " (" + source.name + ".json)";
                Missions[id] = new Prepared
                {
                    Source = source,
                    Outcome = outcome,
                    Problem = outcome.Ok ? null : label + " cannot be played:\n" + outcome.Explanation,
                };
                Report(label, outcome);
            }
        }

        /// <summary>The prepared mission with this id. False when it was not prepared or could not be.</summary>
        public static bool TryGet(string missionId, out PreparedMission mission)
        {
            mission = missionId != null && Missions.TryGetValue(missionId, out Prepared prepared)
                ? prepared.Outcome.Mission
                : null;
            return mission != null;
        }

        /// <summary>
        /// Why the mission with this id cannot be played, naming the mission and what is wrong. Null when it
        /// was prepared, or has not been tried.
        /// </summary>
        public static string ProblemOf(string missionId)
        {
            return missionId != null && Missions.TryGetValue(missionId, out Prepared prepared) ? prepared.Problem : null;
        }

        /// <summary>
        /// Prepares a mission file outside the campaign, for a mission scene opened on its own during
        /// development. It counts for no campaign entry, and is prepared once per session like the rest.
        /// Null, after logging why, when it cannot be played.
        /// </summary>
        public static PreparedMission PrepareStandalone(TextAsset source, WorldLooks looks)
        {
            if (source == null)
            {
                Debug.LogError("Squadra: this mission scene was opened on its own and has no mission to play.");
                return null;
            }

            standalone ??= new Dictionary<TextAsset, PreparationOutcome>();
            if (!standalone.TryGetValue(source, out PreparationOutcome outcome))
            {
                WorldPools pools = looks != null ? looks.ToPools() : new WorldPools(null, null, null, null);
                outcome = MissionPreparation.Prepare(null, source.name, source.text, pools, Random);
                standalone.Add(source, outcome);
                Report(source.name + ".json", outcome);
            }

            return outcome.Mission;
        }

        static void Report(string label, PreparationOutcome outcome)
        {
            if (!outcome.Ok)
            {
                Debug.LogError("Squadra: " + label + " cannot be played:\n" + outcome.Explanation);
                return;
            }

            if (outcome.Warnings.Count > 0)
                Debug.LogWarning("Squadra: " + label + " was prepared with warnings:\n" + string.Join("\n", outcome.Warnings));
        }
    }
}
