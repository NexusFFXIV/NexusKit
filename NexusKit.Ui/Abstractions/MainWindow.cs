using Dalamud.Bindings.ImGui;
using NexusKit.Core.Localization;
using NexusKit.Persistence.Settings;

namespace NexusKit.Ui.Abstractions;

/// <summary>
/// Plugin main window. Persists position and size like every <see cref="NexusWindow"/>;
/// the <c>restoreOpenState</c> ctor flag controls whether it also reopens at startup if
/// it was open before (default <c>true</c> — the typical "pick up where I left off" behavior).
/// <para>The cog-button-to-open-settings feature lives on the <see cref="NexusWindow"/>
/// base; opt in by passing <c>windowManager</c> + <c>showSettingsButton: true</c>.</para>
/// </summary>
public abstract class MainWindow : NexusWindow
{
    protected MainWindow(
        string name,
        ISettingsStore store,
        IWindowManager? windowManager = null,
        ImGuiWindowFlags flags = ImGuiWindowFlags.None,
        bool restoreOpenState = true,
        bool showSettingsButton = false,
        ILocalizer? localizer = null)
        : base(name, store, flags, restoreOpenState, windowManager, showSettingsButton, localizer)
    {
    }
}
