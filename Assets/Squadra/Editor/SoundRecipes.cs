using System;
using static Squadra.EditorTools.SoundSynth;

namespace Squadra.EditorTools
{
    /// <summary>
    /// How the generated Squadra sound effects are made. Fleet and battle events have several takes that
    /// differ in pitch, length and character so repeated events do not sound identical. Departures and
    /// arrivals are shaped from recordings (Kenney's CC0 "Sci-Fi Sounds"); the rest is synthesized.
    /// Tweak the numbers here and run <c>Squadra &gt; Rebuild Sound Effects</c>.
    /// </summary>
    static class SoundRecipes
    {
        public const int DepartureTakes = 3;
        public const int ArrivalTakes = 3;
        public const int BattleStartTakes = 3;
        public const int ShotTakes = 5;

        // The recordings each take is shaped from, by file name in the audio sources folder.
        public static readonly string[] DepartureEngines =
            { "spaceEngineSmall_001", "spaceEngineSmall_004", "spaceEngineSmall_003" };
        public static readonly string[] ArrivalEngines =
            { "spaceEngineSmall_004", "spaceEngineSmall_003", "spaceEngineSmall_001" };
        public static readonly string[] ArrivalPulses = { "forceField_000", "forceField_002", "forceField_003" };

        const double OutputPeak = 0.89; // about -1 dB

        // Equal-tempered intervals.
        const double MajorThird = 1.259921;
        const double Fifth = 1.498307;

        // ------------------------------------------------------------------ fleets

        /// <summary>A fleet pulling away: a recorded engine hum that spools up in pitch, then recedes.</summary>
        public static float[][] Departure(int take, Recording engine)
        {
            double length = new[] { 1.3, 1.15, 1.45 }[take];
            double offset = new[] { 0.5, 1.7, 2.9 }[take]; // where in the engine loop the take starts
            double slow = new[] { 0.75, 0.85, 0.65 }[take];
            double fast = new[] { 1.6, 1.75, 1.45 }[take];

            float[] body = Replay(engine, offset, length, t => Sweep(slow, fast, t / length));
            Shape(body, t => Smooth(t / 0.3) * Math.Pow(1 - Clamp01(t / length), 1.2));

            return Finish(OutputPeak, Reverb(body, 0.7, 0.7, 0.5, 0.12));
        }

        /// <summary>
        /// A fleet settling into orbit: the engine hum winding down, then a recorded force-field pulse as
        /// the ships take up station.
        /// </summary>
        public static float[][] Arrival(int take, Recording engine, Recording pulse)
        {
            double brake = new[] { 0.6, 0.5, 0.7 }[take];
            double offset = new[] { 1.2, 3.1, 0.3 }[take];

            float[] approach = Replay(engine, offset, brake, t => Sweep(1.4, 0.6, t / brake));
            Shape(approach, t => Smooth(t / 0.1) * (1 - Smooth((t - brake * 0.4) / (brake * 0.6))));
            Normalize(approach, 1);

            float[] settle = Replay(pulse, 0, pulse.Seconds, t => 1);
            Normalize(settle, 1);

            double settleAt = brake * 0.55;
            float[] mix = Buffer(settleAt + pulse.Seconds);
            Mix(mix, approach, 0, 0.6);
            Mix(mix, settle, settleAt, 1);

            return Finish(OutputPeak, Reverb(mix, 0.7, 0.7, 0.5, 0.12));
        }

        // ------------------------------------------------------------------ battle

