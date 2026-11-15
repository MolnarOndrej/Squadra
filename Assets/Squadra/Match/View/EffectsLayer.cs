using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Squadra
{
    /// <summary>Short-lived feedback: expanding shockwave rings and floating text. Everything is pooled.</summary>
    public sealed class EffectsLayer : MonoBehaviour
    {
        [SerializeField] Material ringMaterial;
        [Tooltip("Inactive template cloned for floating battle text.")]
        [SerializeField] TextMeshPro floatingTextTemplate;
        [SerializeField] int waveSortingOrder = 15;

        sealed class Wave
        {
            public RingStyle Ring;
            public Vector2 Position;
            public Color Color;
            public float FromRadius;
            public float ToRadius;
            public float Intensity;
            public float Age;
            public float Life;
            public bool Active;
        }

        sealed class Floater
        {
            public TextMeshPro Text;
            public Vector2 Start;
            public Color Color;
            public float Rise;
            public float Age;
            public float Life;
            public float Delay;
            public bool Active;
        }

        readonly List<Wave> waves = new List<Wave>();
        readonly List<Floater> floaters = new List<Floater>();

        public void Shockwave(Vector2 position, Color color, float fromRadius, float toRadius, float life = 0.5f,
            float intensity = 2f)
        {
            Wave wave = null;
            foreach (Wave candidate in waves)
            {
                if (!candidate.Active)
                {
                    wave = candidate;
                    break;
                }
            }

            if (wave == null)
            {
                MeshRenderer renderer = Shapes.CreateQuad("Shockwave", transform, ringMaterial, waveSortingOrder);
                wave = new Wave { Ring = new RingStyle(renderer) };
                waves.Add(wave);
            }

            wave.Position = position;
            wave.Color = color;
            wave.FromRadius = fromRadius;
            wave.ToRadius = toRadius;
            wave.Intensity = intensity;
            wave.Age = 0f;
            wave.Life = Mathf.Max(0.05f, life);
            wave.Active = true;
            wave.Ring.Renderer.transform.position = position;
            wave.Ring.Visible = true;
            ApplyWave(wave);
        }

        public void FloatText(Vector2 position, string text, Color color, float fontSize = 4f, float delay = 0f,
            float life = 1.1f)
        {
            Floater floater = null;
            foreach (Floater candidate in floaters)
            {
                if (!candidate.Active)
                {
                    floater = candidate;
                    break;
                }
            }

            if (floater == null)
            {
                floater = new Floater { Text = Instantiate(floatingTextTemplate, transform) };
                floaters.Add(floater);
            }

            floater.Start = position;
            floater.Color = color;
            floater.Rise = 0.4f;
            floater.Age = 0f;
            floater.Life = life;
            floater.Delay = delay;
            floater.Active = true;
            floater.Text.text = text;
            floater.Text.fontSize = fontSize;
            floater.Text.transform.position = position;
            floater.Text.gameObject.SetActive(delay <= 0f);
            ApplyFloater(floater);
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;

            foreach (Wave wave in waves)
            {
                if (!wave.Active) continue;
                wave.Age += deltaTime;
                if (wave.Age >= wave.Life)
                {
                    wave.Active = false;
                    wave.Ring.Visible = false;
                    continue;
                }

                ApplyWave(wave);
            }

            foreach (Floater floater in floaters)
            {
                if (!floater.Active) continue;
                if (floater.Delay > 0f)
                {
                    floater.Delay -= deltaTime;
                    if (floater.Delay > 0f) continue;
                    floater.Text.gameObject.SetActive(true);
                }

                floater.Age += deltaTime;
                if (floater.Age >= floater.Life)
                {
                    floater.Active = false;
                    floater.Text.gameObject.SetActive(false);
                    continue;
                }

                ApplyFloater(floater);
            }
        }

        static void ApplyWave(Wave wave)
        {
            float t = wave.Age / wave.Life;
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);
            float fade = (1f - t) * (1f - t);

            RingStyle ring = wave.Ring;
            ring.Radius = Mathf.Lerp(wave.FromRadius, wave.ToRadius, eased);
            ring.Thickness = Mathf.Lerp(0.09f, 0.015f, t);
            ring.Softness = Mathf.Lerp(0.12f, 0.3f, t);
            ring.Glow = 0.6f;
            ring.Additive = 0.9f;
            ring.Tint = Palette.Hdr(wave.Color, wave.Intensity, fade);
            ring.Apply();
        }

        static void ApplyFloater(Floater floater)
        {
            float t = Mathf.Clamp01(floater.Age / floater.Life);
            float eased = 1f - (1f - t) * (1f - t);
            float pop = 1f + 0.35f * Mathf.Clamp01(1f - t * 6f);

            Transform textTransform = floater.Text.transform;
            textTransform.position = floater.Start + Vector2.up * (floater.Rise * eased);
            textTransform.localScale = new Vector3(pop, pop, 1f);

            Color color = floater.Color;
            color.a = Mathf.Clamp01((1f - t) * 2.5f);
            floater.Text.color = color;
        }
    }
}
