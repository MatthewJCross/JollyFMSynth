using NAudio.Wave;
using JollyFMSynth.Synthesis;

namespace JollyFMSynth.Audio
{
    /// <summary>Eight independent FM voices sharing one editable patch.</summary>
    public sealed class SynthSampleProvider : ISampleProvider
    {
        public const int Polyphony = 8;
        private readonly object _sync = new();
        private readonly SynthEffects _effects = new();
        public void ConfigureEffects(double chorus, double delay, double reverb, double cutoff, double master, double time, double feedback) => _effects.Configure(chorus, delay, reverb, cutoff, master, time, feedback);
        private readonly SynthVoice[] _voices = Enumerable.Range(0, Polyphony).Select(_ => new SynthVoice()).ToArray();
        private readonly long[] _ages = new long[Polyphony];
        private long _clock;
        // Compatibility with callers inspecting the original voice's patch.
        public SynthVoice Voice => _voices[0];
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);

        public void NoteOn(int note, double velocity = 1.0)
        {
            lock (_sync)
            {
                note = Math.Clamp(note, 0, 127);
                // Repeated NoteOn for an already held pitch retriggers that voice.
                int slot = Array.FindIndex(_voices, v => v.Note == note);
                if (slot < 0) 
                    slot = Array.FindIndex(_voices, v => !v.IsSounding);

                if (slot < 0) 
                    slot = Enumerable.Range(0, Polyphony).Where(i => !_voices[i].Active).OrderBy(i => _ages[i]).DefaultIfEmpty(-1).First();

                if (slot < 0)
                    slot = Enumerable.Range(0, Polyphony).OrderBy(i => _ages[i]).First();

                _voices[slot].NoteOn(note, velocity);
                _ages[slot] = ++_clock;
            }
        }

        public void NoteOff(int note)
        {
            lock (_sync)
            foreach (var voice in _voices)
            {
                if (voice.Note == note)
                    voice.NoteOff(note);
            }
        }

        public void AllNotesOff()
        {
            lock (_sync)
            foreach (var voice in _voices)
            {
                voice.AllNotesOff();
            }
        }

        public void ConfigureOperator(int index, Waveform waveform, OperatorFrequencyMode frequencyMode, double ratio, double fixedFrequencyHz, double cents, double level)
        {
            lock (_sync)
            foreach (var voice in _voices)
            {
                var op = voice.Operators[index];
                op.Waveform = waveform;
                op.Ratio = ratio;
                op.FrequencyMode = frequencyMode;
                op.FixedFrequencyHz = fixedFrequencyHz;
                op.DetuneCents = cents;
                op.Level = level;
            }
        }

        public void ConfigureEnvelope(int index, double attack, double decay, double sustain, double release, EnvelopeCurveType attackCurve, EnvelopeCurveType decayCurve, EnvelopeCurveType releaseCurve, double tension)
        {
            lock (_sync)
            foreach (var voice in _voices)
            {
                voice.ConfigureEnvelope(index, attack, decay, sustain, release, attackCurve, decayCurve, releaseCurve, tension);
            }
        }

        public void ConfigureLfo(LfoWaveform waveform, double rate, double pitchDepth, double amplitudeDepth)
        {
            lock (_sync)
            foreach (var voice in _voices)
            {
                voice.LfoWaveform = waveform;
                voice.LfoRate = Math.Clamp(rate, 0.05, 30);
                voice.LfoPitchDepth = Math.Clamp(pitchDepth, 0, 100);
                voice.LfoAmplitudeDepth = Math.Clamp(amplitudeDepth, 0, 1);
            }
        }

        public void ConfigureLfoTiming(double delay, double fade)
        {
            lock (_sync) 
            foreach (var voice in _voices) 
            { 
                voice.LfoDelay = Math.Clamp(delay, 0, 10); 
                voice.LfoFade = Math.Clamp(fade, 0, 10); 
            }
        }

        public void ConfigureOperatorLfo(int index, double pitch, double amplitude, double fm)
        {
            if (index is < 0 or >= SynthVoice.OperatorCount) 
                return;

            lock (_sync) 
            foreach (var voice in _voices)
            {
                voice.LfoPitchAmounts[index] = Math.Clamp(pitch, 0, 1);
                voice.LfoAmplitudeAmounts[index] = Math.Clamp(amplitude, 0, 1);
                voice.LfoFmAmounts[index] = Math.Clamp(fm, 0, 1);
            }
        }

        public bool ConnectOperators(int from, int to)
        {
            lock (_sync)
            {
                // Identical routing across all voices; only first voice decides cycle validity.
                if (!Voice.TryConnect(from, to)) 
                    return false;

                for (int i = 1; i < Polyphony; i++) 
                    _voices[i].TryConnect(from, to);

                return true;
            }
        }

        public void SetModulationDepth(int from, int to, double depth)
        {
            lock (_sync) 
            foreach (var voice in _voices) 
                voice.SetModulationDepth(from, to, depth);
        }

        public void DisconnectOperators(int from, int to)
        {
            lock (_sync) 
            foreach (var voice in _voices) 
                voice.Disconnect(from, to);
        }

        public int Read(float[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public int Read(Span<float> buffer)
        {
            lock (_sync)
            {
                int i = 0;
                while (i + 1 < buffer.Length)
                {
                    double mix = 0;
                    foreach (var voice in _voices)
                    {
                        if (voice.IsSounding)
                            mix += voice.NextSample(WaveFormat.SampleRate);
                    }

                    // Gain scales with voice count; soft saturation prevents extreme clipping.
                    float sample = (float)Math.Tanh(mix * 0.55);
                    _effects.Process(sample, out float left, out float right);
                    buffer[i++] = left;
                    buffer[i++] = right;
                }

                if (i < buffer.Length)
                    buffer[i] = 0;
            }

            return buffer.Length;
        }
    }
}