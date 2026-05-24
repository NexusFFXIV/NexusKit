using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace NexusKit.Ui.Widgets;

/// <summary>
/// Right-aligned row of <see cref="NexusIconButton"/>-style slots that
/// computes its own width from the slots that are actually present.
/// Conditional slots (build-config gated, runtime spinner/button swap,
/// etc.) compose without the caller needing to hand-update a button-count
/// constant — drop or add a <see cref="Slot"/> and the right edge tracks.
///
/// Slots also carry their own width, so a spinner narrower than a standard
/// 32px button doesn't reserve a full slot and leave a visible gap.
/// </summary>
public static class NexusIconToolbar
{
    /// <summary>Standard icon-button slot width, matching
    /// <see cref="NexusIconButton"/>'s default size.</summary>
    public const float DefaultSlotWidth = 32f;

    /// <summary>One drawable position in the toolbar. Use <see cref="Button"/>
    /// for the common icon-button case and <see cref="Custom"/> for spinners
    /// or anything else that needs a non-standard width.</summary>
    public readonly struct Slot
    {
        public readonly float Width;
        public readonly Action Draw;

        internal Slot(float width, Action draw) { Width = width; Draw = draw; }

        public static Slot Button(FontAwesomeIcon icon, string tooltip,
                                  Action onClick, bool enabled = true,
                                  float width = DefaultSlotWidth)
            => new(width, () =>
            {
                if (!enabled) ImGui.BeginDisabled();
                NexusIconButton.Draw(icon, tooltip, onClick);
                if (!enabled) ImGui.EndDisabled();
            });

        public static Slot Custom(float width, Action draw) => new(width, draw);
    }

    /// <summary>Draws the slots right-aligned to the remaining content width
    /// on the current line. The first slot is positioned via
    /// <c>SameLine(absoluteX)</c>, so callers typically invoke this directly
    /// after an <c>ImGui.EndGroup()</c> to place the toolbar on the same row
    /// as the group's top line.</summary>
    public static void DrawRightAligned(IReadOnlyList<Slot> slots)
    {
        if (slots.Count == 0) return;

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var totalWidth = 0f;
        for (var i = 0; i < slots.Count; i++)
            totalWidth += slots[i].Width + (i > 0 ? spacing : 0f);

        var avail = ImGui.GetContentRegionAvail().X;
        ImGui.SameLine(ImGui.GetCursorPosX() + avail - totalWidth);

        for (var i = 0; i < slots.Count; i++)
        {
            if (i > 0) ImGui.SameLine();
            slots[i].Draw();
        }
    }

    public static void DrawRightAligned(params Slot[] slots)
        => DrawRightAligned((IReadOnlyList<Slot>)slots);
}
