using UnityEngine;
using UnityEngine.UIElements;

namespace Squadra
{
    /// <summary>
    /// The celebration behind the Campaign Complete panel: small stars that rise, sway and twinkle in
    /// the game's gold and starlight. It is decoration only and never takes the pointer.
    /// </summary>
    [UxmlElement]
    public sealed partial class ResultsCelebration : VisualElement
    {
        const int SparkCount = 54;
        const long FrameMilliseconds = 33;

        static readonly Color[] Colors =
        {
            Palette.Accent, Palette.Accent, Palette.Sunlight, Palette.Starlight, Palette.Bright(Palette.Blue, 0.3f),
        };

        struct Spark
        {
            public float X;
            public float Start;
            public float Speed;
            public float Size;
            public float Sway;
            public float Phase;
            public Color Color;
        }

        readonly Spark[] sparks = new Spark[SparkCount];
        IVisualElementScheduledItem ticker;
        float time;

        public ResultsCelebration()
        {
            pickingMode = PickingMode.Ignore;

            // A fixed seed: the same pleasant spread every time, with nothing left to chance.
            var random = new System.Random(11);
            float Next(float from, float to) => from + (float)random.NextDouble() * (to - from);
            for (int i = 0; i < sparks.Length; i++)
            {
                sparks[i] = new Spark
                {
                    X = Next(0.02f, 0.98f),
                    Start = Next(0f, 1f),
                    Speed = Next(0.05f, 0.13f),
                    Size = Next(4f, 11f),
                    Sway = Next(6f, 22f),
                    Phase = Next(0f, Mathf.PI * 2f),
                    Color = Colors[random.Next(Colors.Length)],
                };
            }

            generateVisualContent += Draw;
        }

        /// <summary>Starts or stops the animation. It costs nothing while stopped.</summary>
        public bool Playing
        {
            get => ticker != null && ticker.isActive;
            set
            {
                if (value == Playing) return;

                if (ticker == null) ticker = schedule.Execute(Tick).Every(FrameMilliseconds);
                else if (value) ticker.Resume();
                if (!value) ticker.Pause();
            }
        }

        void Tick(TimerState state)
        {
            time += state.deltaTime / 1000f;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            Rect box = contentRect;
            if (box.width < 1f || box.height < 1f) return;

            Painter2D painter = context.painter2D;
            foreach (Spark spark in sparks)
            {
                // Each star rises from the bottom edge to the top, fading in and out at the ends.
                float life = Mathf.Repeat(spark.Start + time * spark.Speed, 1f);
                float twinkle = 0.6f + 0.4f * Mathf.Sin(time * 4.5f + spark.Phase);
                float alpha = Mathf.Sin(life * Mathf.PI) * twinkle * 0.6f;
                if (alpha <= 0.01f) continue;

                var center = new Vector2(
                    box.x + spark.X * box.width + Mathf.Sin(time * 0.9f + spark.Phase) * spark.Sway,
                    box.y + (1f - life) * box.height);
                Color color = spark.Color;
                color.a = alpha;
                painter.fillColor = color;
                Star(painter, center, spark.Size * (0.75f + 0.25f * twinkle));
            }
        }

        // A four-pointed star.
        static void Star(Painter2D painter, Vector2 center, float radius)
        {
            painter.BeginPath();
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 0.25f;
                float reach = (i & 1) == 0 ? radius : radius * 0.34f;
                var point = center + new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * reach;
                if (i == 0) painter.MoveTo(point);
                else painter.LineTo(point);
            }

            painter.ClosePath();
            painter.Fill();
        }
    }
}
