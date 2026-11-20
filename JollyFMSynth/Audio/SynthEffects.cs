namespace JollyFMSynth.Audio
{
    /// <summary>Allocation-free stereo effects chain. Called under the provider audio lock.</summary>
    public sealed class SynthEffects
    {
        private const int SampleRate = 48000;
        private readonly float[] _delayL = new float[SampleRate * 2], _delayR = new float[SampleRate * 2];
        private readonly float[] _chorusL = new float[4096], _chorusR = new float[4096];
        private readonly Comb[] _reverbL = [new(1557), new(1617), new(1491), new(1422)];
        private readonly Comb[] _reverbR = [new(1580), new(1640), new(1514), new(1445)];
        private int _delayPos;
        private int _chorusPos;
        private double _chorusPhase;
        private double _filterL;
        private double _filterR;
        private double _chorus;
        private double _delay;
        private double _reverb;
        private double _cutoff = 1;
        private double _master = 0.8;
        private double _delaySeconds = 0.32;
        private double _feedback = 0.35;

        public void Configure(double chorus, double delay, double reverb, double cutoff, double master, double delaySeconds, double feedback)
        {
            _chorus = Math.Clamp(chorus, 0, 1);
            _delay = Math.Clamp(delay, 0, 1);
            _reverb = Math.Clamp(reverb, 0, 1);
            _cutoff = Math.Clamp(cutoff, 0, 1);
            _master = Math.Clamp(master, 0, 1);
            _delaySeconds = Math.Clamp(delaySeconds, 0.05, 1.5);
            _feedback = Math.Clamp(feedback, 0, 0.85);
        }

        public void Process(float input, out float left, out float right)
        {
            double l = input;
            double r = input;

            // One-pole low-pass, 150 Hz to 20 kHz, with bypass at maximum cutoff.
            double cutoff = _cutoff;
            if (cutoff < 0.999)
            {
                double hz = 150 * Math.Pow(20000.0 / 150, cutoff);
                double a = 1 - Math.Exp(-2 * Math.PI * hz / SampleRate);
                _filterL += a * (l - _filterL); _filterR += a * (r - _filterR);
                l = _filterL; 
                r = _filterR;
            }
            else 
            { 
                _filterL = l; 
                _filterR = r; 
            }

            // Stereo chorus: short modulated fractional delays with different phase offsets.
            int cp = _chorusPos;
            _chorusL[cp] = (float)l; 
            _chorusR[cp] = (float)r;
            if (_chorus > 0)
            {
                double offsetL = 650 + 260 * Math.Sin(_chorusPhase);
                double offsetR = 650 + 260 * Math.Sin(_chorusPhase + Math.PI * 0.5);
                double wetL = ReadFractional(_chorusL, cp - offsetL);
                double wetR = ReadFractional(_chorusR, cp - offsetR);
                l += _chorus * 0.5 * wetL; 
                r += _chorus * 0.5 * wetR;
            }

            _chorusPos = (cp + 1) % _chorusL.Length;
            _chorusPhase += 2 * Math.PI * 0.65 / SampleRate;
            if (_chorusPhase >= 2 * Math.PI) 
                _chorusPhase -= 2 * Math.PI;

            // Feedback delay remains running when wet amount is zero, allowing smooth changes.
            int dp = _delayPos, length = _delayL.Length;
            int read = (dp - Math.Clamp((int)(_delaySeconds * SampleRate), 1, length - 1) + length) % length;
            double dl = _delayL[read], dr = _delayR[read];
            _delayL[dp] = (float)Math.Clamp(l + dl * _feedback, -4, 4);
            _delayR[dp] = (float)Math.Clamp(r + dr * _feedback, -4, 4);
            _delayPos = (dp + 1) % length;
            l += _delay * dl;
            r += _delay * dr;

            // Four parallel damped feedback combs per channel: lightweight diffuse reverb.
            double rvL = 0, rvR = 0;
            for (int i = 0; i < 4; i++) 
            { 
                rvL += _reverbL[i].Process(l); 
                rvR += _reverbR[i].Process(r); 
            }

            l += _reverb * rvL * 0.23; 
            r += _reverb * rvR * 0.23;

            // Final bounded output prevents clipping at high wet levels.
            left = (float)Math.Tanh(l * _master);
            right = (float)Math.Tanh(r * _master);
        }

        private static double ReadFractional(float[] buffer, double position)
        {
            int n = buffer.Length;
            position %= n; if (position < 0) position += n;
            int i = (int)position; double t = position - i;
            return buffer[i] * (1 - t) + buffer[(i + 1) % n] * t;
        }

        private sealed class Comb
        {
            private readonly float[] _buffer;
            private int _position;
            private double _damped;
            public Comb(int length) => _buffer = new float[length];

            public double Process(double input)
            {
                double output = _buffer[_position];
                _damped = output * 0.65 + _damped * 0.35;
                _buffer[_position] = (float)Math.Clamp(input + _damped * 0.73, -4, 4);
                if (++_position == _buffer.Length) 
                    _position = 0;

                return output;
            }
        }
    }
}