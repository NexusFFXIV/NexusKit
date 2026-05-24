using NexusKit.Core;

namespace NexusKit.Hosting;

/// <summary>
/// Bridges an <see cref="ISessionStateProvider"/> (registered by the
/// hosting application, typically wrapping a platform-specific session
/// signal like Dalamud's <c>IClientState</c>) to the framework-owned
/// <see cref="PluginLifetime"/>. Eagerly resolved by
/// <see cref="PluginHostBuilder"/> right after the DI container builds so
/// the lifecycle state is correct before any other service starts using it.
/// <para>If no <see cref="ISessionStateProvider"/> is in DI, the host
/// instead falls back to <c>NotifyReady(true)</c> — see
/// <see cref="PluginHostBuilder"/>. This class is only constructed when
/// a provider is registered.</para>
/// </summary>
internal sealed class LifetimeBridge : IDisposable
{
    private readonly ISessionStateProvider mSession;
    private readonly PluginLifetime mLifetime;
    private readonly Action mOnActivated;
    private readonly Action mOnDeactivated;
    private bool mDisposed;

    public LifetimeBridge(ISessionStateProvider session, PluginLifetime lifetime)
    {
        mSession = session;
        mLifetime = lifetime;
        // Snapshot first, then subscribe — if we subscribed first an
        // Activated event firing between subscription and NotifyReady would
        // be swallowed by the state-machine's "skip transitions out of
        // Initializing if target is already Active" idempotence (harmless
        // here, but the ordering keeps the semantics obvious).
        mLifetime.NotifyReady(session.IsActive);
        mOnActivated = mLifetime.NotifyLoggedIn;
        mOnDeactivated = mLifetime.NotifyLoggedOut;
        session.Activated += mOnActivated;
        session.Deactivated += mOnDeactivated;
    }

    public void Dispose()
    {
        if (mDisposed) return;
        mDisposed = true;
        mSession.Activated -= mOnActivated;
        mSession.Deactivated -= mOnDeactivated;
    }
}
