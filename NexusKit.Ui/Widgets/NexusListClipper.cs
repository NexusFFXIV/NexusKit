using Dalamud.Bindings.ImGui;

namespace NexusKit.Ui.Widgets;

/// <summary>
/// Virtualized iteration over a list, backed by <c>ImGuiListClipper</c>. Off-screen
/// rows are skipped, so this scales to thousands of entries without per-frame cost.
/// Use the <see cref="ForEach{T}"/> callback form — the helper handles the native
/// clipper's allocation and disposal so callers can't leak it.
/// <para>The clipper must know each row's height in advance for its layout
/// calculation; pass <c>rowHeight</c> in unscaled pixels.</para>
/// <para>Pattern lifted from PlayerNexus / PlayerTrack which both use the same
/// <c>ImGuiNative.ImGuiListClipper()</c> factory — note the function symbol is just
/// <c>ImGuiListClipper()</c>, NOT the doubled <c>ImGuiListClipper_ImGuiListClipper</c>
/// variant other ImGui bindings expose. A default-constructed
/// <see cref="ImGuiListClipperPtr"/> wraps a null pointer and crashes inside Begin().</para>
/// </summary>
public static unsafe class NexusListClipper
{
    public static void ForEach<T>(IReadOnlyList<T> items, float rowHeight, Action<int, T> draw)
    {
        if (items.Count == 0) return;

        var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper());
        try
        {
            clipper.Begin(items.Count, rowHeight);
            while (clipper.Step())
            {
                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                    draw(i, items[i]);
            }
        }
        finally
        {
            // Destroy() also calls End() — equivalent to End() + native destroy.
            clipper.Destroy();
        }
    }
}
