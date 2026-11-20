using JollyFMSynth.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace JollyFMSynth.Controls
{
    public partial class AdsrPanel : UserControl
    {
        private INotifyPropertyChanged? _observed;

        public AdsrPanel()
        {
            InitializeComponent();
            Unloaded += (_, _) => Detach();
            Loaded += (_, _) => Attach();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Detach();
            Attach();
        }

        private void Attach()
        {
            if (_observed != null) 
                return;

            _observed = DataContext as INotifyPropertyChanged;
            if (_observed != null) 
                _observed.PropertyChanged += OnParameterChanged;

            UpdateGraph();
        }

        private void Detach()
        {
            if (_observed != null) 
                _observed.PropertyChanged -= OnParameterChanged;

            _observed = null;
        }

        private void OnParameterChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(OperatorSettings.Attack) or nameof(OperatorSettings.Decay) or nameof(OperatorSettings.Sustain) or nameof(OperatorSettings.Release) or nameof(OperatorSettings.AttackCurveType) or nameof(OperatorSettings.DecayCurveType) or nameof(OperatorSettings.ReleaseCurveType) or nameof(OperatorSettings.CurveTension)) 
                UpdateGraph();
        }

        private void UpdateGraph()
        {
            if (EnvelopeLine == null) return;
            var settings = DataContext as OperatorSettings;
            double a = settings?.Attack ?? .01, d = settings?.Decay ?? .2;
            double s = Math.Clamp(settings?.Sustain ?? .7, 0, 1), r = settings?.Release ?? .3;

            // Scale the time-based segments to fit the preview, with a visible sustain plateau.
            double total = Math.Max(.01, a + d + r + .25);
            double x1 = 5 + 200 * a / total;
            double x2 = x1 + 200 * d / total;
            double x3 = x2 + 200 * .25 / total;
            double x4 = 205;
            var points = new PointCollection();
            var tension = settings?.CurveTension ?? .2;
            void Segment(double startX, double endX, double startY, double endY, JollyFMSynth.Synthesis.EnvelopeCurveType curve)
            {
                for (int i = 0; i <= 20; i++)
                {
                    double t = i / 20.0;
                    double v = JollyFMSynth.Synthesis.EnvelopeCurves.Evaluate(t, curve, tension);
                    points.Add(new Point(startX + (endX - startX) * t, startY + (endY - startY) * v));
                }
            }
            double sustainY = 43 - 38 * s;
            Segment(5, x1, 63, 5, settings?.AttackCurveType ?? JollyFMSynth.Synthesis.EnvelopeCurveType.Linear);
            Segment(x1, x2, 5, sustainY, settings?.DecayCurveType ?? JollyFMSynth.Synthesis.EnvelopeCurveType.Linear);
            points.Add(new Point(x3, sustainY));
            Segment(x3, x4, sustainY, 63, settings?.ReleaseCurveType ?? JollyFMSynth.Synthesis.EnvelopeCurveType.Linear);
            EnvelopeLine.Points = points;
        }
    }
}