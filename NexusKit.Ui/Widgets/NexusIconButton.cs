using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using NexusKit.Core.Actions;

namespace NexusKit.Ui.Widgets;

/// <summary>
/// Compact icon-only button with a hover tooltip. The visual counterpart to
/// <see cref="NexusHint"/> — same icon-font plumbing but clickable. Returns
/// <c>true</c> on click so callers can chain it directly:
/// <code>
/// if (NexusIconButton.Draw(FontAwesomeIcon.SyncAlt, "Refresh"))
///     state.Refresh();
/// </code>
/// An overload accepting an <see cref="Action"/> covers the toolbar case
/// where the action is fixed.
/// </summary>
public static class NexusIconButton
{
    private static readonly Vector2 DefaultSize = new(32f, 24f);

    public static bool Draw(FontAwesomeIcon icon, string tooltip, Vector2? size = null)
        => Draw(icon, tooltip, accentColor: null, size);

    /// <summary>
    /// Overload that optionally tints the ICON glyph (not the button
    /// background) with <paramref name="accentColor"/> via
    /// <c>ImGuiCol.Text</c>. Tinting the glyph instead of the background
    /// keeps the button frame visually consistent with neighbouring
    /// buttons while still drawing the eye to the colored icon — best fit
    /// for the <see cref="NexusKit.Core.Actions.ActionRenderHint"/>
    /// pattern where multiple variants of one action share the same
    /// button slot. <c>BeginDisabled</c> (applied by the hint overload
    /// when <c>CanExecute = false</c>) still dims the tinted text via
    /// the global disabled alpha, so the disabled state stays readable.
    /// </summary>
    public static bool Draw(FontAwesomeIcon icon, string tooltip,
                            Vector4? accentColor, Vector2? size = null)
    {
        var pushed = accentColor is { } accent;
        if (pushed) ImGui.PushStyleColor(ImGuiCol.Text, accentColor!.Value);
        try
        {
            ImGui.PushFont(UiBuilder.IconFont);
            // The "##" suffix scopes the imgui id per icon kind so two buttons
            // with the same glyph next to each other don't share state.
            var clicked = ImGui.Button($"{icon.ToIconString()}##nx_btn_{(int)icon}",
                size ?? DefaultSize);
            ImGui.PopFont();
            // AllowWhenDisabled is what makes the tooltip survive an enclosing
            // BeginDisabled scope. Without it a greyed-out button explains
            // nothing, which is the one moment the explanation matters most.
            if (!string.IsNullOrEmpty(tooltip)
                && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip(tooltip);
            return clicked;
        }
        finally
        {
            if (pushed) ImGui.PopStyleColor();
        }
    }

    public static void Draw(FontAwesomeIcon icon, string tooltip, Action onClick,
                            Vector2? size = null)
    {
        if (Draw(icon, tooltip, size)) onClick();
    }

    /// <summary>
    /// Convenience overload for callers that already resolved an
    /// <see cref="ActionRenderHint"/> from an adapter's <c>Preview…</c>
    /// method. Applies the hint's <see cref="ActionRenderHint.AccentColor"/>
    /// tint and wraps the draw in <c>ImGui.BeginDisabled</c> when
    /// <see cref="ActionRenderHint.CanExecute"/> is <c>false</c> — so the
    /// button stays visible (with the explanatory tooltip) but ignores
    /// clicks. The caller still resolves <paramref name="tooltip"/>
    /// because the localizer lives at the call site; the hint's
    /// <see cref="ActionRenderHint.TooltipKey"/> is just a key, not a
    /// resolved string.
    /// </summary>
    public static bool Draw(FontAwesomeIcon icon, string tooltip,
                            ActionRenderHint hint, Vector2? size = null)
    {
        var disabled = !hint.CanExecute;
        // Hint can attach a second tooltip line (raw, non-localized — e.g. the
        // literal IPC command an adapter is about to send). Single \n keeps
        // ImGui's SetTooltip happy without extra paragraph padding.
        var fullTooltip = hint.DetailText is { } d && !string.IsNullOrEmpty(d)
            ? $"{tooltip}\n{d}"
            : tooltip;
        if (disabled) ImGui.BeginDisabled();
        try
        {
            return Draw(icon, fullTooltip, hint.AccentColor, size);
        }
        finally
        {
            if (disabled) ImGui.EndDisabled();
        }
    }
}
