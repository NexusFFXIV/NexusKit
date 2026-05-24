using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;

namespace NexusKit.Ui.Widgets;

/// <summary>
/// Compact "big number + label" tile used in stat headers (e.g. "Mounts 145/344").
/// Render multiple side-by-side with <c>ImGui.SameLine()</c> for a dashboard strip.
/// </summary>
public sealed class NexusStatCard
{
    /// <summary>Short caption displayed above the value, e.g. "Mounts".</summary>
    public required string Label { get; init; }

    /// <summary>Optional muted suffix rendered inline after <see cref="Label"/>
    /// (typically grey, parenthesised), e.g. "Mounts (42%)".</summary>
    public string? LabelSuffix { get; init; }

    /// <summary>Primary value, rendered emphasized.</summary>
    public required string Value { get; init; }

    /// <summary>Optional small caption below the value (e.g. percentage).</summary>
    public string? SubLabel { get; init; }

    /// <summary>Optional muted suffix rendered inline after <see cref="Value"/>
    /// (typically grey, parenthesised), e.g. "145 / 344  (42%)".</summary>
    public string? ValueSuffix { get; init; }

    /// <summary>Optional accent color for the value text. Defaults to the standard text color.</summary>
    public Vector4? AccentColor { get; init; }

    /// <summary>Render at the current cursor position, sized to <paramref name="width"/>.
    /// Height is computed from the label/value/sub text lines plus padding — the
    /// underlying <c>ImGui.BeginChild</c> defaults to "fill remaining" when given
    /// a 0 height, which would stretch each card to the tab's full vertical space.</summary>
    public void Draw(float width)
    {
        const float Padding = 8f;
        // Visual budget: label line, separator (~1 px line + 4 px spacing), value
        // line, sublabel line, top/bottom padding. Reserve the sublabel slot even
        // when empty so a row of stat cards stays aligned.
        var line = ImGui.GetTextLineHeightWithSpacing();
        var height = 3f * line + 5f + Padding * 2f;

        var id = $"##stat_{Label}";
        using (NexusCard.Begin(id, new Vector2(width, height), border: true, padding: Padding))
        {
            ImGui.TextColored(ImGuiColors.DalamudYellow, Label);
            if (!string.IsNullOrEmpty(LabelSuffix))
            {
                ImGui.SameLine();
                ImGui.TextDisabled(LabelSuffix);
            }
            ImGui.Separator();

            if (AccentColor is { } accent)
                ImGui.TextColored(accent, Value);
            else
                ImGui.TextUnformatted(Value);

            if (!string.IsNullOrEmpty(ValueSuffix))
            {
                ImGui.SameLine();
                ImGui.TextDisabled(ValueSuffix);
            }

            if (!string.IsNullOrEmpty(SubLabel))
                ImGui.TextDisabled(SubLabel);
        }
    }
}
