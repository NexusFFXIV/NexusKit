namespace NexusKit.Ui.Abstractions;

/// <summary>
/// Lookup + open/close facade over every <see cref="NexusWindow"/> the plugin
/// registered with NexusKit.Ui's DI extensions. Lets cross-window glue (e.g. a
/// main-window title-bar button that opens the registered settings) work without
/// each window having to inject every other window it might want to talk to.
/// <para>Resolution is by service type: <see cref="OpenSettings"/> looks up the
/// service registered against <see cref="SettingsWindow"/>, <see cref="Open{T}"/>
/// resolves the requested type directly. Calls are no-ops when the corresponding
/// window isn't registered.</para>
/// </summary>
public interface IWindowManager
{
    /// <summary>Open the registered <see cref="MainWindow"/> if any plugin added one.</summary>
    void OpenMain();

    /// <summary>Open the registered <see cref="SettingsWindow"/> if any plugin added one.
    /// Wired by <see cref="MainWindow"/>'s settings title-bar button.</summary>
    void OpenSettings();

    /// <summary>Open a specific window by type. Use for plugin-defined sub-windows
    /// registered via <c>AddWindow&lt;T&gt;</c>.</summary>
    void Open<T>() where T : NexusWindow;

    /// <summary>Close a window by type. No-op if not registered or already closed.</summary>
    void Close<T>() where T : NexusWindow;

    /// <summary>Flip a window's open state.</summary>
    void Toggle<T>() where T : NexusWindow;

    /// <summary>Return whether the registered window is open. Returns false if the
    /// window isn't registered.</summary>
    bool IsOpen<T>() where T : NexusWindow;

    /// <summary>Retrieve the registered window directly. Returns null when unregistered.</summary>
    T? Get<T>() where T : NexusWindow;

    /// <summary>Resolve the registered window of type <typeparamref name="T"/> and run
    /// <paramref name="action"/> against it on Dalamud's framework thread, so callers
    /// reaching in from a background thread (e.g. a chat-link handler whose work went
    /// async) don't have to marshal themselves. Runs inline when already on the
    /// framework thread; no-op when the window isn't registered.</summary>
    void Invoke<T>(Action<T> action) where T : NexusWindow;
}
