using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace NexusKit.Ui.Widgets;

/// <summary>
/// Padded, optionally bordered child region — the basic visual container used by
/// most panels and tab contents. Use inside a <c>using</c> block so the matching
/// <c>EndChild</c> and style-var pop are guaranteed even on early returns or
/// exceptions:
/// <code>
/// using (NexusCard.Begin("##profile", new Vector2(0, 0)))
/// {
///     ImGui.TextUnformatted("Profile");
///     // …
/// }
/// </code>
/// </summary>
public static class NexusCard
{
    /// <summary>Open a card child region. The returned scope MUST be disposed
    /// (handled automatically by a <c>using</c> declaration).</summary>
    /// <param name="id">ImGui id (typically starts with <c>##</c>).</param>
    /// <param name="size">Child size — <c>Vector2.Zero</c> means fill available.</param>
    /// <param name="border">Draw the framed border around the card.</param>
    /// <param name="padding">Inner padding in unscaled pixels (default 10).</param>
    public static Scope Begin(string id, Vector2 size = default, bool border = true, float padding = 10f)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(padding, padding));
        ImGui.BeginChild(id, size, border);
        return default;
    }

    /// <summary>Disposable handle returned by <see cref="Begin"/>.</summary>
    public readonly struct Scope : IDisposable
    {
        public void Dispose()
        {
            ImGui.EndChild();
            ImGui.PopStyleVar();
        }
    }
}
