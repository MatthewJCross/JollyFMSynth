using System.ComponentModel;

namespace JollyFMSynth.ViewModels
{
    public sealed class FmConnectionViewModel : INotifyPropertyChanged
    {
        private double _depth;
        public int Source { get; }
        public int Destination { get; }
        public double Depth { get => _depth; set { _depth = Math.Clamp(value, 0, 1); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Depth))); } }
        
        public FmConnectionViewModel(int source, int destination, double depth) 
        { 
            Source = source; 
            Destination = destination; 
            _depth = Math.Clamp(depth, 0, 1);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}