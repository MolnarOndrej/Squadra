using System;
using System.IO;

namespace Squadra.EditorTools
{
    /// <summary>
    /// A small offline sound toolkit for <see cref="SoundRecipes"/>: oscillators, filtered noise, replay of
    /// recordings, envelopes, a reverb and a WAV writer. Everything works on mono <c>float[]</c> buffers at
    /// <see cref="Rate"/> and is deterministic, so rebuilding the sounds reproduces the same files.
    /// </summary>
    static class SoundSynth
    {
        public const int Rate = 44100;

        public enum Wave
        {
            Sine,
            Saw,
            Square,
        }

        public enum Filter
        {
            Low,
            Band,
            High,
        }

        /// <summary>A mono recording used as raw material, at its own sample rate.</summary>
        public readonly struct Recording
        {
            public readonly float[] Samples;
            public readonly int SampleRate;

            public Recording(float[] samples, int sampleRate)
            {
                Samples = samples;
                SampleRate = sampleRate;
            }

            public double Seconds => Samples.Length / (double)SampleRate;
        }

        // ------------------------------------------------------------------ buffers

        public static float[] Buffer(double seconds)
        {
            return new float[Math.Max(1, Samples(seconds))];
        }

        static int Samples(double seconds)
        {
            return (int)Math.Round(seconds * Rate);
        }

        /// <summary>Adds <paramref name="source"/> into <paramref name="target"/> starting at a time offset.</summary>
        public static void Mix(float[] target, float[] source, double at = 0, double gain = 1)
        {
            int offset = Samples(at);
            int count = Math.Min(source.Length, target.Length - offset);
            for (int i = Math.Max(0, -offset); i < count; i++)
                target[offset + i] += (float)(source[i] * gain);
        }

        /// <summary>Multiplies the buffer by an envelope given as a function of time in seconds.</summary>
        public static void Shape(float[] data, Func<double, double> envelope)
        {
            for (int i = 0; i < data.Length; i++)
                data[i] *= (float)envelope(i / (double)Rate);
        }

        public static float Peak(float[] data)
        {
            float peak = 0f;
            foreach (float sample in data)
                peak = Math.Max(peak, Math.Abs(sample));
            return peak;
        }

        public static void Normalize(float[] data, double peak)
        {
            float current = Peak(data);
            if (current < 1e-9f) return;

            float gain = (float)(peak / current);
            for (int i = 0; i < data.Length; i++)
                data[i] *= gain;
        }

        /// <summary>Soft saturation: adds grit and tames peaks. Higher amounts distort more.</summary>
        public static void Drive(float[] data, double amount)
        {
            double scale = 1.0 / Math.Tanh(amount);
            for (int i = 0; i < data.Length; i++)
                data[i] = (float)(Math.Tanh(data[i] * amount) * scale);
        }

        // ------------------------------------------------------------------ curves

        public static double Clamp01(double x)
        {
            return x < 0 ? 0 : x > 1 ? 1 : x;
        }

        public static double Smooth(double x)
        {
            x = Clamp01(x);
            return x * x * (3 - 2 * x);
        }

        /// <summary>Exponential glide from one frequency to another as <paramref name="x"/> goes 0 to 1.</summary>
        public static double Sweep(double from, double to, double x)
        {
            return from * Math.Pow(to / from, Clamp01(x));
        }

        /// <summary>A struck sound: a short linear attack followed by an exponential decay.</summary>
        public static double Pluck(double t, double attack, double tau)
        {
            if (t < 0) return 0;
            return t < attack ? t / attack : Math.Exp(-(t - attack) / tau);
        }

        /// <summary>A held sound: eases in over the attack, holds until <paramref name="end"/>, eases out.</summary>
        public static double Hold(double t, double attack, double end, double release)
        {
            if (t < 0 || t > end + release) return 0;
            return Smooth(t / attack) * (1 - Smooth((t - end) / release));
        }

        // ------------------------------------------------------------------ sources

        public static float[] Noise(Random rng, double seconds)
        {
            float[] data = Buffer(seconds);
            for (int i = 0; i < data.Length; i++)
                data[i] = (float)(rng.NextDouble() * 2 - 1);
            return data;
        }

