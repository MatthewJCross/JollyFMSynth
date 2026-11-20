using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows;

namespace JollyFMSynth.Utils
{
    public class WireConnection
    {
        public Ellipse FromPort { get; }
        public Ellipse ToPort { get; }
        public Path WirePath { get; }

        public WireConnection(Ellipse from, Ellipse to, Path path)
        {
            FromPort = from;
            ToPort = to;
            WirePath = path;
        }

        public void UpdatePath()
        {
            Point p1 = GetPortCenter(FromPort);
            Point p2 = GetPortCenter(ToPort);
            WirePath.Data = CreateBezierPath(p1, p2);
        }

        public void Remove(Canvas canvas)
        {
            if (canvas.Children.Contains(WirePath))
            {
                canvas.Children.Remove(WirePath);
            }
        }

        private static Point GetPortCenter(Ellipse port)
        {
            double x = Canvas.GetLeft(port) + port.Width / 2;
            double y = Canvas.GetTop(port) + port.Height / 2;
            return new Point(x, y);
        }

        public static PathGeometry CreateBezierPath(Point start, Point end)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;

            double distance = Math.Sqrt(dx * dx + dy * dy);

            // Base droop amount, scaled by horizontal factor
            double horizontalFactor = dx * dx / (dx * dx + dy * dy + 0.01);
            double baseDroop = horizontalFactor * distance * 0.6;

            // Increase droop when going left-to-right
            double directionFactor = dx > 0 ? 1.4 : 0.8; // Tune these values to taste
            double droop = baseDroop * directionFactor;

            // Midpoint Y shifted downward
            double midY = (start.Y + end.Y) / 2 + droop;

            // Control points are symmetric in X, but droop the Y
            Point ctrl1 = new Point(start.X, midY);
            Point ctrl2 = new Point(end.X, midY);

            var figure = new PathFigure { StartPoint = start };
            figure.Segments.Add(new BezierSegment(ctrl1, ctrl2, end, true));

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            return geometry;
        }
    }
}
