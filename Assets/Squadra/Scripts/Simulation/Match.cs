using System;
using System.Collections.Generic;

namespace Squadra.Simulation
{
    /// <summary>
    /// The whole match simulation: fleet orders, elapsed time, arrivals, battles, capture, production, each
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
        readonly List<Battle> battles = new List<Battle>();
        readonly List<MatchPlayer> players = new List<MatchPlayer>();
        readonly int[] dartsDestroyed;
        readonly int[] dartsLost;
        int nextFleetId = 1;

        public Match(MatchConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (config.FleetSpeed <= 0) throw new ArgumentException("Fleet speed must be positive.", nameof(config));

            FleetSpeed = config.FleetSpeed;
            CaptureDuration = Math.Max(0, config.CaptureDuration);
            BattleTiming = config.BattleTiming ?? new BattleTiming();
            for (int i = 0; i < config.Nodes.Count; i++)
                nodes.Add(new Node(i, config.Nodes[i], CaptureDuration));

            Human = config.Human;
            BuildRoster(config);
            dartsDestroyed = new int[players.Count];
            dartsLost = new int[players.Count];
        }

        public IReadOnlyList<Node> Nodes => nodes;
        public IReadOnlyList<Fleet> Fleets => fleets;

        /// <summary>The battles being fought right now, at most one per map object.</summary>
        public IReadOnlyList<Battle> Battles => battles;

        public double FleetSpeed { get; }
        public double CaptureDuration { get; }

        /// <summary>How long battles last. Changing it affects the battles and reinforcements that follow.</summary>
        public BattleTiming BattleTiming { get; }

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
        /// the roster after losing every Dart and object. Neutral is never on it.
        /// </summary>
        public IReadOnlyList<MatchPlayer> Players => players;

        /// <summary>The final results, taken when the match ends. Null while it is in progress.</summary>
        public MatchResults Results { get; private set; }

        public event Action<Fleet> FleetLaunched;

        /// <summary>
        /// Raised for every fleet that reaches its destination, whether it merged, landed, started a
        /// battle or joined one. A battle it started has been raised through <see cref="BattleStarted"/>
        /// just before.
        /// </summary>
        public event Action<ArrivalReport> FleetArrived;
        public event Action<Node> DartProduced;

        /// <summary>Raised when ownership formally changes. The second argument is the previous owner.</summary>
        public event Action<Node, Faction> CaptureCompleted;

        /// <summary>Raised when a hostile fleet reaches a defended object. Nobody has lost a Dart yet.</summary>
        public event Action<Battle> BattleStarted;

        /// <summary>
        /// Raised each time a battle destroys Darts. The second argument is the number of Darts each side
        /// lost in this step; the battle's counts are already the ones after it.
        /// </summary>
        public event Action<Battle, int> BattleCasualties;

        /// <summary>Raised when a side has run out, after the outcome has been applied to the object.</summary>
        public event Action<BattleReport> BattleEnded;
        public event Action<MatchStatus> StatusChanged;

        // ------------------------------------------------------------------ players

        // The roster comes from the mission, not from who still has Darts: the configured players, then
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

        /// <summary>Individual Darts this player's fleets have destroyed so far, neutral guards included.</summary>
        public int DartsDestroyed(Faction faction)
        {
            int index = PlayerIndex(faction);
            return index < 0 ? 0 : dartsDestroyed[index];
        }

        /// <summary>This player's Darts destroyed in combat so far, attacking or defending.</summary>
        public int DartsLost(Faction faction)
        {
            int index = PlayerIndex(faction);
            return index < 0 ? 0 : dartsLost[index];
        }

        // Casualties of one step of a battle, counted when they happen. A side's casualties are its own
        // losses and the other side's kills. Only roster players keep totals, so Darts lost to neutral
        // guards are credited to nobody, while destroyed guards still count for the player that
        // destroyed them.
        void RecordCasualties(Faction arriving, int arrivingCasualties, Faction stationed, int stationedCasualties)
        {
            int arrivingPlayer = PlayerIndex(arriving);
            if (arrivingPlayer >= 0)
            {
                dartsLost[arrivingPlayer] += arrivingCasualties;
                dartsDestroyed[arrivingPlayer] += stationedCasualties;
            }

            int stationedPlayer = PlayerIndex(stationed);
            if (stationedPlayer >= 0)
            {
                dartsLost[stationedPlayer] += stationedCasualties;
                dartsDestroyed[stationedPlayer] += arrivingCasualties;
            }
        }

