using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NexusKit.Core;
using NexusKit.Core.Context;
using NexusKit.Core.Ipc;
using NexusKit.Core.Logging;
using NexusKit.Core.Modules;
using NexusKit.Hosting.Logging;
using NexusKit.Persistence;
using NexusKit.Persistence.Maintenance;
using NexusKit.Persistence.Migrations;
using NexusKit.Persistence.Schema;

namespace NexusKit.Hosting;

public sealed class PluginHostBuilder
{
    private IPluginContext? mContext;
    private IPluginLogSink? mSink;
    private readonly List<IPluginModule> mModules = new();
    private readonly List<Action<IServiceCollection>> mServiceConfigurations = new();

    public PluginHostBuilder WithContext(IPluginContext context)
    {
        mContext = context;
        return this;
    }

    public PluginHostBuilder WithLogSink(IPluginLogSink sink)
    {
        mSink = sink;
        return this;
    }

    public PluginHostBuilder WithModule(IPluginModule module)
    {
        mModules.Add(module);
        return this;
    }

    public PluginHostBuilder ConfigureServices(Action<IServiceCollection> configure)
    {
        mServiceConfigurations.Add(configure);
        return this;
    }

    public async Task<PluginHost> BuildAsync(CancellationToken ct = default)
    {
        if (mContext is null) throw new InvalidOperationException("PluginContext is required. Call WithContext(...) before BuildAsync().");
        if (mSink is null) throw new InvalidOperationException("PluginLogSink is required. Call WithLogSink(...) before BuildAsync().");

        var services = new ServiceCollection();
        services.AddSingleton(mContext);
        services.AddSingleton(mSink);
        // Plugin-lifetime token — concrete class registered for the host to
        // cancel from Dispose, public interface exposed for any service
        // that wants to thread the token through its async calls. The
        // BuildAsync caller's `ct` is the plugin-lifecycle token already
        // (the same one DbInitializer uses for the startup migrations), so
        // link to it: an external cancel signals shutdown to every service
        // without us having to invoke RequestStop ourselves.
        services.AddSingleton(_ => new PluginLifetime(ct));
        services.AddSingleton<NexusKit.Core.IPluginLifetime>(sp => sp.GetRequiredService<PluginLifetime>());
        // Always register the bridge — the eager resolve below only fires
        // it when an ISessionStateProvider has actually been added by the
        // host application's ConfigureServices. The singleton lifetime
        // ensures the DI container disposes it on shutdown, which
        // unsubscribes the provider's event handlers cleanly.
        services.AddSingleton<LifetimeBridge>();
        services.AddLogging(b =>
        {
            b.AddProvider(new PluginLoggerProvider(mSink));
            // Demote EF Core's per-query INFO firehose to DEBUG. Microsoft.
            // EntityFrameworkCore.Database.Command logs every executed
            // statement at Information, which floods Dalamud's plugin log
            // with hundreds of lines per minute (one per observation tick).
            // Debug-level still surfaces them when the user explicitly opts
            // in via Dalamud's log-level UI.
            b.AddFilter("Microsoft.EntityFrameworkCore.Database.Command",
                Microsoft.Extensions.Logging.LogLevel.Debug);
        });

        foreach (var configure in mServiceConfigurations)
            configure(services);

        foreach (var module in mModules)
            module.Register(services, mContext);

        var provider = services.BuildServiceProvider(validateScopes: false);

        var dbFactory = provider.GetService<IDbContextFactory<PluginDbContext>>();
        if (dbFactory is not null)
        {
            var migrationModules = provider.GetServices<IMigrationModule>();
            var viewBuilders = provider.GetServices<IDatabaseViewBuilder>();
            await DbInitializer.InitializeAsync(dbFactory, migrationModules, viewBuilders, ct).ConfigureAwait(false);

            // Eager-resolve the maintenance service so its singleton
            // constructor runs — that's where the background loop is kicked
            // off (auto-starts via the IPluginLifetime cancellation token).
            // Result is unused; the side effect (construction + auto-start
            // + IAsyncDisposable hook) is the point.
            _ = provider.GetService<IDbMaintenanceService>();
        }

        // Drive the lifecycle out of Initializing. If the host registered
        // an ISessionStateProvider (the Dalamud plugin does so via
        // DalamudSessionStateProvider), eagerly resolve the bridge — its
        // constructor calls NotifyReady(provider.IsActive) and subscribes
        // to Activated / Deactivated events. Otherwise default to "active"
        // so consumers gated on State==Active aren't permanently idle on
        // a host that has no session concept.
        if (provider.GetService<ISessionStateProvider>() is not null)
            provider.GetRequiredService<LifetimeBridge>();
        else
            provider.GetRequiredService<PluginLifetime>().NotifyReady(true);

        // Eagerly resolve IPC providers — their constructors register IPCs
        // via IIpcRegistry, so all own IPCs are live before user code runs.
        foreach (var _ in provider.GetServices<IIpcProvider>())
        {
            // resolution is the registration side-effect; no further action needed
        }

        // Eagerly resolve background services — same resolution-as-side-effect
        // pattern: the ctor starts a background loop / subscribes to events,
        // and modules opt in via AddSingleton<IPluginBackgroundService>(...)
        // in their own DI extension. Keeps long-lived module-owned workers
        // off the plugin's LoadAsync.
        foreach (var _ in provider.GetServices<IPluginBackgroundService>())
        {
            // resolution is the registration side-effect; no further action needed
        }

        return new PluginHost(provider);
    }
}
