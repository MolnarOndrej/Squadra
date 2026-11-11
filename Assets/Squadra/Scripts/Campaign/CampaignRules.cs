using System.Collections.Generic;

namespace Squadra.Campaign
{
    /// <summary>How one mission appears on the map.</summary>
    public enum MissionState
    {
        /// <summary>No playable scenario yet: shown locked, as "coming soon".</summary>
        Preview,

        /// <summary>Playable, but the mission before it is not completed.</summary>
        Locked,

        /// <summary>Playable now and not won yet.</summary>
        Available,

        /// <summary>Won this session. Still playable.</summary>
        Completed,
    }

    /// <summary>One entry of the ordered mission catalog, as the campaign rules see it.</summary>
    public readonly struct MissionSlot
    {
        /// <summary>Stable id. Progress is tracked by it, so reordering the catalog keeps completions.</summary>
        public readonly string Id;

        /// <summary>True when the mission has a gameplay scene that is included in the build.</summary>
        public readonly bool Ready;

        public MissionSlot(string id, bool ready)
        {
            Id = id;
            Ready = ready;
        }
    }

    /// <summary>
    /// The campaign's unlock rules. A mission can be launched when it is ready and the mission right
    /// before it in the catalog is completed; the first mission has no prerequisite.
    /// </summary>
    public static class CampaignRules
    {
        public static MissionState StateOf(IReadOnlyList<MissionSlot> missions, CampaignProgress progress, int index)
        {
            MissionSlot mission = missions[index];
            if (!mission.Ready) return MissionState.Preview;
            if (progress.IsCompleted(mission.Id)) return MissionState.Completed;
            if (index == 0 || progress.IsCompleted(missions[index - 1].Id)) return MissionState.Available;
            return MissionState.Locked;
        }

        public static bool CanLaunch(IReadOnlyList<MissionSlot> missions, CampaignProgress progress, int index)
        {
            if (index < 0 || index >= missions.Count) return false;

            MissionState state = StateOf(missions, progress, index);
            return state == MissionState.Available || state == MissionState.Completed;
        }

        /// <summary>
        /// True when the mission became available by completing its predecessor and the map has not shown
        /// that yet. The first mission is available from the start, so it never needs a reveal.
        /// </summary>
        public static bool NeedsReveal(IReadOnlyList<MissionSlot> missions, CampaignProgress progress, int index)
        {
            return index > 0
                && StateOf(missions, progress, index) == MissionState.Available
                && !progress.IsRevealed(missions[index].Id);
        }

        /// <summary>The first mission waiting for its unlock to be shown, or -1.</summary>
        public static int FirstPendingReveal(IReadOnlyList<MissionSlot> missions, CampaignProgress progress)
        {
            for (int i = 1; i < missions.Count; i++)
            {
                if (NeedsReveal(missions, progress, i)) return i;
            }

            return -1;
        }

        /// <summary>The first mission that is available and not won yet, or -1: where the player goes next.</summary>
        public static int NextMission(IReadOnlyList<MissionSlot> missions, CampaignProgress progress)
        {
            for (int i = 0; i < missions.Count; i++)
            {
                if (StateOf(missions, progress, i) == MissionState.Available) return i;
            }

            return -1;
        }

        /// <summary>The catalog index of a mission id, or -1 when the catalog has no such mission.</summary>
        public static int IndexOf(IReadOnlyList<MissionSlot> missions, string missionId)
        {
            if (string.IsNullOrEmpty(missionId)) return -1;

            for (int i = 0; i < missions.Count; i++)
            {
                if (missions[i].Id == missionId) return i;
            }

            return -1;
        }

        /// <summary>
        /// The match result boundary: call once when an attempt at a mission ends. A victory is recorded
        /// first, so that it counts toward what the results screen then offers; a defeat changes nothing.
        /// The mission that follows is always the very next catalog entry, never a later one that happens
        /// to be ready. Winning the last catalog entry completes the campaign.
        /// A match played outside the campaign (<paramref name="missionId"/> is null or not in the
        /// catalog) gets a plain Victory or Defeat with no Next Mission.
        /// </summary>
        public static ResultsPlan ConcludeMission(IReadOnlyList<MissionSlot> missions, CampaignProgress progress,
            string missionId, bool victory)
        {
            if (victory) progress.Complete(missionId);

            ResultsHeading heading = victory ? ResultsHeading.Victory : ResultsHeading.Defeat;
            int index = IndexOf(missions, missionId);
            if (index < 0)
                return new ResultsPlan(heading, NextMissionState.Hidden, -1, ResultsAction.Restart);

            int next = index + 1;
            if (next >= missions.Count)
            {
                return victory
                    ? new ResultsPlan(ResultsHeading.CampaignComplete, NextMissionState.Hidden, -1,
                        ResultsAction.BackToMenu)
                    : new ResultsPlan(heading, NextMissionState.Hidden, -1, ResultsAction.Restart);
            }

            if (!victory)
                return new ResultsPlan(heading, NextMissionState.WinRequired, next, ResultsAction.Restart);

            return CanLaunch(missions, progress, next)
                ? new ResultsPlan(heading, NextMissionState.Enabled, next, ResultsAction.NextMission)
                : new ResultsPlan(heading, NextMissionState.ComingSoon, next, ResultsAction.Restart);
        }
    }
}
