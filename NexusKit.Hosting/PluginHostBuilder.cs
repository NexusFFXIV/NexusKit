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
    private CancellationToken mShutdownSignal;
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

    /// <summary>Supply a token that genuinely fires when the HOST wants the
    /// plugin to shut down. Almost no Dalamud plugin needs this: the
    /// authoritative unload signal is <c>IDalamudPlugin.Dispose</c> /
    /// <c>IAsyncDisposable.DisposeAsync</c>, which <see cref="PluginHost"/>
    /// already turns into <see cref="PluginLifetime.RequestStop()"/>.
    /// <para>In particular, do NOT pass <c>LoadAsync</c>'s / this builder's
    /// <c>BuildAsync</c> cancellation token — under Dalamud that is a
    /// 60-second load timeout, not a shutdown signal. See the
    /// <see cref="PluginLifetime"/> constructor for the full story.</para></summary>
    public PluginHostBuilder WithShutdownSignal(CancellationToken shutdownSignal)
    {
        mShutdownSignal = shutdownSignal;
        return this;
    }

    public async Task<PluginHost> BuildAsync(CancellationToken ct = default)
    {
        if (mContext is null) throw new InvalidOperationException("PluginContext is required. Call WithContext(...) before BuildAsync().");
        if (mSink is null) throw new InvalidOperationException("PluginLogSink is required. Call WithLogSink(...) before BuildAsync().");
        // Local copy so the build-failure handler below doesn't depend on
        // nullable flow analysis surviving the awaits in between.
        var sink = mSink;

        var services = new ServiceCollection();
        services.AddSingleton(mContext);
        services.AddSingleton(sink);
        // Plugin-lifetime token — concrete class registered for the host to
        // cancel from Dispose, public interface exposed for any service
        // that wants to thread the token through its async calls.
        //
        // Deliberately NOT linked to BuildAsync's `ct`. Under Dalamud that
        // token is a 60-SECOND LOAD TIMEOUT, not an unload signal:
        // LocalPlugin.LoadAsync fabricates a CTS with CancelAfter(60s)
        // whenever the caller passes no token, and no call site passes one.
        // Linking to it cancelled the lifetime 60s after every load and
        // silently disabled every IsStopping-gated service (observation
        // persistence, encounter tracking, history, the refresh-queue
        // worker, DB maintenance) for the rest of the session while the
        // plugin kept running. The authoritative shutdown signal is
        // PluginHost.Dispose/DisposeAsync → RequestStop; a host that has a
        // REAL shutdown token opts in via WithShutdownSignal.
        //
        // Keep this registration near the front of the collection: the
        // ServiceProvider disposes in reverse registration order, so being
        // registered early is what keeps the lifetime's CTS alive while
        // every other singleton unwinds.
        services.AddSingleton(sp => new PluginLifetime(
            mShutdownSignal,
            sp.GetRequiredService<IPluginLogSink>()));
        services.AddSingleton<NexusKit.Core.IPluginLifetime>(sp => sp.GetRequiredService<PluginLifetime>());
        // Always register the bridge — the eager resolve below only fires
        // it when an ISessionStateProvider has actually been added by the
        // host application's ConfigureServices. The singleton lifetime
        // ensures the DI container disposes it on shutdown, which
        // unsubscribes the provider's event handlers cleanly.
        services.AddSingleton<LifetimeBridge>();
        services.AddLogging(b =>
        {
            b.AddProvider(new PluginLoggerProvider(sink));
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

        // Everything below can throw: DbInitializer honours `ct`, a module's
        // eagerly-resolved constructor can fail, a view builder can blow up on
        // a malformed DB. The provider already exists at this point and the
        // eager resolves below start background loops (IDbMaintenanceService,
        // IPluginBackgroundService), so on failure we must signal shutdown and
        // dispose it instead of letting it leak undisposed with live workers.
        // Routing the cleanup through PluginHost.DisposeAsync guarantees the
        // same signal-then-teardown ordering as the normal shutdown path.
        try
        {
            var dbFactory = provider.GetService<IDbContextFactory<PluginDbContext>>();
            if (dbFactory is not null)
            {
                var migrationModules = provider.GetServices<IMigrationModule>();
                var viewBuilders = provider.GetServices<IDatabaseViewBuilder>();
                // `ct` belongs here and only here: a genuinely aborted load
                // should stop migrating. Each migration is transaction-wrapped,
                // so an abort rolls back cleanly and the next load retries.
                // (Under Dalamud `ct` firing means "60s elapsed", so a very slow
                // first-run migration aborts the load — unchanged from before.)
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
        catch (Exception buildEx)
        {
            sink.Error(
                "[PluginHostBuilder] Host build failed after the DI container was created; "
                + "signalling shutdown and disposing the partially-initialised container. "
                + $"{buildEx.GetType().Name}: {buildEx.Message}");
            try
            {
                await new PluginHost(provider).DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception disposeEx)
            {
                // Swallowed on purpose so the ORIGINAL build failure is what
                // propagates to the caller — that's the actionable one.
                sink.Error(
                    "[PluginHostBuilder] Cleanup after the failed host build threw and was swallowed.",
                    disposeEx);
            }
            throw;
        }
    }
}
