using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using NexusKit.Core.Localization;
using NexusKit.Ui.Abstractions;

namespace NexusKit.Ui;

public sealed class PluginUiHost : IDisposable
{
    private readonly IDalamudPluginInterface mPluginInterface;
    private readonly WindowSystem mWindowSystem;
    private readonly LocalizationManager mLocalization;
    private readonly IWindowManager mWindowManager;
    private readonly IReadOnlyList<NexusWindow> mAllWindows;

    public PluginUiHost(
        IDalamudPluginInterface pluginInterface,
        WindowSystem windowSystem,
        LocalizationManager localization,
        IEnumerable<NexusWindow> windows,
        IWindowManager windowManager)
    {
        mPluginInterface = pluginInterface;
        mWindowSystem = windowSystem;
        mLocalization = localization;
        mWindowManager = windowManager;
        mAllWindows = windows.ToList();

        foreach (var w in mAllWindows)
            mWindowSystem.AddWindow(w);

        mPluginInterface.UiBuilder.Draw += mWindowSystem.Draw;
        mPluginInterface.UiBuilder.OpenMainUi += OpenMain;
        mPluginInterface.UiBuilder.OpenConfigUi += OpenConfig;

        // Bridge Dalamud's UI language into the (Dalamud-free) LocalizationManager.
        // ReportHostCulture only applies when no explicit Override is set — so a
        // plugin/user choice in the settings UI keeps overruling Dalamud's language.
        mLocalization.ReportHostCulture(mPluginInterface.UiLanguage);
        mPluginInterface.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged(string langCode) => mLocalization.ReportHostCulture(langCode);

    public void OpenMain() => mWindowManager.OpenMain();

    public void OpenConfig() => mWindowManager.OpenSettings();

    public void Dispose()
    {
        mPluginInterface.UiBuilder.Draw -= mWindowSystem.Draw;
        mPluginInterface.UiBuilder.OpenMainUi -= OpenMain;
        mPluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;
        mPluginInterface.LanguageChanged -= OnLanguageChanged;
        foreach (var w in mAllWindows)
            w.Dispose();
        mWindowSystem.RemoveAllWindows();
    }
}
