using NexusKit.Core;
using NexusKit.Core.Logging;

namespace NexusKit.Hosting;

/// <summary>
/// Default <see cref="IPluginLifetime"/> backed by a single
/// <see cref="CancellationTokenSource"/> + a lifecycle state machine.
/// The host registers this as a singleton during
/// <see cref="PluginHostBuilder.BuildAsync"/>; the application then drives
/// the state machine via <see cref="NotifyReady"/> /
/// <see cref="NotifyLoggedIn"/> / <see cref="NotifyLoggedOut"/> and
/// <see cref="RequestStop()"/> as the very first shutdown step — before any
/// other service's <c>Dispose</c> runs and well before the DI container
/// disposes the DbContextFactory.
/// </summary>
public sealed class PluginLifetime : IPluginLifetime, IDisposable
{
    // Deliberately NOT a linked token source. See the ctor doc for the
    // regression that linking caused.
    private readonly CancellationTokenSource mCts = new();
    private readonly IPluginLogSink? mLog;
    private readonly CancellationTokenRegistration mExternalReg;
    private readonly object mLock = new();
    private PluginLifecycleState mState = PluginLifecycleState.Initializing;
    private bool mDisposed;

    /// <summary>Build the lifetime token.</summary>
    /// <param name="shutdownSignal">A token that GENUINELY represents host
    /// shutdown. Optional — the normal path is <see cref="RequestStop()"/>
    /// from <c>PluginHost.Dispose</c>/<c>DisposeAsync</c>, which is what
    /// Dalamud calls on unload.
    /// <para><b>Do NOT pass Dalamud's <c>IAsyncDalamudPlugin.LoadAsync</c> /
    /// <c>PluginHostBuilder.BuildAsync</c> token here.</b> Dalamud's
    /// <c>LocalPlugin.LoadAsync</c> fabricates its own
    /// <c>CancellationTokenSource</c> with <c>CancelAfter(60s)</c> whenever the
    /// caller supplies no token — and no call site supplies one. That token is
    /// therefore a LOAD TIMEOUT: it cancels 60 seconds after the load begins
    /// whether the load succeeded or not, and nothing disposes it. This class
    /// used to link its CTS to it, which made every lifetime-gated service
    /// (observation persistence, encounter tracking, history, the refresh-queue
    /// worker, DB maintenance) go permanently dark one minute into every
    /// session while the plugin carried on running.</para></param>
    /// <param name="log">Optional sink for the lifecycle transitions. Shutdown
    /// transitions log at Warning so a lifetime cancel can never again be
    /// silent — see <see cref="SetState"/>.</param>
    public PluginLifetime(CancellationToken shutdownSignal = default, IPluginLogSink? log = null)
    {
        mLog = log;
        // Register, don't link. Funnelling the external path through
        // RequestStop gives it the SAME two-phase semantics as the dispose
        // path: Stopping fires while mCts is still live (the last-chance
        // synchronous write window subscribers rely on), and only then does
        // Stopped cancel it. Linking inverted that — the token was already
        // dead by the time Stopping fired, so every subscriber's final write
        // bailed at its own IsStopping guard.
        // NOTE: the callback runs on the canceller's thread and StateChanged
        // handlers are synchronous, so whoever cancels shutdownSignal must
        // expect to block for the duration of that write window.
        if (shutdownSignal.CanBeCanceled)
            mExternalReg = shutdownSignal.Register(
                static s => ((PluginLifetime)s!).RequestStop("external shutdown signal"), this);
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

    public void RequestStop() => RequestStop("PluginHost dispose");

    /// <summary>Begin shutdown, recording <paramref name="reason"/> in the
    /// logged transitions so a lifetime cancel can be attributed after the
    /// fact.</summary>
    public void RequestStop(string reason)
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
        SetState(PluginLifecycleState.Stopping, reason);
        SetState(PluginLifecycleState.Stopped, reason);
        try { mCts.Cancel(); }
        catch (ObjectDisposedException) { /* already torn down */ }
    }

    private void SetState(PluginLifecycleState target, string? reason = null)
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

        // Shutdown transitions log at WARNING, unconditionally. This is the
        // only signal that distinguishes "the plugin unloaded" from "the
        // plugin is still running but its lifetime token got cancelled" —
        // the failure mode that silently disabled every IsStopping-gated
        // service 60s into every session and produced ZERO log output,
        // because PluginLoggerProvider suppresses Information in Release and
        // all the gated bail paths return without a word. Two lines per
        // unload is a rounding error; a silent permanent shutdown is not.
        // Idle/Active are per-login churn, so they stay at Information.
        // try/catch for the same reason the subscriber loop below has one: the
        // Stopping/Stopped transitions run during teardown, and a sink that
        // throws there (e.g. a host logger already torn down) must not be able
        // to abort the shutdown sequence.
        try
        {
            if (target is PluginLifecycleState.Stopping or PluginLifecycleState.Stopped)
                mLog?.Warning($"[PluginLifetime] Lifecycle → {target} (reason: {reason ?? "unspecified"}).");
            else
                mLog?.Information($"[PluginLifetime] Lifecycle → {target}.");
        }
        catch { /* swallow — a broken sink shouldn't break the state machine */ }

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
        // Release the external registration first so we stop rooting `this`
        // inside a foreign CancellationTokenSource that may outlive us.
        try { mExternalReg.Dispose(); } catch { /* swallow — final teardown */ }
        try { mCts.Dispose(); } catch { /* swallow — final teardown */ }
    }
}
