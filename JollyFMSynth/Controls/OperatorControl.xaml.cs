using System.Windows.Controls;

namespace JollyFMSynth.Controls
{
    public partial class OperatorControl : UserControl
    {
        public OperatorControl() => InitializeComponent();
        public JackSocketControl InputJack => InputSocket;
        public JackSocketControl OutputJack => OutputSocket;
    }
}