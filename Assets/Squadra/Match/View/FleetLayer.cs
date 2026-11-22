using System.Collections.Generic;
using Squadra.Simulation;
using TMPro;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// Draws every Dart in the scene: fleets orbiting their worlds, fleets in transit, the two
    /// formations of a battle with their shots and explosions, and sparks. What is drawn is presentation
    /// only. Counts and casualties come from the match, and large fleets are shown in aggregate: a Dart
    /// drawn, a shot or an explosion stands for several Darts there, up to the limits of
    /// <see cref="DartDisplay"/>. A Dart drawn keeps its place on screen when its fleet arrives, goes
    /// into battle or returns to orbit; it moves and turns to its new place instead of being replaced.
    ///
    /// This component keeps the dots of every world and of every fleet in transit, hands them from
    /// one to the other and decides when a battle fires and sounds. Where a dot belongs in an orbit
    /// or a formation is worked out by <see cref="DartFormations"/>; a fleet in transit is drawn as a
    /// <see cref="DartSwarm"/> with its <see cref="FleetCourse"/>, which the sandbox draws its samples
    /// with too; shots, explosions and sparks live in <see cref="DartEffects"/>. Garrisons and transits stay together here because a dot
    /// passes between them without leaving the screen.
    ///
    /// How the Darts and a fleet's lines look comes from the shared <see cref="FleetStyle"/> and
    /// <see cref="RouteStyle"/>; how many are drawn for a fleet and how fast they orbit comes from
    /// <see cref="DartDisplay"/>. The Darts' bodies are drawn by a <see cref="DartBatch"/> on a child made
    /// when the match is bound, with the style's mesh and material; trails, shots and sparks stay round
    /// dots drawn by this object's own renderer, just in front of the bodies.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class FleetLayer : MonoBehaviour
    {
        [Tooltip("How the dots look. Shared with every mission.")]
        [SerializeField] FleetStyle style;
        [Tooltip("Inactive template cloned for the Dart count that travels with each fleet.")]
        [SerializeField] TextMeshPro fleetLabelTemplate;

        [Header("Battle")]
        [Tooltip("Seconds before a hostile fleet arrives that the defenders start gathering to face it. "
            + "Only what is drawn: the defenders can still be ordered away.")]
        [SerializeField] float gatherSeconds = 1.4f;
        [Tooltip("Seconds a dot takes to settle into a new place: into formation, or back into orbit.")]
        [SerializeField] float settleSeconds = 0.32f;
        [Tooltip("Most shots drawn per second at one battle, however large the fleets.")]
        [SerializeField] float maxShotsPerSecond = 24f;
        [Tooltip("Most shot sounds per second at one battle.")]
        [SerializeField] float maxShotSounds = 8f;
        [Tooltip("Most explosion sounds per second at one battle.")]
        [SerializeField] float maxBlastSounds = 5f;

        /// <summary>One Dart drawn at a world, placed by angle and distance around it.</summary>
        sealed class Dot
        {
            public float Angle;
            public float Radius;
            public float AngleSpeed;
            public float RadiusSpeed;
            public float Heading;     // the way it faces, in radians
            public float HeadingSpeed;
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
            public DartSwarm Swarm;
            public TextMeshPro Label;
            public FleetCourse Course;
        }

        readonly Dictionary<int, Transit> transits = new Dictionary<int, Transit>();
        readonly Stack<TextMeshPro> labelPool = new Stack<TextMeshPro>();
        readonly Stack<FleetCourse> coursePool = new Stack<FleetCourse>();
        readonly Stack<Dot> dotPool = new Stack<Dot>();
        readonly DartEffects effects = new DartEffects();

        DotBatch batch;
        DartBatch bodies;
        Match match;
        IReadOnlyList<WorldView> worlds;
        Material lineMaterial;
        RouteStyle routeStyle;
        DartDisplay display;
        float orbitShift;
        SoundLayer sounds;
        Garrison[] garrisons;
        int paintedPalette;

        /// <summary>Dots drawn for the fleet stationed at a world right now, the ones fading out aside.</summary>
        public int StationedDots(int world) => garrisons[world].Stationed.Count;

        /// <summary>Dots drawn for the force that arrived at a world and is fighting there.</summary>
        public int ArrivingDots(int world) => garrisons[world].Arriving.Count;

        /// <summary>Shots in flight across all battles.</summary>
        public int ShotsInFlight => effects.ShotsInFlight;

        /// <summary>Sparks alive right now: explosions, landings and captures.</summary>
        public int SparkCount => effects.SparkCount;

        /// <summary>
        /// Starts drawing a match, in place of any match drawn before. Leave <paramref name="dartDisplay"/>
        /// out for the game's own Dart behavior; a sandbox may pass other values. A match can be bound with
        /// fleets already on their way and battles already being fought, as a sandbox starts: the fleets are
        /// shown where they are, and the two fronts of each battle stand formed up where
        /// <paramref name="battleFronts"/> says, in radians by world (0, to the right, when it says nothing).
        /// </summary>
        public void Bind(Match boundMatch, IReadOnlyList<WorldView> boundWorlds, Material routeMaterial,
            RouteStyle routes, SoundLayer battleSounds = null, DartDisplay dartDisplay = null,
            IReadOnlyList<float> battleFronts = null)
        {
            Unbind();
            match = boundMatch;
            worlds = boundWorlds;
            lineMaterial = routeMaterial;
            routeStyle = routes;
            UseDisplay(dartDisplay);

            // Like a world without its surface, Darts without their look cannot be played.
            string problem = style != null ? style.DartProblem() : "The fleet layer has no fleet style.";
            if (problem != null) throw new System.InvalidOperationException(problem);
            sounds = battleSounds;

            garrisons = new Garrison[worlds.Count];
            for (int n = 0; n < garrisons.Length; n++)
            {
                var garrison = new Garrison();
                garrisons[n] = garrison;
                Node node = match.Nodes[n];
                Battle battle = node.Battle;
                if (battle != null)
                {
                    garrison.ArrivingFaction = battle.Arriving;
                    garrison.FrontAngle = battleFronts != null && n < battleFronts.Count ? battleFronts[n] : 0f;
                    garrison.Engaged = 1f;
                    garrison.ShotIn = 0.25f;
                }

                // The starting fleets are simply there: nothing fades in, and a battle is already formed up.
                AddStartingDots(garrison, n, garrison.Stationed, node.Darts, battle != null, true);
                if (battle != null) AddStartingDots(garrison, n, garrison.Arriving, battle.ArrivingDarts, true, false);
            }

            if (batch == null) batch = new DotBatch(GetComponent<MeshFilter>());
            if (bodies == null) bodies = MakeBodies();

            paintedPalette = Palette.Version;
            foreach (Fleet fleet in match.Fleets)
                BeginTransit(fleet);
        }

        void AddStartingDots(Garrison garrison, int world, List<Dot> dots, int darts, bool formed, bool stationed)
        {
            int count = DartFormations.StationedDots(darts, display);
            for (int i = 0; i < count; i++)
            {
                Dot dot = RentDot();
                if (formed)
                    DartFormations.FormationPlace(worlds[world], i, count, garrison.FrontAngle, stationed, style,
                        out dot.Angle, out dot.Radius);
                else
                    DartFormations.OrbitPlace(worlds[world], i, 0f, display.OrbitSpeed, 0f, style, out dot.Angle,
                        out dot.Radius);
                dot.Heading = DartFormations.Heading(dot.Angle, i, stationed, formed ? 1f : 0f, display.OrbitSpeed);
                dot.Shown = 1f;
                dot.Position = worlds[world].Position + DartFormations.Polar(dot.Angle, dot.Radius);
                dots.Add(dot);
            }
        }

        // Puts away everything drawn for the match bound before: its fleets in transit, its dots, shots
        // and sparks. Nothing of it fades out; the new battlefield takes its place at once.
        void Unbind()
        {
            foreach (Transit transit in transits.Values)
            {
                transit.Label.gameObject.SetActive(false);
                labelPool.Push(transit.Label);
                transit.Course.Hide();
                coursePool.Push(transit.Course);
            }

            transits.Clear();
            if (garrisons != null)
            {
                foreach (Garrison garrison in garrisons)
                {
                    foreach (Dot dot in garrison.Stationed) dotPool.Push(dot);
                    foreach (Dot dot in garrison.Arriving) dotPool.Push(dot);
                    foreach (Dot dot in garrison.Leaving) dotPool.Push(dot);
                }
            }

            garrisons = null;
            effects.Clear();
        }

        /// <summary>
        /// Draws the Darts by other values from now on, as the sandbox tuning does: the dots at a world
        /// follow at once, a fleet already in transit keeps the dots it set out with.
        /// </summary>
        public void SetDisplay(DartDisplay dartDisplay)
        {
            UseDisplay(dartDisplay);
        }

        void UseDisplay(DartDisplay dartDisplay)
        {
            DartDisplay next = dartDisplay ?? DartDisplay.Default;

            // A new orbit speed carries on from where every Dart is now. Without the shift, the orbit would
            // jump to where the new speed would have put it by now, and the dots would swing back to it.
            if (display != null && next.OrbitSpeed != display.OrbitSpeed)
                orbitShift += Time.time * (display.OrbitSpeed - next.OrbitSpeed);
            display = next;
        }

        void OnDestroy()
        {
            batch?.Dispose();
            bodies?.Dispose();
        }

        // The bodies are drawn by a renderer of their own, with the Dart material. It is made here rather
        // than by the scene builder, so the scene stays as it was and the child is never saved; it goes
        // with this object.
        DartBatch MakeBodies()
        {
            var own = GetComponent<MeshRenderer>();
            var bodiesObject = new GameObject("Dart Bodies") { hideFlags = HideFlags.DontSave };
            bodiesObject.transform.SetParent(transform, false);
            var renderer = bodiesObject.AddComponent<MeshRenderer>();
            renderer.sortingLayerID = own.sortingLayerID;
            renderer.sortingOrder = own.sortingOrder - 1; // trails, shots and sparks are drawn over the bodies
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return new DartBatch(bodiesObject.AddComponent<MeshFilter>(), renderer);
        }

        // ------------------------------------------------------------------ transit

        public void BeginTransit(Fleet fleet)
        {
            int dots = DartFormations.TravellingDots(fleet.Darts, display);
            var transit = new Transit
            {
                Fleet = fleet,
                Swarm = new DartSwarm(fleet.Id, dots, worlds[fleet.SourceId], worlds[fleet.TargetId], style),
                Label = RentLabel(fleet),
                Course = RentCourse(fleet.Faction),
            };
            transit.Swarm.Standoff = HeadsForOuterFormation(fleet) ? 1f : 0f;
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
                garrison.FrontAngle = Mathf.Atan2(-transit.Swarm.Direction.y, -transit.Swarm.Direction.x);
                garrison.ShotIn = 0.25f;
                side = garrison.Arriving;
            }
            else if (report.Outcome == ArrivalOutcome.BattleJoined && report.Arriving == garrison.ArrivingFaction)
            {
                side = garrison.Arriving;
            }

            float heading = Mathf.Atan2(transit.Swarm.Direction.y, transit.Swarm.Direction.x);
            foreach (Vector2 position in transit.Swarm.Positions)
            {
                Dot dot = RentDot();
                Vector2 offset = position - world.Position;
                dot.Angle = Mathf.Atan2(offset.y, offset.x);
                dot.Radius = offset.magnitude;
                dot.Heading = heading;
                dot.Shown = 1f;
                dot.Position = position;
                side.Add(dot);
            }

            // A world draws no more Darts than the stationed limit, however many fly in. The rest fade
            // where they are, as surplus ones always do, rather than being shown as casualties.
            Retire(garrison, side, display.MostStationedDots, Palette.Of(report.Arriving));

            transit.Label.gameObject.SetActive(false);
            labelPool.Push(transit.Label);
            transit.Course.Hide();
            coursePool.Push(transit.Course);
        }

        // True when the fleet will form up outside the orbit, facing a force stationed at its target.
        bool HeadsForOuterFormation(Fleet fleet)
        {
            return DartSwarm.FormsUpOutside(fleet.Faction, WorldState.Of(match.Nodes[fleet.TargetId]));
        }

        FleetCourse RentCourse(Faction faction)
        {
            FleetCourse course = coursePool.Count > 0 ? coursePool.Pop() : new FleetCourse(transform, lineMaterial);

            // Taken from the style every time, so a pooled line shows the style as it is now.
            course.Paint(faction, routeStyle);
            return course;
        }

        TextMeshPro RentLabel(Fleet fleet)
        {
            TextMeshPro label = labelPool.Count > 0
                ? labelPool.Pop()
                : Instantiate(fleetLabelTemplate, transform);
            ShowCount(label, fleet.Faction, fleet.Darts);
            label.gameObject.SetActive(true);
            return label;
        }

        /// <summary>Writes the Dart count of a fleet in transit on its label, in its owner's color.</summary>
        internal static void ShowCount(TextMeshPro label, Faction faction, int darts)
        {
            label.SetText("{0}", darts);
            label.color = Palette.Bright(Palette.Of(faction), 0.35f);
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
            LoseDots(garrison, garrison.Stationed, DartFormations.StationedDots(battle.StationedDarts, display),
                Palette.Of(battle.Stationed), ref garrison.StationedFlashIn);
            LoseDots(garrison, garrison.Arriving, DartFormations.StationedDots(battle.ArrivingDarts, display),
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
                    effects.Explode(dot.Position, color, 1f, style.DotRadius);
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
                effects.Explode(dot.Position + Random.insideUnitCircle * 0.05f, color, 0.7f, style.DotRadius);
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
            effects.Burst(center, spawnRadius, color, count, speed, life, style.DotRadius);
        }

        // ------------------------------------------------------------------ drawing

        /// <summary>
        /// Moves and draws everything for one frame. With no time passing, as under the pause menu,
        /// the same picture is drawn again: no dot moves, no shot is fired and no spark ages. A count that
        /// changes with no time passing, as a sandbox edit while paused, is drawn at once: new dots stand in
        /// their places and surplus ones are gone, while every other dot stays where it was.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (match == null) return;

            float time = Time.time;
            bool playing = match.Status == MatchStatus.InProgress;
            if (paintedPalette != Palette.Version) Repaint();
            batch.Begin();
            bodies.Begin(style);
            FindApproaches();
            for (int n = 0; n < garrisons.Length; n++)
                DrawGarrison(n, time, deltaTime, playing);
            DrawTransits(time, deltaTime);
            effects.Draw(batch, deltaTime, style.DotRadius);
            bodies.End();
            batch.End();
        }

        // The lines and labels of fleets in transit keep the color they were given. When a sandbox shows a
        // player in another color, they are painted again.
        void Repaint()
        {
            paintedPalette = Palette.Version;
            foreach (Transit transit in transits.Values)
            {
                transit.Course.Paint(transit.Fleet.Faction, routeStyle);
                ShowCount(transit.Label, transit.Fleet.Faction, transit.Fleet.Darts);
            }
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
                garrison.ApproachAngle = Mathf.Atan2(-transit.Swarm.Direction.y, -transit.Swarm.Direction.x);
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
            int stationedDots = DartFormations.StationedDots(node.Darts, display);
            int arrivingDots = fighting ? DartFormations.StationedDots(battle.ArrivingDarts, display) : 0;
            bool frozen = deltaTime <= 0f;
            Resize(garrison, garrison.Stationed, stationedDots, stationedColor, world, time, gathered, true, frozen);
            Resize(garrison, garrison.Arriving, arrivingDots, arrivingColor, world, time, 1f, false, frozen);

            DrawSide(garrison, garrison.Stationed, world, time, deltaTime, gathered, true,
                style.StationedTint(stationedColor));
            DrawSide(garrison, garrison.Arriving, world, time, deltaTime, 1f, false,
                style.MovingTint(arrivingColor));
            DrawLeaving(garrison, deltaTime);

            if (fighting && playing && deltaTime > 0f) Fight(garrison, battle, deltaTime);
        }

        // Brings a side to the number of dots its Darts call for. New dots appear in their places one
        // after another; surplus ones fade out. Dots destroyed in battle never get here: they explode.
        // Frozen, with no time to appear or fade in, new dots are drawn whole at once and surplus ones go.
        void Resize(Garrison garrison, List<Dot> dots, int target, Color color, WorldView world, float time,
            float gathered, bool stationed, bool frozen)
        {
            if (dots.Count > target)
            {
                if (frozen) Drop(dots, target);
                else Retire(garrison, dots, target, color);
                return;
            }

            for (int added = 0; dots.Count < target; added++)
            {
                Dot dot = RentDot();
                dots.Add(dot);
                Place(garrison, world, dots.Count - 1, target, time, gathered, stationed, out dot.Angle, out dot.Radius);
                dot.Heading = DartFormations.Heading(dot.Angle, dots.Count - 1, stationed, stationed ? gathered : 1f,
                    display.OrbitSpeed);
                dot.Shown = frozen ? 1f : -0.1f * added;
                dot.Position = world.Position + DartFormations.Polar(dot.Angle, dot.Radius);
            }
        }

        void Retire(Garrison garrison, List<Dot> dots, int target, Color color)
        {
            Vector4 tint = style.StationedTint(color);
            for (int faded = 0; dots.Count > target; faded++)
            {
                Dot dot = dots[dots.Count - 1];
                dots.RemoveAt(dots.Count - 1);
                dot.Tint = tint;
                dot.Shown = Mathf.Min(dot.Shown, 1f) + 0.03f * faded;
                garrison.Leaving.Add(dot);
            }
        }

        void Drop(List<Dot> dots, int target)
        {
            while (dots.Count > target)
            {
                dotPool.Push(dots[dots.Count - 1]);
                dots.RemoveAt(dots.Count - 1);
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

                    // It turns as it settles: along its orbit, toward the enemy in formation.
                    float heading = DartFormations.Heading(dot.Angle, i, stationed, stationed ? gathered : 1f,
                        display.OrbitSpeed);
                    dot.Heading = Mathf.SmoothDampAngle(dot.Heading * Mathf.Rad2Deg, heading * Mathf.Rad2Deg,
                        ref dot.HeadingSpeed, settleSeconds, Mathf.Infinity, deltaTime) * Mathf.Deg2Rad;
                    dot.Shown = Mathf.MoveTowards(dot.Shown, 1f, deltaTime * 7f);
                }

                // Fighting dots keep moving: some dart forward at the enemy and back, all of them drift.
                float lunge = 0f;
                float drift = 0f;
                if (garrison.Engaged > 0f)
                {
                    float pace = 1.7f + 1.3f * Mathf.Repeat(dot.Phase * 3.7f, 1f);
                    float stray = style.StrayDistance;
                    lunge = stray * dot.Restless * (0.5f + 0.5f * Mathf.Sin(time * pace + dot.Phase))
                        * towardEnemy * garrison.Engaged;
                    drift = 0.35f * stray * Mathf.Sin(time * 2.3f + dot.Phase * 5f) * garrison.Engaged;
                }

                float shownRadius = dot.Radius + lunge;
                float shownAngle = dot.Angle + drift / Mathf.Max(0.2f, shownRadius);
                dot.Position = center + DartFormations.Polar(shownAngle, shownRadius);
                bodies.Add(dot.Position, dot.Heading, style.DotSize * Mathf.Clamp01(dot.Shown), color);
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

                bodies.Add(dot.Position, dot.Heading, style.DotSize * Mathf.Clamp01(dot.Shown), dot.Tint);
            }
        }

        // Where dot number `index` of `count` belongs: on its orbit, in its battle formation, or part
        // of the way between the two while the defenders gather.
        void Place(Garrison garrison, WorldView world, int index, int count, float time, float gathered,
            bool stationed, out float angle, out float radius)
        {
            if (!stationed)
            {
                DartFormations.FormationPlace(world, index, count, garrison.FrontAngle, false, style,
                    out angle, out radius);
                return;
            }

            DartFormations.OrbitPlace(world, index, time, display.OrbitSpeed, orbitShift, style, out angle, out radius);
            if (gathered <= 0f) return;

            DartFormations.FormationPlace(world, index, count, garrison.FrontAngle, true, style,
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
                DartSwarm swarm = transit.Swarm;
                swarm.Standoff = Mathf.MoveTowards(swarm.Standoff, HeadsForOuterFormation(fleet) ? 1f : 0f,
                    deltaTime / 0.5f);

                PlaceTransit(transit, time, out Vector2 center, out float formation);
                swarm.Draw(bodies, batch, fleet.Faction, style);
                transit.Label.transform.position = swarm.LabelPosition(center, formation, style);
            }
        }

        void PlaceTransit(Transit transit, float time, out Vector2 center, out float formation)
        {
            Fleet fleet = transit.Fleet;
            transit.Swarm.Place(fleet.Progress, fleet.Elapsed, fleet.Remaining, time, style, transit.Course,
                out center, out formation);
        }

        // ------------------------------------------------------------------ helpers

        Dot RentDot()
        {
            Dot dot = dotPool.Count > 0 ? dotPool.Pop() : new Dot();
            dot.AngleSpeed = 0f;
            dot.RadiusSpeed = 0f;
            dot.HeadingSpeed = 0f;
            dot.Shown = 1f;
            dot.Phase = Random.value * Mathf.PI * 2f;
            dot.Restless = Random.value < 0.4f ? 1f : 0.2f;
            return dot;
        }
    }
}
