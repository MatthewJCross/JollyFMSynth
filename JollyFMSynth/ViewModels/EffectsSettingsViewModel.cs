using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace JollyFMSynth.ViewModels
{
    public sealed class EffectsSettingsViewModel : INotifyPropertyChanged
    {
        private double _chorus, _delay, _reverb, _cutoff = 1, _master = 0.8, _delaySeconds = 0.32, _feedback = 0.35;
        public double Chorus { get => _chorus; set { _chorus = value; Changed(); } }
        public double Delay { get => _delay; set { _delay = value; Changed(); } }
        public double Reverb { get => _reverb; set { _reverb = value; Changed(); } }
        public double Cutoff { get => _cutoff; set { _cutoff = value; Changed(); } }
        public double Master { get => _master; set { _master = value; Changed(); } }
        public double DelaySeconds { get => _delaySeconds; set { _delaySeconds = value; Changed(); } }
        public double Feedback { get => _feedback; set { _feedback = value; Changed(); } }
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}