        // ------------------------------------------------------------------ orders

        public double TravelTime(int sourceId, int targetId)
        {
            return MapPoint.Distance(nodes[sourceId].Position, nodes[targetId].Position) / FleetSpeed;
        }

        /// <summary>
        /// True when <paramref name="issuer"/> may send the fleet stationed at the source to the target:
        /// the issuer owns the source, the source holds Darts that are neither mid-capture nor committed
        /// to a battle, and the target differs.
        /// </summary>
        public bool CanLaunch(Faction issuer, int sourceId, int targetId)
        {
            if (Status != MatchStatus.InProgress) return false;
            if (issuer == Faction.Neutral) return false;
            if (!IsNode(sourceId) || !IsNode(targetId) || sourceId == targetId) return false;

            Node source = nodes[sourceId];
            return source.Owner == issuer && !source.IsBeingCaptured && !source.IsContested && source.Darts > 0;
        }

        /// <summary>Sends every Dart stationed at the source toward the target. The source stays owned.</summary>
        public bool TryLaunch(Faction issuer, int sourceId, int targetId, out Fleet fleet)
        {
            fleet = null;
            if (!CanLaunch(issuer, sourceId, targetId)) return false;

            Node source = nodes[sourceId];
            fleet = new Fleet(nextFleetId++, issuer, source.Darts, sourceId, targetId, TravelTime(sourceId, targetId));
            source.Darts = 0;
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
                if (node.IsContested)
                    next = Math.Min(next, node.Battle.TimeToNextPair);
                else if (node.IsBeingCaptured)
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
                if (node.IsContested)
                {
                    // A contested object neither produces nor is captured: both clocks keep their place.
                    node.Battle.ScheduleClock += step;
                    node.Battle.Elapsed += step;
                }
                else if (node.IsBeingCaptured) node.CaptureElapsed += step;
                else if (node.IsProducing) node.ProductionClock += step;
            }

