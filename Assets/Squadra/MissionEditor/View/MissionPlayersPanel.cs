using System.Collections.Generic;
using System.Text;
using Squadra.MissionEditing;
using Squadra.Missions;
using UnityEngine.UIElements;

namespace Squadra
{
    /// <summary>
    /// The Players section of the property panel: each player with its color and a Remove button, and
    /// the row that adds another. It is made anew whenever the players or their colors change.
    /// </summary>
    sealed class MissionPlayersPanel
    {
        readonly MissionEditorSession session;
        readonly VisualElement body;

        string shownPlayers;
        TextField newPlayerId;
        DropdownField newPlayerColor;
        Label newPlayerError;

        public MissionPlayersPanel(MissionEditorSession session, VisualElement body)
        {
            this.session = session;
            this.body = body;
        }

        /// <summary>The rows belong to a mission that is gone: the next refresh makes them anew.</summary>
        public void Invalidate()
        {
            shownPlayers = null;
        }

        public void Refresh()
        {
            MissionDefinition mission = session.Document.Mission;
            var signature = new StringBuilder();
            foreach (MissionPlayer player in mission.Players)
                signature.Append(player.Id).Append(':').Append((int)player.Color).Append(';');
            if (signature.ToString() == shownPlayers) return;

            shownPlayers = signature.ToString();
            body.Clear();
            if (mission.Players.Count == 0)
                body.Add(MissionEditorRows.Note("This mission has no players. Every node is neutral."));

            foreach (MissionPlayer player in mission.Players) body.Add(PlayerRow(player));
            BuildAddPlayer(mission);
        }

        VisualElement PlayerRow(MissionPlayer player)
        {
            string id = player.Id;
            var row = new VisualElement();
            row.AddToClassList("player");

            var swatch = new VisualElement();
            swatch.AddToClassList("player__swatch");
            swatch.style.backgroundColor = PlayerColors.Of(player.Color);
            row.Add(swatch);

            var name = new Label(id);
            name.AddToClassList("player__id");
            row.Add(name);

            // The colors nobody has, and this player's own as the choice that changes nothing.
            var color = new DropdownField { choices = PlayerColors.NamesOf(session.Document.AvailableColors(id)) };
            color.AddToClassList("row__control");
            color.AddToClassList("player__color");
            color.SetValueWithoutNotify(PlayerColors.NameOf(player.Color));
            color.RegisterValueChangedCallback(changed =>
            {
                if (PlayerColors.TryParse(changed.newValue, out PlayerColor chosen))
                    session.Document.SetPlayerColor(id, chosen);
            });
            row.Add(color);

            var remove = new Button(() => session.RequestRemovePlayer(id)) { text = "Remove" };
            remove.AddToClassList("tool-button");
            remove.AddToClassList("small-button");
            row.Add(remove);
            return row;
        }

        void BuildAddPlayer(MissionDefinition mission)
        {
            var add = new VisualElement();
            add.AddToClassList("players__add");
            body.Add(add);

            bool room = session.Document.CanAddPlayer;
            var row = new VisualElement();
            row.AddToClassList("player");
            add.Add(row);

            newPlayerId = new TextField { value = room ? SuggestedPlayerId(mission) : "" };
            newPlayerId.AddToClassList("row__control");
            newPlayerId.AddToClassList("player__add-id");
            newPlayerId.RegisterValueChangedCallback(_ => ShowNewPlayerError(null));
            row.Add(newPlayerId);

            List<string> free = PlayerColors.NamesOf(session.Document.AvailableColors());
            newPlayerColor = new DropdownField { choices = free };
            newPlayerColor.AddToClassList("row__control");
            newPlayerColor.AddToClassList("player__color");
            if (free.Count > 0) newPlayerColor.SetValueWithoutNotify(free[0]);
            row.Add(newPlayerColor);

            var button = new Button(AddPlayer) { name = "addPlayerButton", text = "Add Player" };
            button.AddToClassList("tool-button");
            button.AddToClassList("small-button");
            row.Add(button);

            newPlayerError = new Label();
            newPlayerError.AddToClassList("row__error");
            newPlayerError.style.marginLeft = 0f;
            add.Add(newPlayerError);

            // Six players is the most a mission holds: Add Player is unavailable until one is removed.
            newPlayerId.SetEnabled(room);
            newPlayerColor.SetEnabled(room);
            button.SetEnabled(room);
            ShowNewPlayerError(room ? null : "A mission can have at most " + MissionRules.MaxPlayers + " players.");
            if (!room) newPlayerError.style.color = new StyleColor(Palette.TextDim);
        }

        /// <summary>What the Add Player button does with the ID and color in its row.</summary>
        public void AddPlayer()
        {
            if (session.Busy || newPlayerId == null) return;

            if (!PlayerColors.TryParse(newPlayerColor.value, out PlayerColor color))
            {
                ShowNewPlayerError("Every color is taken.");
                return;
            }

            EditResult result = session.Document.AddPlayer(newPlayerId.value, color);
            if (result.Rejected) ShowNewPlayerError(result.Message);
        }

        void ShowNewPlayerError(string message)
        {
            newPlayerError.text = message ?? "";
            newPlayerError.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
            newPlayerId.EnableInClassList(MissionEditorRows.InvalidClass,
                !string.IsNullOrEmpty(message) && session.Document.CanAddPlayer);
        }

        static string SuggestedPlayerId(MissionDefinition mission)
        {
            for (int number = 1;; number++)
            {
                string id = "player" + number;
                if (mission.FindPlayer(id) == null) return id;
            }
        }
    }
}
