using Openthesia.Core.Midi;
using Openthesia.Ui.Windows;
using ImGuiNET;

namespace Openthesia.Core;

public class Application
{
    public static Application AppInstance;
    protected bool _isRunning = true;
    protected List<ImGuiWindow> _imguiWindows = new();

    public Application()
    {
        AppInstance = this;
        Init();
    }

    private void Init()
    {
        CreateWindows();
    }

    private void CreateWindows()
    {
        HomeWindow homeWindow = new();
        MidiBrowserWindow midiBrowserWindow = new();
        ModeSelectionWindow modeSelectionWindow = new();
        MidiPlaybackWindow midiPlaybackWindow = new();
        PlayModeWindow playModeWindow = new();
        SettingsWindow settingsWindow = new();
        _imguiWindows.Add(homeWindow);
        _imguiWindows.Add(midiBrowserWindow);
        _imguiWindows.Add(modeSelectionWindow);
        _imguiWindows.Add(midiPlaybackWindow);
        _imguiWindows.Add(playModeWindow);
        _imguiWindows.Add(settingsWindow);
    }

    public List<ImGuiWindow> GetWindows()
    {
        return _imguiWindows;
    }

    public void OnUpdate()
    {
        if (MidiPlayer.ShouldAdvanceQueue)
        {
            MidiPlayer.ShouldAdvanceQueue = false;
            string nextFile = SongQueueManager.GetNext();
            if (nextFile != null)
            {
                MidiFileHandler.LoadMidiFile(nextFile);
                MidiPlayer.Timer = 0;
                MidiPlayer.Seconds = 0;
                MidiPlayer.Playback.Start();
                MidiPlayer.StartTimer();
            }
        }

        foreach (ImGuiWindow window in GetWindows())
        {
            if (window.Active())
                window.RenderWindow();
        }

        // Render global theme toggle
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(ImGui.GetIO().DisplaySize.X - 60, 10));
        ImGui.PushStyleColor(ImGuiCol.WindowBg, System.Numerics.Vector4.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        if (ImGui.Begin("##GlobalThemeToggle", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.PushFont(FontController.Font16_Icon16);
            string icon = Settings.ThemeManager.Theme == Enums.Themes.Light ? IconFonts.FontAwesome6.Moon : IconFonts.FontAwesome6.Sun;
            if (ImGui.Button(icon, new System.Numerics.Vector2(40, 40)))
            {
                if (Settings.ThemeManager.Theme == Enums.Themes.Light)
                    Settings.ThemeManager.SetTheme(Enums.Themes.Sky);
                else
                    Settings.ThemeManager.SetTheme(Enums.Themes.Light);
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Toggle Light/Dark Theme");
            ImGui.PopFont();
            ImGui.End();
        }
        ImGui.PopStyleVar();
        ImGui.PopStyleColor();
        ImGuiController.UpdateMouseCursor();
    }

    public bool IsRunning()
    {
        return _isRunning;
    }

    public void Quit()
    {
        MidiPlayer.SoundFontEngine?.Dispose();
        _isRunning = false;
    }
}
