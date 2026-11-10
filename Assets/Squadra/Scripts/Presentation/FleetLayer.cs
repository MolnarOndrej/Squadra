using System.Collections.Generic;
using Squadra.Simulation;
using TMPro;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// Draws every ship dot in the scene: fleets orbiting their worlds, fleets in transit, and battle sparks.
    /// Dots are presentation only. Counts come from the match, and large fleets are shown in aggregate.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class FleetLayer : MonoBehaviour
    {
        const float GoldenAngle = 2.39996323f;

        [SerializeField] float dotRadius = 0.055f;
        [SerializeField] float dotBrightness = 1.5f;
        [Tooltip("Inactive template cloned for the ship count that travels with each fleet.")]
        [SerializeField] TextMeshPro fleetLabelTemplate;

        sealed class Transit
        {
            public Fleet Fleet;
            public Vector2 Start;
            public Vector2 End;
            public Vector2 Direction;
            public Vector2[] Offsets;
            public float[] Phases;
            public TextMeshPro Label;
            public TransitRoute Route;
        }

        sealed class TransitRoute
        {
            public LineStyle Lead;
            public LineStyle Wake;
        }

        struct Spark
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Age;
            public float Life;
            public float Size;
            public Vector4 Color;
        }

        readonly Dictionary<int, Transit> transits = new Dictionary<int, Transit>();
        readonly Stack<TextMeshPro> labelPool = new Stack<TextMeshPro>();
        readonly Stack<TransitRoute> routePool = new Stack<TransitRoute>();
        readonly List<Spark> sparks = new List<Spark>(256);

        DotBatch batch;
        Match match;
        IReadOnlyList<WorldView> worlds;
        Material lineMaterial;
        float[] shownDots;

        public void Bind(Match boundMatch, IReadOnlyList<WorldView> boundWorlds, Material routeMaterial)
        {
            match = boundMatch;
            worlds = boundWorlds;
            lineMaterial = routeMaterial;
            shownDots = new float[worlds.Count];
            for (int i = 0; i < shownDots.Length; i++)
                shownDots[i] = DotCount(match.Nodes[i].Ships);

            if (batch == null) batch = new DotBatch(GetComponent<MeshFilter>());
        }

        void OnDestroy()
        {
            batch?.Dispose();
        }

        /// <summary>How many dots stand for a fleet. One per ship at first, then growing slowly.</summary>
        public static int DotCount(int ships)
        {
            if (ships <= 0) return 0;
            if (ships <= 24) return ships;
            return Mathf.Min(64, 24 + Mathf.RoundToInt(Mathf.Sqrt(ships - 24) * 2.5f));
        }

        // ------------------------------------------------------------------ transit

        public void BeginTransit(Fleet fleet)
        {
            WorldView source = worlds[fleet.SourceId];
            WorldView target = worlds[fleet.TargetId];
            Vector2 delta = target.Position - source.Position;
            Vector2 direction = delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector2.right;

            int dots = DotCount(fleet.Ships);
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

            transits[fleet.Id] = new Transit
            {
                Fleet = fleet,
                Start = source.Position + direction * source.OrbitRadius,
                End = target.Position - direction * target.OrbitRadius,
                Direction = direction,
                Offsets = offsets,
                Phases = phases,
                Label = RentLabel(fleet),
                Route = RentRoute(fleet.Faction),
            };
        }

        public void EndTransit(int fleetId)
        {
            if (!transits.TryGetValue(fleetId, out Transit transit)) return;
            transits.Remove(fleetId);
            transit.Label.gameObject.SetActive(false);
            labelPool.Push(transit.Label);
            transit.Route.Lead.Visible = false;
            transit.Route.Wake.Visible = false;
            routePool.Push(transit.Route);
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
            label.SetText("{0}", fleet.Ships);
            label.color = Palette.Bright(Palette.Of(fleet.Faction), 0.35f);
            label.gameObject.SetActive(true);
            return label;
        }

        // ------------------------------------------------------------------ sparks

        /// <summary>Throws a ring of short-lived sparks outward from a circle around <paramref name="center"/>.</summary>
        public void Burst(Vector2 center, float spawnRadius, Color color, int count, float speed, float life = 0.55f)
        {
            Vector4 hdr = Palette.Hdr(color, 3f, 1f);
            for (int i = 0; i < count; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 scatter = Random.insideUnitCircle * 0.5f;
                sparks.Add(new Spark
                {
                    Position = center + direction * spawnRadius,
                    Velocity = (direction + scatter) * (speed * Random.Range(0.45f, 1.15f)),
                    Age = 0f,
                    Life = life * Random.Range(0.6f, 1.2f),
                    Size = dotRadius * Random.Range(0.7f, 1.5f),
                    Color = hdr,
                });
            }
        }

        // ------------------------------------------------------------------ drawing

        public void Tick(float deltaTime)
        {
            if (match == null) return;

            float time = Time.time;
            batch.Begin();
            DrawOrbits(time, deltaTime);
            DrawTransits(time);
            DrawSparks(deltaTime);
            batch.End();
        }

        void DrawOrbits(float time, float deltaTime)
        {
            for (int n = 0; n < worlds.Count; n++)
            {
                Node node = match.Nodes[n];
                WorldView world = worlds[n];

                // Dots appear and disappear one after another rather than all at once.
                float target = DotCount(node.Ships);
                float rate = Mathf.Max(30f, Mathf.Abs(target - shownDots[n]) * 7f);
                shownDots[n] = Mathf.MoveTowards(shownDots[n], target, rate * deltaTime);

                Vector4 color = Palette.Hdr(Palette.Of(node.Occupant), dotBrightness, 1f);
                Vector2 center = world.Position;
                int count = Mathf.CeilToInt(shownDots[n]);
                for (int i = 0; i < count; i++)
                {
                    float visible = Mathf.Clamp01(shownDots[n] - i);
                    int lane = i % 3;
                    int index = i / 3;
                    float speed = 0.42f - lane * 0.075f;
                    float angle = index * GoldenAngle + lane * 2.1f + time * speed;
                    float radius = world.OrbitRadius + (lane - 1) * 0.13f + 0.018f * Mathf.Sin(time * 1.3f + i * 1.7f);
                    var position = new Vector2(center.x + Mathf.Cos(angle) * radius, center.y + Mathf.Sin(angle) * radius);
                    batch.Add(position, dotRadius * 2f * visible, color);
                }
            }
        }

        void DrawTransits(float time)
        {
            foreach (Transit transit in transits.Values)
            {
                Fleet fleet = transit.Fleet;
                Vector2 center = Vector2.Lerp(transit.Start, transit.End, fleet.Progress);
                transit.Route.Lead.Set(center, transit.End);
                transit.Route.Wake.Set(transit.Start, center);
                Vector2 direction = transit.Direction;
                var normal = new Vector2(-direction.y, direction.x);

                // The swarm fans out after launch and gathers again before arrival.
                float opening = Mathf.Clamp01((float)fleet.Elapsed / 0.45f);
                float closing = Mathf.Clamp01((float)fleet.Remaining / 0.3f);
                float formation = Mathf.Lerp(0.3f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Min(opening, closing)));

                Vector4 color = Palette.Hdr(Palette.Of(fleet.Faction), dotBrightness * 1.15f, 1f);
                Vector4 trail = color;
                for (int i = 0; i < transit.Offsets.Length; i++)
                {
                    Vector2 offset = transit.Offsets[i] * formation;
                    float wobble = 0.03f * Mathf.Sin(time * 3.1f + transit.Phases[i]);
                    Vector2 position = center + direction * offset.x + normal * (offset.y + wobble);
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

        void DrawSparks(float deltaTime)
        {
            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                Spark spark = sparks[i];
                spark.Age += deltaTime;
                if (spark.Age >= spark.Life)
                {
                    sparks[i] = sparks[sparks.Count - 1];
                    sparks.RemoveAt(sparks.Count - 1);
                    continue;
                }

                spark.Velocity *= Mathf.Exp(-3.2f * deltaTime);
                spark.Position += spark.Velocity * deltaTime;
                sparks[i] = spark;

                float remaining = 1f - spark.Age / spark.Life;
                Vector4 color = spark.Color;
                color.w = remaining;
                batch.Add(spark.Position, spark.Size * 2f * (0.35f + 0.65f * remaining), color);
            }
        }
    }
}
