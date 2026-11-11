using System;
using System.Collections.Generic;

namespace Squadra.Simulation
{
    /// <summary>
    /// The whole match simulation: fleet orders, elapsed time, arrivals, combat, capture, production, each
    /// player's combat totals and the match result. The scene sends orders and time to this boundary and
    /// displays the state it exposes.
    /// </summary>
    public sealed class Match
    {
        // Tolerance for "this event is due now", so that stepping time in many small float increments
        // lands on whole-second events instead of missing them by a rounding error.
        const double Epsilon = 1e-7;

        readonly List<Node> nodes = new List<Node>();
        readonly List<Fleet> fleets = new List<Fleet>();
        readonly List<Fleet> arrivals = new List<Fleet>();
        readonly List<MatchPlayer> players = new List<MatchPlayer>();
        readonly int[] shipsDestroyed;
        readonly int[] shipsLost;
        int nextFleetId = 1;

        public Match(MatchConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (config.FleetSpeed <= 0) throw new ArgumentException("Fleet speed must be positive.", nameof(config));

            FleetSpeed = config.FleetSpeed;
            CaptureDuration = Math.Max(0, config.CaptureDuration);
            for (int i = 0; i < config.Nodes.Count; i++)
                nodes.Add(new Node(i, config.Nodes[i], CaptureDuration));

            Human = config.Human;
            BuildRoster(config);
            shipsDestroyed = new int[players.Count];
            shipsLost = new int[players.Count];
        }

        public IReadOnlyList<Node> Nodes => nodes;
        public IReadOnlyList<Fleet> Fleets => fleets;
        public double FleetSpeed { get; }
        public double CaptureDuration { get; }

        /// <summary>
        /// Seconds of simulated play. Only <see cref="Advance"/> moves it, so time the caller does not
        /// pass in (a pause, a loading screen, the results screen) is never counted.
        /// </summary>
        public double Elapsed { get; private set; }
        public MatchStatus Status { get; private set; } = MatchStatus.InProgress;

        /// <summary>The player at the controls.</summary>
        public Faction Human { get; }

        /// <summary>
        /// Everyone taking part, the human player first and the rest in roster order. A player stays on
        /// the roster after losing every ship and object. Neutral is never on it.
        /// </summary>
        public IReadOnlyList<MatchPlayer> Players => players;

        /// <summary>The final results, taken when the match ends. Null while it is in progress.</summary>
        public MatchResults Results { get; private set; }

        public event Action<Fleet> FleetLaunched;
        public event Action<ArrivalReport> FleetArrived;
        public event Action<Node> ShipProduced;

        /// <summary>Raised when ownership formally changes. The second argument is the previous owner.</summary>
        public event Action<Node, Faction> CaptureCompleted;
        public event Action<MatchStatus> StatusChanged;

        // ------------------------------------------------------------------ players

        // The roster comes from the mission, not from who still has ships: the configured players, then
        // any starting owner the configuration left out, so nobody who can fight is without a row.
        void BuildRoster(MatchConfig config)
        {
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

            int human = roster.FindIndex(listed => listed.Faction == Human);
            if (human >= 0) players.Add(roster[human]);
            for (int i = 0; i < roster.Count; i++)
            {
                if (i != human) players.Add(roster[i]);
            }
        }

