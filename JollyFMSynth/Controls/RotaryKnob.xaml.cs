using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace JollyFMSynth.Controls
{
    public partial class RotaryKnob : UserControl
    {
        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(RotaryKnob), new PropertyMetadata(0.0, OnRangeChanged));
        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(RotaryKnob), new PropertyMetadata(1.0, OnRangeChanged));
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(RotaryKnob), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));
        public static readonly DependencyProperty DefaultValueProperty = DependencyProperty.Register(nameof(DefaultValue), typeof(double), typeof(RotaryKnob), new PropertyMetadata(0.0));
        public static readonly DependencyProperty StepProperty = DependencyProperty.Register(nameof(Step), typeof(double), typeof(RotaryKnob), new PropertyMetadata(0.01));
        public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(nameof(Caption), typeof(string), typeof(RotaryKnob), new PropertyMetadata("", OnVisualChanged));
        public static readonly DependencyProperty ValueFormatProperty = DependencyProperty.Register(nameof(ValueFormat), typeof(string), typeof(RotaryKnob), new PropertyMetadata("F2", OnVisualChanged));

        public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
        public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
        public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
        public double DefaultValue { get => (double)GetValue(DefaultValueProperty); set => SetValue(DefaultValueProperty, value); }
        public double Step { get => (double)GetValue(StepProperty); set => SetValue(StepProperty, value); }
        public string Caption { get => (string)GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }
        public string ValueFormat { get => (string)GetValue(ValueFormatProperty); set => SetValue(ValueFormatProperty, value); }

        private Point _lastPoint;
        private bool _dragging;

        public RotaryKnob()
        {
            InitializeComponent();
            Width = 76;
            Height = 94;
            RefreshVisuals();
        }

        private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((RotaryKnob)d).RefreshVisuals();

        private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var knob = (RotaryKnob)d;
            knob.SetCurrentValue(ValueProperty, knob.Clamp(knob.Value));
            knob.RefreshVisuals();
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var knob = (RotaryKnob)d;
            double value = (double)e.NewValue;
            double clamped = knob.Clamp(value);
            if (value != clamped)
                knob.SetCurrentValue(ValueProperty, clamped);

            knob.RefreshVisuals();
        }

        private double Clamp(double value)
        {
            double min = Math.Min(Minimum, Maximum), max = Math.Max(Minimum, Maximum);
            return double.IsFinite(value) ? Math.Clamp(value, min, max) : min;
        }

        private void RefreshVisuals()
        {
            if (TrackArc is null)
                return;

            const double start = 135, sweep = 270, cx = 38, cy = 28, radius = 27;
            double range = Maximum - Minimum;
            double fraction = range > 0 ? Math.Clamp((Value - Minimum) / range, 0, 1) : 0;
            TrackArc.Data = MakeArc(cx, cy, radius, start, sweep);
            ValueArc.Data = MakeArc(cx, cy, radius, start, sweep * fraction);
            double angle = (start + sweep * fraction) * Math.PI / 180;
            Pointer.X1 = cx;
            Pointer.Y1 = cy;
            Pointer.X2 = cx + Math.Cos(angle) * 17;
            Pointer.Y2 = cy + Math.Sin(angle) * 17;
            CaptionText.Text = Caption;
            try 
            { 
                ValueText.Text = Value.ToString(ValueFormat, CultureInfo.CurrentCulture);
            }
            catch (FormatException)
            {
                ValueText.Text = Value.ToString("F2", CultureInfo.CurrentCulture);
            }
        }

        private static Geometry MakeArc(double cx, double cy, double radius, double start, double sweep)
        {
            var geometry = new StreamGeometry();
            if (sweep <= 0)
                return geometry;

            int segments = Math.Max(2, (int)Math.Ceiling(sweep / 5));
            using (var ctx = geometry.Open())
            {
                for (int i = 0; i <= segments; i++)
                {
                    double angle = (start + sweep * i / segments) * Math.PI / 180;
                    var point = new Point(cx + radius * Math.Cos(angle), cy + radius * Math.Sin(angle));
                    if (i == 0) ctx.BeginFigure(point, false, false);
                    else ctx.LineTo(point, true, false);
                }
            }

            geometry.Freeze();
            return geometry;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();
            if (e.ClickCount >= 2)
            {
                SetCurrentValue(ValueProperty, Clamp(DefaultValue));
                e.Handled = true;
                return;
            }

            _lastPoint = e.GetPosition(this);
            _dragging = true;
            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging)
                return;

            Point current = e.GetPosition(this);
            double delta = _lastPoint.Y - current.Y;
            _lastPoint = current;
            double sensitivity = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 0.001 : 0.005;
            SetCurrentValue(ValueProperty, Clamp(Value + delta * (Maximum - Minimum) * sensitivity));
            e.Handled = true;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            _dragging = false;
            if (IsMouseCaptured)
                ReleaseMouseCapture();

            e.Handled = true;
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            _dragging = false;
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            double step = Step > 0 ? Step : (Maximum - Minimum) / 100.0;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                step *= 0.1;

            SetCurrentValue(ValueProperty, Clamp(Value + Math.Sign(e.Delta) * step));
            e.Handled = true;
        }
    }
}