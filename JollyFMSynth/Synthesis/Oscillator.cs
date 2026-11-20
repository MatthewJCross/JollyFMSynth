namespace JollyFMSynth.Synthesis
{
    public enum Waveform { Sine, Triangle, Sawtooth, Square, Noise }

    public sealed class Oscillator
    {
        private double _phase;
        private uint _noise = 0xA341316Cu;
        public Waveform Waveform { get; set; } = Waveform.Sine;
        public double Ratio { get; set; } = 1.0;
        public OperatorFrequencyMode FrequencyMode { get; set; } = OperatorFrequencyMode.Ratio;
        public double FixedFrequencyHz { get; set; } = 440.0;
        public double DetuneCents { get; set; }
        public double Level { get; set; }

        public void Reset() => _phase = 0;

        // modulationRadians is the sum of incoming operator signals expressed
        // as a phase offset in radians. The phase accumulator is never modified by it.
        public double Next(double fundamental, int sampleRate, double modulationRadians = 0)
        {
            double baseFrequency = FrequencyMode == OperatorFrequencyMode.Fixed ? Math.Clamp(FixedFrequencyHz, 1.0, 16000.0) : fundamental * Math.Max(0.01, Ratio);
            double frequency = Math.Clamp(baseFrequency * Math.Pow(2.0, DetuneCents / 1200.0), 0.0, sampleRate * 0.49);
            double phase = _phase + modulationRadians / (2.0 * Math.PI);
            phase -= Math.Floor(phase);
            double value = Waveform switch
            {
                Waveform.Sine => Math.Sin(2.0 * Math.PI * phase),
                Waveform.Triangle => 1.0 - 4.0 * Math.Abs(phase - 0.5),
                Waveform.Sawtooth => 2.0 * phase - 1.0,
                Waveform.Square => phase < 0.5 ? 1.0 : -1.0,
                Waveform.Noise => NextNoise(),
                _ => 0.0
            };

            _phase += frequency / sampleRate;
            _phase -= Math.Floor(_phase);
            return value * Math.Clamp(Level, 0.0, 1.0);
        }

        private double NextNoise()
        {
            _noise ^= _noise << 13;
            _noise ^= _noise >> 17;
            _noise ^= _noise << 5;
            return (_noise / (double)uint.MaxValue) * 2.0 - 1.0;
        }
    }
}