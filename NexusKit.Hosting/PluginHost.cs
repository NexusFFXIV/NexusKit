using Microsoft.Extensions.DependencyInjection;

namespace NexusKit.Hosting;

public sealed class PluginHost : IAsyncDisposable, IDisposable
{
    private readonly ServiceProvider mProvider;

    internal PluginHost(ServiceProvider provider)
    {
        mProvider = provider;
    }

    public IServiceProvider Services => mProvider;

    public async ValueTask DisposeAsync()
    {
        // Step 1 — signal shutdown. Every service holding the
        // IPluginLifetime.Stopping token cancels its in-flight async work
        // BEFORE we start disposing the DI container, so EF Core's
        // CreateDbContextAsync / SaveChangesAsync / etc. throw
        // OperationCanceledException instead of writing into a half-torn
        // container. Must happen first; the rest of the steps depend on
        // background tasks already winding down.
        mProvider.GetService<PluginLifetime>()?.RequestStop();

        await mProvider.DisposeAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
        // Same shutdown-signal-first ordering as DisposeAsync — see comment there.
        mProvider.GetService<PluginLifetime>()?.RequestStop();

        mProvider.Dispose();
    }
}