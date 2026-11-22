using System;
using System.Collections.Generic;
using Squadra.Simulation;

namespace Squadra.Sandbox
{
    /// <summary>
    /// One run of the sandbox: a <see cref="Match"/> set up from the authored <see cref="SandboxSetup"/>
    /// that never ends and has no AI, and every command the sandbox gives it. Any player's world can be
    /// ordered, without switching players; worlds can be edited; the rules can be retuned for this run;
    /// and the battlefield can be reset, replenished or prepared for an example capture or battle.
    ///
    /// The match keeps every rule. This only lays the starting moment down, resolves a command into the
    /// match's own operations and remembers the run's tuning. A command that cannot be carried out changes
    /// nothing and says why. The setup is never changed, nor anything outside the run.
    /// </summary>
    public sealed class SandboxSession
    {
        /// <summary>The most Darts a world can be given by hand.</summary>
        public const int MostDarts = 1000000;

        /// <summary>The range of fleet speeds the sandbox can be tuned to, in map units per second.</summary>
        public const double SlowestFleet = 0.01;
        public const double FastestFleet = 1000;

        /// <summary>The range of seconds per Dart the sandbox can be tuned to.</summary>
        public const double FastestProduction = 0.05;
        public const double SlowestProduction = 3600;

        // The examples: a fleet put on its way this many seconds before it arrives, of these sizes.
        const double ExampleLead = 1.5;
        const int CaptureExampleDarts = 10;
        const int BattleExampleDefenders = 15;
        const int BattleExampleAttackers = 25;

        readonly SandboxSetup setup;
        readonly MatchOverrides rules;
        readonly List<string> problems = new List<string>();

        public SandboxSession(SandboxSetup setup)
        {
            this.setup = setup ?? throw new ArgumentNullException(nameof(setup));
            rules = new MatchOverrides
            {
                FleetSpeed = setup.Rules.FleetSpeed,
                PlanetProductionInterval = setup.Rules.PlanetProductionInterval,
                MoonProductionInterval = setup.Rules.MoonProductionInterval,
            };
            Start();
        }

        /// <summary>The run's match. A reset replaces it with a new one; see <see cref="Started"/>.</summary>
        public Match Match { get; private set; }

        /// <summary>The players, in the order the setup lists them.</summary>
        public IReadOnlyList<MatchPlayer> Players => Match.Players;

        /// <summary>
        /// What of the setup could not be laid down when the match was last set up, one line per entry,
        /// naming it and why. The rest of the setup was laid down all the same.
        /// </summary>
        public IReadOnlyList<string> Problems => problems;

        /// <summary>Raised after <see cref="Reset"/> has set up a new <see cref="Match"/>.</summary>
        public event Action Started;

        /// <summary>
        /// Lays the authored moment down again: worlds, captures, battles and fleets under way as they were
        /// set up, with nothing of this run's orders or edits. The run's tuning is kept.
        /// </summary>
        public void Reset()
        {
            Start();
            Started?.Invoke();
        }

        public void Advance(double seconds)
        {
            Match.Advance(seconds);
        }

