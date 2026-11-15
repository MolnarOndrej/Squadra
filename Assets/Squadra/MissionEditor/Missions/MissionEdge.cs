namespace Squadra.Missions
{
    /// <summary>
    /// A route between two nodes. Routes run both ways: source and target only name the two ends.
    /// </summary>
    public sealed class MissionEdge
    {
        public MissionEdge()
        {
        }

        public MissionEdge(string source, string target)
        {
            Source = source;
            Target = target;
        }

        public string Source { get; set; }
        public string Target { get; set; }

        /// <summary>True when this route joins the two nodes, whichever end each is.</summary>
        public bool Joins(string a, string b)
        {
            return (MissionRules.SameId(Source, a) && MissionRules.SameId(Target, b))
                || (MissionRules.SameId(Source, b) && MissionRules.SameId(Target, a));
        }
    }
}
