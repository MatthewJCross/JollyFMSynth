using JollyFMSynth.Audio;
using JollyFMSynth.Controls;
using JollyFMSynth.Services;
using JollyFMSynth.Synthesis;
using JollyFMSynth.ViewModels;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace JollyFMSynth;

public partial class MainWindow : Window
{
    private readonly SynthAudioEngine _audio = new();
    private readonly SynthViewModel _viewModel;
    private ObservableCollection<OperatorSettings> Settings => _viewModel.Operators;
    private bool LoadingPatch { get => _viewModel.SuspendOperatorUpdates; set => _viewModel.SuspendOperatorUpdates = value; }

    // Patch cables drive the voice modulation graph.
    private sealed class Connection
    {
        public required JackSocketControl FromPort { get; init; }
        public required JackSocketControl ToPort { get; init; }
        public required System.Windows.Shapes.Path Wire { get; init; }
        public required System.Windows.Shapes.Path HitArea { get; init; }
        public required int Source { get; init; }
        public required int Destination { get; init; }
        public double Depth { get; set; } = 0.5;
        public required System.Windows.Shapes.Ellipse FromBlob { get; init; }
        public required System.Windows.Shapes.Ellipse ToBlob { get; init; }
    }

    private readonly List<Connection> _connections = [];
    private Connection? _selectedCable;
    private readonly Dictionary<int, OperatorControl> _operatorPanels = [];
    private JackSocketControl? _startPort;
    private System.Windows.Shapes.Path? _tempWire;
    private bool _updatingConnections;
    private readonly HashSet<Key> _pressed = [];
    private readonly List<int> _heldNotes = [];
    private readonly Dictionary<Key, int> _keyboardNotes = [];
    private readonly ComputerKeyboardMapper _keyboardMapper = new();

    private int? GetKeyboardNote(Key key) => _keyboardMapper.GetNote(key);

    private void ReleaseKeyboardNote(Key key)
    {
        if (!_keyboardNotes.Remove(key, out int note))
            return;

        _pressed.Remove(key);
        _heldNotes.Remove(note);
        _viewModel.NoteOff(note);
        if (_heldNotes.Count == 0) _viewModel.Status = "Ready";
    }

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new SynthViewModel(_audio);
        DataContext = _viewModel;
        _viewModel.PatchRequested += ApplyPatch;
        _viewModel.StopRequested += ClearHeldNotes;
        PianoKeyboard.NotePressed += note =>
        {
            _heldNotes.Remove(note);
            _heldNotes.Add(note);
            _viewModel.NoteOn(note);
            _viewModel.Status = $"Playing MIDI note {note}";
        };

        PianoKeyboard.NoteReleased += note =>
        {
            _heldNotes.Remove(note);
            _viewModel.NoteOff(note);
            if (_heldNotes.Count == 0) _viewModel.Status = "Ready";
        };

