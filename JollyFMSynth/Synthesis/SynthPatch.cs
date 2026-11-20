using System.Text.Json;
using System.Text.Json.Serialization;

namespace JollyFMSynth.Synthesis
{
    /// <summary>Portable, versioned snapshot of the synthesizer controls and FM routing.</summary>
    public sealed class SynthPatch
    {
        public int Version { get; set; } = 1;
        public string Name { get; set; } = "Untitled";
        public List<OperatorPatch> Operators { get; set; } = new();
        public List<CablePatch> Cables { get; set; } = new();
        public LfoPatch Lfo { get; set; } = new();
        public EffectsPatch Effects { get; set; } = new();
        public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    }

    public sealed class OperatorPatch
    {
        public Waveform Waveform { get; set; }
        public OperatorFrequencyMode FrequencyMode { get; set; }
        public double Ratio { get; set; }
        public double FixedFrequencyHz { get; set; }
        public double Detune { get; set; }
        public double Level { get; set; }
        public double Attack { get; set; }
        public double Decay { get; set; }
        public double Sustain { get; set; }
        public double Release { get; set; }
        public EnvelopeCurveType AttackCurveType { get; set; }
        public EnvelopeCurveType DecayCurveType { get; set; }
        public EnvelopeCurveType ReleaseCurveType { get; set; }
        public double CurveTension { get; set; }
        public double FmDepth { get; set; }
        public double LfoPitch { get; set; }
        public double LfoAmplitude { get; set; }
        public double LfoFm { get; set; }
    }

    public sealed class CablePatch
    {
        public int Source { get; set; }
        public int Destination { get; set; }
        public double Depth { get; set; }
    }

    public sealed class LfoPatch
    {
        public LfoWaveform Waveform { get; set; } = LfoWaveform.Sine;
        public double Rate { get; set; } = 5;
        public double Pitch { get; set; }
        public double Amplitude { get; set; }
        public double Delay { get; set; }
        public double Fade { get; set; }
    }

    public sealed class EffectsPatch
    {
        public double Chorus { get; set; }
        public double Delay { get; set; }
        public double Reverb { get; set; }
        public double Cutoff { get; set; } = 1;
        public double Master { get; set; } = 0.8;
        public double DelaySeconds { get; set; } = 0.32;
        public double Feedback { get; set; } = 0.35;
    }
}