using NexusKit.Core;

namespace NexusKit.Persistence;

/// <summary>
/// Plugin-wide DbContext factory that knows the plugin's
/// <see cref="IPluginLifetime"/>. Services consume this instead of the raw
/// <c>IDbContextFactory&lt;PluginDbContext&gt;</c> so they don't have to
/// thread the lifetime token through every async call by hand: a context
/// created here, and any subsequent <c>SaveChangesAsync</c> /
/// <c>BeginTransactionAsync</c> / query call passed
/// <see cref="LifetimeToken"/>, will throw
/// <see cref="OperationCanceledException"/> on plugin unload instead of
/// resuming after the DI container has started tearing down.
/// <para>The factory links any caller-supplied <c>CancellationToken</c> with
/// the lifetime token automatically — both the explicit ct and the shutdown
/// signal can cancel the create call.</para>
/// </summary>
public interface INexusDbContextFactory
{
    /// <summary>The plugin lifetime cancellation token. Use this on
    /// <c>SaveChangesAsync</c>, <c>BeginTransactionAsync</c>, and async
    /// LINQ calls so they unwind cleanly during plugin unload.</summary>
    CancellationToken LifetimeToken { get; }

    /// <summary>True once plugin shutdown has been requested. Cheap
    /// synchronous guard for the entry of an async handler — bail out early
    /// instead of opening a context that's about to throw.</summary>
    bool IsStopping { get; }

    /// <summary>Create a new <see cref="PluginDbContext"/>. The caller's
    /// optional <paramref name="ct"/> is linked with
    /// <see cref="LifetimeToken"/> so either source can cancel.</summary>
    Task<PluginDbContext> CreateDbContextAsync(CancellationToken ct = default);
}
