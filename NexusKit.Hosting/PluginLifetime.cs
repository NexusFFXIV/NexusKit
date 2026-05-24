using NexusKit.Core;

namespace NexusKit.Hosting;

/// <summary>
/// Default <see cref="IPluginLifetime"/> backed by a single
/// <see cref="CancellationTokenSource"/> + a lifecycle state machine.
/// The host registers this as a singleton during
/// <see cref="PluginHostBuilder.BuildAsync"/>; the application then drives
/// the state machine via <see cref="NotifyReady"/> /
/// <see cref="NotifyLoggedIn"/> / <see cref="NotifyLoggedOut"/> and
/// <see cref="RequestStop"/> as the very first shutdown step — before any
/// other service's <c>Dispose</c> runs and well before the DI container
/// disposes the DbContextFactory.
/// </summary>
public sealed class PluginLifetime : IPluginLifetime, IDisposable
{
    private readonly CancellationTokenSource mCts;
    private readonly object mLock = new();
    private PluginLifecycleState mState = PluginLifecycleState.Initializing;
    private bool mDisposed;

    /// <summary>Build the lifetime token. <paramref name="externalToken"/>
    /// is the cancellation token passed into <c>PluginHostBuilder.BuildAsync</c>
    /// — it ALREADY represents the plugin's lifecycle (Dalamud / the host
    /// signals it on unload), so we link our internal CTS to it. Either
    /// path — external cancel or our own <see cref="RequestStop"/> from
    /// <c>PluginHost.Dispose</c> — flips <see cref="Stopping"/>.</summary>
    public PluginLifetime(CancellationToken externalToken = default)
    {
        mCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        // If the external token is cancelled WITHOUT RequestStop being
        // called first (e.g. Dalamud aborts BuildAsync mid-flight), run
        // the state machine through Stopping → Stopped just like the
        // normal path so subscribers receive every transition. The CT is
        // already cancelled by the time this fires, so an EF write inside
        // a Stopping handler that uses LifetimeToken will throw OCE — but
        // the event firing consistently is more important than the write
        // succeeding: subscribers shouldn't have to special-case which
        // path they were torn down from. In the regular RequestStop path
        // these calls become no-ops (state is already Stopped, SetState
        // guards return early), so this is safe to do unconditionally.
        mCts.Token.Register(() =>
        {
            SetState(PluginLifecycleState.Stopping);
            SetState(PluginLifecycleState.Stopped);
        });
    }

    public CancellationToken Stopping => mCts.Token;
    public bool IsStopping => mCts.IsCancellationRequested;
    public PluginLifecycleState State { get { lock (mLock) return mState; } }
    public event Action<PluginLifecycleState>? StateChanged;

    /// <summary>Move out of <see cref="PluginLifecycleState.Initializing"/>.
    /// Called once after host build completes, with whatever the current
    /// game-session state is at that moment. After this, the state machine
    /// is driven by <see cref="NotifyLoggedIn"/> / <see cref="NotifyLoggedOut"/>.</summary>
    public void NotifyReady(bool initiallyLoggedIn)
        => SetState(initiallyLoggedIn ? PluginLifecycleState.Active : PluginLifecycleState.Idle);

    /// <summary>Signal that the user has logged in to a character. Idempotent.</summary>
    public void NotifyLoggedIn() => SetState(PluginLifecycleState.Active);

    /// <summary>Signal that the user has logged out (returned to character
    /// select / title screen) WITHOUT the plugin unloading. Idempotent.
    /// Does NOT cancel <see cref="Stopping"/> — background workers stay
    /// alive across login transitions.</summary>
    public void NotifyLoggedOut() => SetState(PluginLifecycleState.Idle);

    public void RequestStop()
    {
        // Two-phase shutdown:
        //   1. Stopping — DI alive, CT still active. Subscribers do their
        //      last synchronous writes here. SetState fires StateChanged
        //      synchronously, so we block until every subscriber has
        //      returned before continuing.
        //   2. Stopped — CT cancels, in-flight async work bails. The DI
        //      teardown that follows is now safe: no service is mid-write.
        // Doing both transitions inside RequestStop keeps the contract
        // single-call from the host's perspective.
        SetState(PluginLifecycleState.Stopping);
        SetState(PluginLifecycleState.Stopped);
        try { mCts.Cancel(); }
        catch (ObjectDisposedException) { /* already torn down */ }
    }

    private void SetState(PluginLifecycleState target)
    {
        Action<PluginLifecycleState>? handler;
        lock (mLock)
        {
            if (mState == target) return;
            // Terminal — never transition out of Stopped, even if late
            // Logout events arrive during teardown.
            if (mState == PluginLifecycleState.Stopped) return;
            // Once in Stopping, the only valid next state is Stopped — a
            // late Login/Logout event reaching us during the shutdown
            // window must not rewind the state machine back to Idle/Active.
            if (mState == PluginLifecycleState.Stopping && target != PluginLifecycleState.Stopped) return;
            mState = target;
            handler = StateChanged;
        }
        // Subscriber callbacks intentionally fire outside the lock so a
        // slow / blocking handler can't stall other state queries. The
        // try/catch keeps a buggy subscriber from poisoning the rest.
        if (handler is null) return;
        foreach (var sub in handler.GetInvocationList())
        {
            try { ((Action<PluginLifecycleState>)sub).Invoke(target); }
            catch { /* swallow — bad subscriber shouldn't break shutdown */ }
        }
    }

    public void Dispose()
    {
        if (mDisposed) return;
        mDisposed = true;
        try { mCts.Dispose(); } catch { /* swallow — final teardown */ }
    }
}
