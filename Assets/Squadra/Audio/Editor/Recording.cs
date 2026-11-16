namespace Squadra.EditorTools
{
    /// <summary>A mono recording used as raw material, at its own sample rate.</summary>
    readonly struct Recording
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
}
