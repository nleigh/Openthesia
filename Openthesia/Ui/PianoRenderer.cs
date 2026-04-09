using ImGuiNET;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Openthesia.Core;
using Openthesia.Settings;
using Openthesia.Ui.Helpers;
using System.Numerics;
using System.Linq;

namespace Openthesia.Ui;

public class PianoRenderer
{
    static uint _black => ImGui.GetColorU32(ImGuiTheme.HtmlToVec4("#141414"));
    static uint _white => ImGui.GetColorU32(ImGuiTheme.HtmlToVec4("#FFFFFF"));
    static uint _whitePressed => ImGui.GetColorU32(ImGuiTheme.HtmlToVec4("#888888"));
    static uint _blackPressed => ImGui.GetColorU32(ImGuiTheme.HtmlToVec4("#555555"));

    public static float Width;
    public static float Height;
    public static Vector2 P;

    public static Dictionary<SevenBitNumber, int> WhiteNoteToKey = new();
    public static Dictionary<SevenBitNumber, int> BlackNoteToKey = new();
    public static Dictionary<int, (Vector4 Color, float Alpha)> ApproachingNotes = new();

    public static void RenderKeyboard()
    {
        ImGui.PushFont(FontController.GetFontOfSize((int)(18 * FontController.DSF)));
        ImDrawListPtr draw_list = ImGui.GetWindowDrawList();
        P = ImGui.GetCursorScreenPos();

        Width = ImGui.GetIO().DisplaySize.X * 1.9f / 100;
        Height = ImGui.GetIO().DisplaySize.Y - ImGui.GetIO().DisplaySize.Y * 76f / 100;

        int cur_key = 22; // Start from first black key since we need to handle black keys mouse input before white ones

        /* Check if a black key is pressed */
        bool blackKeyClicked = false;
        bool blackKeyHovered = false;
        for (int key = 0; key < 52; key++)
        {
            if (KeysUtils.HasBlack(key))
            {
                Vector2 min = new(P.X + key * Width + Width * 3 / 4, P.Y);
                Vector2 max = new(P.X + key * Width + Width * 5 / 4 + 1, P.Y + Height / 1.5f);

                if (ImGui.IsMouseHoveringRect(min, max))
                {
                    blackKeyHovered = true;
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        blackKeyClicked = true;
                    }
                }

                cur_key += 2;
            }
            else
            {
                cur_key++;
            }
        }

