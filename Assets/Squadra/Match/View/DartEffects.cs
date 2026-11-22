using System.Collections.Generic;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// The short-lived dots of the fleet layer: the shots of a battle, the explosion of a destroyed
    /// dot and the sparks of a landing or a capture. They decide nothing and stand for no Darts; each
    /// one only ages and fades. Both kinds have a cap, so a huge battle cannot flood the frame.
    /// </summary>
    sealed class DartEffects
    {
        const int MaxSparks = 700;
        const int MaxShots = 160;

        struct Spark
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Age;
            public float Life;
            public float Size;
            public Vector4 Color;
        }

        struct Shot
        {
            public Vector2 From;
            public Vector2 To;
            public float Age;
            public float Life;
            public Vector4 Color;
        }

        readonly List<Spark> sparks = new List<Spark>(256);
        readonly List<Shot> shots = new List<Shot>(64);

        /// <summary>Shots in flight across all battles.</summary>
        public int ShotsInFlight => shots.Count;

        /// <summary>Sparks alive right now: explosions, landings and captures.</summary>
        public int SparkCount => sparks.Count;

        /// <summary>A shot from one dot toward another, in the shooter's color. Dropped when too many are in flight.</summary>
        public void Shoot(Vector2 from, Vector2 to, Color shooterColor)
        {
            if (shots.Count >= MaxShots) return;

            shots.Add(new Shot
            {
                From = from,
                To = to,
                Life = Mathf.Max(0.07f, Vector2.Distance(from, to) / Random.Range(4.5f, 6.5f)),
                Color = Palette.Hdr(Palette.Bright(shooterColor, 0.55f), 4.2f, 1f),
            });
        }

        /// <summary>A white-hot flash that shrinks on the spot, and debris in the fleet's color.</summary>
        public void Explode(Vector2 position, Color color, float scale, float dotRadius)
        {
            if (sparks.Count >= MaxSparks) return;

            sparks.Add(new Spark
            {
                Position = position,
                Life = 0.22f,
                Size = dotRadius * 1.7f * scale,
                Color = Palette.Hdr(Palette.Bright(color, 0.6f), 3.6f, 1f),
            });

            Vector4 debris = Palette.Hdr(Palette.Bright(color, 0.2f), 3.2f, 1f);
            int pieces = Mathf.RoundToInt(6f * scale);
            for (int i = 0; i < pieces; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                sparks.Add(new Spark
                {
                    Position = position,
                    Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(0.6f, 2.2f) * scale,
                    Life = Random.Range(0.28f, 0.55f),
                    Size = dotRadius * Random.Range(0.6f, 1.2f),
                    Color = debris,
                });
            }
        }

        /// <summary>Throws a ring of short-lived sparks outward from a circle around <paramref name="center"/>.</summary>
        public void Burst(Vector2 center, float spawnRadius, Color color, int count, float speed, float life,
            float dotRadius)
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

        /// <summary>Ages and draws every shot, then every spark. With no time passing nothing ages.</summary>
        /// <summary>Drops every shot and spark at once, as when the battlefield is laid down afresh.</summary>
        public void Clear()
        {
            shots.Clear();
            sparks.Clear();
        }

        public void Draw(DotBatch batch, float deltaTime, float dotRadius)
        {
            DrawShots(batch, deltaTime, dotRadius);
            DrawSparks(batch, deltaTime);
        }

        void DrawShots(DotBatch batch, float deltaTime, float dotRadius)
        {
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                Shot shot = shots[i];
                shot.Age += deltaTime;
                if (shot.Age >= shot.Life)
                {
                    shots[i] = shots[shots.Count - 1];
                    shots.RemoveAt(shots.Count - 1);
                    continue;
                }

                shots[i] = shot;

                // A bright head with a short tail behind it.
                Vector2 path = shot.To - shot.From;
                Vector2 direction = path.sqrMagnitude > 1e-6f ? path.normalized : Vector2.right;
                Vector2 head = Vector2.Lerp(shot.From, shot.To, shot.Age / shot.Life);
                Vector4 color = shot.Color;
                batch.Add(head, dotRadius * 1.15f, color);
                color.w = 0.5f;
                batch.Add(head - direction * 0.06f, dotRadius * 0.95f, color);
                color.w = 0.22f;
                batch.Add(head - direction * 0.12f, dotRadius * 0.75f, color);
            }
        }

        void DrawSparks(DotBatch batch, float deltaTime)
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
