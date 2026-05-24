namespace NexusKit.Core.Ipc;

/// <summary>
/// Provider + consumer surface for plugin-to-plugin IPC.
/// <para>
/// All <b>own</b> IPC names are auto-prefixed with the plugin name, yielding the
/// fully-qualified form <c>[PluginName].[subsystem].[member]</c>. Caller supplies
/// only <c>subsystem</c> (typically a module name) and <c>member</c>.
/// </para>
/// <para>
/// <b>Foreign</b> IPCs are addressed by their full registered name, exactly as
/// the publishing plugin chose it (e.g. <c>"Visibility.Disable"</c>).
/// </para>
/// </summary>
public interface IIpcRegistry
{
    /// <summary>
    /// Build the fully-qualified IPC name: <c>[PluginName].[subsystem].[member]</c>.
    /// </summary>
    string BuildName(string subsystem, string member);

    // --- Provider: own IPCs (Func) -----------------------------------------

    IDisposable RegisterFunc<TResult>(string subsystem, string function, Func<TResult> handler);
    IDisposable RegisterFunc<T1, TResult>(string subsystem, string function, Func<T1, TResult> handler);
    IDisposable RegisterFunc<T1, T2, TResult>(string subsystem, string function, Func<T1, T2, TResult> handler);

    // --- Provider: own IPCs (Action) ---------------------------------------

    IDisposable RegisterAction(string subsystem, string action, Action handler);
    IDisposable RegisterAction<T1>(string subsystem, string action, Action<T1> handler);
    IDisposable RegisterAction<T1, T2>(string subsystem, string action, Action<T1, T2> handler);

    // --- Provider: own events (SendMessage) --------------------------------

    void Publish(string subsystem, string eventName);
    void Publish<T1>(string subsystem, string eventName, T1 arg);

    // --- Consumer: foreign IPCs (typed proxy) ------------------------------

    IIpcFunc<TResult> GetFunc<TResult>(string fullName);
    IIpcFunc<T1, TResult> GetFunc<T1, TResult>(string fullName);
    IIpcFunc<T1, T2, TResult> GetFunc<T1, T2, TResult>(string fullName);

    IIpcAction GetAction(string fullName);
    IIpcAction<T1> GetAction<T1>(string fullName);
    IIpcAction<T1, T2> GetAction<T1, T2>(string fullName);

    // --- Consumer: foreign events ------------------------------------------

    IDisposable Subscribe(string fullName, Action handler);
    IDisposable Subscribe<T1>(string fullName, Action<T1> handler);
}
