using NAudio.Wave;

namespace JollyFMSynth.Audio
{
    public sealed class SynthAudioEngine : IDisposable
    {
        private readonly WaveOut _output;
        public SynthSampleProvider Synth { get; } = new();

        public SynthAudioEngine()
        {
            _output = new WaveOut();
            _output.Init(Synth);
            _output.Play();
        }

        public void Dispose()
        {
            Synth.AllNotesOff();
            _output.Stop();
            _output.Dispose();
        }
    }
}