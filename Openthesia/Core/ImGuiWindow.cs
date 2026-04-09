using ImGuiNET;
using System.Numerics;

namespace Openthesia.Core;

public abstract class ImGuiWindow
{
    protected ImGuiIOPtr _io = ImGui.GetIO();

    /// <summary>
    /// ImGui window id
    /// </summary>
    protected string _id = string.Empty;

    /// <summary>
    /// ImGui window state
    /// </summary>
    protected bool _active;

    /// <summary>
    /// ImGui window flags
    /// </summary>
    protected ImGuiWindowFlags _windowFlags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar
        | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize;

    /// <summary>
    /// True if window fills screen content
    /// </summary>
    protected bool _isMainWindow = true;

    /// <summary>
    /// Timer utility
    /// </summary>
    protected float _timer = 0f;
    protected float _fadeInTimer = 0f;
    protected const float FadeDuration = 0.3f;

    public string GetId()
    {
        return _id;
    }

    public ref bool Active()
    {
        return ref _active;
    }

    public void SetActive(bool active)
    {
        if (active && !_active)
        {
            _fadeInTimer = 0f;
        }
        _active = active;
    }

    /// <summary>
    /// Window rendering
    /// </summary>
    public void RenderWindow()
    {
        float fadeAlpha = 1f;
        if (_fadeInTimer < FadeDuration)
        {
            _fadeInTimer += _io.DeltaTime;
            fadeAlpha = Math.Clamp(_fadeInTimer / FadeDuration, 0f, 1f);
        }

        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, fadeAlpha);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        if (ImGui.Begin(_id, ref _active, _windowFlags))
        {
            if (_isMainWindow)
            {
                ImGui.SetWindowPos(Vector2.Zero);
                ImGui.SetWindowSize(_io.DisplaySize);
            }

            _timer += _io.DeltaTime; // update window related timer
            OnImGui();
            ImGui.End();
        }
        ImGui.PopStyleVar(); // WindowBorderSize
        ImGui.PopStyleVar(); // Alpha
    }

    /// <summary>
    /// Window content rendering
    /// </summary>
    protected abstract void OnImGui();
}
