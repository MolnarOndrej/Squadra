using Squadra.Campaign;
using Unity.Scripting.LifecycleManagement;

namespace Squadra
{
    /// <summary>
    /// The campaign state that outlives a scene: progress for this game session, the mission being played
    /// and a pending request to open the menu on the mission map. Nothing here is saved, so every launch
    /// starts a fresh campaign.
    /// </summary>
    [AutoStaticsCleanup]
    public static partial class CampaignSession
    {
        static CampaignProgress progress;
        static string activeMissionId;
        static bool mapRequested;

        public static CampaignProgress Progress => progress ??= new CampaignProgress();

        /// <summary>The mission launched from the map, or null in the menu or a scene opened on its own.</summary>
        public static string ActiveMissionId => activeMissionId;

        /// <summary>Call when the map launches a mission, before its scene loads.</summary>
        public static void BeginMission(string missionId)
        {
            activeMissionId = missionId;
        }

        /// <summary>Call when the player is back in the menu: a later victory belongs to no mission.</summary>
        public static void EndMission()
        {
            activeMissionId = null;
        }

        /// <summary>
        /// The match result boundary: call when the player wins the running match. Completes the active
        /// campaign mission; a defeat, a restart or leaving the scene never reaches this.
        /// </summary>
        public static void ReportVictory()
        {
            if (activeMissionId != null) Progress.Complete(activeMissionId);
        }

        /// <summary>Asks the menu to open on the mission map the next time its scene loads.</summary>
        public static void RequestMap()
        {
            mapRequested = true;
        }

        /// <summary>True once per <see cref="RequestMap"/>.</summary>
        public static bool ConsumeMapRequest()
        {
            bool requested = mapRequested;
            mapRequested = false;
            return requested;
        }
    }
}
