namespace JollyFMSynth.Synthesis
{
    /// <summary>ADSR with independently selectable curves for attack, decay and release.</summary>
    public sealed class AdsrEnvelope
    {
        private enum Stage { Idle, Attack, Decay, Sustain, Release }
        private Stage _stage;
        private double _value, _elapsed, _releaseStart;
        public double Attack { get; set; } = .01;
        public double Decay { get; set; } = .2;
        public double Sustain { get; set; } = .7;
        public double Release { get; set; } = .3;
        public EnvelopeCurveType AttackCurveType { get; set; } = EnvelopeCurveType.Linear;
        public EnvelopeCurveType DecayCurveType { get; set; } = EnvelopeCurveType.Linear;
        public EnvelopeCurveType ReleaseCurveType { get; set; } = EnvelopeCurveType.Linear;
        public double CurveTension { get; set; } = .2;
        public bool IsActive => _stage != Stage.Idle;

        public void NoteOn() 
        { 
            _value = 0; 
            _elapsed = 0; 
            _stage = Stage.Attack;
        }

        public void NoteOff(int sampleRate)
        {
            if (_stage is Stage.Idle or Stage.Release) return;
            _releaseStart = _value;
            _elapsed = 0;
            _stage = Stage.Release;
        }

        public void Reset() 
        { 
            _value = 0; 
            _elapsed = 0;
            _stage = Stage.Idle; 
        }

        public double Next(int sampleRate)
        {
            if (_stage == Stage.Idle)
                return 0;

            _elapsed += 1.0 / Math.Max(1, sampleRate);
            switch (_stage)
            {
                case Stage.Attack:
                    _value = EnvelopeCurves.Evaluate(Attack <= 0 ? 1 : _elapsed / Attack, AttackCurveType, CurveTension);
                    if (_elapsed >= Attack)
                    { 
                        _value = 1;
                        _elapsed = 0; 
                        _stage = Stage.Decay; 
                    }
                    break;

                case Stage.Decay:
                    double s = Math.Clamp(Sustain, 0, 1);
                    _value = 1 - (1 - s) * EnvelopeCurves.Evaluate(Decay <= 0 ? 1 : _elapsed / Decay, DecayCurveType, CurveTension);
                    if (_elapsed >= Decay) 
                    {
                        _value = s;
                        _elapsed = 0;
                        _stage = Stage.Sustain;
                    }
                    break;

                case Stage.Sustain:
                    _value = Math.Clamp(Sustain, 0, 1);
                    break;

                case Stage.Release:
                    _value = _releaseStart * (1 - EnvelopeCurves.Evaluate(Release <= 0 ? 1 : _elapsed / Release, ReleaseCurveType, CurveTension));
                    if (_elapsed >= Release) 
                        Reset();

                    break;
            }

            return Math.Clamp(_value, 0, 1);
        }
    }
}