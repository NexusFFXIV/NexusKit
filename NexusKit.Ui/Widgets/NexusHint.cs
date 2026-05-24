using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;

namespace NexusKit.Ui.Widgets;

/// <summary>
/// Inline icon + hover-tooltip used to annotate fields ("this changed",
/// "click for details", gender badges, …). Pure visual primitive — the
/// caller decides whether the hint applies and supplies the tooltip text.
/// <para>Defaults to <see cref="ImGuiColors.DalamudYellow"/> as a generic
/// "notice me" colour and to <c>SameLine</c> placement because hints almost
/// always sit beside another piece of text.</para>
/// </summary>
public static class NexusHint
{
    public static void Draw(
        FontAwesomeIcon icon,
        string tooltip,
        Vector4? color = null,
        bool sameLine = true)
    {
        if (sameLine) ImGui.SameLine();
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextColored(color ?? ImGuiColors.DalamudYellow, icon.ToIconString());
        ImGui.PopFont();
        if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);
    }
}
