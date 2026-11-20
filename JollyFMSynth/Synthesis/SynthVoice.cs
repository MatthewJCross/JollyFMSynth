namespace JollyFMSynth.Synthesis
{
    public sealed class SynthVoice
    {
        public const int OperatorCount = 6;
        // A single operator at level 1 can produce up to 2*pi radians of phase modulation.
        public const double ModulationScale = 2.0 * Math.PI;
        private readonly AdsrEnvelope[] _envelopes = Enumerable.Range(0, OperatorCount).Select(_ => new AdsrEnvelope()).ToArray();
        private readonly double[,] _routes = new double[OperatorCount, OperatorCount];
        private readonly int[] _order = new int[OperatorCount];
        private readonly bool[] _hasOutgoing = new bool[OperatorCount];
        private readonly double[] _samples = new double[OperatorCount];
        // One-sample delayed, smoothed feedback per operator. State is private to each voice.
        private readonly double[] _feedbackHistory = new double[OperatorCount];
        private readonly double[] _feedbackSmoothed = new double[OperatorCount];
        private readonly double[] _feedbackGain = new double[OperatorCount];
        private const double MaxFeedbackRadians = 4.0 * Math.PI;
        private const double FeedbackSmoothingSeconds = 0.0015;

        public Oscillator[] Operators { get; } = Enumerable.Range(0, OperatorCount).Select(_ => new Oscillator()).ToArray();
        private double _lfoPhase;
        public LfoWaveform LfoWaveform { get; set; } = LfoWaveform.Sine;
        public double LfoRate { get; set; } = 5;
        public double LfoPitchDepth { get; set; } // cents, up to 100
        public double LfoAmplitudeDepth { get; set; } // 0..1
        public double LfoDelay { get; set; }
        public double LfoFade { get; set; }
        public double[] LfoPitchAmounts { get; } = new double[OperatorCount];
        public double[] LfoAmplitudeAmounts { get; } = new double[OperatorCount];
        public double[] LfoFmAmounts { get; } = new double[OperatorCount];
        private double _lfoElapsed;
        public int? Note { get; private set; }
        private int _lastNote = 60;
        public double Velocity { get; private set; }
        public bool Active => Note.HasValue;
        public bool IsSounding => Active || _envelopes.Any(e => e.IsActive);

        public SynthVoice()
        {
            Operators[0].Level = 0.65;
            RebuildOrder();
        }

        public void ConfigureEnvelope(int index, double attack, double decay, double sustain, double release, EnvelopeCurveType attackCurve, EnvelopeCurveType decayCurve, EnvelopeCurveType releaseCurve, double tension)
        {
            if (index is < 0 or >= OperatorCount) return;
            var env = _envelopes[index];
            env.Attack = Math.Clamp(attack, 0, 10);
            env.Decay = Math.Clamp(decay, 0, 10);
            env.Sustain = Math.Clamp(sustain, 0, 1);
            env.Release = Math.Clamp(release, 0, 10);
            env.AttackCurveType = attackCurve;
            env.DecayCurveType = decayCurve;
            env.ReleaseCurveType = releaseCurve;
            env.CurveTension = Math.Clamp(tension, 0, 1);
        }

        public bool HasConnection(int from, int to) => _routes[from, to] > 0;

        public void SetModulationDepth(int from, int to, double depth)
        {
            if (from is < 0 or >= OperatorCount || to is < 0 or >= OperatorCount)
                return;

            if (_routes[from, to] > 0)
            {
                _routes[from, to] = Math.Clamp(depth, 0.000001, 1.0);
                if (from == to)
                    _feedbackGain[from] = _routes[from, to];
            }
        }

        public bool TryConnect(int from, int to)
        {
            if (from is < 0 or >= OperatorCount || to is < 0 or >= OperatorCount)
                return false;

            if (_routes[from, to] > 0) 
                return true;

            _routes[from, to] = 0.5;
            if (from == to)
                _feedbackGain[from] = 0.5;

            if (RebuildOrder()) 
                return true;

            _routes[from, to] = 0;
            if (from == to)
            {
                _feedbackGain[from] = 0;
                _feedbackHistory[from] = 0;
                _feedbackSmoothed[from] = 0;
            }

            RebuildOrder();
            return false;
        }

        public void Disconnect(int from, int to)
        {
            if (from is < 0 or >= OperatorCount || to is < 0 or >= OperatorCount)
                return;

            _routes[from, to] = 0;
            if (from == to)
            {
                _feedbackGain[from] = 0;
                _feedbackHistory[from] = 0;
                _feedbackSmoothed[from] = 0;
            }

            RebuildOrder();
        }

        // Kahn topological ordering: modulators are evaluated before their destinations.
        private bool RebuildOrder()
        {
            Span<int> indegree = stackalloc int[OperatorCount];
            Span<bool> visited = stackalloc bool[OperatorCount];
            Array.Clear(_hasOutgoing);
            for (int from = 0; from < OperatorCount; from++)
                for (int to = 0; to < OperatorCount; to++)
                    if (_routes[from, to] > 0 && from != to) { indegree[to]++; _hasOutgoing[from] = true; }

            for (int pos = 0; pos < OperatorCount; pos++)
            {
                int next = -1;
                for (int i = 0; i < OperatorCount; i++)
                    if (!visited[i] && indegree[i] == 0) { next = i; break; }
                if (next == -1) 
                    return false;

                visited[next] = true;
                _order[pos] = next;
                for (int to = 0; to < OperatorCount; to++)
                    if (next != to && _routes[next, to] > 0) indegree[to]--;
            }
            return true;
        }

        public void NoteOn(int note, double velocity = 1.0)
        {
            Note = Math.Clamp(note, 0, 127);
            _lastNote = Note.Value;
            Velocity = Math.Clamp(velocity, 0, 1);
            _lfoPhase = 0;
            _lfoElapsed = 0;
            foreach (var op in Operators)
                op.Reset();

            foreach (var env in _envelopes) 
                env.NoteOn();

            Array.Clear(_samples);
            Array.Clear(_feedbackHistory);
            Array.Clear(_feedbackSmoothed);
        }

        public void NoteOff(int note)
        {
            if (Note == note)
            {
                Note = null;
                foreach (var env in _envelopes) 
                    env.NoteOff(48000);
            }
        }

        public void AllNotesOff() 
        { 
            Note = null; 
            Velocity = 0; 
            Array.Clear(_samples);
            Array.Clear(_feedbackHistory); 
            Array.Clear(_feedbackSmoothed); 
            foreach (var env in _envelopes) 
                env.Reset(); 
        }

        public double NextSample(int sampleRate)
        {
            if (Note is null && !_envelopes.Any(e => e.IsActive)) 
                return 0;

            double lfo = LfoWaveform switch
            {
                LfoWaveform.Triangle => 1.0 - 4.0 * Math.Abs(_lfoPhase - 0.5),
                LfoWaveform.Square => _lfoPhase < 0.5 ? 1.0 : -1.0,
                LfoWaveform.Sawtooth => 2.0 * _lfoPhase - 1.0,
                _ => Math.Sin(2.0 * Math.PI * _lfoPhase)
            };

            _lfoPhase += Math.Clamp(LfoRate, 0.05, 30.0) / sampleRate;
            _lfoPhase -= Math.Floor(_lfoPhase);
            _lfoElapsed += 1.0 / sampleRate;
            double fade = LfoFade <= 0 ? 1 : Math.Clamp((_lfoElapsed - LfoDelay) / LfoFade, 0, 1);
            double effectiveLfo = _lfoElapsed < LfoDelay ? 0 : lfo * fade;
            double hz = 440.0 * Math.Pow(2.0, (_lastNote - 69) / 12.0);
            // Sample-rate-independent smoothing prevents abrupt feedback changes from clicking.
            double feedbackAlpha = 1.0 - Math.Exp(-1.0 / (Math.Max(1, sampleRate) * FeedbackSmoothingSeconds));
            for (int pos = 0; pos < OperatorCount; pos++)
            {
                int op = _order[pos];
                double pitchCents = effectiveLfo * LfoPitchDepth * LfoPitchAmounts[op];
                double operatorHz = hz * Math.Pow(2.0, pitchCents / 1200.0);
                double modulation = 0;
                for (int from = 0; from < OperatorCount; from++)
                    if (from != op && _routes[from, op] > 0)
                        modulation += _samples[from] * _routes[from, op] * ModulationScale * Math.Clamp(1 + effectiveLfo * LfoFmAmounts[from], 0, 2);

                // Feedback is strictly delayed by one sample (no recursive evaluation).
                // Smooth the gain, then saturate the delayed signal to keep phase excursions bounded.
                double targetGain = _routes[op, op] > 0 ? Math.Clamp(_routes[op, op] * (1 + effectiveLfo * LfoFmAmounts[op]), 0, 1) : 0;
                _feedbackGain[op] += (targetGain - _feedbackGain[op]) * feedbackAlpha;
                double feedback = Math.Tanh(_feedbackHistory[op] * _feedbackGain[op] * 2.0) * MaxFeedbackRadians;
                double amplitude = 1 - Math.Clamp(LfoAmplitudeDepth * LfoAmplitudeAmounts[op], 0, 1) * (1 - effectiveLfo) * 0.5;
                double output = Operators[op].Next(operatorHz, sampleRate, modulation + feedback) * _envelopes[op].Next(sampleRate) * amplitude;
                _samples[op] = double.IsFinite(output) ? output : 0;
                // Low-pass the delayed feedback path to reduce high-frequency harshness/aliasing.
                _feedbackSmoothed[op] += (_samples[op] - _feedbackSmoothed[op]) * 0.65;
                _feedbackHistory[op] = Math.Clamp(_feedbackSmoothed[op], -1.0, 1.0);
            }

            double mix = 0;
            int carriers = 0;
            for (int i = 0; i < OperatorCount; i++)
            {
                if (!_hasOutgoing[i])
                {
                    mix += _samples[i];
                    carriers++;
                }
            }

            return Math.Tanh(mix * Velocity * 0.6 / Math.Sqrt(Math.Max(1, carriers)));
        }
    }
}