        void Start()
        {
            problems.Clear();

            var config = new MatchConfig { EndsOnElimination = false, ThirdSideWaits = true };
            foreach (Faction player in setup.Players)
                config.Players.Add(new MatchPlayer(player));
            if (setup.Players.Count > 0) config.Human = setup.Players[0];

            foreach (NodeConfig world in setup.Worlds)
                config.Nodes.Add(Copy(world));

            for (int i = 0; i < setup.Routes.Count; i++)
            {
                Route route = setup.Routes[i];
                if (IsWorld(route.A) && IsWorld(route.B) && route.A != route.B) config.Routes.Add(route);
                else problems.Add("Route " + (i + 1) + " is left out: it does not join two different worlds.");
            }

            rules.ApplyTo(config);
            Match = new Match(config);

            // Captures first: the Darts stationed at a world being captured are the captor's, and a battle
            // there is fought by them.
            foreach (StartingCapture capture in setup.Captures)
            {
                if (!Match.TryStageCapture(capture.World, capture.Captor, capture.Progress, out string problem))
                    problems.Add("The capture of " + WorldName(capture.World) + " is left out: " + problem);
            }

            foreach (StartingBattle battle in setup.Battles)
            {
                if (!Match.TryStageBattle(battle.World, battle.Attacker, battle.Darts, out string problem))
                    problems.Add("The battle at " + WorldName(battle.World) + " is left out: " + problem);
            }

            for (int i = 0; i < setup.Fleets.Count; i++)
            {
                StartingFleet fleet = setup.Fleets[i];
                if (!Match.TryPlaceFleet(fleet.Faction, fleet.Darts, fleet.From, fleet.To, fleet.Progress, out _,
                        out string problem))
                    problems.Add("Fleet " + (i + 1) + " (" + fleet.Faction + ", " + fleet.Darts + " Darts) is left out: "
                        + problem);
            }
        }

        static NodeConfig Copy(NodeConfig world)
        {
            return new NodeConfig
            {
                Name = world.Name,
                Kind = world.Kind,
                Position = world.Position,
                Owner = world.Owner,
                Darts = world.Darts,
                ProductionInterval = world.ProductionInterval,
            };
        }

        bool IsWorld(int index) => index >= 0 && index < setup.Worlds.Count;

        string WorldName(int index) => IsWorld(index) ? setup.Worlds[index].Name : "world " + index;

        // ------------------------------------------------------------------ orders

        /// <summary>
        /// True for a world whose fleet can be ordered: owned by a player, whoever it is, and with its Darts
        /// neither capturing nor fighting. It may have no Darts yet.
        /// </summary>
        public bool IsOrderSource(int world)
        {
            if (!IsWorld(world)) return false;
            Node node = Match.Nodes[world];
            return node.Owner != Faction.Neutral && !node.IsBeingCaptured && !node.IsContested;
        }

        /// <summary>True when the owner of <paramref name="source"/> can send its fleet to <paramref name="target"/> now.</summary>
        public bool CanOrder(int source, int target)
        {
            return IsWorld(source) && Match.CanLaunch(Match.Nodes[source].Owner, source, target);
        }

        /// <summary>
        /// Sends every Dart stationed at <paramref name="source"/> to <paramref name="target"/>, as ordered
        /// by the source's owner.
        /// </summary>
        public bool TryOrder(int source, int target, out string refusal)
        {
            refusal = null;
            if (!IsWorld(source) || !IsWorld(target)) refusal = "Choose two worlds.";
            else if (source == target) refusal = "Choose another world to send the fleet to.";
            else
            {
                Node from = Match.Nodes[source];
                if (from.Owner == Faction.Neutral) refusal = "Neutral worlds take no orders.";
                else if (from.IsContested) refusal = "The Darts at " + from.Name + " are fighting and cannot leave.";
                else if (from.IsBeingCaptured) refusal = "The Darts at " + from.Name + " are capturing it and cannot leave.";
                else if (!Match.AreConnected(source, target))
                    refusal = "No route from " + from.Name + " to " + Match.Nodes[target].Name + ".";
                else if (from.Darts == 0) refusal = "No Darts stationed at " + from.Name + " yet.";
            }

            if (refusal != null) return false;
            return Match.TryLaunch(Match.Nodes[source].Owner, source, target);
        }

        // ------------------------------------------------------------------ editing worlds

        /// <summary>Replaces the Darts stationed at a world; see <see cref="Match.TrySetStationedDarts"/>.</summary>
        public bool TrySetDarts(int world, int darts, out string problem)
        {
            if (darts < 0 || darts > MostDarts)
            {
                problem = "Darts must be a whole number from 0 to " + MostDarts + ".";
                return false;
            }

            return Match.TrySetStationedDarts(world, darts, out problem);
        }

