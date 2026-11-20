using System.ComponentModel;
using System.Runtime.CompilerServices;
using JollyFMSynth.Synthesis;

namespace JollyFMSynth.ViewModels
{
    public sealed class OperatorSettings : INotifyPropertyChanged
    {
        public int Index { get; }
        public string Title => $"OPERATOR {Index + 1}";
        public Array Waveforms { get; } = Enum.GetValues(typeof(Waveform));
        public Array FrequencyModes { get; } = Enum.GetValues(typeof(OperatorFrequencyMode));
        private OperatorFrequencyMode _frequencyMode = OperatorFrequencyMode.Ratio;
        private double _fixedFrequencyHz = 440.0;
        public OperatorFrequencyMode FrequencyMode { get => _frequencyMode; set { _frequencyMode = value; Changed(); Changed(nameof(IsRatioMode)); Changed(nameof(IsFixedMode)); } }
        public bool IsRatioMode => FrequencyMode == OperatorFrequencyMode.Ratio;
        public bool IsFixedMode => FrequencyMode == OperatorFrequencyMode.Fixed;
        public double FixedFrequencyHz { get => _fixedFrequencyHz; set { _fixedFrequencyHz = value; Changed(); } }
        private Waveform _waveform = Waveform.Sine;
        private double _ratio = 1;
        private double _detune;
        private double _level;
        private double _attack = 0.01, _decay = 0.2, _sustain = 0.7, _release = 0.3, _curveTension = 0.2;
        private EnvelopeCurveType _attackCurveType, _decayCurveType, _releaseCurveType;
        public Array CurveTypes { get; } = Enum.GetValues(typeof(EnvelopeCurveType));
        public EnvelopeCurveType AttackCurveType { get => _attackCurveType; set { _attackCurveType = value; Changed(); } }
        public EnvelopeCurveType DecayCurveType { get => _decayCurveType; set { _decayCurveType = value; Changed(); } }
        public EnvelopeCurveType ReleaseCurveType { get => _releaseCurveType; set { _releaseCurveType = value; Changed(); } }
        public double CurveTension { get => _curveTension; set { _curveTension = value; Changed(); } }
        public OperatorSettings(int index) { Index = index; _level = index == 0 ? 0.65 : 0; }
        public Waveform Waveform { get => _waveform; set { _waveform = value; Changed(); } }
        public double Ratio { get => _ratio; set { _ratio = value; Changed(); } }
        public double Detune { get => _detune; set { _detune = value; Changed(); } }
        public double Level { get => _level; set { _level = value; Changed(); } }
        public double Attack { get => _attack; set { _attack = value; Changed(); } }
        public double Decay { get => _decay; set { _decay = value; Changed(); } }
        public double Sustain { get => _sustain; set { _sustain = value; Changed(); } }
        public double Release { get => _release; set { _release = value; Changed(); } }
        private double _fmDepth = 0.5, _lfoPitch, _lfoAmplitude, _lfoFm;
        private int _outgoingCableCount;
        public double FmDepth { get => _fmDepth; set { _fmDepth = Math.Clamp(value, 0, 1); Changed(); } }
        public double LfoPitch { get => _lfoPitch; set { _lfoPitch = Math.Clamp(value, 0, 1); Changed(); } }
        public double LfoAmplitude { get => _lfoAmplitude; set { _lfoAmplitude = Math.Clamp(value, 0, 1); Changed(); } }
        public double LfoFm { get => _lfoFm; set { _lfoFm = Math.Clamp(value, 0, 1); Changed(); } }
        public int OutgoingCableCount { get => _outgoingCableCount; set { if (_outgoingCableCount == value) return; _outgoingCableCount = value; Changed(); Changed(nameof(HasOutgoingCables)); } }
        public bool HasOutgoingCables => OutgoingCableCount > 0;
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}