using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using NexusKit.Core.Localization;
using NexusKit.Persistence.Settings;

namespace NexusKit.Ui.Abstractions;

/// <summary>
/// <see cref="Window"/> base class that persists the <see cref="Window.IsOpen"/> state
/// across plugin sessions. Position and size are deliberately not managed here —
/// Dalamud's ImGui layer already persists them via <c>imgui.ini</c> as long as the
/// concrete window sets <see cref="Window.SizeCondition"/> / <see cref="Window.PositionCondition"/>
/// to a non-restrictive condition (e.g. <see cref="ImGuiCond.FirstUseEver"/>).
/// <para>Opt-in via <c>restoreOpenState: true</c>: the window reopens at startup if it
/// was open at last shutdown. Disabled windows leave <see cref="Window.IsOpen"/>
/// untouched and only open via Dalamud's UI hooks.</para>
/// <para>Cross-window features that any concrete window can opt into via ctor flags:
/// pass <c>windowManager</c> + <c>showSettingsButton: true</c> to attach a cog
/// button to the title bar that opens the registered <see cref="SettingsWindow"/>
/// through the injected <see cref="IWindowManager"/>. Default is off so
/// settings/sub-windows don't accidentally render a cog on themselves.</para>
/// </summary>
public abstract class NexusWindow : Window, IDisposable
{
    private readonly ISettingsStore mStore;
    private readonly string mStateKey;
    private readonly bool mRestoreOpenState;
    private bool mLastSavedOpen;

    /// <summary>The plugin's localizer if one was injected, otherwise null. Subclasses
    /// should prefer <see cref="L(string)"/> which falls through to the literal key when
    /// the localizer is missing — works in tests / minimal plugin set-ups too.</summary>
    protected ILocalizer? Localizer { get; }

    protected NexusWindow(
        string name,
        ISettingsStore store,
        ImGuiWindowFlags flags = ImGuiWindowFlags.None,
        bool restoreOpenState = false,
        IWindowManager? windowManager = null,
        bool showSettingsButton = false,
        ILocalizer? localizer = null)
        : base(name, flags)
    {
        mStore = store;
        Localizer = localizer;
        var sanitized = SanitizeKey(name);
        mStateKey = $"ui.window.{sanitized}.is_open";
        mRestoreOpenState = restoreOpenState;

        if (mRestoreOpenState)
        {
            try
            {
                IsOpen = mStore.GetAsync<bool>(mStateKey).GetAwaiter().GetResult();
            }
            catch
            {
                // Best-effort restore — fall back to the default (closed).
            }
        }

        // One-shot cleanup of the pre-simplification record (a WindowState struct stored
        // under the un-suffixed key, with bogus null geometry). Fire-and-forget; safe to
        // call even when nothing's stored.
        _ = mStore.DeleteAsync($"ui.window.{sanitized}");

        mLastSavedOpen = IsOpen;

        if (showSettingsButton)
        {
            if (windowManager is null)
                throw new ArgumentException(
                    "showSettingsButton requires a non-null windowManager — register IWindowManager via AddNexusKitUi() and forward it.",
                    nameof(windowManager));
            AddSettingsTitleBarButton(windowManager.OpenSettings);
        }
    }

    public override void OnOpen()
    {
        base.OnOpen();
        PersistOpenStateIfChanged();
    }

    public override void OnClose()
    {
        base.OnClose();
        PersistOpenStateIfChanged();
    }

    public virtual void Dispose() => PersistOpenStateIfChanged(synchronous: true);

    /// <summary>Convenience accessor: localized text for the given resource key, or
    /// the key itself when no <see cref="ILocalizer"/> was injected / no entry exists.
    /// Keeps call sites short — typical use: <c>ImGui.TextUnformatted(L("ui.tab.summary"))</c>.</summary>
    protected string L(string key)
        => Localizer is { } loc && loc.TryGet(key, out var text) ? text : key;

    private void PersistOpenStateIfChanged(bool synchronous = false)
    {
        if (!mRestoreOpenState) return;
        if (mLastSavedOpen == IsOpen) return;
        mLastSavedOpen = IsOpen;

        if (synchronous)
        {
            try { mStore.SetAsync(mStateKey, IsOpen).GetAwaiter().GetResult(); }
            catch { /* best-effort during shutdown */ }
        }
        else
        {
            _ = mStore.SetAsync(mStateKey, IsOpen);
        }
    }

    private void AddSettingsTitleBarButton(Action onClick)
    {
        TitleBarButtons.Add(new TitleBarButton
        {
            Icon = FontAwesomeIcon.Cog,
            // FA glyphs draw from their baseline, so the Cog sits in the top-left of the
            // button cell without nudging. (2,1) lands it visually centred against
            // Dalamud's close-button geometry; IconOffset is font-scaled automatically.
            IconOffset = new Vector2(2, 1),
            // Restrict to left-click — middle/right shouldn't trigger the action.
            Click = btn => { if (btn == ImGuiMouseButton.Left) onClick(); },
            ShowTooltip = () =>
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted("Settings");
                ImGui.EndTooltip();
            },
        });
    }

    private static string SanitizeKey(string name)
    {
        var idx = name.IndexOf("###", StringComparison.Ordinal);
        var clean = idx >= 0 ? name[..idx] : name;
        var chars = clean.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_').ToArray();
        return new string(chars).Trim('_');
    }
}
