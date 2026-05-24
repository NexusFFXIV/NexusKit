using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;

namespace NexusKit.Ui.Widgets;

/// <summary>
/// Helper for the recurring "subtle title text + horizontal separator above
/// content" pattern that groups related controls inside a card. Caller draws
/// the actual section content after calling <see cref="Header"/>.
/// </summary>
public static class NexusSection
{
    /// <summary>Disabled-colored title followed by a separator. Adds a small
    /// vertical pad before the header so consecutive sections breathe.</summary>
    public static void Header(string title)
    {
        ImGui.Spacing();
        ImGui.TextColored(ImGuiColors.DalamudGrey, title);
        ImGui.Separator();
    }
}