        /// <summary>Two fleets meet: a heavy impact under a dissonant klaxon and a metallic ring.</summary>
        public static float[][] BattleStart(int take)
        {
            var rng = new Random(1300 + take);
            double root = new[] { 196.00, 174.61, 207.65 }[take]; // G3, F3, G#3

            // Klaxon pulses: start, length, pitch bend from and to (as a multiple of the root).
            double[][] pulses;
            switch (take)
            {
                case 0:
                    pulses = new[] { new[] { 0.02, 0.17, 0.94, 1.0 }, new[] { 0.24, 0.42, 0.94, 1.0 } };
                    break;
                case 1:
                    pulses = new[] { new[] { 0.02, 0.6, 1.06, 0.92 } };
                    break;
                default:
                    pulses = new[]
                    {
                        new[] { 0.02, 0.11, 0.94, 1.0 }, new[] { 0.17, 0.11, 1.0, 1.06 },
                        new[] { 0.32, 0.36, 1.06, 1.12 },
                    };
                    break;
            }

            float[] impact = Buffer(1.0);
            Osc(impact, Wave.Sine, 0, 1.0, t => Sweep(140, 55, t / 0.3), t => Pluck(t, 0.003, 0.22));
            float[] burst = Noise(rng, 0.8);
            Svf(burst, Filter.Low, t => Sweep(1500, 180, t / 0.4), 0.9);
            Normalize(burst, 1);
            Shape(burst, t => Pluck(t, 0.002, 0.16));
            Mix(impact, burst, 0, 0.7);
            Drive(impact, 2.5);

            // Root plus a tritone above it: the interval that reads as an alarm.
            float[] horn = Buffer(1.0);
            foreach (double[] pulse in pulses)
            {
                double start = pulse[0], span = pulse[1], bendFrom = pulse[2], bendTo = pulse[3];
                double Bend(double t) => root * (bendFrom + (bendTo - bendFrom) * Clamp01(t / span));
                double Gate(double t) => Hold(t, 0.006, span - 0.05, 0.05);

                Osc(horn, Wave.Saw, start, span, t => Bend(t), t => 0.5 * Gate(t), rng.NextDouble());
                Osc(horn, Wave.Saw, start, span, t => Bend(t) * 1.41421, t => 0.35 * Gate(t), rng.NextDouble());
                Osc(horn, Wave.Square, start, span, t => Bend(t) * 2, t => 0.2 * Gate(t), rng.NextDouble());
                Osc(horn, Wave.Square, start, span, t => Bend(t) * 0.5, t => 0.3 * Gate(t), rng.NextDouble());
            }

            Svf(horn, Filter.Low, t => 2600, 1.4);
            Normalize(horn, 1);

            float[] mix = Buffer(1.3);
            Mix(mix, impact, 0, 0.7);
            Mix(mix, horn, 0, 0.75);

            foreach (double ringPitch in new[] { 1830.0, 2740.0 })
            {
                float[] ring = Noise(rng, 1.0);
                Svf(ring, Filter.Band, t => ringPitch, 30);
                Normalize(ring, 1);
                Shape(ring, t => Pluck(t, 0.002, 0.35));
                Mix(mix, ring, 0, ringPitch < 2000 ? 0.12 : 0.08);
            }

            return Finish(OutputPeak, Reverb(mix, 1.3, 0.84, 0.35, 0.33));
        }

        /// <summary>One weapon shot: a tone that drops fast in pitch. Takes range from light zips to heavy bolts.</summary>
        public static float[][] Shot(int take)
        {
            var rng = new Random(1400 + take);
            float[] mix = Buffer(0.6);
            switch (take)
            {
                case 0:
                    Zap(mix, rng, 0, 2600, 300, 0.26, Wave.Saw, 0);
                    break;
                case 1:
                    Zap(mix, rng, 0, 3400, 520, 0.18, Wave.Square, 0);
                    break;
                case 2:
                    Zap(mix, rng, 0, 1500, 140, 0.4, Wave.Saw, 0);
                    Osc(mix, Wave.Sine, 0, 0.4, t => Sweep(200, 70, t / 0.2), t => 0.5 * Pluck(t, 0.003, 0.1));
                    break;
                case 3:
                    Zap(mix, rng, 0, 2900, 420, 0.11, Wave.Saw, 0);
                    Zap(mix, rng, 0.095, 3100, 450, 0.13, Wave.Saw, 0);
                    break;
                default:
                    Zap(mix, rng, 0, 2100, 260, 0.3, Wave.Saw, 0.09);
                    break;
            }

            return Finish(OutputPeak, Reverb(mix, 0.45, 0.6, 0.45, 0.2));
        }

        static void Zap(float[] target, Random rng, double start, double from, double to, double length, Wave wave,
            double wobble)
        {
            double Pitch(double t)
            {
                return Sweep(from, to, Math.Pow(t / length, 0.55)) * (1 + wobble * Math.Sin(2 * Math.PI * 38 * t));
            }

            float[] zap = Buffer(length);
            Osc(zap, wave, 0, length, Pitch, t => 0.6);
            Osc(zap, Wave.Sine, 0, length, t => Pitch(t) * 0.5, t => 0.4);
            Svf(zap, Filter.Low, t => Pitch(t) * 2.5 + 500, 2.2);
            Shape(zap, t => Pluck(t, 0.002, length * 0.6) * (1 - Smooth((t - length * 0.8) / (length * 0.2))));
            Normalize(zap, 1);

            float[] click = Noise(rng, 0.05);
            Svf(click, Filter.High, t => 3200, 0.8);
            Shape(click, t => Pluck(t, 0.0005, 0.009));

            Mix(target, zap, start, 0.8);
            Mix(target, click, start, 0.3);
        }

