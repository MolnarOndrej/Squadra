using System;
using System.Collections.Generic;

namespace Squadra.Simulation
{
    /// <summary>
    /// The players of one match and their combat totals. The roster is fixed when the match starts;
    /// the totals grow with every casualty a battle reports and are read out once, as the results.
    /// </summary>
    sealed class MatchRoster
    {
        readonly List<MatchPlayer> players = new List<MatchPlayer>();
        readonly Faction human;
        readonly int[] dartsDestroyed;
        readonly int[] dartsLost;

        // The roster comes from the mission, not from who still has Darts: the configured players, then
        // any starting owner the configuration left out, so nobody who can fight is without a row.
        public MatchRoster(MatchConfig config, IReadOnlyList<Node> nodes)
        {
            human = config.Human;

            var roster = new List<MatchPlayer>();
            foreach (MatchPlayer player in config.Players)
            {
                if (player.Faction == Faction.Neutral)
                    throw new ArgumentException("Neutral cannot be a player.", nameof(config));
                if (roster.Exists(listed => listed.Faction == player.Faction))
                    throw new ArgumentException(player.Faction + " is on the roster twice.", nameof(config));
                roster.Add(player);
            }

            foreach (Node node in nodes)
            {
                Faction owner = node.Owner;
                if (owner != Faction.Neutral && !roster.Exists(listed => listed.Faction == owner))
                    roster.Add(new MatchPlayer(owner));
            }

            if (roster.Count > MatchConfig.MaxPlayers)
            {
                throw new ArgumentException(
                    "A match has at most " + MatchConfig.MaxPlayers + " players, not " + roster.Count + ".",
                    nameof(config));
            }

            int humanIndex = roster.FindIndex(listed => listed.Faction == human);
            if (humanIndex >= 0) players.Add(roster[humanIndex]);
            for (int i = 0; i < roster.Count; i++)
            {
                if (i != humanIndex) players.Add(roster[i]);
            }

            dartsDestroyed = new int[players.Count];
            dartsLost = new int[players.Count];
        }

        /// <summary>The human player first and the rest in roster order. Neutral is never listed.</summary>
        public IReadOnlyList<MatchPlayer> Players => players;

        public bool Contains(Faction faction) => IndexOf(faction) >= 0;

        public int DartsDestroyed(Faction faction)
        {
            int index = IndexOf(faction);
            return index < 0 ? 0 : dartsDestroyed[index];
        }

        public int DartsLost(Faction faction)
        {
            int index = IndexOf(faction);
            return index < 0 ? 0 : dartsLost[index];
        }

        // Casualties of one step of a battle, counted when they happen. A side's casualties are its own
        // losses and the other side's kills. Only roster players keep totals, so Darts lost to neutral
        // guards are credited to nobody, while destroyed guards still count for the player that
        // destroyed them.
        public void RecordCasualties(Faction arriving, int arrivingCasualties, Faction stationed, int stationedCasualties)
        {
            int arrivingPlayer = IndexOf(arriving);
            if (arrivingPlayer >= 0)
            {
                dartsLost[arrivingPlayer] += arrivingCasualties;
                dartsDestroyed[arrivingPlayer] += stationedCasualties;
            }

            int stationedPlayer = IndexOf(stationed);
            if (stationedPlayer >= 0)
            {
                dartsLost[stationedPlayer] += stationedCasualties;
                dartsDestroyed[stationedPlayer] += arrivingCasualties;
            }
        }

        /// <summary>Every player's totals as they stand now, in roster order.</summary>
        public PlayerResult[] Totals()
        {
            var totals = new PlayerResult[players.Count];
            for (int i = 0; i < totals.Length; i++)
            {
                MatchPlayer player = players[i];
                totals[i] = new PlayerResult(player.Faction, player.Name, player.Faction == human,
                    dartsDestroyed[i], dartsLost[i]);
            }

            return totals;
        }

        int IndexOf(Faction faction)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].Faction == faction) return i;
            }

            return -1;
        }
    }
}
