using System.Collections.Generic;

namespace Squadra.Campaign
{
    /// <summary>
    /// What the player has achieved in the campaign during one game session: which missions are won and
    /// which unlocks the mission map has already shown. It lives in memory only, so a fresh launch starts
    /// with nothing completed. It knows nothing about matches or settings.
    /// </summary>
    public sealed class CampaignProgress
    {
        readonly HashSet<string> completed = new HashSet<string>();
        readonly HashSet<string> revealed = new HashSet<string>();

        public int CompletedCount => completed.Count;

        public bool IsCompleted(string missionId)
        {
            return missionId != null && completed.Contains(missionId);
        }

        /// <summary>Records a victory. Returns true the first time the mission is completed.</summary>
        public bool Complete(string missionId)
        {
            return !string.IsNullOrEmpty(missionId) && completed.Add(missionId);
        }

        /// <summary>True once the map has shown this mission becoming available.</summary>
        public bool IsRevealed(string missionId)
        {
            return missionId != null && revealed.Contains(missionId);
        }

        /// <summary>Call when the map has finished showing the mission's unlock, so it is not shown again.</summary>
        public void MarkRevealed(string missionId)
        {
            if (!string.IsNullOrEmpty(missionId)) revealed.Add(missionId);
        }
    }
}
