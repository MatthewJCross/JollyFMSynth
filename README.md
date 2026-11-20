# JollyFMSynth

**A modern six-operator FM synthesizer built with C#, WPF and NAudio.**

JollyFMSynth is a software FM synthesizer featuring a visual patch-cable interface, six configurable operators, flexible modulation routing, ADSR envelopes, polyphonic playback and built-in audio effects.

The project explores FM synthesis through an interactive interface where operators can be connected to create anything from classic electric pianos and bells to evolving pads, metallic textures and experimental sounds.

## Features

### Six-Operator FM Synthesis

- Six independently configurable FM operators.
- Multiple waveforms: Sine, Triangle, Sawtooth, Square and Noise.
- Frequency Ratio and Fixed Frequency modes.
- Fine tuning using detune in cents.
- Independent operator output levels.
- Per-operator FM modulation depth.
- Operator self-feedback.
- Eight-voice polyphony for chords and overlapping notes.

### Visual FM Patch Cables

JollyFMSynth uses an interactive patch-cable system instead of a fixed FM algorithm.

- Connect an operator's output to another operator's input.
- Create multiple modulation connections.
- Use self-feedback for more complex timbres.
- Adjust modulation intensity using the FM Depth control.
- Select and remove existing cables.
- Automatic detection and prevention of circular modulation routes between different operators.

The visual routing system makes it easy to experiment with different FM algorithms without needing to understand predefined algorithm numbers.

### ADSR Envelope Generator

Each operator has an independent envelope generator with:

- Attack
- Decay
- Sustain
- Release
- Selectable envelope curves: Linear, Exponential, Logarithmic and Bézier
- Adjustable Bézier tension
- Visual envelope preview

Envelope settings allow precise control over how individual operators evolve throughout a note.

### Global LFO

A global low-frequency oscillator provides additional movement and modulation.

- Selectable waveform
- Adjustable rate
- Pitch modulation
- Amplitude modulation
- FM modulation
- Delay and fade-in
- Independent modulation amounts for each operator

The LFO can be used for vibrato, tremolo, evolving timbres and animated FM textures.

### Built-in Audio Effects

JollyFMSynth includes an integrated effects processor featuring:

- Chorus
- Delay with feedback
- Reverb
- Low-pass filtering
- Master output level
- Soft limiting

Effects can be adjusted in real time while playing.

### Polyphonic Keyboard

The synthesizer includes an interactive six-octave piano keyboard covering C2–B7.

- Mouse-controlled notes
- Drag across keys to play successive notes
- Computer keyboard support
- Eight simultaneous voices
- Octave shifting

## Computer Keyboard Controls

| Keys | Notes |
|---|---|
| Z S X D C V G B H N J M | C4–B4 |
| Q 2 W 3 E R 5 T 6 Y 7 U | C5–B5 |
| Left Arrow | Shift down one octave |
| Right Arrow | Shift up one octave |

### Knob Controls

| Action | Function |
|---|---|
| Drag vertically | Adjust parameter |
| Shift + Drag | Fine adjustment |
| Double-click | Reset parameter |

## Factory Presets

JollyFMSynth includes 18 factory presets covering a variety of FM sounds.

| Category | Presets |
|---|---|
| Keys | Classic Electric Piano, Drawbar Organ, FM Clavinet |
| Bells | Glass Bell, Tubular Chimes, Soft Celesta |
| Percussion | FM Marimba, Crystal Pluck |
| Bass | Deep FM Bass |
| Leads | Bright Synth Lead, Feedback Growl |
| Pads | Soft Ambient Pad, Evolving Motion Pad, Warm Choir |
| Brass | Digital Brass |
| Electronic | Retro Arcade, Industrial Metallic, Sci Fi Pulse |

Presets can be selected directly from the factory preset browser.

### Custom Patches

Synthesizer configurations can be saved and loaded using the `.jollypatch` format.

Patch files store operator parameters, modulation routing, envelope settings, LFO configuration and effects settings.

This makes it possible to build and maintain a personal sound library.

## Technology

| Component | Technology |
|---|---|
| Language | C# |
| Framework | .NET 10 |
| User Interface | WPF / XAML |
| Audio | NAudio |
| Architecture | MVVM |
| Patch Format | JSON (`.jollypatch`) |
| Platform | Windows |

## Architecture

JollyFMSynth uses a Model-View-ViewModel architecture to separate the user interface from synthesizer configuration and audio processing.

The main areas of the application are:

- **Views and Controls** — WPF windows, rotary knobs, operator panels, envelope displays and keyboard.
- **ViewModels** — Observable synthesizer settings, operator configuration, FM routing, commands and preset selection.
- **Synthesis** — FM operators, polyphonic voices, envelope processing, modulation and audio generation.
- **Audio** — Audio output and effects processing.
- **Services** — Patch loading and saving, plus supporting application functionality.

Visual interactions such as patch-cable dragging remain in the WPF view layer, while synthesizer state and parameter management are handled through ViewModels.

The application is maintained as a single WPF project.

## Building the Project

### Requirements

- Windows 10 or Windows 11
- .NET 10 SDK
- Visual Studio 2022 or later with appropriate .NET 10 support
- .NET desktop development workload

### Build Instructions

1. Clone the repository.
2. Open the solution in Visual Studio.
3. Restore NuGet packages.
4. Build the solution.
5. Run JollyFMSynth.

Alternatively, build from the command line:

```bash
dotnet restore
dotnet build -c Release
dotnet run --project JollyFMSynth
```

## Getting Started

1. Launch JollyFMSynth.
2. Select a factory preset or start configuring the six operators.
3. Choose each operator's waveform, frequency and output level.
4. Connect operator outputs to inputs using the patch cables.
5. Adjust FM Depth to change the modulation intensity.
6. Shape each operator using its ADSR envelope.
7. Add movement using the LFO.
8. Adjust chorus, delay, reverb and filtering.
9. Play notes using the on-screen or computer keyboard.
10. Save your sound as a `.jollypatch` file.

## Future Development

Potential enhancements include:

- MIDI keyboard input
- Velocity-sensitive modulation
- Pitch bend and portamento
- Stereo operator panning
- Oscilloscope and spectrum analyser
- Advanced modulation matrix
- Keyboard scaling
- WAV audio export
- Patch randomisation
- DX7 SysEx patch import

## Project Goals

JollyFMSynth is designed as both a practical synthesizer and an exploration of digital FM synthesis.

The aim is to provide a flexible sound engine with a clear visual interface, making it easier to understand how operators interact and how modulation affects sound.

The project is written in C# and WPF, with an emphasis on readable code, reusable controls and maintainable architecture.

---

**JollyFMSynth — Modern FM synthesis with visual patch routing.**