using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;

namespace NexusKit.Ui.Widgets;

/// <summary>
/// In-place animated loading indicator. Renders as a rotating ring of dots at the
/// current cursor position. Suitable for inline use next to a "Refreshing…" label
/// or in place of a refresh icon while a fetch is in flight.
/// </summary>
public static class NexusLoadingSpinner
{
    private const int Segments = 8;
    private const float DotRadius = 1.8f;
    private const float Speed = 6f; // rad/s

    /// <summary>Draw at the current cursor position. <paramref name="size"/> is the
    /// outer diameter in unscaled pixels (the widget reserves a size×size area).</summary>
    public static void Draw(float size = 16f, Vector4? color = null)
    {
        var drawList = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var center = pos + new Vector2(size * 0.5f, size * 0.5f);
        var t = (float)ImGui.GetTime();
        var col = color ?? ImGuiColors.DalamudGrey;

        for (var i = 0; i < Segments; i++)
        {
            var angle = (i / (float)Segments) * MathF.PI * 2f - t * Speed;
            var dot = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (size * 0.4f);
            // Fade trailing dots so the head looks bright and the tail dim.
            var alpha = ((Segments - i) / (float)Segments) * col.W;
            var rgba = new Vector4(col.X, col.Y, col.Z, alpha);
            drawList.AddCircleFilled(dot, DotRadius, ImGui.GetColorU32(rgba));
        }

        // Reserve the layout space so subsequent widgets advance past the spinner.
        ImGui.Dummy(new Vector2(size, size));
    }
}
