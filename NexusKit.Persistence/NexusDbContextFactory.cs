using Microsoft.EntityFrameworkCore;
using NexusKit.Core;

namespace NexusKit.Persistence;

internal sealed class NexusDbContextFactory : INexusDbContextFactory
{
    private readonly IDbContextFactory<PluginDbContext> mInner;
    private readonly IPluginLifetime mLifetime;

    public NexusDbContextFactory(
        IDbContextFactory<PluginDbContext> inner,
        IPluginLifetime lifetime)
    {
        mInner = inner;
        mLifetime = lifetime;
    }

    public CancellationToken LifetimeToken => mLifetime.Stopping;
    public bool IsStopping => mLifetime.IsStopping;

    public async Task<PluginDbContext> CreateDbContextAsync(CancellationToken ct = default)
    {
        // No caller token: just use the lifetime token directly.
        if (!ct.CanBeCanceled)
            return await mInner.CreateDbContextAsync(mLifetime.Stopping).ConfigureAwait(false);

        // Caller passed a token — link with lifetime so either path cancels.
        // The linked CTS must outlive the await; `using` disposes it after
        // CreateDbContextAsync returns, which is fine because the context
        // itself stops caring about the token once it's been created.
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, mLifetime.Stopping);
        return await mInner.CreateDbContextAsync(linked.Token).ConfigureAwait(false);
    }
}