        /// <summary>
        /// Adds an oscillator to the buffer. Frequency and amplitude are functions of the time since
        /// <paramref name="start"/>. Saw and square are band-limited so pitch sweeps stay clean.
        /// </summary>
        public static void Osc(float[] target, Wave wave, double start, double length,
            Func<double, double> frequency, Func<double, double> amplitude, double phase = 0)
        {
            int from = Math.Max(0, Samples(start));
            int to = Math.Min(target.Length, Samples(start + length));
            for (int i = from; i < to; i++)
            {
                double t = (i - from) / (double)Rate;
                double step = Math.Min(frequency(t), Rate * 0.45) / Rate;
                double sample;
                switch (wave)
                {
                    case Wave.Saw:
                        sample = 2 * phase - 1 - PolyBlep(phase, step);
                        break;
                    case Wave.Square:
                        sample = (phase < 0.5 ? 1 : -1) + PolyBlep(phase, step) - PolyBlep((phase + 0.5) % 1.0, step);
                        break;
                    default:
                        sample = Math.Sin(2 * Math.PI * phase);
                        break;
                }

                target[i] += (float)(sample * amplitude(t));
                phase += step;
                phase -= Math.Floor(phase);
            }
        }

        // Rounds off the discontinuity of a naive saw or square so it does not alias.
        static double PolyBlep(double phase, double step)
        {
            if (step <= 0) return 0;
            if (phase < step)
            {
                double x = phase / step;
                return x + x - x * x - 1;
            }

            if (phase > 1 - step)
            {
                double x = (phase - 1) / step;
                return x * x + x + x + 1;
            }

            return 0;
        }

        /// <summary>
        /// Replays a recording for <paramref name="length"/> seconds at a speed that may change over time
        /// (1 is the original pitch), starting <paramref name="offset"/> seconds in. Running past the end
        /// wraps to the start, which is what engine loops are made for.
        /// </summary>
        public static float[] Replay(Recording recording, double offset, double length, Func<double, double> speed)
        {
            float[] source = recording.Samples;
            float[] data = Buffer(length);
            double position = offset * recording.SampleRate;
            for (int i = 0; i < data.Length; i++)
            {
                position %= source.Length;
                int index = (int)position;
                double fraction = position - index;
                float next = source[(index + 1) % source.Length];
                data[i] = (float)(source[index] * (1 - fraction) + next * fraction);
                position += speed(i / (double)Rate) * recording.SampleRate / Rate;
            }

            return data;
        }

        /// <summary>A bell or chime: a few slightly inharmonic sine partials, higher ones dying sooner.</summary>
        public static void Bell(float[] target, double start, double frequency, double amplitude, double tau)
        {
            double[] ratios = { 1, 2, 3.01, 4.18, 5.43 };
            double[] gains = { 1, 0.42, 0.2, 0.1, 0.05 };
            for (int k = 0; k < ratios.Length; k++)
            {
                double pitch = frequency * ratios[k];
                double gain = amplitude * gains[k];
                double decay = tau / (1 + 0.6 * k);
                Osc(target, Wave.Sine, start, decay * 8, t => pitch, t => gain * Pluck(t, 0.003, decay));
            }
        }

        // ------------------------------------------------------------------ processing

        /// <summary>
        /// A two-pole state-variable filter whose cutoff may move over time. <paramref name="q"/> near 0.7
        /// is neutral; higher values ring at the cutoff. The band-pass output peaks at unity gain.
        /// </summary>
        public static void Svf(float[] data, Filter mode, Func<double, double> cutoff, double q)
        {
            double k = 1.0 / q;
            double a1 = 0, a2 = 0, a3 = 0, ic1 = 0, ic2 = 0;
            for (int i = 0; i < data.Length; i++)
            {
                if ((i & 15) == 0)
                {
                    double hertz = Math.Max(15, Math.Min(cutoff(i / (double)Rate), Rate * 0.45));
                    double g = Math.Tan(Math.PI * hertz / Rate);
                    a1 = 1.0 / (1.0 + g * (g + k));
                    a2 = g * a1;
                    a3 = g * a2;
                }

                double v0 = data[i];
                double v3 = v0 - ic2;
                double v1 = a1 * ic1 + a2 * v3;
                double v2 = ic2 + a2 * ic1 + a3 * v3;
                ic1 = 2 * v1 - ic1;
                ic2 = 2 * v2 - ic2;

                switch (mode)
                {
                    case Filter.Low:
                        data[i] = (float)v2;
                        break;
                    case Filter.Band:
                        data[i] = (float)(k * v1);
                        break;
                    default:
                        data[i] = (float)(v0 - k * v1 - v2);
                        break;
                }
            }
        }

        static readonly int[] CombLengths = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
        static readonly int[] AllpassLengths = { 556, 441, 341, 225 };

