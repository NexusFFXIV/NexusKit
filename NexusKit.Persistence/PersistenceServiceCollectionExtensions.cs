using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NexusKit.Core.Context;
using NexusKit.Persistence.Maintenance;
using NexusKit.Persistence.Migrations;
using NexusKit.Persistence.Migrations.Internal;
using NexusKit.Persistence.Schema;
using NexusKit.Persistence.Settings;
using NexusKit.Persistence.Settings.Schema;

namespace NexusKit.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddNexusKitPersistence(this IServiceCollection services)
    {
        services.AddDbContextFactory<PluginDbContext>((sp, options) =>
        {
            var context = sp.GetRequiredService<IPluginContext>();
            Directory.CreateDirectory(context.ConfigDirectory);
            var dbPath = Path.Combine(context.ConfigDirectory, $"{context.PluginName}.db");
            options.UseSqlite($"Data Source={dbPath}");
            // Demote every per-query Info event to Debug. The category-level
            // filter in PluginHostBuilder (Microsoft.EntityFrameworkCore.
            // Database.Command → Debug) is a MINIMUM threshold, so it lets
            // Info through too — and "Executed DbCommand" floods Dalamud's
            // plugin log with hundreds of lines per minute. Downgrading the
            // specific event IDs at the source is the only way to make
            // them disappear unless the user opts into Debug verbosity.
            options.ConfigureWarnings(w => w
                .Log((RelationalEventId.CommandExecuted,     LogLevel.Debug))
                .Log((RelationalEventId.ConnectionOpened,    LogLevel.Debug))
                .Log((RelationalEventId.ConnectionClosed,    LogLevel.Debug))
                .Log((RelationalEventId.TransactionStarted,  LogLevel.Debug))
                .Log((RelationalEventId.TransactionCommitted,LogLevel.Debug))
                .Log((RelationalEventId.TransactionDisposed, LogLevel.Debug))
                // TransactionError fires at Error level whenever a tx is
                // rolled back, including the OperationCanceledException
                // path that our PluginLifetime takes during shutdown. The
                // application catches OCE and logs real failures via
                // ILogger.LogWarning, so EF's internal Error-level log is
                // pure noise here. Downgrade to Debug.
                .Log((RelationalEventId.TransactionError,    LogLevel.Debug))
                .Log((CoreEventId.ContextInitialized,        LogLevel.Debug))
                .Log((CoreEventId.SaveChangesCompleted,      LogLevel.Debug)));
        });

        // Lifetime-aware factory consumers should depend on. Resolves the
        // raw factory + the plugin lifetime token under the hood so callers
        // don't have to thread the cancellation token manually.
        services.AddSingleton<INexusDbContextFactory, NexusDbContextFactory>();

        // Migrations tracking table is part of the persistence baseline.
        services.AddEntityModule<AppliedMigrationsEntityModule>();

        // Background DB maintenance: periodic contributors (cache eviction,
        // refresh-queue prune, weekly VACUUM/ANALYZE/OPTIMIZE/REINDEX) plus
        // SQLite shutdown chores (WAL checkpoint, pool clear). The host
        // calls .Start() after DbInitializer.InitializeAsync and
        // .ShutdownAsync() during plugin disposal.
        services.AddSingleton<DbMaintenanceService>();
        services.AddSingleton<IDbMaintenanceService>(sp => sp.GetRequiredService<DbMaintenanceService>());
        services.AddMaintenanceContributor<VacuumAndOptimizeContributor>();

        // Read-only stats snapshot for the Settings UI's DB maintenance
        // section. Same shape DbInspect's --inspect-sizes surfaces.
        services.AddSingleton<IDbStatsService, DbStatsService>();

        return services;
    }

    public static IServiceCollection AddNexusKitSettings(this IServiceCollection services)
    {
        services.AddEntityModule<SettingsEntityModule>();
        services.AddSingleton<ISettingsStore, SettingsStore>();
        return services;
    }

    public static IServiceCollection AddEntityModule<TModule>(this IServiceCollection services)
        where TModule : class, IEntityModule
    {
        services.AddSingleton<IEntityModule, TModule>();
        return services;
    }

    /// <summary>
    /// Register a module's migration history. The runner will baseline it on first
    /// startup (since <c>EnsureCreated</c> already produced the latest schema) and
    /// only execute newly added migrations on subsequent startups.
    /// </summary>
    public static IServiceCollection AddMigrationModule<TModule>(this IServiceCollection services)
        where TModule : class, IMigrationModule
    {
        services.AddSingleton<IMigrationModule, TModule>();
        return services;
    }

    /// <summary>
    /// Register a maintenance contributor. The background
    /// <see cref="IDbMaintenanceService"/> walks every registered
    /// contributor on a 15-minute inner tick and invokes the ones whose
    /// <see cref="IDbMaintenanceContributor.Interval"/> has elapsed.
    /// </summary>
    public static IServiceCollection AddMaintenanceContributor<TContributor>(this IServiceCollection services)
        where TContributor : class, IDbMaintenanceContributor
    {
        services.AddSingleton<IDbMaintenanceContributor, TContributor>();
        return services;
    }

    /// <summary>
    /// Register a view builder. Builders run on every plugin start after
    /// migrations, declaring SQL views via idempotent DROP/CREATE so schema
    /// changes propagate without migration baseline bookkeeping.
    /// </summary>
    public static IServiceCollection AddViewBuilder<TBuilder>(this IServiceCollection services)
        where TBuilder : class, IDatabaseViewBuilder
    {
        services.AddSingleton<IDatabaseViewBuilder, TBuilder>();
        return services;
    }

    public static IServiceCollection AddSettings<T>(
        this IServiceCollection services,
        Action<SettingsSchemaBuilder<T>> configure)
        where T : class, new()
    {
        services.TryAddSingleton<ISettingsSchemaProvider, SettingsSchemaProvider>();

        var builder = new SettingsSchemaBuilder<T>();
        configure(builder);
        var schema = builder.Build();
        services.AddSingleton<IRegisteredSettingsSchema>(schema);
        return services;
    }
}