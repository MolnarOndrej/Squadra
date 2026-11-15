using System;
using Squadra.Missions;

namespace Squadra.MissionEditing
{
    /// <summary>The one object the author has selected: nothing, a node, or the route between two nodes.</summary>
    public readonly struct Selection : IEquatable<Selection>
    {
        Selection(SelectionKind kind, string nodeId, string otherNodeId)
        {
            Kind = kind;
            NodeId = nodeId;
            OtherNodeId = otherNodeId;
        }

        public SelectionKind Kind { get; }

        /// <summary>The selected node, or one end of the selected route.</summary>
        public string NodeId { get; }

        /// <summary>The other end of the selected route; null otherwise.</summary>
        public string OtherNodeId { get; }

        public static readonly Selection None = default;

        public static Selection Node(string id)
        {
            return new Selection(SelectionKind.Node, id, null);
        }

        public static Selection Edge(string a, string b)
        {
            return new Selection(SelectionKind.Edge, a, b);
        }

        public bool IsNode(string id)
        {
            return Kind == SelectionKind.Node && MissionRules.SameId(NodeId, id);
        }

        public bool IsEdge(MissionEdge edge)
        {
            return Kind == SelectionKind.Edge && edge != null && edge.Joins(NodeId, OtherNodeId);
        }

        public bool Equals(Selection other)
        {
            if (Kind != other.Kind) return false;
            if (Kind == SelectionKind.None) return true;
            if (Kind == SelectionKind.Node) return MissionRules.SameId(NodeId, other.NodeId);
            return (MissionRules.SameId(NodeId, other.NodeId) && MissionRules.SameId(OtherNodeId, other.OtherNodeId))
                || (MissionRules.SameId(NodeId, other.OtherNodeId) && MissionRules.SameId(OtherNodeId, other.NodeId));
        }

        public override bool Equals(object obj)
        {
            return obj is Selection other && Equals(other);
        }

        public override int GetHashCode()
        {
            int a = NodeId != null ? StringComparer.Ordinal.GetHashCode(NodeId) : 0;
            int b = OtherNodeId != null ? StringComparer.Ordinal.GetHashCode(OtherNodeId) : 0;
            return (int)Kind ^ a ^ b;
        }
    }
}
