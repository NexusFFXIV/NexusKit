using System.Numerics;

namespace NexusKit.Core.Actions;

/// <summary>
/// Optional UI render hint produced by an adapter / service method that
/// the UI layer can apply to make a button look distinct based on what
/// the action would actually do at this moment. The hint is purely
/// presentational — the action's <c>Try…</c> method still owns the
/// actual side-effect.
/// <para>Intended use: an adapter exposes a <c>Preview&lt;X&gt;(…)</c>
/// method alongside its <c>Try&lt;X&gt;(…)</c> method (same arguments).
/// Preview returns <c>null</c> when the action can't run, or a hint
/// describing which "variant" the call would follow. UI callers query
/// the preview, render the button with the optional tint + tooltip key,
/// and call the matching <c>Try…</c> on click. The variant identifier
/// itself stays opaque to the UI — UI code reads the user-facing
/// fields (<see cref="TooltipKey"/>, <see cref="AccentColor"/>) and
/// leaves adapter internals alone.</para>
/// </summary>
/// <param name="Variant">Adapter-defined variant identifier, opaque to
/// the UI layer. Useful for telemetry / test assertions ("did the
/// preview choose the cross-DC branch?") but should not be parsed by
/// generic UI code.</param>
/// <param name="TooltipKey">Optional localization key for a
/// variant-specific tooltip. When set, the UI should prefer this over
/// any fallback. When <see cref="CanExecute"/> is <c>false</c> the
/// adapter can point this at a key that explains why — same field, two
/// jobs, depending on state.</param>
/// <param name="AccentColor">Optional accent color (RGBA in 0..1).
/// Lives in <see cref="System.Numerics.Vector4"/> so this record
/// stays Dalamud-free. UI consumers typically push it into
/// <c>ImGuiCol.Text</c> so the icon glyph picks up the variant color
/// while the button frame stays visually consistent with neighbouring
/// buttons. Disabled state still dims the tinted text via the global
/// disabled alpha, so the readability stays intact.</param>
/// <param name="CanExecute">Whether the action can run RIGHT NOW. Adds a
/// third "visible but disabled" state to the two-state hide/show model:
/// <list type="bullet">
/// <item><c>null</c> hint → hide the slot entirely; the adapter has no
/// opinion.</item>
/// <item>non-null hint, <c>CanExecute = true</c> → render normally with
/// the tint + tooltip.</item>
/// <item>non-null hint, <c>CanExecute = false</c> → render greyed out
/// (the UI helper wraps the draw in <c>BeginDisabled</c>); use
/// <see cref="TooltipKey"/> to explain why. Useful when the slot
/// should stay visible so the user knows the action exists — e.g.
/// "FC estate address unknown, can't travel yet" beats silently
/// dropping the button.</item>
/// </list>
/// Default <c>true</c> so existing callers / adapters that don't care
/// about disabled states stay correct.</param>
/// <param name="DetailText">Optional raw (non-localized) detail line the
/// UI appends to the tooltip on a second line. Use this for dynamic,
/// literal strings where translation doesn't apply — e.g. the actual
/// command an IPC adapter is about to dispatch (<c>"/li Mist 17 15"</c>).
/// <see cref="TooltipKey"/> still resolves the localizable first line;
/// <see cref="DetailText"/> is concatenated after a single <c>\n</c>.
/// <c>null</c> when there's no detail worth showing.</param>
public sealed record ActionRenderHint(
    string Variant,
    string? TooltipKey = null,
    Vector4? AccentColor = null,
    bool CanExecute = true,
    string? DetailText = null);
