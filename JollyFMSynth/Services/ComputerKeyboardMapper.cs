using System.Windows.Input;

namespace JollyFMSynth.Services
{
    public sealed class ComputerKeyboardMapper
    {
        private static readonly Key[] LowerKeys = [Key.Z, Key.S, Key.X, Key.D, Key.C, Key.V, Key.G, Key.B, Key.H, Key.N, Key.J, Key.M];
        private static readonly Key[] UpperKeys = [Key.Q, Key.D2, Key.W, Key.D3, Key.E, Key.R, Key.D5, Key.T, Key.D6, Key.Y, Key.D7, Key.U];
        public int OctaveShift { get; private set; }

        public int? GetNote(Key key)
        {
            int lower = Array.IndexOf(LowerKeys, key);
            if (lower >= 0) 
                return 60 + OctaveShift + lower;

            int upper = Array.IndexOf(UpperKeys, key);
            return upper >= 0 ? 72 + OctaveShift + upper : null;
        }

        public bool ShiftOctave(int semitones)
        {
            int next = Math.Clamp(OctaveShift + semitones, -36, 24);
            if (next == OctaveShift) 
                return false;

            OctaveShift = next;
            return true;
        }
    }
}