            foreach (Fleet fleet in fleets)
                fleet.Elapsed += step;
        }

        // Order within one instant: production, then capture completion, then arrivals in launch order,
        // then the casualties of battles. A fleet therefore meets every Dart produced up to and including
        // its moment of arrival, and a reinforcement is counted before a casualty due at the same moment.
        void ResolveDueEvents()
        {
            foreach (Node node in nodes)
            {
                while (node.IsProducing && node.ProductionInterval - node.ProductionClock <= Epsilon)
                {
                    node.ProductionClock = Math.Max(0, node.ProductionClock - node.ProductionInterval);
                    node.Darts++;
                    DartProduced?.Invoke(node);
                }
            }

            foreach (Node node in nodes)
            {
                if (!node.IsContested && node.IsBeingCaptured && node.CaptureDuration - node.CaptureElapsed <= Epsilon)
                    CompleteCapture(node);
            }

            arrivals.Clear();
            foreach (Fleet fleet in fleets)
            {
                if (fleet.Duration - fleet.Elapsed <= Epsilon) arrivals.Add(fleet);
            }

            foreach (Fleet fleet in arrivals)
            {
                RequireTwoSides(fleet);
                fleets.Remove(fleet);
                ResolveArrival(fleet);
            }

            foreach (Node node in nodes)
            {
                if (node.IsContested) ResolveBattle(node);
            }
        }

        // ------------------------------------------------------------------ capture

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

        // ------------------------------------------------------------------ arrivals

        // A battle has exactly two sides. What a third faction's arrival at a battle should do has not
        // been decided, so it is refused loudly rather than given a rule nobody agreed to.
        void RequireTwoSides(Fleet fleet)
        {
            Battle battle = nodes[fleet.TargetId].Battle;
            if (battle == null || battle.Involves(fleet.Faction)) return;

            throw new NotSupportedException(
                "A " + fleet.Faction + " fleet reached " + nodes[fleet.TargetId].Name + " during a battle between "
                + battle.Stationed + " and " + battle.Arriving + ". Battles with a third side are not supported.");
        }

        void ResolveArrival(Fleet fleet)
        {
            Node node = nodes[fleet.TargetId];
            Faction stationed = node.Occupant;
            int stationedDarts = node.Darts;
            ArrivalOutcome outcome;

            if (node.IsContested)
            {
                // Join the side the fleet belongs to. The battle's schedule is replaced once every
                // arrival of this instant has been counted.
                Battle battle = node.Battle;
                if (fleet.Faction == battle.Stationed)
                {
                    battle.StationedDarts += fleet.Darts;
                    node.Darts = battle.StationedDarts;
                }
                else
                {
                    battle.ArrivingDarts += fleet.Darts;
                }

                battle.Reinforced = true;
                outcome = ArrivalOutcome.BattleJoined;
            }
            else if (fleet.Faction == stationed)
            {
                // Allied with whoever holds the object: join them. A capture clock keeps running.
                node.Darts += fleet.Darts;
                outcome = node.IsBeingCaptured ? ArrivalOutcome.ReinforcedCapture : ArrivalOutcome.Merged;
            }
            else if (stationedDarts == 0)
            {
                // Nobody to fight: the whole fleet lands. An owner coming home takes control back at
                // once; anyone else starts the capture countdown.
                node.Darts = fleet.Darts;
                if (fleet.Faction == node.Owner)
                {
                    ClearCapture(node);
                    outcome = ArrivalOutcome.Merged;
                }
                else
                {
                    node.Captor = fleet.Faction;
                    node.CaptureElapsed = 0;
                    outcome = ArrivalOutcome.Landed;
                }
            }
            else
            {
                // The stationed side is whoever's Darts are actually there: a capturing fleet or neutral
                // guards rather than the object's formal owner. Nothing is lost and nothing is decided yet.
                var battle = new Battle(node.Id, stationed, stationedDarts, fleet.Faction, fleet.Darts);
                node.Battle = battle;
                battles.Add(battle);
                Schedule(battle);
                outcome = ArrivalOutcome.BattleStarted;
                BattleStarted?.Invoke(battle);
            }

            FleetArrived?.Invoke(new ArrivalReport(node.Id, fleet.Id, fleet.Faction, fleet.Darts, stationed,
                stationedDarts, outcome));
        }

        // ------------------------------------------------------------------ battles

        // Spreads the pairs still to be lost evenly over a fresh duration for the forces now present.
        void Schedule(Battle battle)
        {
            battle.ScheduleClock = 0;
            battle.ScheduleDuration = BattleTiming.Duration(battle.ArrivingDarts, battle.StationedDarts);
            battle.SchedulePairs = Math.Max(1, Math.Min(battle.ArrivingDarts, battle.StationedDarts));
            battle.PairsDone = 0;
        }

        void ResolveBattle(Node node)
        {
            Battle battle = node.Battle;
            int pairs = 0;

            if (battle.Reinforced)
            {
                // Reinforcements have been counted. A pair that was already due still falls now, against
                // the reinforced forces; whatever fighting is left gets a fresh schedule.
                battle.Reinforced = false;
                if (battle.TimeToNextPair <= Epsilon) pairs += LosePair(battle);
                if (battle.ArrivingDarts > 0 && battle.StationedDarts > 0) Schedule(battle);
            }

            while (battle.ArrivingDarts > 0 && battle.StationedDarts > 0 && battle.TimeToNextPair <= Epsilon)
                pairs += LosePair(battle);

            if (pairs > 0)
            {
                node.Darts = battle.StationedDarts;
                RecordCasualties(battle.Arriving, pairs, battle.Stationed, pairs);
                BattleCasualties?.Invoke(battle, pairs);
            }

            if (battle.ArrivingDarts == 0 || battle.StationedDarts == 0) EndBattle(node, battle);
        }

        // One Dart from each side, at the same moment.
        static int LosePair(Battle battle)
        {
            battle.ArrivingDarts--;
            battle.StationedDarts--;
            battle.LossesPerSide++;
            battle.PairsDone++;
            return 1;
        }

        void EndBattle(Node node, Battle battle)
        {
            node.Battle = null;
            battles.Remove(battle);
            battle.IsOver = true;

            BattleOutcome outcome;
            if (battle.ArrivingDarts > 0)
            {
                node.Darts = battle.ArrivingDarts;
                if (battle.Arriving == node.Owner)
                {
                    // The incumbent owner beat a capturing force: control is restored immediately.
                    ClearCapture(node);
                    outcome = BattleOutcome.ControlRestored;
                }
                else
                {
                    // A new claimant starts its own countdown from zero.
                    node.Captor = battle.Arriving;
                    node.CaptureElapsed = 0;
                    outcome = BattleOutcome.AttackWon;
                }
            }
            else if (battle.StationedDarts > 0)
            {
                // The stationed side carries on where it was: an owner producing, a captor capturing.
                node.Darts = battle.StationedDarts;
                outcome = BattleOutcome.Repelled;
            }
            else
            {
                // A tie empties the object and clears any capture: it stays with its prior owner,
                // or neutral if it was neutral.
                node.Darts = 0;
                ClearCapture(node);
                outcome = BattleOutcome.Tie;
            }

            BattleEnded?.Invoke(new BattleReport(node.Id, battle.Arriving, battle.Stationed, outcome,
                battle.LossesPerSide, node.Darts, battle.Elapsed));
        }

        // ------------------------------------------------------------------ result

        /// <summary>
        /// A side stays alive while it owns an object, has a fleet in transit, is capturing, or has
        /// Darts fighting in a battle.
        /// </summary>
        public bool IsAlive(Faction faction)
        {
            foreach (Node node in nodes)
            {
                if (node.Owner == faction || node.Captor == faction) return true;
            }

            return HasFleetInTransit(faction) || IsAttacking(faction);
        }

        public bool HasFleetInTransit(Faction faction)
        {
            foreach (Fleet fleet in fleets)
            {
                if (fleet.Faction == faction) return true;
            }

            return false;
        }

        /// <summary>True while this faction has Darts fighting in a battle, on either side.</summary>
        public bool IsEngaged(Faction faction)
        {
            foreach (Battle battle in battles)
            {
                if (battle.Involves(faction)) return true;
            }

            return false;
        }

        // True while a fleet of this faction that arrived somewhere is still fighting there. Such a force
        // is no longer in transit, but it has not been beaten either.
        bool IsAttacking(Faction faction)
        {
            foreach (Battle battle in battles)
            {
                if (battle.Arriving == faction) return true;
            }

            return false;
        }

        /// <summary>Darts a faction has stationed, fighting in battles and in transit.</summary>
        public int TotalDarts(Faction faction)
        {
            int total = 0;
            foreach (Node node in nodes)
            {
                if (node.Occupant == faction) total += node.Darts;
            }

            foreach (Battle battle in battles)
            {
                if (battle.Arriving == faction) total += battle.ArrivingDarts;
            }

            foreach (Fleet fleet in fleets)
            {
                if (fleet.Faction == faction) total += fleet.Darts;
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

            // Moon ownership is deliberately not part of the victory check. A Red fleet that has arrived
            // and is still fighting counts like one in transit: its battle is not over.
            if (planets > 0 && bluePlanets == planets && !HasFleetInTransit(Faction.Red) && !IsAttacking(Faction.Red))
                SetStatus(MatchStatus.BlueVictory);
            else if (!IsAlive(Faction.Blue))
                SetStatus(MatchStatus.BlueDefeat);
        }

        // Runs after the events of the deciding moment are resolved, so the casualties that end the match
        // are in the results, and time has already stopped, so nothing can be added to them later.
        void SetStatus(MatchStatus status)
        {
            Status = status;

            var totals = new PlayerResult[players.Count];
            for (int i = 0; i < totals.Length; i++)
            {
                MatchPlayer player = players[i];
                totals[i] = new PlayerResult(player.Faction, player.Name, player.Faction == Human,
                    dartsDestroyed[i], dartsLost[i]);
            }

            Results = new MatchResults(status, Elapsed, totals);
            StatusChanged?.Invoke(status);
        }

        bool IsNode(int id) => id >= 0 && id < nodes.Count;
    }
}
