namespace NexusKit.Core;

/// <summary>
/// Pluggable source for "is the user in an active session right now?" —
/// the framework consumes this to drive the <see cref="IPluginLifetime"/>
/// state machine (Idle ↔ Active transitions) without depending on Dalamud
/// directly. Plugins register an adapter that wraps their host's session
/// signal (Dalamud's <c>IClientState</c>, a desktop app's user-login event,
/// etc.) and the framework handles the rest.
/// <para>If no provider is registered, the lifetime defaults to
/// <see cref="PluginLifecycleState.Active"/> immediately after host build
/// — i.e. "always active" semantics for hosts that have no session concept.</para>
/// </summary>
public interface ISessionStateProvider
{
    /// <summary>Snapshot of the current session state. Read once at host
    /// build to set the initial lifecycle state.</summary>
    bool IsActive { get; }

    /// <summary>Fires when the host's session becomes active (user logs
    /// in, becomes available, etc.). Implementations MUST invoke this
    /// after <see cref="IsActive"/> would return <c>true</c>.</summary>
    event Action? Activated;

    /// <summary>Fires when the host's session becomes inactive (user logs
    /// out, character select, etc.). The session can re-activate later —
    /// this event is NOT a shutdown signal.</summary>
    event Action? Deactivated;
}
