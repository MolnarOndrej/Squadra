using Squadra.Simulation;

namespace Squadra.Preparation
{
    /// <summary>
    /// One world of a prepared mission, in game terms: its node ID, the index the match knows it by, its
    /// kind, its starting owner and Darts, where it is in game world units, its body radius and its look.
    /// </summary>
    public sealed class PreparedWorld
    {
        internal PreparedWorld(string nodeId, int index, NodeKind kind, Faction owner, int darts, MapPoint position,
            float radius, WorldLook look)
        {
            NodeId = nodeId;
            Index = index;
            Kind = kind;
            Owner = owner;
            Darts = darts;
            Position = position;
            Radius = radius;
            Look = look;
        }

        /// <summary>The node's ID in the mission file: its identity for references and diagnostics.</summary>
        public string NodeId { get; }

        /// <summary>The node's index in the match.</summary>
        public int Index { get; }

        public NodeKind Kind { get; }
        public Faction Owner { get; }
        public int Darts { get; }

        /// <summary>The center in game world units: map position times the distance scale.</summary>
        public MapPoint Position { get; }

        /// <summary>Body radius in game world units, exactly as authored; the distance scale does not change it.</summary>
        public float Radius { get; }

        public WorldLook Look { get; }
    }
}
