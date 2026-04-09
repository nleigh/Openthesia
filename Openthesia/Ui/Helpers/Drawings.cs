using ImGuiNET;
using Melanchall.DryWetMidi.Interaction;
using Openthesia.Enums;
using Openthesia.Settings;
using System.Numerics;
using System.Linq;

namespace Openthesia.Ui.Helpers;

public class Drawings
{
    public static IntPtr C;
    public static IntPtr CSharp;
    public static IntPtr CSharpWhite;
    public static IntPtr SustainPedalOff;
    public static IntPtr SustainPedalOn;

    public static void RenderMatrixBackground()
    {
        var drawList = ImGui.GetWindowDrawList();
        var io = ImGui.GetIO();
        var time = ImGui.GetTime();

        var screenWidth = (int)io.DisplaySize.X;
        var screenHeight = (int)io.DisplaySize.Y;

        var random = new Random(100);

        for (int i = 0; i < 20; i++)
        {
            int baseX = random.Next(0, screenWidth);
            int startY = random.Next(0, screenHeight);
            int length = random.Next(10, 50);
            float speed = random.Next(250, 500); // pixels/sec

            float y = (startY + (float)(time * speed)) % (screenHeight + length);

            if (CoreSettings.NeonFx)
            {
                for (int j = 0; j < 3; j++)
                {
                    float thickness = j * 2;
                    float alpha = 0.2f + (3 - j) * 0.2f;
                    uint color = ImGui.GetColorU32(new Vector4(
                        0.529f,
                        0.784f,
                        0.325f,
                        alpha * 0.5f));

                    drawList.AddRect(
                        new Vector2(baseX - 1, y - 1),
                        new Vector2(baseX + 20 + 1, y + length + 1),
                        color,
                        5f,
                        0,
                        thickness
                    );
                }
            }

            drawList.AddRectFilled(
                new Vector2(baseX, y),
                new Vector2(baseX + 20, y + length),
                ImGui.GetColorU32(new Vector4(0.529f, 0.784f, 0.325f, 1f)),
                5,
                ImDrawFlags.RoundCornersAll
            );
        }
    }

    public static void Tooltip(string description)
    {
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.None))
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
            ImGui.TextUnformatted(description);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
    }

    public static void NoteTooltip(string description)
    {
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
        ImGui.TextUnformatted(description);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    public static string GetNoteTextAs(TextTypes textType, Note note)
    {
        switch (textType)
        {
            case TextTypes.NoteName:
                return note.NoteName.ToString();
            case TextTypes.Velocity:
                return note.Velocity.ToString();
            case TextTypes.Octave:
                return note.Octave.ToString();
            default:
                return note.NoteName.ToString();
        }
    }

    public static void AddTextOutlined(ImDrawListPtr drawList, Vector2 pos, uint textColor, uint outlineColor, string text, float thickness = 1.0f)
    {
        drawList.AddText(pos + new Vector2(-thickness, 0), outlineColor, text);
        drawList.AddText(pos + new Vector2(thickness, 0), outlineColor, text);
        drawList.AddText(pos + new Vector2(0, -thickness), outlineColor, text);
        drawList.AddText(pos + new Vector2(0, thickness), outlineColor, text);
        
        drawList.AddText(pos + new Vector2(-thickness, -thickness), outlineColor, text);
        drawList.AddText(pos + new Vector2(thickness, -thickness), outlineColor, text);
        drawList.AddText(pos + new Vector2(-thickness, thickness), outlineColor, text);
        drawList.AddText(pos + new Vector2(thickness, thickness), outlineColor, text);

        drawList.AddText(pos, textColor, text);
    }

    private static List<int> _cachedPressedKeys = new List<int>();
    private static string _cachedChord = "";

    public static string GetDetectedChord()
    {
        var keys = Openthesia.Core.IOHandle.PressedKeys.ToList();
        if (keys.SequenceEqual(_cachedPressedKeys))
            return _cachedChord;

        _cachedPressedKeys = keys;
        _cachedChord = "";

        if (keys.Count < 3) return "";
        
        try 
        {
            var noteNames = keys.Select(k => Melanchall.DryWetMidi.MusicTheory.Note.Get((Melanchall.DryWetMidi.Common.SevenBitNumber)k).NoteName).Distinct().ToList();
            var chord = new Melanchall.DryWetMidi.MusicTheory.Chord(noteNames);
            var chordNames = chord.GetNames();
            if (chordNames.Any())
                _cachedChord = chordNames.First();
        }
        catch { }

        return _cachedChord;
    }
}