        Loaded += (_, _) => { PopulateFactoryPresets(); Dispatcher.BeginInvoke(UpdateAllConnections, DispatcherPriority.Loaded); };
        LayoutUpdated += (_, _) => UpdateAllConnections();
        StateChanged += MainWindow_StateChanged;
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (this.WindowState == WindowState.Maximized)
        {
            RestoreButton.Visibility = Visibility.Visible;
            MaximizeButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            MaximizeButton.Visibility = Visibility.Visible;
            RestoreButton.Visibility = Visibility.Collapsed;
        }
    }

    private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = true;
    }

    private void CommandBinding_Executed_Minimize(object sender, ExecutedRoutedEventArgs e)
    {
        SystemCommands.MinimizeWindow(this);
    }

    private void CommandBinding_Executed_Maximize(object sender, ExecutedRoutedEventArgs e)
    {
        SystemCommands.MaximizeWindow(this);
    }

    private void CommandBinding_Executed_Restore(object sender, ExecutedRoutedEventArgs e)
    {
        SystemCommands.RestoreWindow(this);
    }

    private void CommandBinding_Executed_Close(object sender, ExecutedRoutedEventArgs e)
    {
        SystemCommands.CloseWindow(this);
    }

    private SynthPatch CapturePatch()
    {
        var patch = new SynthPatch();
        foreach (var op in Settings)
        {
            _operatorPanels.TryGetValue(op.Index, out var panel);
            patch.Operators.Add(new OperatorPatch
            {
                Waveform = op.Waveform, FrequencyMode = op.FrequencyMode, Ratio = op.Ratio,
                FixedFrequencyHz = op.FixedFrequencyHz, Detune = op.Detune, Level = op.Level,
                Attack = op.Attack, Decay = op.Decay, Sustain = op.Sustain, Release = op.Release,
                AttackCurveType = op.AttackCurveType, DecayCurveType = op.DecayCurveType,
                ReleaseCurveType = op.ReleaseCurveType, CurveTension = op.CurveTension,
                FmDepth = op.FmDepth,
                LfoPitch = op.LfoPitch, LfoAmplitude = op.LfoAmplitude, LfoFm = op.LfoFm
            });
        }
        patch.Cables = [.. _viewModel.Connections.Select(c => new CablePatch { Source = c.Source, Destination = c.Destination, Depth = c.Depth })];
        patch.Lfo = _viewModel.CaptureLfoPatch();
        patch.Effects = _viewModel.CaptureEffectsPatch();
        return patch;
    }

    private void ApplyPatch(SynthPatch patch)
    {
        if (patch.Version != 1 || patch.Operators is null || patch.Operators.Count != 6 || patch.Cables is null || patch.Lfo is null || patch.Effects is null)
            throw new InvalidDataException("Unsupported or incomplete JollyFMSynth patch.");

        if (patch.Cables.Any(c => c.Source is < 0 or > 5 || c.Destination is < 0 or > 5 || !double.IsFinite(c.Depth)))
            throw new InvalidDataException("Patch contains invalid FM connections.");

        // Validate the complete routing graph before changing the current patch.
        var edges = new bool[6, 6];
        foreach (var cable in patch.Cables)
        {
            if (edges[cable.Source, cable.Destination]) 
                throw new InvalidDataException("Duplicate FM connection.");

            edges[cable.Source, cable.Destination] = true;
        }

        bool Reaches(int node, int target, HashSet<int> visited)
        {
            if (!visited.Add(node)) 
                return false;

            for (int i = 0; i < 6; i++)
                if (node != i && edges[node, i] && (i == target || Reaches(i, target, visited)))
                    return true;

            return false;
        }
        for (int i = 0; i < 6; i++)
            for (int j = 0; j < 6; j++)
                if (i != j && edges[i, j] && Reaches(j, i, []))
                    throw new InvalidDataException("Patch contains a multi-operator FM cycle.");

        _viewModel.Stop();
        foreach (var cable in _connections.ToArray()) RemoveCable(cable);
        LoadingPatch = true;
        try
        {
            for (int i = 0; i < 6; i++)
            {
                var source = patch.Operators[i] ?? throw new InvalidDataException("Missing operator settings.");
                var op = Settings[i];
                op.Waveform = source.Waveform; op.FrequencyMode = source.FrequencyMode;
                op.Ratio = source.Ratio; op.FixedFrequencyHz = source.FixedFrequencyHz;
                op.Detune = source.Detune; op.Level = source.Level;
                op.Attack = source.Attack; op.Decay = source.Decay;
                op.Sustain = source.Sustain; op.Release = source.Release;
                op.AttackCurveType = source.AttackCurveType; op.DecayCurveType = source.DecayCurveType;
                op.ReleaseCurveType = source.ReleaseCurveType; op.CurveTension = source.CurveTension;
                op.FmDepth = source.FmDepth;
                op.LfoPitch = source.LfoPitch; op.LfoAmplitude = source.LfoAmplitude; op.LfoFm = source.LfoFm;
            }

            _viewModel.ApplyPatchSettings(patch.Lfo, patch.Effects);
        }
        finally
        { 
            LoadingPatch = false;
        }

        _viewModel.RefreshOperators();
        for (int i = 0; i < 6; i++)
            _audio.Synth.ConfigureOperatorLfo(i, Settings[i].LfoPitch, Settings[i].LfoAmplitude, Settings [i].LfoFm);

        _viewModel.ApplyLfo();
        _viewModel.ApplyEffects();

        // Restore cables directly from the saved operator indices, not by simulating mouse gestures.
        foreach (var cable in patch.Cables)
            AddSavedConnection(cable.Source, cable.Destination, cable.Depth);

        UpdateOperatorDepthDisplays();
        UpdateAllConnections();
    }

    private void AddSavedConnection(int source, int destination, double depth)
    {
        if (!_operatorPanels.TryGetValue(source, out var sourcePanel) || !_operatorPanels.TryGetValue(destination, out var destinationPanel))
            throw new InvalidOperationException("Operator controls are not loaded.");

        if (!_viewModel.TryConnect(source, destination, depth))
            throw new InvalidDataException($"Cannot connect OP{source + 1} to OP{destination + 1}.");

        var from = sourcePanel.OutputJack;
        var to = destinationPanel.InputJack;
        var geometry = CreateBezier(GetPortCenter(from), GetPortCenter(to));
        var wire = new System.Windows.Shapes.Path 
        {
            Data = geometry,
            Stroke = Brushes.Red,
            StrokeThickness = 2, 
            IsHitTestVisible = false 
        };

        var hitArea = new System.Windows.Shapes.Path 
        { 
            Data = geometry, 
            Stroke = Brushes.Transparent, 
            StrokeThickness = 14,
            Fill = Brushes.Transparent
        };

        CableCanvas.Children.Add(wire);
        CableCanvas.Children.Add(hitArea);
        var connection = new Connection 
        { 
            FromPort = from, 
            ToPort = to, 
            Source = source,
            Destination = destination,
            Wire = wire,
            HitArea = hitArea,
            FromBlob = CreateBlob(GetPortCenter(from)),
            ToBlob = CreateBlob(GetPortCenter(to)),
            Depth = Math.Clamp(depth, 0, 1) 
        };

        hitArea.MouseLeftButtonDown += (_, e) => { SelectCable(connection); e.Handled = true; };
        hitArea.MouseRightButtonDown += (_, e) => { RemoveCable(connection); e.Handled = true; };

        _connections.Add(connection);
        from.IsPluggedIn = true; 
        to.IsPluggedIn = true;
    }

    private void PopulateFactoryPresets()
    {
        _viewModel.DiscoverFactoryPresets(System.IO.Path.Combine(AppContext.BaseDirectory, "Presets"));
    }

    private void SavePatch_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "JollySynth patches (*.jollypatch)|*.jollypatch|JSON files (*.json)|*.json",
            DefaultExt = ".jollypatch", FileName = "My Patch.jollypatch" };
        if (dialog.ShowDialog(this) != true) 
            return;

        try
        {
            var patch = CapturePatch();
            PatchFileService.Save(dialog.FileName, patch);
            _viewModel.Status = $"Saved patch: {patch.Name}";
        }
        catch (Exception ex) 
        {
            MessageBox.Show(this, ex.Message, "Save patch failed", MessageBoxButton.OK, MessageBoxImage.Error); 
        }
    }

    private void LoadPatch_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "JollySynth patches (*.jollypatch)|*.jollypatch|JSON files (*.json)|*.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var patch = PatchFileService.Load(dialog.FileName);
            ApplyPatch(patch);
            _viewModel.Status = $"Loaded patch: {patch.Name}";
        }
        catch (Exception ex) 
        { 
            MessageBox.Show(this, ex.Message, "Load patch failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearHeldNotes()
    {
        _heldNotes.Clear();
        _pressed.Clear();
        _keyboardNotes.Clear();
        PianoKeyboard.ReleaseAll();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && _selectedCable != null)
        {
            RemoveSelectedCable();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Left or Key.Right)
        {
            if (e.IsRepeat) { e.Handled = true; return; }
            if (_keyboardMapper.ShiftOctave(e.Key == Key.Right ? 12 : -12))
            {
                // Release physical keys using their original notes before changing octave.
                foreach (var key in _keyboardNotes.Keys.ToArray()) ReleaseKeyboardNote(key);
            }

            _viewModel.Status = $"Keyboard: lower C{4 + _keyboardMapper.OctaveShift / 12}–B{4 + _keyboardMapper.OctaveShift / 12}, upper C{5 + _keyboardMapper.OctaveShift / 12}–B{5 + _keyboardMapper.OctaveShift / 12}";
            e.Handled = true;
            return;
        }

        int? mapped = GetKeyboardNote(e.Key);
        if (mapped is not int note || !_pressed.Add(e.Key)) 
            return;

        _keyboardNotes[e.Key] = note;
        _heldNotes.Remove(note);
        _heldNotes.Add(note);
        _viewModel.NoteOn(note);
        _viewModel.Status = $"Playing MIDI note {note}";
        e.Handled = true;
    }

    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        if (!_keyboardNotes.ContainsKey(e.Key))
            return;

        ReleaseKeyboardNote(e.Key);
        e.Handled = true;
    }

    private void OperatorControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not OperatorControl control) 
            return;

        if (control.DataContext is OperatorSettings settings)
        {
            _operatorPanels[settings.Index] = control;
            UpdateOperatorDepthDisplays();
        }
        // The sockets stay inside the reusable operator control; the window
        // owns patch-cable interaction and rendering.
        foreach (var socket in new[] { control.InputJack, control.OutputJack })
        {
            socket.MouseLeftButtonDown -= Socket_MouseLeftButtonDown;
            socket.MouseLeftButtonUp -= Socket_MouseLeftButtonUp;
            socket.MouseLeftButtonDown += Socket_MouseLeftButtonDown;
            socket.MouseLeftButtonUp += Socket_MouseLeftButtonUp;
        }
    }

    private void Socket_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not JackSocketControl port) 
            return;

        if (_startPort != null) 
            CancelConnection();

        _startPort = port;
        _tempWire = new System.Windows.Shapes.Path 
        { 
            Stroke = Brushes.Red, 
            StrokeThickness = 2, 
            IsHitTestVisible = false
        };

        CableCanvas.Children.Add(_tempWire);
        CaptureMouse();
        MouseMove += MainWindow_MouseMove;
        MouseLeftButtonUp += MainWindow_MouseLeftButtonUp;
        e.Handled = true;
    }

    private void Socket_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_startPort != null && sender is JackSocketControl port)
        {
            CompleteConnection(port);
            e.Handled = true;
        }
    }

    private void MainWindow_MouseMove(object sender, MouseEventArgs e)
    {
        if (_startPort != null && _tempWire != null)
            _tempWire.Data = CreateBezier(GetPortCenter(_startPort), e.GetPosition(CableCanvas));
    }

    private void MainWindow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_startPort == null) 
            return;

        var point = e.GetPosition(CableCanvas);
        var target = FindVisualChildren<JackSocketControl>(OperatorItems).Where(p => p.IsVisible).OrderBy(p => (GetPortCenter(p) - point).Length).FirstOrDefault(p => (GetPortCenter(p) - point).Length < 24);
        if (target != null) 
            CompleteConnection(target);
        else
            CancelConnection();

        e.Handled = true;
    }

    private void CompleteConnection(JackSocketControl endPort)
    {
        if (_startPort == null || _tempWire == null)
            return;

        var start = _startPort;
        JackSocketControl? from = start.Name == "OutputSocket" ? start : endPort.Name == "OutputSocket" ? endPort : null;
        JackSocketControl? to = start.Name == "InputSocket" ? start : endPort.Name == "InputSocket" ? endPort : null;
        if (from != null && to != null && from != to && from.Tag is int a && to.Tag is int b && !_connections.Any(c => c.FromPort == from && c.ToPort == to) && _viewModel.TryConnect(a, b))
        {
            var p1 = GetPortCenter(from);
            var p2 = GetPortCenter(to);
            _tempWire.Data = CreateBezier(p1, p2);
            _tempWire.Stroke = Brushes.Red;
            var hitArea = new System.Windows.Shapes.Path
            {
                Data = _tempWire.Data, Stroke = Brushes.Transparent, StrokeThickness = 16,
                Cursor = Cursors.Hand, Fill = null
            };

            CableCanvas.Children.Add(hitArea);
            var fromBlob = CreateBlob(p1);
            var toBlob = CreateBlob(p2);
            var connection = new Connection 
            { 
                FromPort = from, 
                ToPort = to, 
                Wire = _tempWire,
                HitArea = hitArea,
                Source = a,
                Destination = b,
                FromBlob = fromBlob, 
                ToBlob = toBlob 
            };

            hitArea.MouseLeftButtonDown += (_, args) =>
            {
                SelectCable(connection);
                args.Handled = true;
            };

            hitArea.MouseRightButtonDown += (_, args) =>
            {
                RemoveCable(connection);
                args.Handled = true;
            };

            connection.Depth = Settings[a].FmDepth;
            _connections.Add(connection);
            UpdateOperatorDepthDisplays();
            from.IsPluggedIn = true;
            to.IsPluggedIn = true;
            _viewModel.Status = $"FM: OP{a + 1} modulates OP{b + 1} — {_connections.Count} cable(s)";
            _tempWire = null;
        }
        else if (from != null && to != null && from.Tag is int source && to.Tag is int destination )
            _viewModel.Status = "Connection rejected (duplicate or multi-operator modulation cycle).";

        CancelConnection();
    }

    private void SelectCable(Connection connection)
    {
        if (_selectedCable != null)
        {
            _selectedCable.Wire.Stroke = Brushes.Red;
            _selectedCable.Wire.StrokeThickness = 2;
        }

        _selectedCable = connection;
        connection.Wire.Stroke = Brushes.Orange;
        connection.Wire.StrokeThickness = 4;
        UpdateOperatorDepthDisplays();
        _viewModel.Status = $"Selected OP{connection.Source + 1} → OP{connection.Destination + 1} · Delete to remove";
    }

    private static void UpdateOperatorDepthDisplays()
    {
        // Operator knob enablement is bound to OperatorSettings.HasOutgoingCables.
    }

    private void RemoveSelectedCable()
    {
        if (_selectedCable != null) 
            RemoveCable(_selectedCable);
    }

    private void RemoveCable(Connection connection)
    {
        _viewModel.Disconnect(connection.Source, connection.Destination);
        CableCanvas.Children.Remove(connection.Wire);
        CableCanvas.Children.Remove(connection.HitArea);
        CableCanvas.Children.Remove(connection.FromBlob);
        CableCanvas.Children.Remove(connection.ToBlob);
        _connections.Remove(connection);
        connection.FromPort.IsPluggedIn = _connections.Any(c => c.FromPort == connection.FromPort || c.ToPort == connection.FromPort);
        connection.ToPort.IsPluggedIn = _connections.Any(c => c.FromPort == connection.ToPort || c.ToPort == connection.ToPort);
        if (_selectedCable == connection)
            _selectedCable = null;

        UpdateOperatorDepthDisplays();
        _viewModel.Status = $"Removed OP{connection.Source + 1} → OP{connection.Destination + 1}";
    }

    private Ellipse CreateBlob(Point point)
    {
        var blob = new Ellipse 
        { 
            Width = 8,
            Height = 8,
            Fill = Brushes.Lime,
            Stroke = Brushes.Black,
            StrokeThickness = 1,
            IsHitTestVisible = false 
        };

        Canvas.SetLeft(blob, point.X - 4);
        Canvas.SetTop(blob, point.Y - 4);
        CableCanvas.Children.Add(blob);
        return blob;
    }

    private void CancelConnection()
    {
        if (_tempWire != null) 
            CableCanvas.Children.Remove(_tempWire);

        _tempWire = null;
        _startPort = null;
        MouseMove -= MainWindow_MouseMove;
        MouseLeftButtonUp -= MainWindow_MouseLeftButtonUp;
        if (IsMouseCaptured) 
            ReleaseMouseCapture();
    }

    private Point GetPortCenter(UIElement port)
    {
        var element = (FrameworkElement)port;
        return element.TranslatePoint(new Point(element.ActualWidth / 2, 10), CableCanvas);
    }

    // Original MainWindow.CreateBezier mathematics from the uploaded project.
    public static PathGeometry CreateBezier(Point start, Point end)
    {
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);
        double horizontalFactor = dx * dx / (dx * dx + dy * dy + 0.01);
        double baseDroop = horizontalFactor * distance * 0.1;
        double directionFactor = dx > 0 ? 1.4 : 0.8;
        double droop = baseDroop * directionFactor;
        double midY = (start.Y + end.Y) / 2 + droop;
        Point ctrl1 = new(start.X, midY);
        Point ctrl2 = new(end.X, midY);
        var figure = new PathFigure { StartPoint = start };
        figure.Segments.Add(new BezierSegment(ctrl1, ctrl2, end, true));
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    private void UpdateAllConnections()
    {
        if (_updatingConnections || CableCanvas == null) 
            return;

        _updatingConnections = true;
        try
        {
            foreach (var c in _connections)
            {
                var p1 = GetPortCenter(c.FromPort);
                var p2 = GetPortCenter(c.ToPort);
                c.Wire.Data = CreateBezier(p1, p2);
                c.HitArea.Data = c.Wire.Data;
                Canvas.SetLeft(c.FromBlob, p1.X - 4);
                Canvas.SetTop(c.FromBlob, p1.Y - 4);
                Canvas.SetLeft(c.ToBlob, p2.X - 4);
                Canvas.SetTop(c.ToBlob, p2.Y - 4);
            }
        }
        finally 
        { 
            _updatingConnections = false;
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) 
                yield return match;

            foreach (var nested in FindVisualChildren<T>(child))
                yield return nested;
        }
    }

    private void OperatorScroll_ScrollChanged(object sender, ScrollChangedEventArgs e) => UpdateAllConnections();
    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (IsLoaded) 
            Dispatcher.BeginInvoke(UpdateAllConnections, DispatcherPriority.Loaded);
    }

    private void Window_Closed(object? sender, EventArgs e) => _audio.Dispose();
}

