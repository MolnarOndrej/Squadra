using System;
using System.Collections.Generic;

namespace Squadra.Simulation
{
    /// <summary>
    /// Who ships and map objects belong to. Neutral is nobody; every other value is the stable identity
    /// of one player, and a mission's roster names the ones that take part. Only Blue and Red are
    /// playable so far: the other identities exist so rosters and results can hold up to six players.
    /// </summary>
    public enum Faction
    {
        Neutral = 0,
        Blue = 1,
        Red = 2,
        Green = 3,
        Yellow = 4,
        Purple = 5,
        Orange = 6,
    }

    public enum NodeKind
    {
        Planet,
        Moon,
    }

    public enum MatchStatus
    {
        InProgress,
        BlueVictory,
        BlueDefeat,
    }

    /// <summary>What happened when a fleet reached its destination.</summary>
    public enum ArrivalOutcome
    {
        /// <summary>Allied arrival at an owned object: the ships joined the stationed fleet.</summary>
        Merged,

        /// <summary>Arrival allied with the fleet that is capturing the object: joined it, timer untouched.</summary>
        ReinforcedCapture,

        /// <summary>Nobody was stationed there: the whole fleet landed and began capturing.</summary>
        Landed,

        /// <summary>The arriving fleet won the battle and its survivors began capturing.</summary>
        AttackWon,

        /// <summary>The arriving fleet was destroyed; the stationed fleet keeps its survivors.</summary>
        Repelled,

        /// <summary>Both fleets were destroyed. Any capture in progress is cleared.</summary>
        Tie,

        /// <summary>The incumbent owner's fleet destroyed the capturing fleet and took control back at once.</summary>
        ControlRestored,
    }

    /// <summary>A position on the map, in map units.</summary>
    public readonly struct MapPoint : IEquatable<MapPoint>
    {
        public readonly float X;
        public readonly float Y;

        public MapPoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static double Distance(MapPoint a, MapPoint b)
        {
            double dx = (double)a.X - b.X;
            double dy = (double)a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public bool Equals(MapPoint other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is MapPoint other && Equals(other);
        public override int GetHashCode() => unchecked((X.GetHashCode() * 397) ^ Y.GetHashCode());
        public override string ToString() => $"({X}, {Y})";
    }

    /// <summary>The visible facts of one fleet arrival, for presentation and for tests.</summary>
    public readonly struct ArrivalReport
    {
        public readonly int NodeId;
        public readonly int FleetId;
        public readonly Faction Arriving;
        public readonly int ArrivingShips;

        /// <summary>Faction of the fleet stationed at the object before the arrival.</summary>
        public readonly Faction Stationed;
        public readonly int StationedShips;

        public readonly ArrivalOutcome Outcome;

        /// <summary>Ships destroyed on each side. Zero for peaceful arrivals.</summary>
        public readonly int LossesPerSide;

        /// <summary>Ships stationed at the object once the arrival was resolved.</summary>
        public readonly int ShipsAfter;

        public ArrivalReport(int nodeId, int fleetId, Faction arriving, int arrivingShips, Faction stationed,
            int stationedShips, ArrivalOutcome outcome, int lossesPerSide, int shipsAfter)
        {
            NodeId = nodeId;
            FleetId = fleetId;
            Arriving = arriving;
            ArrivingShips = arrivingShips;
            Stationed = stationed;
            StationedShips = stationedShips;
            Outcome = outcome;
            LossesPerSide = lossesPerSide;
            ShipsAfter = shipsAfter;
        }

        public bool WasBattle => LossesPerSide > 0;
    }

    /// <summary>One participating player of a match, as its roster lists them.</summary>
    public readonly struct MatchPlayer
    {
        /// <summary>Stable identity. Results are keyed by it.</summary>
        public readonly Faction Faction;

        /// <summary>Display name.</summary>
        public readonly string Name;

        public MatchPlayer(Faction faction, string name = null)
        {
            Faction = faction;
            Name = string.IsNullOrEmpty(name) ? faction.ToString() : name;
        }
    }

    /// <summary>One player's combat totals for a single attempt at a mission.</summary>
    public readonly struct PlayerResult
    {
        public readonly Faction Faction;
        public readonly string Name;
        public readonly bool IsHuman;

        /// <summary>Individual enemy and neutral guard ships this player's fleets destroyed.</summary>
        public readonly int ShipsDestroyed;

        /// <summary>This player's ships destroyed in combat, attacking or defending.</summary>
        public readonly int ShipsLost;

        public PlayerResult(Faction faction, string name, bool isHuman, int shipsDestroyed, int shipsLost)
        {
            Faction = faction;
            Name = name;
            IsHuman = isHuman;
            ShipsDestroyed = shipsDestroyed;
            ShipsLost = shipsLost;
        }
    }

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

    /// <summary>Formats match time for display.</summary>
    public static class MatchClock
    {
        /// <summary>
        /// Whole elapsed seconds as minutes:seconds. Minutes are not wrapped into hours, so a long
        /// match reads "75:09".
        /// </summary>
        public static string Format(double seconds)
        {
            long whole = WholeSeconds(seconds);
            return (whole / 60).ToString("00") + ":" + (whole % 60).ToString("00");
        }

        /// <summary>
        /// Completed seconds. A moment that is a rounding error short of a whole second counts as that
        /// second, because time arrives in small float steps.
        /// </summary>
        public static long WholeSeconds(double seconds)
        {
            return seconds > 0 ? (long)Math.Floor(seconds + 1e-6) : 0;
        }
    }
}