        // ------------------------------------------------------------------ worlds

        /// <summary>
        /// A world changes hands: a C major arpeggio of chimes climbing over a warm chord and a rush of air.
        /// There is one take on purpose, so a capture always sounds the same.
        /// </summary>
        public static float[][] Capture()
        {
            var rng = new Random(1600);
            const double root = 523.25; // C5
            const double gap = 0.09;
            double[] steps = { 1, MajorThird, Fifth, 2 };

            float[] mix = Buffer(2.0);
            for (int i = 0; i < steps.Length; i++)
                Bell(mix, 0.05 + i * gap, root * steps[i], 0.34 + 0.05 * i, 0.42);

            double landing = 0.05 + (steps.Length - 1) * gap;
            Bell(mix, landing + 0.05, root * steps[steps.Length - 1] * 2, 0.16, 0.5);
            Osc(mix, Wave.Sine, landing, 0.8, t => root / 4, t => 0.5 * Pluck(t, 0.01, 0.3));

            float[] pad = Buffer(1.6);
            foreach (double ratio in new[] { 0.5, 0.5 * Fifth, 1.0, MajorThird })
            {
                foreach (double detune in new[] { 0.996, 1.004 })
                    Osc(pad, Wave.Saw, 0, 1.6, t => root * ratio * detune, t => 1, rng.NextDouble());
            }

            Svf(pad, Filter.Low, t => Sweep(500, 2600, t / 0.5), 1.0);
            Normalize(pad, 1);
            Shape(pad, t => Hold(t, 0.3, 0.7, 0.85));
            Mix(mix, pad, 0, 0.3);

            float[] air = Noise(rng, 1.2);
            Svf(air, Filter.Band, t => Sweep(2500, 9000, t / 0.45), 2.5);
            Normalize(air, 1);
            Shape(air, t => Hold(t, 0.42, 0.45, 0.6));
            Mix(mix, air, 0, 0.16);

            return Finish(OutputPeak, Reverb(mix, 1.8, 0.86, 0.25, 0.4));
        }

        // ------------------------------------------------------------------ match result

        /// <summary>Victory: a short brass fanfare rising through C major to a held high note.</summary>
        public static float[][] Victory()
        {
            var rng = new Random(1700);
            const double G3 = 196.00, B3 = 246.94, C3 = 130.81, C4 = 261.63, D4 = 293.66, E4 = 329.63;
            const double G4 = 392.00, C5 = 523.25, D5 = 587.33, E5 = 659.25, G5 = 783.99, C6 = 1046.50;
            const double Final = 1.28; // when the last chord lands

            float[] mix = Buffer(4.0);

            // Melody: start, pitch, length.
            double[][] melody =
            {
                new[] { 0.00, G4, 0.13 }, new[] { 0.16, C5, 0.13 }, new[] { 0.32, E5, 0.13 },
                new[] { 0.48, G5, 0.40 }, new[] { 0.96, D5, 0.13 }, new[] { 1.12, G5, 0.13 },
                new[] { Final, C6, 2.0 },
            };
            foreach (double[] note in melody)
            {
                Brass(mix, rng, note[0], note[1], note[2], 0.6);
                Brass(mix, rng, note[0], note[1] * 0.5, note[2], 0.3);
            }

            // Harmony: C, G, then C held under the last note.
            foreach (double pitch in new[] { C3, G3, C4, E4 })
                Brass(mix, rng, 0.48, pitch, 0.42, 0.22);
            foreach (double pitch in new[] { G3, B3, D4 })
                Brass(mix, rng, 0.96, pitch, 0.27, 0.22);
            foreach (double pitch in new[] { C3, G3, C4, E4, G4 })
                Brass(mix, rng, Final, pitch, 2.0, 0.24);

            // Drums on the two landings.
            Osc(mix, Wave.Sine, 0.48, 0.9, t => Sweep(110, 62, t / 0.12), t => 0.5 * Pluck(t, 0.004, 0.3));
            Osc(mix, Wave.Sine, Final, 1.2, t => Sweep(120, 62, t / 0.14), t => 0.8 * Pluck(t, 0.004, 0.35));

            // A swell of air into the last chord and chimes sparkling above it.
            float[] air = Noise(rng, 3.4);
            Svf(air, Filter.High, t => 5000, 0.8);
            Shape(air, t => t < Final ? Smooth((t - 0.9) / (Final - 0.9)) : Math.Exp(-(t - Final) / 0.7));
            Mix(mix, air, 0, 0.1);

            double[] sparkle = { C6, C6 * MajorThird, C6 * Fifth, C6 * 2 };
            for (int i = 0; i < sparkle.Length; i++)
                Bell(mix, Final + 0.1 + i * 0.08, sparkle[i], 0.1, 0.5);

            return Finish(OutputPeak, Reverb(mix, 2.0, 0.88, 0.3, 0.38), Reverb(mix, 2.0, 0.88, 0.3, 0.38, 23));
        }

