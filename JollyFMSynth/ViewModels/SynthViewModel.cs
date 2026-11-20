using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using JollyFMSynth.Services;
using JollyFMSynth.Audio;
using JollyFMSynth.Synthesis;

namespace JollyFMSynth.ViewModels
{
    public sealed class SynthViewModel : INotifyPropertyChanged
    {
        private readonly SynthAudioEngine _audio;
        private string _status = "Ready";
        public ObservableCollection<OperatorSettings> Operators { get; } = new();
        public LfoSettingsViewModel Lfo { get; } = new();
        public EffectsSettingsViewModel Effects { get; } = new();
        public ObservableCollection<FmConnectionViewModel> Connections { get; } = new();
        public ObservableCollection<FactoryPreset> FactoryPresets { get; } = new();
        public string Status { get => _status; set { if (_status == value) return; _status = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
        public ICommand PlayCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand LoadFactoryPresetCommand { get; }
        public event Action<SynthPatch>? PatchRequested;
        public event Action? StopRequested;
        private FactoryPreset? _selectedFactoryPreset;
        public FactoryPreset? SelectedFactoryPreset
        {
            get => _selectedFactoryPreset;
            set
            {
                if (_selectedFactoryPreset == value) return;
                _selectedFactoryPreset = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedFactoryPreset)));
                if (value != null) LoadFactoryPresetCommand.Execute(value);
            }
        }


        public SynthViewModel(SynthAudioEngine audio)
        {
            _audio = audio;
            PlayCommand = new RelayCommand(_ => NoteOn(60));
            StopCommand = new RelayCommand(_ => Stop());
            LoadFactoryPresetCommand = new RelayCommand(preset =>
            {
                if (preset is not FactoryPreset selected) return;
                try
                {
                    PatchRequested?.Invoke(PatchFileService.Load(selected.FilePath));
                    Status = $"Factory preset: {selected.Name}";
                }
                catch (Exception ex)
                {
                    Status = $"Preset failed: {ex.Message}";
                }
            });

            Lfo.PropertyChanged += (_, _) => ApplyLfo();
            Effects.PropertyChanged += (_, _) => ApplyEffects();
            for (int i = 0; i < 6; i++)
            {
                var settings = new OperatorSettings(i);
                settings.PropertyChanged += (_, e) =>
                {
                    if (SuspendOperatorUpdates)
                        return;

                    if (e.PropertyName is nameof(OperatorSettings.LfoPitch) or nameof(OperatorSettings.LfoAmplitude) or nameof(OperatorSettings.LfoFm))
                    {
                        _audio.Synth.ConfigureOperatorLfo(settings.Index, settings.LfoPitch, settings.LfoAmplitude, settings.LfoFm);
                    }
                    else if (e.PropertyName == nameof(OperatorSettings.FmDepth))
                    {
                        foreach (var cable in Connections.Where(c => c.Source == settings.Index))
                        {
                            cable.Depth = settings.FmDepth;
                            _audio.Synth.SetModulationDepth(cable.Source, cable.Destination, cable.Depth);
                        }
                    }
                    else if (e.PropertyName != nameof(OperatorSettings.OutgoingCableCount))
                    {
                        ApplyOperator(settings);
                    }
                };

                Operators.Add(settings);
                ApplyOperator(settings);
            }
        }

        public void NoteOn(int note)
        {
            _audio.Synth.NoteOn(note);
            Status = $"Playing MIDI note {note}";
        }

        public void NoteOff(int note)
        {
            _audio.Synth.NoteOff(note);
        }

        public void Stop()
        {
            _audio.Synth.AllNotesOff();
            StopRequested?.Invoke();
            Status = "Stopped";
        }

        public bool TryConnect(int source, int destination, double? depth = null)
        {
            if (!_audio.Synth.ConnectOperators(source, destination)) 
                return false;

            var cable = new FmConnectionViewModel(source, destination, depth ?? Operators[source].FmDepth);
            Connections.Add(cable);
            _audio.Synth.SetModulationDepth(source, destination, cable.Depth);
            RefreshCableCounts();
            return true;
        }

