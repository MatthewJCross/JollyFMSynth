namespace JollyFMSynth.Synthesis
{
    public enum EnvelopeCurveType { Linear, Exponential, Logarithmic, Bezier }

    public static class EnvelopeCurves
    {
        public static double Evaluate(double progress, EnvelopeCurveType curve, double tension)
        {
            double t = Math.Clamp(progress, 0, 1);
            return curve switch
            {
                EnvelopeCurveType.Exponential => t * t * t,
                EnvelopeCurveType.Logarithmic => 1 - Math.Pow(1 - t, 3),
                EnvelopeCurveType.Bezier => Bezier(t, tension),
                _ => t
            };
        }

        private static double Bezier(double t, double tension)
        {
            // Cubic Bezier with fixed endpoints and adjustable vertical handles.
            double u = 1 - t;
            double c1 = Math.Clamp(tension, 0, 1);
            double c2 = 1 - c1;
            return 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t;
        }
    }
}