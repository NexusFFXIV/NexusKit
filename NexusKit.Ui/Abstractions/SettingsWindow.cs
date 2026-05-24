using Dalamud.Bindings.ImGui;
using NexusKit.Core.Localization;
using NexusKit.Persistence.Settings;

namespace NexusKit.Ui.Abstractions;

/// <summary>
/// Plugin settings window. Persists position and size like every
/// <see cref="NexusWindow"/>; the <c>restoreOpenState</c> ctor flag controls whether
/// it also reopens at startup if it was open before. Defaults to <c>true</c> so a
/// plugin-defined settings window remembers its state; the framework's
/// <see cref="NexusKit.Ui.AutoSettings.AutoSettingsWindow"/> overrides this to <c>false</c>
/// because it's only meant to be opened on demand via Dalamud's config-ui hook.
/// </summary>
public abstract class SettingsWindow : NexusWindow
{
    protected SettingsWindow(
        string name,
        ISettingsStore store,
        ImGuiWindowFlags flags = ImGuiWindowFlags.None,
        bool restoreOpenState = true,
        ILocalizer? localizer = null)
        : base(name, store, flags, restoreOpenState, localizer: localizer)
    {
    }
}