        public void Disconnect(int source, int destination)
        {
            _audio.Synth.DisconnectOperators(source, destination);
            var cable = Connections.FirstOrDefault(c => c.Source == source && c.Destination == destination);
            if (cable != null) 
                Connections.Remove(cable);

            RefreshCableCounts();
        }

        private void RefreshCableCounts()
        {
            foreach (var op in Operators) 
                op.OutgoingCableCount = Connections.Count(c => c.Source == op.Index);
        }

        public bool SuspendOperatorUpdates { get; set; }

        public void ApplyOperator(OperatorSettings settings)
        {
            _audio.Synth.ConfigureOperator(settings.Index, settings.Waveform, settings.FrequencyMode, settings.Ratio, settings.FixedFrequencyHz, settings.Detune, settings.Level);
            _audio.Synth.ConfigureEnvelope(settings.Index, settings.Attack, settings.Decay, settings.Sustain, settings.Release, settings.AttackCurveType, settings.DecayCurveType, settings.ReleaseCurveType, settings.CurveTension);
        }

        public void ApplyLfo()
        {
            _audio.Synth.ConfigureLfo(Lfo.Waveform, Lfo.Rate, Lfo.Pitch, Lfo.Amplitude);
            _audio.Synth.ConfigureLfoTiming(Lfo.Delay, Lfo.Fade);
        }

        public void ApplyEffects() => _audio.Synth.ConfigureEffects(Effects.Chorus, Effects.Delay, Effects.Reverb, Effects.Cutoff, Effects.Master, Effects.DelaySeconds, Effects.Feedback);

        public void RefreshOperators() 
        { 
            foreach (var settings in Operators) 
                ApplyOperator(settings); 
        }

        public LfoPatch CaptureLfoPatch() => new()
        {
            Waveform = Lfo.Waveform,
            Rate = Lfo.Rate,
            Pitch = Lfo.Pitch,
            Amplitude = Lfo.Amplitude,
            Delay = Lfo.Delay,
            Fade = Lfo.Fade
        };

        public EffectsPatch CaptureEffectsPatch() => new()
        {
            Chorus = Effects.Chorus,
            Delay = Effects.Delay,
            Reverb = Effects.Reverb,
            Cutoff = Effects.Cutoff,
            Master = Effects.Master,
            DelaySeconds = Effects.DelaySeconds,
            Feedback = Effects.Feedback
        };

        public void ApplyPatchSettings(LfoPatch lfo, EffectsPatch effects)
        {
            Lfo.Waveform = lfo.Waveform;
            Lfo.Rate = lfo.Rate;
            Lfo.Pitch = lfo.Pitch;
            Lfo.Amplitude = lfo.Amplitude;
            Lfo.Delay = lfo.Delay;
            Lfo.Fade = lfo.Fade;
            Effects.Chorus = effects.Chorus;
            Effects.Delay = effects.Delay;
            Effects.Reverb = effects.Reverb;
            Effects.Cutoff = effects.Cutoff;
            Effects.Master = effects.Master;
            Effects.DelaySeconds = effects.DelaySeconds;
            Effects.Feedback = effects.Feedback;
        }

        public void DiscoverFactoryPresets(string folder)
        {
            FactoryPresets.Clear();
            if (!Directory.Exists(folder)) 
            {
                Status = "Factory presets folder not found"; 
                return; 
            }

            foreach (string file in Directory.EnumerateFiles(folder, "*.jollypatch").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                int underscore = name.IndexOf('_');
                if (underscore >= 0 && int.TryParse(name[..underscore], out _)) 
                    name = name[(underscore + 1)..];

                FactoryPresets.Add(new FactoryPreset(name.Replace('_', ' '), file));
            }
        }
    }

    public sealed record FactoryPreset(string Name, string FilePath)
    {
        public override string ToString() => Name;
    }
}