        /// <summary>Defeat: a slow line sinking through C minor over a low drone, the last note sagging flat.</summary>
        public static float[][] Defeat()
        {
            var rng = new Random(1800);
            const double C2 = 65.41, G2 = 98.00, C3 = 130.81, Eb3 = 155.56, G3 = 196.00;
            const double C4 = 261.63, D4 = 293.66, Eb4 = 311.13, G4 = 392.00;
            const double Final = 1.95;

            float[] mix = Buffer(4.8);

            Dark(mix, rng, 0.15, G4, 0.55, 0.6, 0);
            Dark(mix, rng, 0.75, Eb4, 0.55, 0.6, 0);
            Dark(mix, rng, 1.35, D4, 0.55, 0.6, 0);
            Dark(mix, rng, Final, C4, 2.2, 0.65, 0.03);
            foreach (double pitch in new[] { C3, Eb3, G3 })
                Dark(mix, rng, Final, pitch, 2.2, 0.28, 0.03);

            float[] drone = Buffer(4.6);
            foreach (double pitch in new[] { C2, C2 * 1.006, G2 })
                Osc(drone, Wave.Saw, 0, 4.6, t => pitch, t => 0.33, rng.NextDouble());
            Svf(drone, Filter.Low, t => 380, 0.9);
            Normalize(drone, 1);
            Shape(drone, t => Hold(t, 0.4, 3.2, 1.3));
            Mix(mix, drone, 0, 0.28);

            // A dull impact to open.
            Osc(mix, Wave.Sine, 0, 1.6, t => Sweep(100, 45, t / 0.5), t => 0.8 * Pluck(t, 0.005, 0.4));
            float[] thud = Noise(rng, 1.0);
            Svf(thud, Filter.Low, t => 320, 0.8);
            Normalize(thud, 1);
            Shape(thud, t => Pluck(t, 0.004, 0.25));
            Mix(mix, thud, 0, 0.4);

            return Finish(OutputPeak, Reverb(mix, 2.4, 0.9, 0.5, 0.45), Reverb(mix, 2.4, 0.9, 0.5, 0.45, 23));
        }

        // ------------------------------------------------------------------ voices

        // Three detuned saws through a filter that opens with the note: a synthetic brass section.
        static void Brass(float[] target, Random rng, double start, double pitch, double length, double amplitude)
        {
            double Envelope(double t) => Hold(t, 0.025, length, 0.15);
            double Vibrato(double t) => 1 + 0.004 * Math.Sin(2 * Math.PI * 5.5 * t) * Smooth((t - 0.25) / 0.3);

            float[] voice = Buffer(length + 0.2);
            foreach (double detune in new[] { 0.9985, 1.0, 1.0015 })
                Osc(voice, Wave.Saw, 0, length + 0.2, t => pitch * detune * Vibrato(t), t => 0.33, rng.NextDouble());
            Svf(voice, Filter.Low, t => Math.Min(9000, 900 + pitch + 3200 * Envelope(t) * Math.Exp(-t / 1.6)), 0.9);
            Shape(voice, Envelope);
            Mix(target, voice, start, amplitude);
        }

        // A muted, closing voice. `sag` lets the pitch sink over the note by that fraction.
        static void Dark(float[] target, Random rng, double start, double pitch, double length, double amplitude,
            double sag)
        {
            double Pitch(double t) => pitch * (1 - sag * Smooth((t - length * 0.35) / (length * 0.65)));

            float[] voice = Buffer(length + 0.5);
            foreach (double detune in new[] { 0.995, 1.005 })
                Osc(voice, Wave.Saw, 0, length + 0.5, t => Pitch(t) * detune, t => 0.4, rng.NextDouble());
            Osc(voice, Wave.Sine, 0, length + 0.5, t => Pitch(t) * 0.5, t => 0.3);
            Svf(voice, Filter.Low, t => Sweep(2200, 700, t / length), 0.9);
            Shape(voice, t => Hold(t, 0.08, length, 0.45));
            Mix(target, voice, start, amplitude);
        }
    }
}
