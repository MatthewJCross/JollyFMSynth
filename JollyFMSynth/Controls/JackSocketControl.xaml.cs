using System.Windows;
using System.Windows.Controls;

namespace JollyFMSynth.Controls
{
    /// <summary>
    /// Interaction logic for JackSocketControl1.xaml
    /// </summary>
    public partial class JackSocketControl : UserControl
    {
        public static readonly DependencyProperty SocketLabelProperty = DependencyProperty.Register(nameof(SocketLabel), typeof(string), typeof(JackSocketControl), new PropertyMetadata(string.Empty));

        public string SocketLabel
        {
            get => (string)GetValue(SocketLabelProperty);
            set => SetValue(SocketLabelProperty, value);
        }

        public static readonly DependencyProperty IsPluggedInProperty = DependencyProperty.Register("IsPluggedIn", typeof(bool), typeof(JackSocketControl), new PropertyMetadata(false, OnIsPluggedInChanged));

        public bool IsPluggedIn
        {
            get => (bool)GetValue(IsPluggedInProperty);
            set => SetValue(IsPluggedInProperty, value);
        }

        private static void OnIsPluggedInChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (JackSocketControl)d;
            bool plugged = (bool)e.NewValue;
            VisualStateManager.GoToState(control, plugged ? "PluggedInVis" : "UnpluggedVis", true);
        }

        public JackSocketControl()
        {
            InitializeComponent();
            VisualStateManager.GoToState(this, "UnpluggedVis", false);
        }
    }
}
