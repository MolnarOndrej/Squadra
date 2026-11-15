namespace Squadra.Campaign
{
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
}
