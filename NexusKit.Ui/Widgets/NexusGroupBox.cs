using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;

namespace NexusKit.Ui.Widgets;

/// <summary>
/// Section block with an emphasized title row, a separator, and a body rendered
/// by a caller-supplied delegate. Used everywhere a panel groups related rows
/// (live observation, session stats, profile, grand company, …) so they share
/// one visual language.
/// </summary>
public static class NexusGroupBox
{
    private const float DefaultTrailingPad = 8f;

    /// <summary>Draw the group at the current cursor position.</summary>
    /// <param name="title">Yellow title rendered above the separator.</param>
    /// <param name="drawContent">Callback that renders the body rows.</param>
    /// <param name="titleSuffix">Optional muted text rendered inline after the
    /// title (grey, typically a count / percentage), e.g.
    /// <c>"145 / 344 (42%)"</c>.</param>
    /// <param name="headerRightAction">Optional callback rendered as an overlay
    /// at the right edge of the title row. The widget supplies a recommended
    /// square <c>size</c> (matching the title's text-line height) so the
    /// caller can draw a compact <see cref="NexusIconButton"/> that fits within
    /// the row without pushing the separator below the neighbouring group's.
    /// <para>The cursor is restored after the callback so the separator and
    /// body draw exactly as they would without an action — side-by-side groups
    /// stay vertically aligned.</para></param>
    /// <param name="trailingPad">Vertical space appended after the body so
    /// consecutive groups breathe. Set 0 if you handle the gap yourself.</param>
    public static void Draw(string title, Action drawContent,
                            string? titleSuffix = null,
                            Action<Vector2>? headerRightAction = null,
                            float trailingPad = DefaultTrailingPad)
    {
        ImGui.BeginGroup();
        var titleStartY = ImGui.GetCursorPosY();
        ImGui.TextColored(ImGuiColors.DalamudYellow, title);
        if (!string.IsNullOrEmpty(titleSuffix))
        {
            ImGui.SameLine();
            ImGui.TextDisabled(titleSuffix);
        }
        if (headerRightAction is not null)
        {
            // Overlay the action at the right edge of the title row without
            // disturbing layout flow. The caller renders inside the suggested
            // text-line-height square so the row height stays equal to a
            // text-only header — keeping the separator at the same Y as
            // neighbouring (no-action) groups in a DrawColumns layout.
            //
            // Two style overrides make the compact button look right:
            //   * FramePadding is shrunk to 2px so a default-sized icon glyph
            //     centers cleanly inside the square without being clipped.
            //   * Right margin uses ItemSpacing.X (≈8px) so the button has
            //     visible breathing room from the column edge, not just a
            //     hairline gap.
            var iconSize = ImGui.GetTextLineHeightWithSpacing();
            var rightMargin = ImGui.GetStyle().ItemSpacing.X;
            var savedPos = ImGui.GetCursorPos();
            var avail = ImGui.GetContentRegionAvail().X;
            ImGui.SetCursorPos(new Vector2(savedPos.X + avail - iconSize - rightMargin, titleStartY));
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(2f, 2f));
            headerRightAction(new Vector2(iconSize, iconSize));
            ImGui.PopStyleVar();
            ImGui.SetCursorPos(savedPos);
        }
        DrawColumnBoundedSeparator();
        ImGui.Dummy(new Vector2(0, 2f));
        drawContent();
        ImGui.EndGroup();
        if (trailingPad > 0f)
            ImGui.Dummy(new Vector2(0, trailingPad));
    }

    /// <summary>
    /// Hand-rolled separator that respects the current column / content region —
    /// <see cref="ImGui.Separator"/> inside a table cell spans the full table row,
    /// which makes side-by-side groups look like one wide block instead of two
    /// distinct cards. Draws a single-pixel line via the window's draw list and
    /// advances the cursor by the same amount the built-in separator would.
    /// </summary>
    private static void DrawColumnBoundedSeparator()
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var color = ImGui.GetColorU32(ImGuiCol.Separator);
        ImGui.GetWindowDrawList().AddLine(
            new Vector2(origin.X, origin.Y),
            new Vector2(origin.X + width, origin.Y),
            color);
        // Cursor still sits on the line; advance so subsequent content doesn't draw on top.
        ImGui.Dummy(new Vector2(0, ImGui.GetStyle().ItemSpacing.Y));
    }

    /// <summary>
    /// Render one or more groups in equal-width side-by-side columns. Each column
    /// is rendered by an <see cref="Action"/>, typically containing one or more
    /// <see cref="Draw"/> calls — that way callers can stack multiple groups in
    /// a single column when needed.
    /// <para>Null entries are skipped (callers can pass <c>condition ? () =&gt; … : null</c>
    /// for optional columns). If only one non-null column remains the table layer
    /// is bypassed so the surviving column gets the full row width instead of
    /// looking visually squeezed.</para>
    /// <para><b>Alignment:</b> every column in a single <c>DrawColumns</c> call starts
    /// at the same y position. If you want two pairs of groups stacked vertically
    /// but with the second pair also aligned at its own top, issue two
    /// <c>DrawColumns</c> calls in sequence rather than stacking two groups inside
    /// one column — the second call's row begins below the tallest column from the
    /// first and re-aligns both sides for the new pair.</para>
    /// </summary>
    /// <param name="id">Stable ImGui id for the underlying table.</param>
    /// <param name="columns">Per-column draw actions. <c>null</c> entries are skipped.</param>
    public static void DrawColumns(string id, params Action?[] columns)
        => DrawGrid(id, columns.Length, columns);

    /// <summary>
    /// Render an arbitrary number of cells in an equal-width grid, wrapping to a
    /// new row every <paramref name="perRow"/> entries. The underlying table is
    /// always declared with <paramref name="perRow"/> columns so widths stay
    /// consistent across rows even when the last row is partially filled.
    /// <para><b>perRow == 1</b> renders each cell stacked beneath the previous —
    /// no table is opened. <b>perRow ≥ 2</b> opens a single table whose rows wrap
    /// after every <paramref name="perRow"/> visible cells.</para>
    /// <para>Null cells are skipped (callers can pass <c>condition ? () =&gt; … : null</c>
    /// for optional positions). If only one non-null cell survives the table layer
    /// is bypassed so it gets the full available width.</para>
    /// <para><b>Alignment:</b> every cell on the same row starts at the same y;
    /// the next row begins below the tallest cell from the previous row.</para>
    /// </summary>
    /// <param name="id">Stable ImGui id for the underlying table.</param>
    /// <param name="perRow">Max cells per row before wrapping. Clamped to ≥ 1.</param>
    /// <param name="cells">Per-cell draw actions. <c>null</c> entries are skipped.</param>
    public static void DrawGrid(string id, int perRow, params Action?[] cells)
    {
        var live = cells.Where(c => c is not null).ToArray();
        if (live.Length == 0) return;
        if (perRow < 1) perRow = 1;

        if (perRow == 1 || live.Length == 1)
        {
            foreach (var c in live) c!();
            return;
        }

        if (!ImGui.BeginTable(id, perRow,
                ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoBordersInBody))
            return;

        for (var i = 0; i < live.Length; i++)
        {
            if (i % perRow == 0) ImGui.TableNextRow();
            ImGui.TableNextColumn();
            live[i]!();
        }

        ImGui.EndTable();
    }
}
