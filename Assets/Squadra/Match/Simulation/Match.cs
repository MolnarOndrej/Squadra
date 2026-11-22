using System;
using System.Collections.Generic;

namespace Squadra.Simulation
{
    /// <summary>
    /// The whole match simulation: fleet orders, elapsed time, arrivals, battles, capture, production, each
    /// player's combat totals and the match result. The scene sends orders and time to this boundary and
    /// displays the state it exposes.
    ///
    /// The match decides what happens and in which order. Each <see cref="Node"/>, <see cref="Fleet"/>
    /// and <see cref="Battle"/> keeps its own clocks and counts, and the <see cref="MatchRoster"/> keeps
    /// the players' combat totals.
    /// </summary>
    public sealed class Match
    {
        // Tolerance for "this event is due now", so that stepping time in many small float increments
        // lands on whole-second events instead of missing them by a rounding error.
        internal const double Epsilon = 1e-7;

        readonly List<Node> nodes = new List<Node>();
        readonly List<Fleet> fleets = new List<Fleet>();
        readonly List<Fleet> arrivals = new List<Fleet>();
        readonly List<Battle> battles = new List<Battle>();
        readonly List<Route> routes = new List<Route>();
        readonly HashSet<Route> routeSet = new HashSet<Route>();
        readonly MatchRoster roster;
        readonly bool endsOnElimination;
        readonly bool thirdSideWaits;
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

            foreach (Route route in config.Routes)
            {
                if (!IsNode(route.A) || !IsNode(route.B) || route.A == route.B)
                    throw new ArgumentException("The route " + route + " does not join two objects of this map.", nameof(config));
                if (routeSet.Add(route)) routes.Add(route);
            }

            Human = config.Human;
            roster = new MatchRoster(config, nodes);
            endsOnElimination = config.EndsOnElimination;
            thirdSideWaits = config.ThirdSideWaits;
        }

        public IReadOnlyList<Node> Nodes => nodes;
        public IReadOnlyList<Fleet> Fleets => fleets;

        /// <summary>The routes fleets may travel, each once, in the order the configuration listed them.</summary>
        public IReadOnlyList<Route> Routes => routes;

        /// <summary>The battles being fought right now, at most one per map object.</summary>
        public IReadOnlyList<Battle> Battles => battles;

        /// <summary>Common fleet speed in map units per second. Only a sandbox edit changes it.</summary>
        public double FleetSpeed { get; private set; }
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
        public IReadOnlyList<MatchPlayer> Players => roster.Players;

        /// <summary>The final results, taken when the match ends. Null while it is in progress.</summary>
        public MatchResults Results { get; private set; }

        public event Action<Fleet> FleetLaunched;

        /// <summary>
        /// Raised for a fleet put on its way by <see cref="TryPlaceFleet"/> rather than launched: nothing
        /// left a world, so nothing should look or sound as if it had.
        /// </summary>
        public event Action<Fleet> FleetPlaced;

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

        /// <summary>True for a roster player. False for Neutral.</summary>
        public bool IsPlayer(Faction faction) => roster.Contains(faction);

        /// <summary>Individual Darts this player's fleets have destroyed so far, neutral guards included.</summary>
        public int DartsDestroyed(Faction faction) => roster.DartsDestroyed(faction);

        /// <summary>This player's Darts destroyed in combat so far, attacking or defending.</summary>
        public int DartsLost(Faction faction) => roster.DartsLost(faction);

        // ------------------------------------------------------------------ orders

        public double TravelTime(int sourceId, int targetId)
        {
            return MapPoint.Distance(nodes[sourceId].Position, nodes[targetId].Position) / FleetSpeed;
        }

        /// <summary>True when a route joins the two objects directly, in either direction.</summary>
        public bool AreConnected(int a, int b) => routeSet.Contains(new Route(a, b));