        cur_key = 21;
        int cCount = 1;
        string[] _names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        for (int key = 0; key < 52; key++)
        {
            uint col = _white;

            if (ImGui.IsMouseHoveringRect(new(P.X + key * Width, P.Y), new(P.X + key * Width + Width, P.Y + Height)))
            {
                if (!blackKeyHovered)
                {
                    ImGui.SetTooltip($"{_names[cur_key % 12]}{(cur_key / 12) - 1}");
                }

                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !CoreSettings.KeyboardInput && !blackKeyClicked)
                {
                    // on key mouse press
                    IOHandle.OnEventReceived(null,
                        new Melanchall.DryWetMidi.Multimedia.MidiEventReceivedEventArgs(new NoteOnEvent((SevenBitNumber)cur_key, new SevenBitNumber(127))));
                    DevicesManager.ODevice?.SendEvent(new NoteOnEvent((SevenBitNumber)cur_key, new SevenBitNumber(127)));
                }
            }

            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left) && !CoreSettings.KeyboardInput)
            {
                if (IOHandle.PressedKeys.Contains(cur_key))
                {
                    // on key mouse release
                    IOHandle.OnEventReceived(null,
                        new Melanchall.DryWetMidi.Multimedia.MidiEventReceivedEventArgs(new NoteOffEvent((SevenBitNumber)cur_key, new SevenBitNumber(0))));
                    DevicesManager.ODevice?.SendEvent(new NoteOffEvent((SevenBitNumber)cur_key, new SevenBitNumber(0)));
                }
            }

            bool isPressed = IOHandle.PressedKeys.Contains(cur_key);
            bool isApproaching = ApproachingNotes.TryGetValue(cur_key, out var appInfo);

            if (isPressed || isApproaching)
            {
                float alpha = isPressed ? 1.0f : appInfo.Alpha;
                Vector4 rawCol = isApproaching ? appInfo.Color : new Vector4(0.529f, 0.784f, 0.325f, 1f);
                Vector3 baseCol = Vector3.One;
                Vector3 targetCol = new Vector3(rawCol.X, rawCol.Y, rawCol.Z);
                Vector3 lerpedCol = baseCol + (targetCol - baseCol) * alpha;
                col = ImGui.GetColorU32(new Vector4(lerpedCol, 1.0f));
            }

            var offset = IOHandle.PressedKeys.Contains(cur_key) ? 2 : 0;

            draw_list.AddImageRounded(Drawings.C,
                new Vector2(P.X + key * Width, P.Y) + new Vector2(offset, 0),
                new Vector2(P.X + key * Width + Width, P.Y + Height) + new Vector2(offset, 0), Vector2.Zero, Vector2.One, col, 5, ImDrawFlags.RoundCornersBottom);

            if (isApproaching && !isPressed && appInfo.Alpha > 0.6f)
            {
                float borderAlpha = (appInfo.Alpha - 0.6f) * 2.5f;
                uint borderCol = ImGui.GetColorU32(new Vector4(appInfo.Color.X, appInfo.Color.Y, appInfo.Color.Z, borderAlpha));
                draw_list.AddRect(
                    new Vector2(P.X + key * Width, P.Y) + new Vector2(offset, 0),
                    new Vector2(P.X + key * Width + Width, P.Y + Height) + new Vector2(offset, 0),
                    borderCol, 5, ImDrawFlags.RoundCornersBottom, 3.0f);
            }

            if (WhiteNoteToKey.Count < 52)
                WhiteNoteToKey.Add((SevenBitNumber)cur_key, key);

            var nName = _names[cur_key % 12];
            var tPos = new Vector2(P.X + key * Width + (Width / 2 - ImGui.CalcTextSize(nName).X / 2), P.Y + Height - 25 * FontController.DSF);
            ImGui.GetForegroundDrawList().AddText(tPos + new Vector2(1), ImGui.GetColorU32(new Vector4(0,0,0,0.8f)), nName);
            ImGui.GetForegroundDrawList().AddText(tPos, (isPressed || isApproaching) ? ImGui.GetColorU32(Vector4.One) : _black, nName);

            if (CoreSettings.KeyboardInput)
            {
                var match = VirtualKeyboard.KeyNoteMap.FirstOrDefault(x => x.Value + VirtualKeyboard.OctaveShift == cur_key);
                if (match.Key != ImGuiKey.None)
                {
                    var ktext = match.Key.ToString();
                    ImGui.GetForegroundDrawList().AddText(new(P.X + key * Width + (Width / 2 - ImGui.CalcTextSize(ktext).X / 2),
                        P.Y + Height - 50 * FontController.DSF), _blackPressed, ktext);
                }
            }

            cur_key++;
            if (KeysUtils.HasBlack(key))
            {
                cur_key++;
            }
        }

        cur_key = 22;
        for (int key = 0; key < 52; key++)
        {
            if (BlackNoteToKey.Count < 52)
                BlackNoteToKey.Add((SevenBitNumber)cur_key, key);

            if (KeysUtils.HasBlack(key))
            {
                uint col = ImGui.GetColorU32(Vector4.One);

                if (ImGui.IsMouseHoveringRect(new(P.X + key * Width + Width * 3 / 4, P.Y),
                    new(P.X + key * Width + Width * 5 / 4 + 1, P.Y + Height / 1.5f)))
                {
                    ImGui.SetTooltip($"{_names[cur_key % 12]}{(cur_key / 12) - 1}");

                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !CoreSettings.KeyboardInput)
                    {
                        IOHandle.OnEventReceived(null,
                            new Melanchall.DryWetMidi.Multimedia.MidiEventReceivedEventArgs(new NoteOnEvent((SevenBitNumber)cur_key, new SevenBitNumber(127))));
                        DevicesManager.ODevice?.SendEvent(new NoteOnEvent((SevenBitNumber)cur_key, new SevenBitNumber(127)));
                    }
                }

                if (ImGui.IsMouseReleased(ImGuiMouseButton.Left) && !CoreSettings.KeyboardInput)
                {
                    if (IOHandle.PressedKeys.Contains(cur_key))
                    {
                        IOHandle.OnEventReceived(null,
                            new Melanchall.DryWetMidi.Multimedia.MidiEventReceivedEventArgs(new NoteOffEvent((SevenBitNumber)cur_key, new SevenBitNumber(0))));
                        DevicesManager.ODevice?.SendEvent(new NoteOffEvent((SevenBitNumber)cur_key, new SevenBitNumber(0)));
                    }
                }

                bool isPressed = IOHandle.PressedKeys.Contains(cur_key);
                bool isApproaching = ApproachingNotes.TryGetValue(cur_key, out var appInfo);

                var blackImage = (isPressed || isApproaching) ? Drawings.CSharpWhite : Drawings.CSharp;

                if (isPressed || isApproaching)
                {
                    float alpha = isPressed ? 1.0f : appInfo.Alpha;
                    Vector4 rawCol = isApproaching ? appInfo.Color : new Vector4(0.529f, 0.784f, 0.325f, 1f);
                    Vector3 darkGray = new Vector3(0.15f, 0.15f, 0.15f);
                    Vector3 targetCol = new Vector3(rawCol.X, rawCol.Y, rawCol.Z);
                    Vector3 lerpedCol = darkGray + (targetCol - darkGray) * alpha;
                    col = ImGui.GetColorU32(new Vector4(lerpedCol, 1.0f));
                }
                else
                {
                    col = ImGui.GetColorU32(Vector4.One);
                }

                var offset = isPressed ? 1 : 0;

                draw_list.AddImage(blackImage,
                    new Vector2(P.X + key * Width + Width * 3 / 4, P.Y),
                    new Vector2(P.X + key * Width + Width * 5 / 4 + 1, P.Y + Height / 1.5f) + new Vector2(offset), Vector2.Zero, Vector2.One, col);

                if (isApproaching && !isPressed && appInfo.Alpha > 0.6f)
                {
                    float borderAlpha = (appInfo.Alpha - 0.6f) * 2.5f; 
                    uint borderCol = ImGui.GetColorU32(new Vector4(appInfo.Color.X, appInfo.Color.Y, appInfo.Color.Z, borderAlpha));
                    draw_list.AddRect(
                        new Vector2(P.X + key * Width + Width * 3 / 4, P.Y),
                        new Vector2(P.X + key * Width + Width * 5 / 4 + 1, P.Y + Height / 1.5f) + new Vector2(offset), 
                        borderCol, 0, 0, 3.0f);
                }

                var nName = _names[cur_key % 12];
                var tPos = new Vector2(P.X + key * Width + Width - ImGui.CalcTextSize(nName).X / 2, P.Y + Height / 1.5f - 25 * FontController.DSF);
                ImGui.GetForegroundDrawList().AddText(tPos + new Vector2(1), ImGui.GetColorU32(new Vector4(0,0,0,0.8f)), nName);
                ImGui.GetForegroundDrawList().AddText(tPos, (isPressed || isApproaching) ? ImGui.GetColorU32(Vector4.One) : _white, nName);

                if (CoreSettings.KeyboardInput)
                {
                    var match = VirtualKeyboard.KeyNoteMap.FirstOrDefault(x => x.Value + VirtualKeyboard.OctaveShift == cur_key);
                    if (match.Key != ImGuiKey.None)
                    {
                        var ktext = match.Key.ToString();
                        ImGui.GetForegroundDrawList().AddText(new(P.X + key * Width + Width - ImGui.CalcTextSize(ktext).X / 2,
                            P.Y + Height / 1.5f - 25 * FontController.DSF), _whitePressed, ktext);
                    }
                }

                cur_key += 2;
            }
            else
            {
                cur_key++;
            }
        }

        ImGui.PopFont();
    }
}
