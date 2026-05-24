using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace NexusKit.Ui.Widgets;

/// <summary>
/// Column descriptor for <see cref="NexusTable"/>. Fixed-width columns get an
/// explicit pixel <paramref name="Width"/>; columns with <c>Width &lt;= 0</c>
/// stretch to fill the remaining horizontal space. Use <paramref name="Flags"/>
/// to add extras like <c>NoHide</c>, <c>DefaultSort</c>, …
/// </summary>
public sealed record NexusTableColumn(
    string Header,
    float Width = 0,
    ImGuiTableColumnFlags Flags = ImGuiTableColumnFlags.None);

/// <summary>
/// A pre-materialised table row. Each entry in <paramref name="Cells"/> is
/// one ImGui-drawing delegate, positionally aligned with the table's
/// column list — index <c>n</c> in the row matches index <c>n</c> in the
/// columns passed to the table widget. Rows with fewer cells than there are
/// columns render the missing ones as empty; rows with more cells than
/// columns simply ignore the extras.
/// <para>The widget calls <see cref="ImGui.TableNextColumn"/> before each
/// delegate, so the body can call any ImGui widget directly. Callers never
/// touch <see cref="ImGui.TableNextRow"/> or <see cref="ImGui.TableNextColumn"/>
/// — that's the widget's job.</para>
/// </summary>
/// <param name="Cells">Per-column draw actions in column order.</param>
public sealed record NexusTableRow(IReadOnlyList<Action> Cells);

/// <summary>
/// Thin convention layer on top of <see cref="ImGui.BeginTable"/> — sets up the
/// columns from a list of <see cref="NexusTableColumn"/>, optionally renders a
/// header row, and iterates the data through <see cref="NexusListClipper"/> so
/// thousands of rows stay cheap.
/// <para>The row callback is responsible for calling
/// <see cref="ImGui.TableNextColumn"/> before each cell — the helper handles the
/// row boundary (<c>TableNextRow</c>) for you.</para>
/// </summary>
public static class NexusTable
{
    /// <summary>Default flags — striped rows, full borders, proportional sizing,
    /// plus user-driven column resize / reorder / hide. ImGui persists those
    /// per-table tweaks through its own .ini machinery (the <c>NoSavedSettings</c>
    /// flag is intentionally omitted), keyed by the table's string id; reloading
    /// the plugin keeps each user's chosen layout. Right-click any header to
    /// toggle individual column visibility.</summary>
    public const ImGuiTableFlags DefaultFlags =
        ImGuiTableFlags.RowBg |
        ImGuiTableFlags.Borders |
        ImGuiTableFlags.SizingStretchProp |
        ImGuiTableFlags.Resizable |
        ImGuiTableFlags.Reorderable |
        ImGuiTableFlags.Hideable;

    public static void Draw<T>(
        string id,
        IReadOnlyList<NexusTableColumn> columns,
        IReadOnlyList<T> rows,
        Action<T> drawRow,
        bool showHeader = true,
        float rowHeight = 22f,
        ImGuiTableFlags flags = DefaultFlags)
    {
        if (columns.Count == 0) return;
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

            if (showHeader)
                ImGui.TableHeadersRow();

            NexusListClipper.ForEach(rows, rowHeight, (_, row) =>
            {
                ImGui.TableNextRow();
                drawRow(row);
            });
        }
        finally
        {
            ImGui.EndTable();
        }
    }

    /// <summary>
    /// Renders a single text cell that gracefully handles narrow columns: if the
    /// rendered text is wider than the cell's available width (cells are clipped
    /// by the table, so the user-visible text gets cut off mid-character
    /// otherwise), the helper shows a hover tooltip with the full string. Use
    /// this for free-form fields whose width the user is likely to shrink
    /// (item names, glamour, materia, creator name, …); short columns with
    /// known-narrow content (level numbers, slot labels) don't need it.
    /// <para>The optional <paramref name="color"/> tints the text without
    /// affecting the truncation check.</para>
    /// </summary>
    public static void CellText(string text, Vector4? color = null)
    {
        if (color is { } c) ImGui.TextColored(c, text);
        else                ImGui.TextUnformatted(text);

        // Measure against the cell's REMAINING space after the text was drawn —
        // GetContentRegionAvail.X inside a cell returns the cell's full visible
        // width; CalcTextSize.X is the un-clipped text width. ItemRectSize on
        // the rendered text returns the clipped extent and would always equal
        // the cell width here, so it's not useful for the comparison.
        var textWidth = ImGui.CalcTextSize(text).X;
        var cellWidth = ImGui.GetColumnWidth();
        if (textWidth > cellWidth && ImGui.IsItemHovered())
            ImGui.SetTooltip(text);
    }
}
