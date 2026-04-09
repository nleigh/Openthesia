using ImGuiNET;
using Openthesia.Core;
using Openthesia.Core.Midi;
using Openthesia.Settings;
using Openthesia.Ui.Helpers;
using System.Numerics;

namespace Openthesia.Ui.Windows;

public class MidiPlaybackWindow : ImGuiWindow
{
    public MidiPlaybackWindow()
    {
        _id = Enums.Windows.MidiPlayback.ToString();
        _active = false;
    }

    protected override void OnImGui()
    {
        Vector2 canvasSize = new(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y * 75 / 100);
        if (ImGui.BeginChild("Screen", canvasSize))
        {
            ScreenCanvas.RenderCanvas();
            ImGui.EndChild();
        }

        Vector2 lineStart = new(0, ImGui.GetCursorPos().Y);
        Vector2 lineEnd = new(ImGui.GetContentRegionAvail().X, ImGui.GetCursorPos().Y);
        uint lineColor = ImGui.GetColorU32(new Vector4(0.529f, 0.784f, 0.325f, 1f));
        const float lineThickness = 2f;
        ImGui.GetForegroundDrawList().AddLine(lineStart, lineEnd, lineColor, lineThickness);

        if (ImGui.BeginChild("Keyboard", ImGui.GetContentRegionAvail()))
        {
            if (ScreenCanvasControls.IsLearningMode && !MidiPlayer.IsTimerRunning && AccuracyScoring.TotalNotes > 0 && !AccuracyScoring.IsTracking)
            {
                RenderScoringHeatmap();
            }
            else
            {
                PianoRenderer.RenderKeyboard();
            }
            ImGui.EndChild();
        }
    }

    private void RenderScoringHeatmap()
    {
        var attempts = AccuracyScoring.GetAttempts().ToList();
        var accuracy = AccuracyScoring.GetAccuracyPercentage();
        
        ImGui.PushFont(FontController.Title);
        string scoreText = $"Final Accuracy: {accuracy:F1}%";
        var textSize = ImGui.CalcTextSize(scoreText);
        ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - textSize.X) / 2);
        ImGui.TextColored(accuracy > 80 ? new Vector4(0.2f, 0.9f, 0.3f, 1f) : new Vector4(1f, 0.3f, 0.2f, 1f), scoreText);
        ImGui.PopFont();

        ImGuiUtils.TextCentered($"{AccuracyScoring.NotesHit} / {AccuracyScoring.TotalNotes} notes hit correctly");
        
        if (ImGui.Button("Retry Session", new Vector2(ImGui.GetContentRegionAvail().X, 40)))
        {
            AccuracyScoring.StartSession();
            MidiPlayer.Playback.MoveToStart();
            MidiPlayer.Timer = 0;
            MidiPlayer.Seconds = 0;
            MidiPlayer.Playback.Start();
            MidiPlayer.StartTimer();
        }

        ImGui.Separator();
        ImGui.Text("Problem Areas (Heatmap):");

        // Simple heatmap: highlight keys that were missed
        var missedNotes = attempts.Where(a => !a.Hit).GroupBy(a => a.NoteNumber).ToDictionary(g => g.Key, g => g.Count());
        
        // We'll hijack PianoRenderer.ApproachingNotes to show the heatmap colors
        PianoRenderer.ApproachingNotes.Clear();
        foreach (var missed in missedNotes)
        {
            float intensity = Math.Min(missed.Value / 3f, 1.0f); // More than 3 misses = full red
            PianoRenderer.ApproachingNotes[missed.Key] = (new Vector4(1f, 0.2f, 0.1f, 1f), intensity);
        }
        
        PianoRenderer.RenderKeyboard();
    }
}
