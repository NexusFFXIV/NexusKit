using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace NexusKit.Ui.Widgets;

/// <summary>
/// Square child region with rounded corners, a flat background colour and a
/// centred icon-font glyph. Used as a fallback "avatar" tile when a real
/// portrait isn't available yet — generic enough that callers map the
/// (colour, icon) pair themselves from whatever domain state they care
/// about. The corner radius scales with the requested size so a 24 px chip
/// and a 96 px header avatar both look balanced.
/// </summary>
public static class NexusRoundedAvatar
{
    private static readonly Vector4 DefaultIconColor = new(1f, 1f, 1f, 0.85f);

    public static void Draw(
        Vector4 backgroundColor,
        FontAwesomeIcon icon,
        float size,
        Vector4? iconColor = null)
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, backgroundColor);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, size * 0.12f);
        try
        {
            ImGui.BeginChild("##nx_rounded_avatar", new Vector2(size, size), false,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

            ImGui.PushFont(UiBuilder.IconFont);
            var glyph = icon.ToIconString();
            var iconSize = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorPos(new Vector2(
                (size - iconSize.X) * 0.5f,
                (size - iconSize.Y) * 0.5f));
            ImGui.TextColored(iconColor ?? DefaultIconColor, glyph);
            ImGui.PopFont();

            ImGui.EndChild();
        }
        finally
        {
            ImGui.PopStyleVar();
            ImGui.PopStyleColor();
        }
    }
}
