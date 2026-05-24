namespace NexusKit.Core;

/// <summary>
/// The plugin's high-level lifecycle phases. Use this when a service needs
/// to react to login / logout / shutdown without having to subscribe to
/// Dalamud's <c>IClientState</c> directly (and without coupling the service
/// to Dalamud at all — the framework owns this signal).
/// </summary>
public enum PluginLifecycleState
{
    /// <summary>Host is still building services / running migrations.
    /// Brief transient state; nothing user-visible should run yet.</summary>
    Initializing,

    /// <summary>Plugin is operational but no character is logged in.
    /// Background work that isn't bound to a character session
    /// (cache prunes, settings writes) is fine; character-scoped work
    /// (encounter tracking, observation persistence) should stay idle.</summary>
    Idle,

    /// <summary>Plugin is operational AND a character is logged in.
    /// All services run normally.</summary>
    Active,

    /// <summary>Shutdown has been initiated but the cancellation token is
    /// NOT yet cancelled and the DI container is still fully alive — this
    /// is the last-chance window for services to flush any pending state
    /// to disk (e.g. stamp a still-open encounter's <c>ended_at</c>) before
    /// the framework tears down. <see cref="IPluginLifetime.StateChanged"/>
    /// subscribers handle this state SYNCHRONOUSLY — the host blocks on
    /// the callback returning before transitioning to <see cref="Stopped"/>,
    /// so a subscriber doing <c>task.GetAwaiter().GetResult()</c> for a
    /// final write is safe and expected. Keep these handlers fast (single
    /// indexed write per service, no network).</summary>
    Stopping,

    /// <summary>Shutdown complete: <see cref="IPluginLifetime.Stopping"/>
    /// is cancelled, the DI container will be disposed momentarily.
    /// Terminal — no further transitions. By the time subscribers see
    /// this state, any DB-backed final-write window is already over;
    /// in-flight EF operations on the lifetime token will throw
    /// <see cref="OperationCanceledException"/> from here on.</summary>
    Stopped,
}

/// <summary>
/// Plugin-wide lifecycle signal owned by the host. Services thread
/// <see cref="Stopping"/> through every asynchronous DB / network call so
/// in-flight work cancels cleanly BEFORE the DI container starts tearing
/// down — which prevents the "phantom writes after dispose" class of bugs
/// where an awaiting <c>SaveChangesAsync</c> resumes and INSERTs a row
/// after the plugin's logical shutdown.
/// <para>The hosting layer cancels the underlying token at the start of
/// plugin disposal, before any <see cref="IDisposable.Dispose"/> on
/// singletons runs and well before the SQLite connection pool is cleared.
/// This is independent of any game-state event (e.g. <c>IClientState.Logout</c>
/// only fires when the user logs out in-game — a plugin unload while the
/// user is still in-world does not raise it).</para>
/// <para>For game-state transitions (login, logout) that should NOT tear
/// down background workers but DO matter for character-bound state, use
/// <see cref="State"/> + <see cref="StateChanged"/>: the cancellation token
/// only fires once on plugin unload, but the state event lets services
/// react to logout without dying for the rest of the session.</para>
/// </summary>
public interface IPluginLifetime
{
    /// <summary>Fires when the plugin enters its shutdown phase. Pass this
    /// to <c>CreateDbContextAsync</c>, <c>SaveChangesAsync</c>,
    /// <c>BeginTransactionAsync</c>, HTTP calls, etc. so awaits unwind with
    /// <see cref="OperationCanceledException"/> instead of writing into a
    /// half-disposed container. One-shot; only cancels when
    /// <see cref="State"/> transitions to
    /// <see cref="PluginLifecycleState.Stopped"/>.</summary>
    CancellationToken Stopping { get; }

    /// <summary>True once shutdown has been requested. Cheap synchronous
    /// check for hot paths that don't want to allocate a token registration
    /// (e.g. the entry guard of an observation handler).</summary>
    bool IsStopping { get; }

    /// <summary>Current lifecycle phase. Cheap synchronous read; suitable
    /// for guard checks at the entry of an async handler.</summary>
    PluginLifecycleState State { get; }

    /// <summary>Fires on every state transition with the NEW state.
    /// Invoked synchronously from the thread that drove the transition.
    /// Subscribers must not throw — exceptions are swallowed but the state
    /// machine continues regardless. Order across multiple subscribers
    /// matches subscription order.</summary>
    event Action<PluginLifecycleState>? StateChanged;
}