        /// <summary>
        /// Returns the dry signal plus a Freeverb-style tail, <paramref name="tail"/> seconds longer than
        /// the input. <paramref name="room"/> (0.5 to 0.95) sets the decay time and <paramref name="damp"/>
        /// how quickly the highs die. A different <paramref name="spread"/> per channel gives stereo width.
        /// </summary>
        public static float[] Reverb(float[] dry, double tail, double room, double damp, double wet, int spread = 0)
        {
            int length = dry.Length + Samples(tail);
            var sum = new float[length];
            float feedback = (float)room;
            float damping = (float)damp;

            foreach (int combLength in CombLengths)
            {
                var line = new float[combLength + spread];
                int index = 0;
                float store = 0f;
                for (int i = 0; i < length; i++)
                {
                    float input = i < dry.Length ? dry[i] * 0.015f : 0f;
                    float output = line[index];
                    store = output * (1f - damping) + store * damping;
                    line[index] = input + store * feedback;
                    if (++index == line.Length) index = 0;
                    sum[i] += output;
                }
            }

            foreach (int allpassLength in AllpassLengths)
            {
                var line = new float[allpassLength + spread];
                int index = 0;
                for (int i = 0; i < length; i++)
                {
                    float input = sum[i];
                    float delayed = line[index];
                    line[index] = input + delayed * 0.5f;
                    if (++index == line.Length) index = 0;
                    sum[i] = delayed - input;
                }
            }

            float wetGain = (float)(wet * 3.0);
            for (int i = 0; i < length; i++)
                sum[i] = sum[i] * wetGain + (i < dry.Length ? dry[i] : 0f);
            return sum;
        }

        /// <summary>
        /// Prepares channels for writing: removes DC, trims the inaudible end of the tail, fades both ends
        /// so the clip never clicks, and normalizes all channels together to the given peak.
        /// </summary>
        public static float[][] Finish(double peak, params float[][] channels)
        {
            float loudest = 0f;
            foreach (float[] channel in channels)
            {
                RemoveDc(channel);
                loudest = Math.Max(loudest, Peak(channel));
            }

            int length = channels[0].Length;
            int last = 0;
            float floor = loudest * 0.0006f; // about -64 dB below the peak
            foreach (float[] channel in channels)
            {
                for (int i = length - 1; i > last; i--)
                {
                    if (Math.Abs(channel[i]) > floor)
                    {
                        last = i;
                        break;
                    }
                }
            }

            int fadeOut = Samples(0.03);
            int fadeIn = Samples(0.002);
            int trimmed = Math.Min(length, last + fadeOut);
            float gain = loudest > 1e-9f ? (float)(peak / loudest) : 1f;

            var result = new float[channels.Length][];
            for (int c = 0; c < channels.Length; c++)
            {
                var data = new float[trimmed];
                for (int i = 0; i < trimmed; i++)
                {
                    float fade = Math.Min(1f, i / (float)fadeIn) * Math.Min(1f, (trimmed - 1 - i) / (float)fadeOut);
                    data[i] = channels[c][i] * gain * fade;
                }

                result[c] = data;
            }

            return result;
        }

        static void RemoveDc(float[] data)
        {
            // One-pole high-pass at about 20 Hz.
            double coefficient = 1.0 - 2 * Math.PI * 20.0 / Rate;
            double previousInput = 0, previousOutput = 0;
            for (int i = 0; i < data.Length; i++)
            {
                double output = data[i] - previousInput + coefficient * previousOutput;
                previousInput = data[i];
                previousOutput = output;
                data[i] = (float)output;
            }
        }

        // ------------------------------------------------------------------ output

        /// <summary>
        /// Halves the sample rate: low-passes below the new Nyquist frequency, then keeps every other
        /// sample. Halves the size of long, soft material such as a music bed. Changes the input buffer.
        /// </summary>
        public static float[] Halve(float[] data)
        {
            Svf(data, Filter.Low, t => Rate * 0.2, 0.7);
            Svf(data, Filter.Low, t => Rate * 0.2, 0.7);
            var half = new float[data.Length / 2];
            for (int i = 0; i < half.Length; i++)
                half[i] = data[i * 2];
            return half;
        }

        /// <summary>Encodes equally long channels as a 16-bit PCM WAV file.</summary>
        public static byte[] EncodeWav(float[][] channels, int rate = Rate)
        {
            int channelCount = channels.Length;
            int frames = channels[0].Length;
            int dataBytes = frames * channelCount * 2;

            using (var stream = new MemoryStream(44 + dataBytes))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataBytes);
                writer.Write(new[] { 'W', 'A', 'V', 'E' });
                writer.Write(new[] { 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1); // PCM
                writer.Write((short)channelCount);
                writer.Write(rate);
                writer.Write(rate * channelCount * 2);
                writer.Write((short)(channelCount * 2));
                writer.Write((short)16);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataBytes);

                for (int i = 0; i < frames; i++)
                {
                    for (int c = 0; c < channelCount; c++)
                    {
                        double sample = Math.Max(-1.0, Math.Min(1.0, channels[c][i]));
                        writer.Write((short)Math.Round(sample * 32767.0));
                    }
                }

                writer.Flush();
                return stream.ToArray();
            }
        }
    }
}
