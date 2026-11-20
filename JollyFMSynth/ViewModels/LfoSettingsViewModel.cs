using System.ComponentModel;
using System.Runtime.CompilerServices;
using JollyFMSynth.Synthesis;

namespace JollyFMSynth.ViewModels
{
    public sealed class LfoSettingsViewModel : INotifyPropertyChanged
    {
        private LfoWaveform _waveform = LfoWaveform.Sine;
        private double _rate = 5, _pitch, _amplitude, _delay, _fade;
        public LfoWaveform Waveform { get => _waveform; set { _waveform = value; Changed(); } }
        public double Rate { get => _rate; set { _rate = value; Changed(); } }
        public double Pitch { get => _pitch; set { _pitch = value; Changed(); } }
        public double Amplitude { get => _amplitude; set { _amplitude = value; Changed(); } }
        public double Delay { get => _delay; set { _delay = value; Changed(); } }
        public double Fade { get => _fade; set { _fade = value; Changed(); } }
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}