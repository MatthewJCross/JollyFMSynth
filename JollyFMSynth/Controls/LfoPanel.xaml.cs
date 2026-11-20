using System.Windows.Controls;
using JollyFMSynth.Synthesis;

namespace JollyFMSynth.Controls
{
    public partial class LfoPanel : UserControl
    {
        public LfoPanel()
        {
            InitializeComponent();
            WaveformPicker.ItemsSource = Enum.GetValues<LfoWaveform>();
        }
    }
}