        int PlayerIndex(Faction faction)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].Faction == faction) return i;
            }

            return -1;
        }

        /// <summary>True for a roster player. False for Neutral.</summary>
        public bool IsPlayer(Faction faction) => PlayerIndex(faction) >= 0;

        /// <summary>Individual ships this player's fleets have destroyed so far, neutral guards included.</summary>
        public int ShipsDestroyed(Faction faction)
        {
            int index = PlayerIndex(faction);
            return index < 0 ? 0 : shipsDestroyed[index];
        }

        /// <summary>This player's ships destroyed in combat so far, attacking or defending.</summary>
        public int ShipsLost(Faction faction)
        {
            int index = PlayerIndex(faction);
            return index < 0 ? 0 : shipsLost[index];
        }

        // One resolved battle. A side's casualties are its own losses and the other side's kills. Only
        // roster players keep totals, so ships lost to neutral guards are credited to nobody, while
        // destroyed guards still count for the player that destroyed them.
        void RecordBattle(Faction arriving, int arrivingCasualties, Faction stationed, int stationedCasualties)
        {
            int arrivingPlayer = PlayerIndex(arriving);
            if (arrivingPlayer >= 0)
            {
                shipsLost[arrivingPlayer] += arrivingCasualties;
                shipsDestroyed[arrivingPlayer] += stationedCasualties;
            }

            int stationedPlayer = PlayerIndex(stationed);
            if (stationedPlayer >= 0)
            {
                shipsLost[stationedPlayer] += stationedCasualties;
                shipsDestroyed[stationedPlayer] += arrivingCasualties;
            }
        }

        // ------------------------------------------------------------------ orders

        public double TravelTime(int sourceId, int targetId)
        {
            return MapPoint.Distance(nodes[sourceId].Position, nodes[targetId].Position) / FleetSpeed;
        }

        /// <summary>
        /// True when <paramref name="issuer"/> may send the fleet stationed at the source to the target:
        /// the issuer owns the source, the source holds ships that are not mid-capture, and the target differs.
        /// </summary>
        public bool CanLaunch(Faction issuer, int sourceId, int targetId)
        {
            if (Status != MatchStatus.InProgress) return false;
            if (issuer == Faction.Neutral) return false;
            if (!IsNode(sourceId) || !IsNode(targetId) || sourceId == targetId) return false;

            Node source = nodes[sourceId];
            return source.Owner == issuer && !source.IsBeingCaptured && source.Ships > 0;
        }

        /// <summary>Sends every ship stationed at the source toward the target. The source stays owned.</summary>
        public bool TryLaunch(Faction issuer, int sourceId, int targetId, out Fleet fleet)
        {
            fleet = null;
            if (!CanLaunch(issuer, sourceId, targetId)) return false;

            Node source = nodes[sourceId];
            fleet = new Fleet(nextFleetId++, issuer, source.Ships, sourceId, targetId, TravelTime(sourceId, targetId));
            source.Ships = 0;
            fleets.Add(fleet);
            FleetLaunched?.Invoke(fleet);
            return true;
        }

        public bool TryLaunch(Faction issuer, int sourceId, int targetId)
        {
            return TryLaunch(issuer, sourceId, targetId, out _);
        }

        // ------------------------------------------------------------------ time

        /// <summary>
        /// Advances the match. Events inside the interval are resolved in time order, so one large step gives
        /// the same result as many small ones. Time stops once the match has a result.
        /// </summary>
        public void Advance(double seconds)
        {
            if (!(seconds > 0)) return;

            double remaining = seconds;
            while (remaining > 0 && Status == MatchStatus.InProgress)
            {
                double step = Math.Min(remaining, TimeToNextEvent());
                Step(step);
                remaining -= step;
                ResolveDueEvents();
                EvaluateStatus();
            }
        }

        double TimeToNextEvent()
        {
            double next = double.PositiveInfinity;
            foreach (Node node in nodes)
            {
                if (node.IsBeingCaptured)
                    next = Math.Min(next, node.CaptureDuration - node.CaptureElapsed);
                else if (node.IsProducing)
                    next = Math.Min(next, node.ProductionInterval - node.ProductionClock);
            }

            foreach (Fleet fleet in fleets)
                next = Math.Min(next, fleet.Duration - fleet.Elapsed);

            return Math.Max(0, next);
        }

        void Step(double step)
        {
            Elapsed += step;
            foreach (Node node in nodes)
            {
                if (node.IsBeingCaptured) node.CaptureElapsed += step;
                else if (node.IsProducing) node.ProductionClock += step;
            }

            foreach (Fleet fleet in fleets)
                fleet.Elapsed += step;
        }

        // Order within one instant: production, then capture completion, then arrivals in launch order.
        // A fleet therefore meets every ship produced up to and including its moment of arrival.
        void ResolveDueEvents()
        {
            foreach (Node node in nodes)
            {
                while (node.IsProducing && node.ProductionInterval - node.ProductionClock <= Epsilon)
                {
                    node.ProductionClock = Math.Max(0, node.ProductionClock - node.ProductionInterval);
                    node.Ships++;
                    ShipProduced?.Invoke(node);
                }
            }

            foreach (Node node in nodes)
            {
                if (node.IsBeingCaptured && node.CaptureDuration - node.CaptureElapsed <= Epsilon)
                    CompleteCapture(node);
            }

            arrivals.Clear();
            foreach (Fleet fleet in fleets)
            {
                if (fleet.Duration - fleet.Elapsed <= Epsilon) arrivals.Add(fleet);
            }

            foreach (Fleet fleet in arrivals)
            {
                fleets.Remove(fleet);
                ResolveArrival(fleet);
            }
        }

        // ------------------------------------------------------------------ capture and combat

        void CompleteCapture(Node node)
        {
            Faction previousOwner = node.Owner;
            node.Owner = node.Captor;
            ClearCapture(node);
            node.ProductionClock = 0; // a newly captured object starts a fresh production interval
            CaptureCompleted?.Invoke(node, previousOwner);
        }

        static void ClearCapture(Node node)
        {
            node.Captor = Faction.Neutral;
            node.CaptureElapsed = 0;
        }

        void ResolveArrival(Fleet fleet)
        {
            Node node = nodes[fleet.TargetId];
            Faction stationed = node.Occupant;
            int stationedShips = node.Ships;
            int arrivingShips = fleet.Ships;
            int losses = 0;
            ArrivalOutcome outcome;

            if (fleet.Faction == stationed)
            {
                // Allied with whoever holds the object: join them. A capture clock keeps running.
                node.Ships += arrivingShips;
                outcome = node.IsBeingCaptured ? ArrivalOutcome.ReinforcedCapture : ArrivalOutcome.Merged;
            }
            else
            {
                // One ship from each side until one side runs out. The stationed side is whoever's ships
                // are actually there: a capturing fleet rather than the object's formal owner.
                losses = Math.Min(arrivingShips, stationedShips);
                if (losses > 0) RecordBattle(fleet.Faction, losses, stationed, losses);
                int attackers = arrivingShips - losses;
                int defenders = stationedShips - losses;

                if (attackers > 0)
                {
                    node.Ships = attackers;
                    if (fleet.Faction == node.Owner)
                    {
                        // The incumbent owner beat a capturing fleet: control is restored immediately.
                        ClearCapture(node);
                        outcome = ArrivalOutcome.ControlRestored;
                    }
                    else
                    {
                        node.Captor = fleet.Faction;
                        node.CaptureElapsed = 0;
                        outcome = stationedShips == 0 ? ArrivalOutcome.Landed : ArrivalOutcome.AttackWon;
                    }
                }
                else if (defenders > 0)
                {
                    node.Ships = defenders;
                    outcome = ArrivalOutcome.Repelled;
                }
                else
                {
                    // A tie empties the object and clears any capture: it stays with its prior owner,
                    // or neutral if it was neutral.
                    node.Ships = 0;
                    ClearCapture(node);
                    outcome = ArrivalOutcome.Tie;
                }
            }

            FleetArrived?.Invoke(new ArrivalReport(node.Id, fleet.Id, fleet.Faction, arrivingShips, stationed,
                stationedShips, outcome, losses, node.Ships));
        }

        // ------------------------------------------------------------------ result

        /// <summary>A side stays alive while it owns an object, has a fleet in transit, or is capturing.</summary>
        public bool IsAlive(Faction faction)
        {
            foreach (Node node in nodes)
            {
                if (node.Owner == faction || node.Captor == faction) return true;
            }

            return HasFleetInTransit(faction);
        }

        public bool HasFleetInTransit(Faction faction)
        {
            foreach (Fleet fleet in fleets)
            {
                if (fleet.Faction == faction) return true;
            }

            return false;
        }

        /// <summary>Ships a faction has stationed and in transit.</summary>
        public int TotalShips(Faction faction)
        {
            int total = 0;
            foreach (Node node in nodes)
            {
                if (node.Occupant == faction) total += node.Ships;
            }

            foreach (Fleet fleet in fleets)
            {
                if (fleet.Faction == faction) total += fleet.Ships;
            }

            return total;
        }

        void EvaluateStatus()
        {
            if (Status != MatchStatus.InProgress) return;

            int planets = 0;
            int bluePlanets = 0;
            foreach (Node node in nodes)
            {
                if (node.Kind != NodeKind.Planet) continue;
                planets++;
                if (node.Owner == Faction.Blue) bluePlanets++;
            }

            // Moon ownership is deliberately not part of the victory check.
            if (planets > 0 && bluePlanets == planets && !HasFleetInTransit(Faction.Red))
                SetStatus(MatchStatus.BlueVictory);
            else if (!IsAlive(Faction.Blue))
                SetStatus(MatchStatus.BlueDefeat);
        }

        // Runs after the events of the deciding moment are resolved, so a battle that ends the match is
        // in the results, and time has already stopped, so nothing can be added to them later.
        void SetStatus(MatchStatus status)
        {
            Status = status;

            var totals = new PlayerResult[players.Count];
            for (int i = 0; i < totals.Length; i++)
            {
                MatchPlayer player = players[i];
                totals[i] = new PlayerResult(player.Faction, player.Name, player.Faction == Human,
                    shipsDestroyed[i], shipsLost[i]);
            }

            Results = new MatchResults(status, Elapsed, totals);
            StatusChanged?.Invoke(status);
        }

        bool IsNode(int id) => id >= 0 && id < nodes.Count;
    }
}
