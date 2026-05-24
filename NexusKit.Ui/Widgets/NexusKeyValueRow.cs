using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;

namespace NexusKit.Ui.Widgets;

/// <summary>
/// "Label : value"-style row used inside profile / detail panels. The label
/// column is left-aligned to a fixed width so consecutive rows line up
/// regardless of value length; the value renders to the right and wraps.
/// </summary>
public static class NexusKeyValueRow
{
    private const float DefaultKeyWidth = 140f;

    public static void Draw(string key, string? value, float keyWidth = DefaultKeyWidth)
    {
        ImGui.TextColored(ImGuiColors.DalamudGrey, key);
        ImGui.SameLine(keyWidth);
        ImGui.TextUnformatted(string.IsNullOrEmpty(value) ? "—" : value);
    }

    /// <summary>Variant where the value column is rendered by a caller-supplied
    /// draw action — use for clickable links, colored chips, etc.</summary>
    public static void Draw(string key, Action drawValue, float keyWidth = DefaultKeyWidth)
    {
        ImGui.TextColored(ImGuiColors.DalamudGrey, key);
        ImGui.SameLine(keyWidth);
        drawValue();
    }
}
