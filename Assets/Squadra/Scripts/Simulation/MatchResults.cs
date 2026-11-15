using System.Collections.Generic;

namespace Squadra.Simulation
{
    /// <summary>
    /// The final facts of one finished match: the outcome, how long it was played and every roster
    /// player's combat totals. It is taken once, when the match ends, and never changes afterwards.
    /// </summary>
    public sealed class MatchResults
    {
        readonly PlayerResult[] players;

        internal MatchResults(MatchStatus outcome, double duration, PlayerResult[] players)
        {
            Outcome = outcome;
            Duration = duration;
            this.players = players;
        }

        public MatchStatus Outcome { get; }

        public bool IsVictory => Outcome == MatchStatus.BlueVictory;

        /// <summary>Seconds of simulated play from the start of the match to its outcome.</summary>
        public double Duration { get; }

        /// <summary>
        /// One entry per roster player, the human player first and the rest in roster order. Players
        /// that were eliminated or never fought are included.
        /// </summary>
        public IReadOnlyList<PlayerResult> Players => players;

        /// <summary>Looks a player up by identity. False for Neutral and for anyone outside the roster.</summary>
        public bool TryGet(Faction faction, out PlayerResult result)
        {
            foreach (PlayerResult player in players)
            {
                if (player.Faction != faction) continue;

                result = player;
                return true;
            }

            result = default;
            return false;
        }
    }
}
