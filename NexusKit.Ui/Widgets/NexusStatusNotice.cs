using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;

namespace NexusKit.Ui.Widgets;

public enum NexusStatusNoticeMode
{
    /// <summary>Icon + wrapped text laid out as a block element. No hover behavior.</summary>
    Text,
    /// <summary>Icon only; hover shows the tooltip. SameLine-friendly (header badge).</summary>
    Tooltip,
    /// <summary>Icon + wrapped text AND a hover tooltip on the icon. Tooltip text
    /// can differ from the body text when the caller passes a separate
    /// <c>tooltipText</c>.</summary>
    Both,
}

/// <summary>
/// Shared "something is going on in the background" notice. Three modes let the
/// same widget carry an inline icon-with-tooltip in a header AND a full wrapped
/// status line at the top of a tab body without duplicating either code path.
/// <para>Color defaults track the existing palette: yellow for icon-only badges
/// (matches the <see cref="NexusHint"/> pattern), grey for full-text notices
/// (matches the legacy <c>LodestonePlaceholder</c> wrap). Either default can be
/// overridden via the <c>color</c> argument.</para>
/// </summary>
public static class NexusStatusNotice
{
    public static void Draw(
        NexusStatusNoticeMode mode,
        FontAwesomeIcon icon,
        string text,
        string? tooltipText = null,
        Vector4? color = null,
        bool sameLine = false)
    {
        if (sameLine) ImGui.SameLine();

        var resolvedColor = color ?? (mode == NexusStatusNoticeMode.Tooltip
            ? ImGuiColors.DalamudYellow
            : ImGuiColors.DalamudGrey);

        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextColored(resolvedColor, icon.ToIconString());
        ImGui.PopFont();

        if (mode is NexusStatusNoticeMode.Tooltip or NexusStatusNoticeMode.Both)
        {
            var tip = tooltipText ?? text;
            if (!string.IsNullOrEmpty(tip) && ImGui.IsItemHovered())
                ImGui.SetTooltip(tip);
        }

        if (mode is NexusStatusNoticeMode.Text or NexusStatusNoticeMode.Both)
        {
            ImGui.SameLine();
            ImGui.PushTextWrapPos();
            ImGui.TextColored(resolvedColor, text);
            ImGui.PopTextWrapPos();
        }
    }
}
