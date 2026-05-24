using Dalamud.Bindings.ImGui;

namespace NexusKit.Ui.Widgets;

/// <summary>One titled group of rows for <see cref="NexusGroupedTable"/>.
/// The widget renders <paramref name="Label"/> as a collapsible header row
/// that spans every column of the underlying table; when open, each row's
/// per-cell delegates fire in column order. The row shape is the shared
/// <see cref="NexusTableRow"/> so the same row materialisation pattern
/// works for both <see cref="NexusTable"/> and <see cref="NexusGroupedTable"/>.
/// <para><paramref name="Id"/> must be unique within the same table — it
/// feeds the ImGui ID stack so per-group expand/collapse state persists
/// independently across frames.</para></summary>
public sealed record NexusGroupedTableGroup(
    string Id,
    string Label,
    IReadOnlyList<NexusTableRow> Rows);

/// <summary>
/// A grouped variant of <see cref="NexusTable"/>: renders multiple labelled
/// row groups inside a <b>single</b> underlying ImGui table so column widths
/// stay perfectly aligned across groups regardless of which ones are
/// expanded. Each group header is a tree-node row that spans every column;
/// expanding it reveals the group's rows.
/// <para>Use this instead of stacking N <see cref="NexusTable"/> calls
/// under N <see cref="ImGui.CollapsingHeader"/> calls — that approach gives
/// each group its own ImGui table id and therefore its own column-resize
/// state, leading to visible misalignment between groups and combo-popup
/// overflow when the inner widgets size themselves against per-table widths.</para>
/// </summary>
public static class NexusGroupedTable
{
    /// <summary>Sensible defaults for a settings-style grouped table —
    /// stretch columns with EQUAL default weights, no body borders (the
    /// tree-node row already separates groups visually).
    /// <para>The "Same" variant is critical here: <c>SizingStretchProp</c>
    /// makes default weights proportional to each column's content width,
    /// which breaks cells whose inner widget is sized to fill the cell
    /// (e.g. a combo with <c>SetNextItemWidth(GetContentRegionAvail().X)</c>).
    /// The combo fills the cell → ImGui records that as the column's
    /// "content width" → the column's weight grows → the combo fills the
    /// now-larger cell → other columns are squeezed to almost nothing.
    /// <c>SizingStretchSame</c> short-circuits the loop by ignoring
    /// content widths and giving every column equal weight unless an
    /// explicit weight is passed to <see cref="ImGui.TableSetupColumn"/>.</para></summary>
    public const ImGuiTableFlags DefaultFlags =
        ImGuiTableFlags.SizingStretchSame |
        ImGuiTableFlags.NoBordersInBody;

    /// <summary>Renders <paramref name="groups"/> as a single table with
    /// collapsible group-header rows. The widget owns row + column
    /// advancement: each row carries one ImGui-drawing delegate per
    /// column, and the widget invokes them in order with the cell already
    /// positioned. Callers never call <see cref="ImGui.TableNextRow"/> or
    /// <see cref="ImGui.TableNextColumn"/> themselves.</summary>
    /// <param name="id">Stable ImGui id for the underlying table.</param>
    /// <param name="columns">Column descriptors — same shape as the
    /// non-grouped <see cref="NexusTable"/>.</param>
    /// <param name="groups">Groups to render in order. Empty groups are
    /// shown as a header-only row; groups whose row carries fewer cells
    /// than there are columns render the missing columns as empty cells.</param>
    /// <param name="defaultOpen">Initial open state for groups whose
    /// state ImGui hasn't yet persisted. Subsequent frames respect the
    /// user's toggle.</param>
    /// <param name="showHeader">When true, the column headers from
    /// <paramref name="columns"/> are rendered. Defaults to false for the
    /// settings-style use case where the controls themselves are the cue.</param>
    /// <param name="flags">Override the default table flags. Resizable /
    /// Reorderable / Hideable are deliberately omitted from the defaults so
    /// the user can't accidentally desynchronise column widths across
    /// groups (although since groups share one table now, that risk is gone
    /// — turn them on if you want them).</param>
    public static void Draw(
        string id,
        IReadOnlyList<NexusTableColumn> columns,
        IReadOnlyList<NexusGroupedTableGroup> groups,
        bool defaultOpen = true,
        bool showHeader = false,
        ImGuiTableFlags flags = DefaultFlags)
    {
        if (columns.Count == 0 || groups.Count == 0) return;
        if (!ImGui.BeginTable(id, columns.Count, flags)) return;

        try
        {
            foreach (var col in columns)
            {
                var sizeFlag = col.Width > 0f
                    ? ImGuiTableColumnFlags.WidthFixed
                    : ImGuiTableColumnFlags.WidthStretch;
                ImGui.TableSetupColumn(col.Header, sizeFlag | col.Flags, col.Width);
            }
            if (showHeader) ImGui.TableHeadersRow();

            foreach (var group in groups)
            {
                var stateKey = id + "::" + group.Id;
                var open = sOpenState.TryGetValue(stateKey, out var v) ? v : defaultOpen;

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                // Selectable as the header row: SpanAllColumns makes the
                // hit-area cover every column so the user can click anywhere
                // on the row to toggle. ImGuiSelectableFlags.SpanAllColumns
                // has been in upstream ImGui since 1.85 and is reliably
                // present in the Dalamud binding (unlike the
                // ImGuiTreeNodeFlags variant which is much newer).
                var arrow = open ? "▼ " : "▶ ";
                if (ImGui.Selectable(
                        arrow + group.Label + "##nx_grp_" + group.Id,
                        selected: false,
                        flags: ImGuiSelectableFlags.SpanAllColumns))
                {
                    open = !open;
                }
                sOpenState[stateKey] = open;

                if (!open) continue;

                // Indent the FIRST column of each in-group row so rows
                // visually attach to their header — matches the TreeNode
                // affordance the user expects from an expand/collapse list.
                // We only shift column 0's cursor so the remaining columns
                // (typically combo controls in settings UIs) keep their
                // full width and right-edge alignment with the table.
                var indent = ImGui.GetStyle().IndentSpacing;

                foreach (var row in group.Rows)
                {
                    ImGui.TableNextRow();
                    var n = Math.Min(columns.Count, row.Cells.Count);
                    for (var c = 0; c < n; c++)
                    {
                        ImGui.TableNextColumn();
                        if (c == 0)
                            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + indent);
                        row.Cells[c]?.Invoke();
                    }
                }
            }
        }
        finally
        {
            ImGui.EndTable();
        }
    }

    /// <summary>Per-(table id, group id) expand/closed state. Kept here
    /// rather than per-instance because the widget is a stateless static —
    /// the keys include the caller's table id so multiple grouped tables
    /// in the same window don't collide. Survives for the lifetime of the
    /// AppDomain; in practice this matches "until the plugin reloads",
    /// which is the same persistence ImGui's own CollapsingHeader provides.</summary>
    private static readonly Dictionary<string, bool> sOpenState =
        new(StringComparer.Ordinal);
}