        /// <summary>
        /// True when <paramref name="issuer"/> may send the fleet stationed at the source to the target:
        /// the issuer owns the source, the source holds Darts that are neither mid-capture nor committed
        /// to a battle, and a route joins the source directly to a different target.
        /// </summary>
        public bool CanLaunch(Faction issuer, int sourceId, int targetId)
        {
            if (Status != MatchStatus.InProgress) return false;
            if (issuer == Faction.Neutral) return false;
            if (!IsNode(sourceId) || !IsNode(targetId) || sourceId == targetId) return false;
            if (!AreConnected(sourceId, targetId)) return false;

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
        /// the same result as many small ones. Time stops once the match has a result; a match that does
        /// not end on elimination never has one.
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
                next = Math.Min(next, node.TimeToNextEvent);

            foreach (Fleet fleet in fleets)
            {
                // A fleet waiting outside a battle has no event of its own: the battle's end releases it.
                if (fleet.HasArrived && MustWait(fleet)) continue;
                next = Math.Min(next, fleet.Duration - fleet.Elapsed);
            }

            return Math.Max(0, next);
        }

        void Step(double step)
        {
            Elapsed += step;
            foreach (Node node in nodes)
                node.Advance(step);

            foreach (Fleet fleet in fleets)
                fleet.Elapsed += step;
        }

        // Order within one instant: production, then capture completion, then arrivals in launch order,
        // then the casualties of battles. A fleet therefore meets every Dart produced up to and including
        // its moment of arrival, and a reinforcement is counted before a casualty due at the same moment.
        // Where third sides wait (a sandbox), a fleet that waited outside a battle arrives right after the
        // casualties that ended it, still in launch order, and the battles are settled again after it.
        void ResolveDueEvents()
        {
            foreach (Node node in nodes)
            {
                while (node.IsProductionDue)
                {
                    node.ProduceDart();
                    DartProduced?.Invoke(node);
                }
            }

            foreach (Node node in nodes)
            {
                if (!node.IsCaptureDue) continue;

                Faction previousOwner = node.CompleteCapture();
                CaptureCompleted?.Invoke(node, previousOwner);
            }

            arrivals.Clear();
            foreach (Fleet fleet in fleets)
            {
                if (fleet.HasArrived) arrivals.Add(fleet);
            }

            foreach (Fleet fleet in arrivals)
            {
                if (MustWait(fleet)) continue; // also a battle that began earlier in this instant
                RequireTwoSides(fleet);
                fleets.Remove(fleet);
                ResolveArrival(fleet);
            }

            do
            {
                foreach (Node node in nodes)
                {
                    if (node.IsContested) ResolveBattle(node);
                }
            }
            while (ReleaseWaitingFleets());
        }

        // ------------------------------------------------------------------ arrivals

        // True for a fleet that must wait outside its target: a battle between two other sides is being
        // fought there and the match lets third sides wait.
        bool MustWait(Fleet fleet)
        {
            if (!thirdSideWaits) return false;
            Battle battle = nodes[fleet.TargetId].Battle;
            return battle != null && !battle.Involves(fleet.Faction);
        }

        // The fleets still in transit that have reached their target are the ones waiting outside a
        // battle. Those whose battle is over now arrive, in launch order; returns whether any did.
        bool ReleaseWaitingFleets()
        {
            if (!thirdSideWaits) return false;

            arrivals.Clear();
            foreach (Fleet fleet in fleets)
            {
                if (fleet.HasArrived) arrivals.Add(fleet);
            }

            bool released = false;
            foreach (Fleet fleet in arrivals)
            {
                if (MustWait(fleet)) continue;
                fleets.Remove(fleet);
                ResolveArrival(fleet);
                released = true;
            }

            return released;
        }

