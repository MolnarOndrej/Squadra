namespace Squadra.Missions
{
    /// <summary>
    /// One thing a mission's author should know: what is wrong, where, and what to do about it.
    /// <see cref="Location"/> names the field as the file would, such as <c>nodes[2].owner</c>.
    /// <see cref="NodeId"/> and <see cref="OtherNodeId"/> name the node, or the two ends of the route,
    /// the issue is about, when it is about one.
    /// </summary>
    public sealed class MissionIssue
    {
        public MissionIssue(IssueSeverity severity, IssueCode code, string location, string message,
            string nodeId = null, string otherNodeId = null)
        {
            Severity = severity;
            Code = code;
            Location = location ?? "";
            Message = message ?? "";
            NodeId = nodeId;
            OtherNodeId = otherNodeId;
        }

        public IssueSeverity Severity { get; }
        public IssueCode Code { get; }
        public string Location { get; }
        public string Message { get; }
        public string NodeId { get; }
        public string OtherNodeId { get; }

        public bool IsError => Severity == IssueSeverity.Error;

        public override string ToString()
        {
            return Location.Length > 0 ? Location + ": " + Message : Message;
        }
    }
}
