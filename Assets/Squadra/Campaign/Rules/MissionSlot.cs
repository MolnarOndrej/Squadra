namespace Squadra.Campaign
{
    /// <summary>One entry of the ordered mission catalog, as the campaign rules see it.</summary>
    public readonly struct MissionSlot
    {
        /// <summary>Stable id. Progress is tracked by it, so reordering the catalog keeps completions.</summary>
        public readonly string Id;

        /// <summary>True when the mission's definition was prepared and the gameplay scene is in the build.</summary>
        public readonly bool Ready;

        /// <summary>
        /// Why an assigned mission cannot be played, naming the mission and what is wrong with it. Null for a
        /// mission that is ready and for one that has no definition assigned yet.
        /// </summary>
        public readonly string Problem;

        public MissionSlot(string id, bool ready)
        {
            Id = id;
            Ready = ready;
            Problem = null;
        }

        MissionSlot(string id, string problem)
        {
            Id = id;
            Ready = false;
            Problem = problem;
        }

        /// <summary>A mission that has a definition assigned which could not be prepared.</summary>
        public static MissionSlot Failed(string id, string problem)
        {
            return new MissionSlot(id, string.IsNullOrEmpty(problem) ? "The mission could not be prepared." : problem);
        }

        public bool HasFailed => Problem != null;
    }
}