        /// <summary>Hands a world to a player, or makes it neutral; see <see cref="Match.TrySetOwner"/>.</summary>
        public bool TrySetOwner(int world, Faction owner, out string problem)
        {
            return Match.TrySetOwner(world, owner, out problem);
        }

        /// <summary>
        /// Makes a world another kind, with the production that kind has in this run, switched on. Owner,
        /// Darts, a capture and a battle there stay as they are.
        /// </summary>
        public bool TrySetKind(int world, NodeKind kind, out string problem)
        {
            return Match.TrySetKind(world, kind, ProductionInterval(kind), out problem);
        }

        /// <summary>Switches a world's production off or on again. Asteroids never produce.</summary>
        public bool TrySetProducing(int world, bool producing, out string problem)
        {
            return Match.TrySetProducing(world, producing, out problem);
        }

        // ------------------------------------------------------------------ tuning

        /// <summary>The game's fleet speed, as the code has it now.</summary>
        public static double DefaultFleetSpeed => new MatchConfig().FleetSpeed;

        /// <summary>The fleet speed this run plays by instead of the game's, or null for the game's.</summary>
        public double? FleetSpeedOverride => rules.FleetSpeed;

        /// <summary>The fleet speed this run plays by.</summary>
        public double FleetSpeed => Match.FleetSpeed;

        /// <summary>
        /// Plays the rest of the run, and any reset of it, at another fleet speed, or at the game's again
        /// for null. Fleets on their way keep how far along they are and fly the rest at the new speed.
        /// </summary>
        public bool TrySetFleetSpeed(double? speed, out string problem)
        {
            if (speed.HasValue && !(speed.Value >= SlowestFleet && speed.Value <= FastestFleet))
            {
                problem = "Fleet speed must be from " + SlowestFleet + " to " + FastestFleet + ".";
                return false;
            }

            if (!Match.TrySetFleetSpeed(speed ?? DefaultFleetSpeed, out problem)) return false;
            rules.FleetSpeed = speed;
            return true;
        }

        /// <summary>The game's seconds per Dart for a kind, as the code has it now. Zero for an asteroid.</summary>
        public static double DefaultProductionInterval(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Planet: return NodeConfig.PlanetProductionInterval;
                case NodeKind.Moon: return NodeConfig.MoonProductionInterval;
                default: return 0;
            }
        }

