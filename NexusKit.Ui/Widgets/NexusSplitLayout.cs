using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace NexusKit.Ui.Widgets;

/// <summary>
/// Horizontal master-detail layout: a fixed-width left column followed by a
/// flexible right column that fills the remaining width. Each side is wrapped
/// in its own child region so scrolling stays independent.
/// </summary>
public static class NexusSplitLayout
{
    /// <summary>Draw a left/right split inside the current ImGui region.</summary>
    /// <param name="id">Stable ImGui id prefix; child windows derive theirs from it.</param>
    /// <param name="leftWidth">Width of the fixed-size left column in unscaled pixels.</param>
    /// <param name="drawLeft">Renders the left column's content.</param>
    /// <param name="drawRight">Renders the right column's content.</param>
    /// <param name="leftScrolls">Allow the left column to grow a scrollbar when content overflows.</param>
    /// <param name="rightScrolls">Allow the right column to grow a scrollbar. Set false when the
    /// caller renders its own fixed header / tabs and only wants the inner content area to scroll.</param>
    public static void Draw(
        string id,
        float leftWidth,
        Action drawLeft,
        Action drawRight,
        bool leftScrolls = true,
        bool rightScrolls = true)
    {
        var avail = ImGui.GetContentRegionAvail();
        var leftFlags = leftScrolls ? ImGuiWindowFlags.None
            : ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        var rightFlags = rightScrolls ? ImGuiWindowFlags.None
            : ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

        ImGui.BeginChild($"{id}.left", new Vector2(leftWidth, avail.Y), false, leftFlags);
        drawLeft();
        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild($"{id}.right", new Vector2(0, avail.Y), false, rightFlags);
        drawRight();
        ImGui.EndChild();
    }
}
