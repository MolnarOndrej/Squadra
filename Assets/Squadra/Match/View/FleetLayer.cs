using System.Collections.Generic;
using Squadra.Simulation;
using TMPro;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// Draws every Dart dot in the scene: fleets orbiting their worlds, fleets in transit, the two
    /// formations of a battle with their shots and explosions, and sparks. Dots are presentation only.
    /// Counts and casualties come from the match, and large fleets are shown in aggregate: a dot, a shot
    /// or an explosion stands for several Darts there. A dot keeps its place on screen when its fleet
    /// arrives, goes into battle or returns to orbit; it moves to its new position instead of being
    /// replaced.
    ///
    /// This component keeps the dots of every world and of every fleet in transit, hands them from
    /// one to the other and decides when a battle fires and sounds. Where a dot belongs in an orbit
    /// or a formation is worked out by <see cref="DartFormations"/>; shots, explosions and sparks
    /// live in <see cref="DartEffects"/>. Garrisons and transits stay together here because a dot
    /// passes between them without leaving the screen.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class FleetLayer : MonoBehaviour
    {
        [SerializeField] float dotRadius = 0.055f;
        [SerializeField] float dotBrightness = 1.5f;
        [Tooltip("Inactive template cloned for the Dart count that travels with each fleet.")]
        [SerializeField] TextMeshPro fleetLabelTemplate;

        [Header("Battle")]
        [Tooltip("Seconds before a hostile fleet arrives that the defenders start gathering to face it. "
            + "Only what is drawn: the defenders can still be ordered away.")]
        [SerializeField] float gatherSeconds = 1.4f;
        [Tooltip("Gap between the defenders' front and the attackers' front, in world units.")]
        [SerializeField] float frontGap = 0.5f;
        [Tooltip("Seconds a dot takes to settle into a new place: into formation, or back into orbit.")]
        [SerializeField] float settleSeconds = 0.32f;
        [Tooltip("How far a fighting dot strays from its place in the formation, in world units.")]
        [SerializeField] float strayDistance = 0.1f;
        [Tooltip("Most shots drawn per second at one battle, however large the fleets.")]
        [SerializeField] float maxShotsPerSecond = 24f;
        [Tooltip("Most shot sounds per second at one battle.")]
        [SerializeField] float maxShotSounds = 8f;
        [Tooltip("Most explosion sounds per second at one battle.")]
        [SerializeField] float maxBlastSounds = 5f;

        /// <summary>One dot stationed at a world, placed by angle and distance around it.</summary>
        sealed class Dot
        {
            public float Angle;
            public float Radius;
            public float AngleSpeed;
            public float RadiusSpeed;
            public float Shown;       // 1 is fully drawn; below zero it has yet to appear
            public float Phase;
            public float Restless;    // how much of the battle movement this dot takes part in
            public Vector2 Position;  // where it was last drawn
            public Vector4 Tint;      // kept by a dot that is fading out
        }

        /// <summary>The dots at one world: its stationed fleet and, during a battle, the force that arrived.</summary>
        sealed class Garrison
        {
            public List<Dot> Stationed = new List<Dot>();
            public List<Dot> Arriving = new List<Dot>();
            public readonly List<Dot> Leaving = new List<Dot>();
            public Faction ArrivingFaction;
            public float FrontAngle;      // where the two fronts meet, seen from the world
            public float Engaged;         // 0 in orbit, 1 while fighting; eased
            public float ApproachWeight;  // how far the defenders have gathered for a fleet on its way
            public float ApproachAngle;
            public float ShotIn;
            public float ShotSoundIn;
            public float BlastSoundIn;
            public float StationedFlashIn;
            public float ArrivingFlashIn;
        }

        sealed class Transit
        {
            public Fleet Fleet;
            public Vector2 Start;
            public Vector2 End;
            public Vector2 Direction;
            public Vector2[] Offsets;
            public Vector2[] Positions;
            public float[] Phases;
            public float Standoff; // 1 when the fleet is heading for the outer formation of a battle
            public TextMeshPro Label;
            public TransitRoute Route;
        }

        sealed class TransitRoute
        {
            public LineStyle Lead;
            public LineStyle Wake;
        }

        readonly Dictionary<int, Transit> transits = new Dictionary<int, Transit>();
        readonly Stack<TextMeshPro> labelPool = new Stack<TextMeshPro>();
        readonly Stack<TransitRoute> routePool = new Stack<TransitRoute>();
        readonly Stack<Dot> dotPool = new Stack<Dot>();
        readonly DartEffects effects = new DartEffects();

        DotBatch batch;
        Match match;
        IReadOnlyList<WorldView> worlds;
        Material lineMaterial;
        SoundLayer sounds;
        Garrison[] garrisons;

        /// <summary>Dots drawn for the fleet stationed at a world right now, the ones fading out aside.</summary>
        public int StationedDots(int world) => garrisons[world].Stationed.Count;

        /// <summary>Dots drawn for the force that arrived at a world and is fighting there.</summary>
        public int ArrivingDots(int world) => garrisons[world].Arriving.Count;

        /// <summary>Shots in flight across all battles.</summary>
        public int ShotsInFlight => effects.ShotsInFlight;

        /// <summary>Sparks alive right now: explosions, landings and captures.</summary>
        public int SparkCount => effects.SparkCount;

        public void Bind(Match boundMatch, IReadOnlyList<WorldView> boundWorlds, Material routeMaterial,
            SoundLayer battleSounds = null)
        {
            match = boundMatch;
            worlds = boundWorlds;
            lineMaterial = routeMaterial;
            sounds = battleSounds;

            garrisons = new Garrison[worlds.Count];
            for (int n = 0; n < garrisons.Length; n++)
            {
                var garrison = new Garrison();
                garrisons[n] = garrison;

                // The starting fleets are simply there: nothing fades in.
                int count = DartFormations.DotCount(match.Nodes[n].Darts);
                for (int i = 0; i < count; i++)
                {
                    Dot dot = RentDot();
                    DartFormations.OrbitPlace(worlds[n], i, 0f, out dot.Angle, out dot.Radius);
                    dot.Shown = 1f;
                    dot.Position = worlds[n].Position + DartFormations.Polar(dot.Angle, dot.Radius);
                    garrison.Stationed.Add(dot);
                }
            }

            if (batch == null) batch = new DotBatch(GetComponent<MeshFilter>());
        }

        void OnDestroy()
        {
            batch?.Dispose();
        }

        // ------------------------------------------------------------------ transit

        public void BeginTransit(Fleet fleet)
        {
            WorldView source = worlds[fleet.SourceId];
            WorldView target = worlds[fleet.TargetId];
            Vector2 delta = target.Position - source.Position;
            Vector2 direction = delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector2.right;

            int dots = DartFormations.DotCount(fleet.Darts);
            var random = new System.Random(fleet.Id * 7919 + 17);
            float spread = 0.14f + 0.045f * Mathf.Sqrt(dots);
            var offsets = new Vector2[dots];
            var phases = new float[dots];
            for (int i = 0; i < dots; i++)
            {
                // A loose teardrop: wide at the front, trailing off behind.
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float radius = Mathf.Sqrt((float)random.NextDouble());
                float along = Mathf.Cos(angle) * radius;
                float across = Mathf.Sin(angle) * radius * Mathf.Lerp(0.35f, 0.8f, (along + 1f) * 0.5f);
                offsets[i] = new Vector2(along * spread * 1.5f, across * spread);
                phases[i] = (float)random.NextDouble() * Mathf.PI * 2f;
            }

            var transit = new Transit
            {
                Fleet = fleet,
                Start = source.Position + direction * source.OrbitRadius,
                End = target.Position - direction * target.OrbitRadius,
                Direction = direction,
                Offsets = offsets,
                Positions = new Vector2[dots],
                Phases = phases,
                Label = RentLabel(fleet),
                Route = RentRoute(fleet.Faction),
            };
            transit.Standoff = HeadsForOuterFormation(fleet) ? 1f : 0f;
            PlaceTransit(transit, Time.time, out _, out _);
            transits[fleet.Id] = transit;
        }

        /// <summary>
        /// Takes a fleet that has reached its world out of transit. Its dots stay where they are and
        /// become part of what is stationed there, or of the force attacking it, and move on from there.
        /// </summary>
        public void Arrive(ArrivalReport report)
        {
            if (!transits.TryGetValue(report.FleetId, out Transit transit)) return;

            transits.Remove(report.FleetId);
            Garrison garrison = garrisons[report.NodeId];
            WorldView world = worlds[report.NodeId];

            List<Dot> side = garrison.Stationed;
            if (report.Outcome == ArrivalOutcome.BattleStarted)
            {
                // The fronts meet where the attackers came in.
                Retire(garrison, garrison.Arriving, 0, Palette.Of(garrison.ArrivingFaction));
                garrison.ArrivingFaction = report.Arriving;
                garrison.FrontAngle = Mathf.Atan2(-transit.Direction.y, -transit.Direction.x);
                garrison.ShotIn = 0.25f;
                side = garrison.Arriving;
            }
            else if (report.Outcome == ArrivalOutcome.BattleJoined && report.Arriving == garrison.ArrivingFaction)
            {
                side = garrison.Arriving;
            }

            foreach (Vector2 position in transit.Positions)
            {
                Dot dot = RentDot();
                Vector2 offset = position - world.Position;
                dot.Angle = Mathf.Atan2(offset.y, offset.x);
                dot.Radius = offset.magnitude;
                dot.Shown = 1f;
                dot.Position = position;
                side.Add(dot);
            }

            transit.Label.gameObject.SetActive(false);
            labelPool.Push(transit.Label);
            transit.Route.Lead.Visible = false;
            transit.Route.Wake.Visible = false;
            routePool.Push(transit.Route);
        }

        // True when the fleet will form up outside the orbit, facing a force stationed at its target.
        bool HeadsForOuterFormation(Fleet fleet)
        {
            Node node = match.Nodes[fleet.TargetId];
            if (node.IsContested) return node.Battle.Arriving == fleet.Faction;
            return node.Darts > 0 && node.Occupant != fleet.Faction;
        }

        TransitRoute RentRoute(Faction faction)
        {
            TransitRoute route = routePool.Count > 0 ? routePool.Pop() : new TransitRoute
            {
                Lead = new LineStyle(Shapes.CreateQuad("Fleet Course", transform, lineMaterial, -42))
                {
                    Width = 0.14f,
                    Dash = 0.23f,
                    Gap = 0.17f,
                    Scroll = 0.9f,
                    EndFade = 0.08f,
                },
                Wake = new LineStyle(Shapes.CreateQuad("Fleet Wake", transform, lineMaterial, -44))
                {
                    Width = 0.10f,
                    Dash = 0.16f,
                    Gap = 0.18f,
                    EndFade = 0.08f,
                },
            };
            Color tint = Palette.Of(faction);
            route.Lead.Tint = Palette.Hdr(Palette.Bright(tint, 0.25f), 1.4f, 0.84f);
            route.Wake.Tint = Palette.Hdr(Palette.Bright(tint, 0.18f), 1f, 0.62f);
            return route;
        }

        TextMeshPro RentLabel(Fleet fleet)
        {
            TextMeshPro label = labelPool.Count > 0
                ? labelPool.Pop()
                : Instantiate(fleetLabelTemplate, transform);
            label.SetText("{0}", fleet.Darts);
            label.color = Palette.Bright(Palette.Of(fleet.Faction), 0.35f);
            label.gameObject.SetActive(true);
            return label;
        }

        // ------------------------------------------------------------------ battles

        /// <summary>
        /// Shows the casualties of one step of a battle. Each side loses dots until it has as many as
        /// its remaining Darts call for, each with an explosion; a fleet too large to lose a dot gets
        /// an explosion among its dots now and then instead.
        /// </summary>
        public void BattleCasualties(Battle battle, int pairs)
        {
            if (pairs <= 0) return;

            Garrison garrison = garrisons[battle.NodeId];
            LoseDots(garrison, garrison.Stationed, DartFormations.DotCount(battle.StationedDarts),
                Palette.Of(battle.Stationed), ref garrison.StationedFlashIn);
            LoseDots(garrison, garrison.Arriving, DartFormations.DotCount(battle.ArrivingDarts),
                Palette.Of(battle.Arriving), ref garrison.ArrivingFlashIn);
        }

        /// <summary>
        /// Ends the battle formations at a world. Whoever is left is the stationed fleet from now on and
        /// drifts back into orbit from where it fought.
        /// </summary>
        public void BattleEnded(BattleReport report)
        {
            Garrison garrison = garrisons[report.NodeId];
            if (report.Winner == report.Arriving && report.Winner != Faction.Neutral)
            {
                Retire(garrison, garrison.Stationed, 0, Palette.Of(report.Stationed));
                List<Dot> survivors = garrison.Arriving;
                garrison.Arriving = garrison.Stationed;
                garrison.Stationed = survivors;
            }
            else
            {
                Retire(garrison, garrison.Arriving, 0, Palette.Of(report.Arriving));
            }
        }

        void LoseDots(Garrison garrison, List<Dot> dots, int target, Color color, ref float flashIn)
        {
            if (dots.Count > target)
            {
                while (dots.Count > target)
                {
                    int index = Random.Range(0, dots.Count);
                    Dot dot = dots[index];
                    effects.Explode(dot.Position, color, 1f, dotRadius);
                    BlastSound(garrison, dot.Position);

                    // The dot from the far end of the formation closes the gap.
                    dots[index] = dots[dots.Count - 1];
                    dots.RemoveAt(dots.Count - 1);
                    dotPool.Push(dot);
                }
            }
            else if (dots.Count > 0 && flashIn <= 0f)
            {
                Dot dot = dots[Random.Range(0, dots.Count)];
                effects.Explode(dot.Position + Random.insideUnitCircle * 0.05f, color, 0.7f, dotRadius);
                BlastSound(garrison, dot.Position);
                flashIn = Random.Range(0.1f, 0.2f);
            }
        }

        void BlastSound(Garrison garrison, Vector2 position)
        {
            if (sounds == null || garrison.BlastSoundIn > 0f) return;

            sounds.DartDestroyed(position, Random.Range(0.65f, 1f));
            garrison.BlastSoundIn = Random.Range(0.8f, 1.3f) / Mathf.Max(0.5f, maxBlastSounds);
        }

        // Shots go both ways across the gap while both sides have dots to fire and to be fired at. They
        // decide nothing: the match removes the Darts, a pair at a time.
        void Fight(Garrison garrison, Battle battle, float deltaTime)
        {
            garrison.ShotSoundIn -= deltaTime;
            garrison.BlastSoundIn -= deltaTime;
            garrison.StationedFlashIn -= deltaTime;
            garrison.ArrivingFlashIn -= deltaTime;

            int stationed = garrison.Stationed.Count;
            int arriving = garrison.Arriving.Count;
            if (stationed == 0 || arriving == 0) return;

            float rate = Mathf.Min(maxShotsPerSecond, 3f + 1.9f * Mathf.Sqrt(stationed + arriving));
            garrison.ShotIn -= deltaTime;
            for (int guard = 0; garrison.ShotIn <= 0f && guard < 8; guard++)
            {
                garrison.ShotIn += Random.Range(0.5f, 1.5f) / rate;

                bool fromStationed = Random.Range(0, stationed + arriving) < stationed;
                List<Dot> shooters = fromStationed ? garrison.Stationed : garrison.Arriving;
                List<Dot> targets = fromStationed ? garrison.Arriving : garrison.Stationed;
                Faction faction = fromStationed ? battle.Stationed : battle.Arriving;
                Vector2 from = shooters[Random.Range(0, shooters.Count)].Position;
                Vector2 to = targets[Random.Range(0, targets.Count)].Position + Random.insideUnitCircle * 0.06f;
                effects.Shoot(from, to, Palette.Of(faction));

                if (sounds != null && garrison.ShotSoundIn <= 0f)
                {
                    sounds.Shot(from, Random.Range(0.55f, 0.95f));
                    garrison.ShotSoundIn = Random.Range(0.75f, 1.35f) / Mathf.Max(0.5f, maxShotSounds);
                }
            }

            if (garrison.ShotIn < 0f) garrison.ShotIn = 0f; // a long frame is not made up with a volley
        }

        // ------------------------------------------------------------------ sparks

        /// <summary>Throws a ring of short-lived sparks outward from a circle around <paramref name="center"/>.</summary>
        public void Burst(Vector2 center, float spawnRadius, Color color, int count, float speed, float life = 0.55f)
        {
            effects.Burst(center, spawnRadius, color, count, speed, life, dotRadius);
        }

        // ------------------------------------------------------------------ drawing

        /// <summary>
        /// Moves and draws everything for one frame. With no time passing, as under the pause menu,
        /// the same picture is drawn again: no dot moves, no shot is fired and no spark ages.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (match == null) return;

            float time = Time.time;
            bool playing = match.Status == MatchStatus.InProgress;
            batch.Begin();
            FindApproaches();
            for (int n = 0; n < garrisons.Length; n++)
                DrawGarrison(n, time, deltaTime, playing);
            DrawTransits(time, deltaTime);
            effects.Draw(batch, deltaTime, dotRadius);
            batch.End();
        }

        // Defenders turn toward a hostile fleet during the last part of its approach. It is read from
        // the match every frame, so a fleet that will find nobody there gathers nobody.
        void FindApproaches()
        {
            foreach (Garrison garrison in garrisons)
                garrison.ApproachWeight = 0f;

            foreach (Transit transit in transits.Values)
            {
                Fleet fleet = transit.Fleet;
                Node node = match.Nodes[fleet.TargetId];
                if (node.IsContested || node.Darts <= 0 || node.Occupant == fleet.Faction) continue;

                float window = Mathf.Min(gatherSeconds, (float)fleet.Duration);
                float weight = window <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, 1f - (float)fleet.Remaining / window);
                Garrison garrison = garrisons[fleet.TargetId];
                if (weight <= garrison.ApproachWeight) continue;

                garrison.ApproachWeight = weight;
                garrison.ApproachAngle = Mathf.Atan2(-transit.Direction.y, -transit.Direction.x);
            }
        }

        void DrawGarrison(int n, float time, float deltaTime, bool playing)
        {
            Node node = match.Nodes[n];
            WorldView world = worlds[n];
            Garrison garrison = garrisons[n];
            Battle battle = node.Battle;
            bool fighting = battle != null;

            float gathered = 1f;
            if (!fighting)
            {
                gathered = garrison.ApproachWeight;
                if (gathered > 0f) garrison.FrontAngle = garrison.ApproachAngle;
            }

            garrison.Engaged = Mathf.MoveTowards(garrison.Engaged, fighting ? 1f : 0f, deltaTime / 0.35f);

            Color stationedColor = Palette.Of(node.Occupant);
            Color arrivingColor = Palette.Of(garrison.ArrivingFaction);
            int stationedDots = DartFormations.DotCount(node.Darts);
            int arrivingDots = fighting ? DartFormations.DotCount(battle.ArrivingDarts) : 0;
            Resize(garrison, garrison.Stationed, stationedDots, stationedColor, world, time, gathered, true);
            Resize(garrison, garrison.Arriving, arrivingDots, arrivingColor, world, time, 1f, false);

            DrawSide(garrison, garrison.Stationed, world, time, deltaTime, gathered, true,
                Palette.Hdr(stationedColor, dotBrightness, 1f));
            DrawSide(garrison, garrison.Arriving, world, time, deltaTime, 1f, false,
                Palette.Hdr(arrivingColor, dotBrightness * 1.15f, 1f));
            DrawLeaving(garrison, deltaTime);

            if (fighting && playing && deltaTime > 0f) Fight(garrison, battle, deltaTime);
        }

        // Brings a side to the number of dots its Darts call for. New dots appear in their places one
        // after another; surplus ones fade out. Dots destroyed in battle never get here: they explode.
        void Resize(Garrison garrison, List<Dot> dots, int target, Color color, WorldView world, float time,
            float gathered, bool stationed)
        {
            if (dots.Count > target)
            {
                Retire(garrison, dots, target, color);
                return;
            }

            for (int added = 0; dots.Count < target; added++)
            {
                Dot dot = RentDot();
                dots.Add(dot);
                Place(garrison, world, dots.Count - 1, target, time, gathered, stationed, out dot.Angle, out dot.Radius);
                dot.Shown = -0.1f * added;
                dot.Position = world.Position + DartFormations.Polar(dot.Angle, dot.Radius);
            }
        }

        void Retire(Garrison garrison, List<Dot> dots, int target, Color color)
        {
            Vector4 tint = Palette.Hdr(color, dotBrightness, 1f);
            for (int faded = 0; dots.Count > target; faded++)
            {
                Dot dot = dots[dots.Count - 1];
                dots.RemoveAt(dots.Count - 1);
                dot.Tint = tint;
                dot.Shown = Mathf.Min(dot.Shown, 1f) + 0.03f * faded;
                garrison.Leaving.Add(dot);
            }
        }

        void DrawSide(Garrison garrison, List<Dot> dots, WorldView world, float time, float deltaTime, float gathered,
            bool stationed, Vector4 color)
        {
            Vector2 center = world.Position;
            float towardEnemy = stationed ? 1f : -1f;
            for (int i = 0; i < dots.Count; i++)
            {
                Dot dot = dots[i];
                Place(garrison, world, i, dots.Count, time, gathered, stationed, out float angle, out float radius);
                if (deltaTime > 0f)
                {
                    // Around the world rather than straight through it.
                    float turn = Mathf.DeltaAngle(dot.Angle * Mathf.Rad2Deg, angle * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                    dot.Angle = Mathf.SmoothDamp(dot.Angle, dot.Angle + turn, ref dot.AngleSpeed, settleSeconds,
                        Mathf.Infinity, deltaTime);
                    dot.Radius = Mathf.SmoothDamp(dot.Radius, radius, ref dot.RadiusSpeed, settleSeconds,
                        Mathf.Infinity, deltaTime);
                    dot.Shown = Mathf.MoveTowards(dot.Shown, 1f, deltaTime * 7f);
                }

                // Fighting dots keep moving: some dart forward at the enemy and back, all of them drift.
                float lunge = 0f;
                float drift = 0f;
                if (garrison.Engaged > 0f)
                {
                    float pace = 1.7f + 1.3f * Mathf.Repeat(dot.Phase * 3.7f, 1f);
                    lunge = strayDistance * dot.Restless * (0.5f + 0.5f * Mathf.Sin(time * pace + dot.Phase))
                        * towardEnemy * garrison.Engaged;
                    drift = 0.35f * strayDistance * Mathf.Sin(time * 2.3f + dot.Phase * 5f) * garrison.Engaged;
                }

                float shownRadius = dot.Radius + lunge;
                float shownAngle = dot.Angle + drift / Mathf.Max(0.2f, shownRadius);
                dot.Position = center + DartFormations.Polar(shownAngle, shownRadius);
                batch.Add(dot.Position, dotRadius * 2f * Mathf.Clamp01(dot.Shown), color);
            }
        }

        void DrawLeaving(Garrison garrison, float deltaTime)
        {
            List<Dot> leaving = garrison.Leaving;
            for (int i = leaving.Count - 1; i >= 0; i--)
            {
                Dot dot = leaving[i];
                dot.Shown -= deltaTime * 7f;
                if (dot.Shown <= 0f)
                {
                    leaving[i] = leaving[leaving.Count - 1];
                    leaving.RemoveAt(leaving.Count - 1);
                    dotPool.Push(dot);
                    continue;
                }

                batch.Add(dot.Position, dotRadius * 2f * Mathf.Clamp01(dot.Shown), dot.Tint);
            }
        }

        // Where dot number `index` of `count` belongs: on its orbit, in its battle formation, or part
        // of the way between the two while the defenders gather.
        void Place(Garrison garrison, WorldView world, int index, int count, float time, float gathered,
            bool stationed, out float angle, out float radius)
        {
            if (!stationed)
            {
                DartFormations.FormationPlace(world, index, count, garrison.FrontAngle, false, frontGap,
                    out angle, out radius);
                return;
            }

            DartFormations.OrbitPlace(world, index, time, out angle, out radius);
            if (gathered <= 0f) return;

            DartFormations.FormationPlace(world, index, count, garrison.FrontAngle, true, frontGap,
                out float formedAngle, out float formedRadius);
            angle += Mathf.DeltaAngle(angle * Mathf.Rad2Deg, formedAngle * Mathf.Rad2Deg) * Mathf.Deg2Rad * gathered;
            radius = Mathf.Lerp(radius, formedRadius, gathered);
        }

        void DrawTransits(float time, float deltaTime)
        {
            foreach (Transit transit in transits.Values)
            {
                Fleet fleet = transit.Fleet;

                // A fleet about to fight stops short of the orbit, where its formation will be. Whether
                // it will is read from the match, so it changes course if the defenders leave.
                transit.Standoff = Mathf.MoveTowards(transit.Standoff, HeadsForOuterFormation(fleet) ? 1f : 0f,
                    deltaTime / 0.5f);

                PlaceTransit(transit, time, out Vector2 center, out float formation);
                Vector2 direction = transit.Direction;
                Vector4 color = Palette.Hdr(Palette.Of(fleet.Faction), dotBrightness * 1.15f, 1f);
                Vector4 trail = color;
                foreach (Vector2 position in transit.Positions)
                {
                    batch.Add(position, dotRadius * 2f, color);

                    trail.w = 0.38f;
                    batch.Add(position - direction * 0.1f, dotRadius * 1.5f, trail);
                    trail.w = 0.16f;
                    batch.Add(position - direction * 0.2f, dotRadius * 1.1f, trail);
                }

                float extent = 0.14f + 0.045f * Mathf.Sqrt(transit.Offsets.Length);
                transit.Label.transform.position = center + Vector2.up * (extent * formation + 0.38f);
            }
        }

        void PlaceTransit(Transit transit, float time, out Vector2 center, out float formation)
        {
            Fleet fleet = transit.Fleet;
            float standoff = (frontGap + DartFormations.RowSpacing) * Mathf.SmoothStep(0f, 1f, transit.Standoff);
            Vector2 end = transit.End - transit.Direction * standoff;
            center = Vector2.Lerp(transit.Start, end, fleet.Progress);
            transit.Route.Lead.Set(center, end);
            transit.Route.Wake.Set(transit.Start, center);
            Vector2 direction = transit.Direction;
            var normal = new Vector2(-direction.y, direction.x);

            // The swarm fans out after launch and gathers again before arrival.
            float opening = Mathf.Clamp01((float)fleet.Elapsed / 0.45f);
            float closing = Mathf.Clamp01((float)fleet.Remaining / 0.3f);
            formation = Mathf.Lerp(0.3f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Min(opening, closing)));

            for (int i = 0; i < transit.Offsets.Length; i++)
            {
                Vector2 offset = transit.Offsets[i] * formation;
                float wobble = 0.03f * Mathf.Sin(time * 3.1f + transit.Phases[i]);
                transit.Positions[i] = center + direction * offset.x + normal * (offset.y + wobble);
            }
        }

        // ------------------------------------------------------------------ helpers

        Dot RentDot()
        {
            Dot dot = dotPool.Count > 0 ? dotPool.Pop() : new Dot();
            dot.AngleSpeed = 0f;
            dot.RadiusSpeed = 0f;
            dot.Shown = 1f;
            dot.Phase = Random.value * Mathf.PI * 2f;
            dot.Restless = Random.value < 0.4f ? 1f : 0.2f;
            return dot;
        }
    }
}