        // A battle has exactly two sides. What a third faction's arrival at a battle should do has not
        // been decided, so a mission refuses it loudly rather than giving it a rule nobody agreed to.
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
                // Join the side the fleet belongs to. The battle replaces its schedule once every
                // arrival of this instant has been counted.
                Battle battle = node.Battle;
                battle.Reinforce(fleet.Faction, fleet.Darts);
                node.Darts = battle.StationedDarts;
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
                    node.ClearCapture();
                    outcome = ArrivalOutcome.Merged;
                }
                else
                {
                    node.BeginCapture(fleet.Faction);
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
                battle.Schedule(BattleTiming);
                outcome = ArrivalOutcome.BattleStarted;
                BattleStarted?.Invoke(battle);
            }

            FleetArrived?.Invoke(new ArrivalReport(node.Id, fleet.Id, fleet.Faction, fleet.Darts, stationed,
                stationedDarts, outcome));
        }

        // ------------------------------------------------------------------ battles

        void ResolveBattle(Node node)
        {
            Battle battle = node.Battle;
            int pairs = battle.TakeDueCasualties(BattleTiming);
            if (pairs > 0)
            {
                node.Darts = battle.StationedDarts;
                roster.RecordCasualties(battle.Arriving, pairs, battle.Stationed, pairs);
                BattleCasualties?.Invoke(battle, pairs);
            }

            if (battle.IsDecided) EndBattle(node, battle);
        }

        void EndBattle(Node node, Battle battle)
        {
            BattleOutcome outcome = FinishBattle(node, battle);
            BattleEnded?.Invoke(new BattleReport(node.Id, battle.Arriving, battle.Stationed, outcome,
                battle.LossesPerSide, node.Darts, battle.Elapsed));
        }

        // Takes a decided battle off the object and leaves the object to whoever is left.
        BattleOutcome FinishBattle(Node node, Battle battle)
        {
            node.Battle = null;
            battles.Remove(battle);
            battle.End();

            BattleOutcome outcome;
            if (battle.ArrivingDarts > 0)
            {
                node.Darts = battle.ArrivingDarts;
                if (battle.Arriving == node.Owner)
                {
                    // The incumbent owner beat a capturing force: control is restored immediately.
                    node.ClearCapture();
                    outcome = BattleOutcome.ControlRestored;
                }
                else
                {
                    // A new claimant starts its own countdown from zero.
                    node.BeginCapture(battle.Arriving);
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
                node.ClearCapture();
                outcome = BattleOutcome.Tie;
            }

            return outcome;
        }

        // ------------------------------------------------------------------ setting up and editing (sandbox)

        // A sandbox lays down a moment of play and edits it while it runs. These are administrative: nothing
        // is fought, produced or captured by them, no casualty is counted, and no match event is raised but
        // FleetPlaced. Each checks everything first and changes nothing when it refuses, saying why.

        /// <summary>
        /// Puts a fleet of <paramref name="darts"/> on its way from one object to another as if it had
        /// already flown <paramref name="progress"/> of the route (0 to 1). Nothing leaves the source.
        /// </summary>
        public bool TryPlaceFleet(Faction faction, int darts, int sourceId, int targetId, double progress,
            out Fleet fleet, out string problem)
        {
            fleet = null;
            problem = null;
            if (!IsPlayer(faction)) problem = faction + " is not a player of this match.";
            else if (!IsNode(sourceId) || !IsNode(targetId)) problem = "It needs a world to set out from and one to fly to.";
            else if (sourceId == targetId) problem = "It sets out from and flies to " + nodes[sourceId].Name + ".";
            else if (!AreConnected(sourceId, targetId))
                problem = "No route from " + nodes[sourceId].Name + " to " + nodes[targetId].Name + ".";
            else if (darts < 1) problem = "A fleet needs at least one Dart.";
            else if (!(progress >= 0 && progress <= 1)) problem = "How far along it is must be from 0 to 1.";
            if (problem != null) return false;

            fleet = new Fleet(nextFleetId++, faction, darts, sourceId, targetId, TravelTime(sourceId, targetId));
            fleet.Elapsed = progress * fleet.Duration;
            fleets.Add(fleet);
            FleetPlaced?.Invoke(fleet);
            return true;
        }

        /// <summary>
        /// Starts a capture of an object by <paramref name="captor"/> as if it had run for
        /// <paramref name="progress"/> of its time (0 to 1). The Darts stationed there are the captor's.
        /// With no Darts there nobody can capture: any capture is cleared and the owner keeps the object.
        /// </summary>
        public bool TryStageCapture(int nodeId, Faction captor, double progress, out string problem)
        {
            problem = null;
            if (!IsNode(nodeId)) problem = "There is no such world.";
            else if (!IsPlayer(captor)) problem = captor + " is not a player of this match.";
            else if (nodes[nodeId].Owner == captor) problem = captor + " already owns " + nodes[nodeId].Name + ".";
            else if (nodes[nodeId].IsContested) problem = "A battle is being fought at " + nodes[nodeId].Name + ".";
            else if (!(progress >= 0 && progress <= 1)) problem = "Capture progress must be from 0 to 1.";
            if (problem != null) return false;

            Node node = nodes[nodeId];
            if (node.Darts == 0) node.ClearCapture();
            else node.StageCapture(captor, progress * CaptureDuration);
            return true;
        }

        /// <summary>
        /// Starts a battle at an object between the Darts stationed there and <paramref name="darts"/> of
        /// <paramref name="attacker"/>, on the normal casualty schedule. With no attackers there is no battle.
        /// With nobody stationed there the attackers land as any fleet would: an owner takes its object back,
        /// anyone else starts a fresh capture.
        /// </summary>
        public bool TryStageBattle(int nodeId, Faction attacker, int darts, out string problem)
        {
            problem = null;
            if (!IsNode(nodeId)) problem = "There is no such world.";
            else if (!IsPlayer(attacker)) problem = attacker + " is not a player of this match.";
            else if (nodes[nodeId].IsContested) problem = "A battle is already being fought at " + nodes[nodeId].Name + ".";
            else if (darts < 0) problem = "The attackers cannot be fewer than none.";
            else if (darts > 0 && nodes[nodeId].Darts > 0 && nodes[nodeId].Occupant == attacker)
                problem = "The Darts stationed at " + nodes[nodeId].Name + " are " + attacker + "'s own.";
            if (problem != null) return false;

            Node node = nodes[nodeId];
            if (darts == 0) return true;

            if (node.Darts == 0)
            {
                node.Darts = darts;
                if (attacker == node.Owner) node.ClearCapture();
                else node.BeginCapture(attacker);
                return true;
            }

            var battle = new Battle(node.Id, node.Occupant, node.Darts, attacker, darts);
            node.Battle = battle;
            battles.Add(battle);
            battle.Schedule(BattleTiming);
            return true;
        }

        /// <summary>
        /// Replaces the number of Darts stationed at an object: the owner's, the captor's during a capture,
        /// or the defenders' during a battle, which is then timed afresh for the forces present, as after a
        /// reinforcement. With none left a capture is cleared and the owner keeps the object, and a battle
        /// ends with the attackers landing as on an empty object.
        /// </summary>
        public bool TrySetStationedDarts(int nodeId, int darts, out string problem)
        {
            problem = null;
            if (!IsNode(nodeId)) problem = "There is no such world.";
            else if (darts < 0) problem = "Darts cannot be fewer than none.";
            if (problem != null) return false;

            Node node = nodes[nodeId];
            Battle battle = node.Battle;
            if (battle != null)
            {
                battle.SetStationedDarts(darts);
                if (darts == 0)
                {
                    FinishBattle(node, battle);
                    return true;
                }

                battle.Schedule(BattleTiming);
            }

            node.Darts = darts;
            if (darts == 0 && node.IsBeingCaptured) node.ClearCapture();
            return true;
        }

        /// <summary>
        /// Hands an object to <paramref name="owner"/>, or makes it neutral, at once. A battle or capture
        /// there is called off: the attackers are gone, without counting as casualties, and the Darts
        /// stationed there are the new owner's. Fleets on their way are left alone.
        /// </summary>
        public bool TrySetOwner(int nodeId, Faction owner, out string problem)
        {
            problem = null;
            if (!IsNode(nodeId)) problem = "There is no such world.";
            else if (owner != Faction.Neutral && !IsPlayer(owner)) problem = owner + " is not a player of this match.";
            if (problem != null) return false;

            Node node = nodes[nodeId];
            Battle battle = node.Battle;
            if (battle != null)
            {
                node.Battle = null;
                battles.Remove(battle);
                battle.End();
            }

            node.SetOwner(owner);
            return true;
        }

        /// <summary>
        /// Makes an object another kind, producing a Dart every <paramref name="productionInterval"/>
        /// seconds unless it is an asteroid, and with its production switched on, as for a new object of that
        /// kind. Owner, Darts, a capture and a battle there stay as they are, and so do routes and fleets.
        /// </summary>
        public bool TrySetKind(int nodeId, NodeKind kind, double productionInterval, out string problem)
        {
            problem = null;
            if (!IsNode(nodeId)) problem = "There is no such world.";
            else if (kind != NodeKind.Planet && kind != NodeKind.Moon && kind != NodeKind.Asteroid)
                problem = "A world is a planet, a moon or an asteroid.";
            else if (kind != NodeKind.Asteroid && !(productionInterval >= Node.MinProductionInterval))
                problem = "Production needs at least " + Node.MinProductionInterval + " seconds per Dart.";
            if (problem != null) return false;

            nodes[nodeId].ChangeKind(kind, productionInterval);
            return true;
        }

        /// <summary>Switches an object's production off or on again. An asteroid never produces.</summary>
        public bool TrySetProducing(int nodeId, bool producing, out string problem)
        {
            problem = null;
            if (!IsNode(nodeId)) problem = "There is no such world.";
            else if (producing && nodes[nodeId].Kind == NodeKind.Asteroid) problem = "Asteroids never produce Darts.";
            if (problem != null) return false;

            nodes[nodeId].SetProductionStopped(!producing);
            return true;
        }

        /// <summary>
        /// Sets an object's seconds per produced Dart. The Dart in progress keeps how far along it is and
        /// is finished at the new pace. An asteroid keeps producing nothing.
        /// </summary>
        public bool TrySetProductionInterval(int nodeId, double seconds, out string problem)
        {
            problem = null;
            if (!IsNode(nodeId)) problem = "There is no such world.";
            else if (!(seconds >= Node.MinProductionInterval))
                problem = "Production needs at least " + Node.MinProductionInterval + " seconds per Dart.";
            if (problem != null) return false;

            Node node = nodes[nodeId];
            if (node.Kind != NodeKind.Asteroid) node.RetimeProduction(seconds);
            return true;
        }

        /// <summary>
        /// Sets the common fleet speed. Fleets on their way keep how far along they are and fly the rest
        /// at the new speed; fleets launched from now on take the new speed throughout.
        /// </summary>
        public bool TrySetFleetSpeed(double speed, out string problem)
        {
            problem = speed > 0 && !double.IsInfinity(speed) ? null : "Fleet speed must be above zero.";
            if (problem != null) return false;

            FleetSpeed = speed;
            foreach (Fleet fleet in fleets)
                fleet.Retime(TravelTime(fleet.SourceId, fleet.TargetId));
            return true;
        }

        // ------------------------------------------------------------------ result

        /// <summary>
        /// A side stays in the match while it owns any planet, moon or asteroid, an empty one included, or
        /// has Darts anywhere: stationed, capturing, fighting on either side of a battle or in transit.
        /// Darts count for the side they belong to, whoever formally owns the object they are at.
        /// </summary>
        public bool IsAlive(Faction faction)
        {
            foreach (Node node in nodes)
            {
                if (node.Owner == faction) return true;
            }

            return TotalDarts(faction) > 0;
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

        // One elimination rule for every mission: the human wins once no other player is left in the match
        // (see IsAlive) and loses once the human is not. Neutral is nobody's enemy, so neutral objects and
        // guards never stand in the way. Should the human and the last opponent go out at the same instant,
        // the human has not survived to win: that is a defeat.
        void EvaluateStatus()
        {
            if (Status != MatchStatus.InProgress || !endsOnElimination) return;

            if (!IsAlive(Human))
                SetStatus(MatchStatus.BlueDefeat);
            else if (!AnyOpponentAlive())
                SetStatus(MatchStatus.BlueVictory);
        }

        bool AnyOpponentAlive()
        {
            foreach (MatchPlayer player in roster.Players)
            {
                if (player.Faction != Human && IsAlive(player.Faction)) return true;
            }

            return false;
        }

        // Runs after the events of the deciding moment are resolved, so the casualties that end the match
        // are in the results, and time has already stopped, so nothing can be added to them later.
        void SetStatus(MatchStatus status)
        {
            Status = status;
            Results = new MatchResults(status, Elapsed, roster.Totals());
            StatusChanged?.Invoke(status);
        }

        bool IsNode(int id) => id >= 0 && id < nodes.Count;
    }
}
