using System;
using System.Collections.Generic;

namespace Squadra.Preparation
{
    /// <summary>
    /// The resolved look of one mission: its space backdrop and the look of every world, keyed by the
    /// mission's ID and each node's ID. It is decided once, when the mission is prepared, and then only
    /// read: a restart, a return to the menu and a capture all show the same backdrop, names and surfaces.
    /// Participants of a future multiplayer match must be handed the same assignment; rolling one of their
    /// own would not give the same worlds.
    /// </summary>
    public sealed class WorldAssignment
    {
        readonly Dictionary<string, WorldLook> looks = new Dictionary<string, WorldLook>(StringComparer.Ordinal);
        readonly List<string> nodeIds = new List<string>();

        public WorldAssignment(string missionId, string backdropId,
            IEnumerable<KeyValuePair<string, WorldLook>> nodeLooks)
        {
            MissionId = missionId ?? "";
            BackdropId = backdropId ?? "";
            foreach (KeyValuePair<string, WorldLook> entry in nodeLooks)
            {
                if (entry.Key == null || entry.Value == null)
                    throw new ArgumentException("Every entry needs a node ID and a look.", nameof(nodeLooks));
                if (looks.ContainsKey(entry.Key))
                    throw new ArgumentException("The node " + entry.Key + " is assigned twice.", nameof(nodeLooks));

                looks.Add(entry.Key, entry.Value);
                nodeIds.Add(entry.Key);
            }
        }

        public string MissionId { get; }

        /// <summary>The space backdrop behind the mission, by the stable ID its pool gives it.</summary>
        public string BackdropId { get; }

        /// <summary>The nodes this assignment covers, in the order it was made.</summary>
        public IReadOnlyList<string> NodeIds => nodeIds;

        /// <summary>The look of one node, or null for a node this assignment does not cover.</summary>
        public WorldLook For(string nodeId)
        {
            return nodeId != null && looks.TryGetValue(nodeId, out WorldLook look) ? look : null;
        }
    }
}
