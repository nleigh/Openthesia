# Openthesia Agent Documentation

This file (`agents.md`) serves as a guide for AI agents and human contributors to the Openthesia repository. It provides high-level architectural context, technology stack details, and coding conventions.

## 🌟 Project Overview
Openthesia is a customizable MIDI visualization software (built in C#) heavily inspired by Synthesia. It includes two primary modes:
1. **MIDI Playback Mode**: Visualizes and plays back MIDI files, and supports "Learning Mode" where the playback waits for the correct note input before continuing.
2. **Play Mode**: Real-time visualization of MIDI keyboard inputs as rising note blocks, with the ability to record the performance to a new MIDI file.

## 🛠 Technology Stack
- **Language**: C# (.NET 6.0)
- **Graphics/Rendering backend**: [Veldrid](https://github.com/mellinoe/veldrid)
- **UI Framework**: [ImGui.NET](https://github.com/ImGuiNET/ImGui.NET) (bindings for Dear ImGui)
- **MIDI Processing**: [DryWetMidi](https://github.com/melanchall/drywetmidi)
- **Audio engine**: [NAudio](https://github.com/naudio/NAudio)
- **Software Synthesizer**: [MeltySynth](https://github.com/sinshu/meltysynth) (supports SoundFonts `.sf2`)
- **Plugin support**: [VST.NET](https://github.com/obiwanjacobi/vst.net) (Vst2 plugins)

## 📁 Repository Structure
The project code sits mainly within the `Openthesia` directory:

- `Openthesia/Program.cs`: The entry point for the application. Sets up Veldrid, ImGui scaffolding, and manages the main application loop.
- `Openthesia/Core/`: The brain of the application.
  - `Application.cs`: Initializes and manages the state of various UI Windows throughout the lifecycle.
  - `Midi/`: Contains logic for tracking MIDI file data, MIDI recording, playing, and managing note callbacks.
  - `Plugins/`: Holds VST Audio/MIDI logic, abstracting VST chains and plugin initialization.
  - `SoundFonts/`: Handles software synthesizer rendering using MeltySynth.
  - `ScreenRecorder.cs`: Logic to record playbacks directly to video.
- `Openthesia/Ui/`: Contains all ImGui visual components.
  - `Windows/`: Classes deriving from `ImGuiWindow` defining the layouts and behaviors of various screens (e.g., `MidiPlaybackWindow`, `HomeWindow`, `SettingsWindow`).
- `Openthesia/Settings/`: Persistent data logic for Audio Drivers, SoundFont paths, and Theme data.
- `Openthesia/Enums/`: Project-wide enums and constants.

## 🖥 Application Lifecycle
1. Application boots via `Program.Main()`. 
2. A generic graphics device is instantiated through `Veldrid` (with SDL2 windowing).
3. `ImGuiController` creates the rendering loop.
4. `Openthesia.Core.Application` is created, which instantiates all derived `ImGuiWindow` classes.
5. In every frame, the SDL2 snapshot events are pumped, and the UI windows currently set as active resolve their `RenderWindow()` functions using `ImGuiNET`.
6. Based on application mode, background audio loops process MIDI/VST data asynchronously or synchronously within frames.

## 🧩 Modifying the UI
Openthesia uses **Immediate Mode GUI (ImGui)**. To add or change UI controls:
- Avoid caching GUI object state between frames unless absolutely needed. The UI is re-declared on every frame iteration.
- Navigate to `Openthesia/Ui/Windows/` and locate the targeted screen.
- Rely on `ImGui.Begin()`, `ImGui.Button()`, etc., using the Dear ImGui standard syntax provided by `ImGui.NET`.
- UI scaling uses `FontController.DSF` for dynamic scaling factor based on window sizing and user preferences. Always use dynamic scaling instead of explicit pixel units where necessary.

## 💾 Persisting Settings
When adding application-wide customizable settings:
1. Define the property in a model class inside `Settings/`.
2. Ensure serialization rules are set properly if `DataContract` / `Newtonsoft.Json` is in use.
3. Hook UI changes from `SettingsWindow` directly to the global settings instance, then instruct `ProgramData.SaveSettings()` upon app exit (which handles persistence on disk).

## 🚀 Building & Running
To build and test Openthesia from your IDE/CLI, you simply need the .NET 6 SDK. *(Note: The project specifically targets `x64` to maintain compatibility with `ScreenRecorderLib`.)*

### 1. Run / Test (Quick Testing)
Whenever you make a small change and want to see the result immediately:
```bash
dotnet run --project Openthesia/Openthesia.csproj
```
*(Running the software will generate an empty `SoundFonts` folder (if missing) alongside the executable to place `.sf2` files.)*

### 2. Build Only (Compilation Check)
If you only want to compile the project to check for build errors:
```bash
dotnet build Openthesia/Openthesia.csproj
```

### 3. Package / Publish (Release)
When compiling a packaged executable that can be distributed:

**Self-contained release (no .NET runtime required on the target machine):**
```bash
dotnet publish Openthesia/Openthesia.csproj -c Release --self-contained true -p:PublishSingleFile=true
```

**Framework-dependent release (smaller file size):**
```bash
dotnet publish Openthesia/Openthesia.csproj -c Release
```