        /// <summary>The seconds per Dart this run plays by instead of the game's for a kind, or null.</summary>
        public double? ProductionOverride(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Planet: return rules.PlanetProductionInterval;
                case NodeKind.Moon: return rules.MoonProductionInterval;
                default: return null;
            }
        }

        /// <summary>The seconds per Dart this run plays by for a kind. Zero for an asteroid.</summary>
        public double ProductionInterval(NodeKind kind)
        {
            return ProductionOverride(kind) ?? DefaultProductionInterval(kind);
        }

        /// <summary>
        /// Plays the rest of the run, and any reset of it, with another production for every world of a
        /// kind, or the game's again for null. A Dart in progress keeps how far along it is.
        /// </summary>
        public bool TrySetProductionInterval(NodeKind kind, double? seconds, out string problem)
        {
            problem = null;
            if (kind == NodeKind.Asteroid) problem = "Asteroids never produce Darts.";
            else if (kind != NodeKind.Planet && kind != NodeKind.Moon) problem = "A world is a planet, a moon or an asteroid.";
            else if (seconds.HasValue && !(seconds.Value >= FastestProduction && seconds.Value <= SlowestProduction))
                problem = "Production must be from " + FastestProduction + " to " + SlowestProduction + " seconds per Dart.";
            if (problem != null) return false;

            double interval = seconds ?? DefaultProductionInterval(kind);
            foreach (Node node in Match.Nodes)
            {
                if (node.Kind == kind) Match.TrySetProductionInterval(node.Id, interval, out _);
            }

            if (kind == NodeKind.Planet) rules.PlanetProductionInterval = seconds;
            else rules.MoonProductionInterval = seconds;
            return true;
        }

        // ------------------------------------------------------------------ replenishing and examples

        /// <summary>
        /// Tops every world a player owns up to the Darts it started with, whoever started with it,
        /// unless it is being captured or fought over. Returns how many worlds were topped up.
        /// </summary>
        public int Replenish()
        {
            int replenished = 0;
            foreach (Node node in Match.Nodes)
            {
                if (node.Owner == Faction.Neutral || node.IsBeingCaptured || node.IsContested) continue;

                int start = setup.Worlds[node.Id].Darts;
                if (node.Darts < start && Match.TrySetStationedDarts(node.Id, start, out _)) replenished++;
            }

            return replenished;
        }

        /// <summary>The world the examples use when none is chosen: the first moon, else the first world; -1 for none.</summary>
        public int DefaultExampleWorld
        {
            get
            {
                foreach (Node node in Match.Nodes)
                {
                    if (node.Kind == NodeKind.Moon) return node.Id;
                }

                return Match.Nodes.Count > 0 ? 0 : -1;
            }
        }

        /// <summary>
        /// Prepares a capture to watch: the world is made neutral and empty, and a fleet of the player that
        /// owns its first neighbor (else the first player) is put on its way there from that neighbor,
        /// shortly before it lands. The match then lands it and runs the capture by its rules.
        /// </summary>
        public bool TryStageCaptureExample(int world, out string problem)
        {
            if (!TryFindNeighbor(world, out int source, out problem)) return false;
            if (Players.Count == 0)
            {
                problem = "A capture needs a player.";
                return false;
            }

            Faction captor = PlayerOf(source, 0);
            Match.TrySetOwner(world, Faction.Neutral, out _);
            Match.TrySetStationedDarts(world, 0, out _);
            return PlaceExampleFleet(captor, CaptureExampleDarts, source, world, out problem);
        }

        /// <summary>
        /// Prepares a battle to watch: the world is given to one player with a few Darts, and a larger fleet
        /// of another player (the owner of its first neighbor, if a player owns it) is put on its way there
        /// from that neighbor, shortly before it arrives. The match then fights the battle by its rules.
        /// </summary>
        public bool TryStageBattleExample(int world, out string problem)
        {
            if (!TryFindNeighbor(world, out int source, out problem)) return false;
            if (Players.Count < 2)
            {
                problem = "A battle needs two players.";
                return false;
            }

            Faction attacker = PlayerOf(source, 1);
            Faction defender = Players[0].Faction != attacker ? Players[0].Faction : Players[1].Faction;
            Match.TrySetOwner(world, defender, out _);
            Match.TrySetStationedDarts(world, BattleExampleDefenders, out _);
            return PlaceExampleFleet(attacker, BattleExampleAttackers, source, world, out problem);
        }

        // The other end of the first route from the world, in route order.
        bool TryFindNeighbor(int world, out int neighbor, out string problem)
        {
            neighbor = -1;
            problem = null;
            if (!IsWorld(world))
            {
                problem = "Choose a world first.";
                return false;
            }

            foreach (Route route in Match.Routes)
            {
                if (route.A == world) neighbor = route.B;
                else if (route.B == world) neighbor = route.A;
                if (neighbor >= 0) return true;
            }

            problem = "No route leads to " + Match.Nodes[world].Name + ", so no fleet can come.";
            return false;
        }

        // The player owning the world, else the player at this place in the list.
        Faction PlayerOf(int world, int fallback)
        {
            Faction owner = Match.Nodes[world].Owner;
            return owner != Faction.Neutral ? owner : Players[fallback % Players.Count].Faction;
        }

        bool PlaceExampleFleet(Faction faction, int darts, int source, int target, out string problem)
        {
            double duration = Match.TravelTime(source, target);
            double progress = duration > ExampleLead ? 1 - ExampleLead / duration : 0;
            return Match.TryPlaceFleet(faction, darts, source, target, progress, out _, out problem);
        }
    }
}
