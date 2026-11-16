using System.Collections.Generic;
using Squadra.MissionEditing;
using UnityEngine.UIElements;

namespace Squadra
{
    /// <summary>The Mission section of the property panel: the mission's ID, name and distance scale.</summary>
    sealed class MissionSettingsPanel
    {
        readonly MissionEditorSession session;
        readonly VisualElement body;
        readonly List<MissionTextEntry> entries = new List<MissionTextEntry>();

        public MissionSettingsPanel(MissionEditorSession session, VisualElement body)
        {
            this.session = session;
            this.body = body;
        }

        /// <summary>Makes the fields anew, for the mission the session holds now.</summary>
        public void Build()
        {
            body.Clear();
            entries.Clear();

            entries.Add(new MissionTextEntry(session, body, "mission.id", "Mission ID", false,
                () => session.Document.Mission.Id,
                text => MissionFields.TryParseId(text, out string error)
                    ? MissionTextEntry.Problem(session.Document.SetMissionId(text))
                    : error,
                text => text == session.Document.Mission.Id));

            entries.Add(new MissionTextEntry(session, body, "mission.name", "Name", false,
                () => session.Document.Mission.Name,
                text => MissionTextEntry.Problem(session.Document.SetName(text)),
                text => text == session.Document.Mission.Name));

            entries.Add(new MissionTextEntry(session, body, "mission.scale", "Distance Scale", false,
                () => MissionFields.Format(session.Document.Mission.DistanceScale),
                text => MissionFields.TryParseDistanceScale(text, out double scale, out string error)
                    ? MissionTextEntry.Problem(session.Document.SetDistanceScale(scale))
                    : error,
                text => MissionFields.TryParseDistanceScale(text, out double scale, out _)
                    && scale == session.Document.Mission.DistanceScale));
        }

        public void Refresh()
        {
            foreach (MissionTextEntry entry in entries) entry.Refresh();
        }
    }
}
