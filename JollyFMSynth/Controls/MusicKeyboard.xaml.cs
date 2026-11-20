using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace JollyFMSynth.Controls
{
    /// <summary>Six-octave piano with mouse glissando and reliable note-off.</summary>
    public partial class MusicKeyboard : UserControl
    {
        private const int FirstNote = 36;
        private const int LastNote = 107;
        private const double WhiteWidth = 22;
        private const double BlackWidth = 15;
        private const double WhiteHeight = 92;
        private const double BlackHeight = 58;

        private readonly Dictionary<int, Border> _keys = new();
        private readonly HashSet<int> _held = new();
        private int? _mouseNote;

        public event Action<int>? NotePressed;
        public event Action<int>? NoteReleased;

        public MusicKeyboard()
        {
            InitializeComponent();
            BuildKeyboard();
            KeysCanvas.MouseLeftButtonDown += KeysCanvas_MouseLeftButtonDown;
            KeysCanvas.MouseMove += KeysCanvas_MouseMove;
            KeysCanvas.MouseLeftButtonUp += KeysCanvas_MouseLeftButtonUp;
            KeysCanvas.MouseLeave += (_, _) => ReleaseMouseNote();
            KeysCanvas.LostMouseCapture += (_, _) => ReleaseMouseNote();
            MouseLeave += (_, _) => ReleaseMouseNote();
        }

        private static bool IsBlack(int pitch) => pitch is 1 or 3 or 6 or 8 or 10;

        private void BuildKeyboard()
        {
            double x = 0;
            for (int note = FirstNote; note <= LastNote; note++)
            {
                if (IsBlack(note % 12)) 
                    continue;

                AddKey(note, x, WhiteWidth, WhiteHeight, false);
                x += WhiteWidth;
            }

            KeysCanvas.Width = x;
            x = 0;
            for (int note = FirstNote; note <= LastNote; note++)
            {
                if (IsBlack(note % 12)) 
                    AddKey(note, x - BlackWidth / 2, BlackWidth, BlackHeight, true);
                else 
                    x += WhiteWidth;
            }
        }

        private void AddKey(int note, double x, double width, double height, bool black)
        {
            var key = new Border
            {
                Width = width,
                Height = height,
                Background = new SolidColorBrush(black ? Color.FromRgb(28, 38, 53) : Color.FromRgb(228, 235, 239)),
                BorderBrush = new SolidColorBrush(black ? Color.FromRgb(83, 104, 126) : Color.FromRgb(63, 80, 99)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(0, 0, 3, 3),
                Tag = note,
                ToolTip = $"MIDI {note}"
            };
            Canvas.SetLeft(key, x);
            Canvas.SetTop(key, 0);
            Panel.SetZIndex(key, black ? 2 : 1);
            _keys[note] = key;
            KeysCanvas.Children.Add(key);
        }

        private int? NoteAt(Point point)
        {
            if (point.X < 0 || point.X >= KeysCanvas.Width || point.Y < 0 || point.Y >= WhiteHeight)
                return null;

            // Black keys take priority wherever they overlap white keys.
            foreach (var pair in _keys)
            {
                if (!IsBlack(pair.Key % 12)) 
                    continue;

                double left = Canvas.GetLeft(pair.Value);
                if (point.X >= left && point.X < left + BlackWidth && point.Y < BlackHeight) 
                    return pair.Key;
            }

            foreach (var pair in _keys)
            {
                if (IsBlack(pair.Key % 12)) 
                    continue;

                double left = Canvas.GetLeft(pair.Value);
                if (point.X >= left && point.X < left + WhiteWidth)
                    return pair.Key;
            }
            return null;
        }

        private void KeysCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            KeysCanvas.CaptureMouse();
            ChangeMouseNote(NoteAt(e.GetPosition(KeysCanvas)));
            e.Handled = true;
        }

        private void KeysCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                ReleaseMouseNote();
                if (Mouse.Captured == KeysCanvas) KeysCanvas.ReleaseMouseCapture();
                return;
            }

            ChangeMouseNote(NoteAt(e.GetPosition(KeysCanvas)));
        }

        private void KeysCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ReleaseMouseNote();
            if (Mouse.Captured == KeysCanvas)
                KeysCanvas.ReleaseMouseCapture();

            e.Handled = true;
        }

        private void ChangeMouseNote(int? next)
        {
            if (_mouseNote == next) 
                return;

            ReleaseMouseNote();
            if (next is not int note)
                return;

            _mouseNote = note;
            Press(note);
        }

        private void ReleaseMouseNote()
        {
            if (_mouseNote is not int note) 
                return;

            _mouseNote = null;
            Release(note);
        }

        public void Press(int note)
        {
            if (!_held.Add(note)) 
                return;

            Paint(note);
            NotePressed?.Invoke(note);
        }

        public void Release(int note)
        {
            if (!_held.Remove(note))
                return;

            Paint(note);
            NoteReleased?.Invoke(note);
        }

        public void ReleaseAll()
        {
            ReleaseMouseNote();
            if (Mouse.Captured == KeysCanvas) 
                KeysCanvas.ReleaseMouseCapture();

            foreach (int note in _held.ToArray())
                Release(note);
        }

        private void Paint(int note)
        {
            if (!_keys.TryGetValue(note, out var key))
                return;

            bool black = IsBlack(note % 12);
            key.Background = new SolidColorBrush(_held.Contains(note) ? Color.FromRgb(105, 225, 207) : black ? Color.FromRgb(28, 38, 53) : Color.FromRgb(228, 235, 239));
        }
    }
}