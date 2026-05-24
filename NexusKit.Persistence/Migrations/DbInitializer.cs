using Microsoft.EntityFrameworkCore;
using NexusKit.Persistence.Migrations.Internal;
using NexusKit.Persistence.Schema;

namespace NexusKit.Persistence.Migrations;

public static class DbInitializer
{
    /// <summary>
    /// Ensure every entity-module's tables exist (via <c>Database.EnsureCreatedAsync</c>),
    /// then iterate each <see cref="IMigrationModule"/>:
    /// <list type="bullet">
    /// <item>If the module has no rows in <c>nexuskit_migrations</c> yet, baseline it —
    /// EnsureCreated produced the current schema, so we just mark every known migration
    /// as applied without running it.</item>
    /// <item>Otherwise apply the migrations whose <see cref="IMigration.Id"/> is not yet
    /// recorded, in ascending Id order, recording each one after a successful run.</item>
    /// </list>
    /// Finally invoke every registered <see cref="IDatabaseViewBuilder"/>. Builders
    /// run unconditionally on each startup (DROP+CREATE patterns are idempotent),
    /// because <c>EnsureCreated</c> doesn't know about views and the migration
    /// baseline path would otherwise skip a view-creating migration on fresh
    /// installs — leaving the view absent.
    /// </summary>
    public static async Task InitializeAsync(
        IDbContextFactory<PluginDbContext> factory,
        IEnumerable<IMigrationModule> migrationModules,
        IEnumerable<IDatabaseViewBuilder> viewBuilders,
        CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await ctx.Database.EnsureCreatedAsync(ct).ConfigureAwait(false);

        foreach (var module in migrationModules)
        {
            await ApplyModuleAsync(ctx, module, ct).ConfigureAwait(false);
        }

        foreach (var builder in viewBuilders)
        {
            await builder.BuildAsync(ctx, ct).ConfigureAwait(false);
        }
    }

    private static async Task ApplyModuleAsync(PluginDbContext ctx, IMigrationModule module, CancellationToken ct)
    {
        var migrations = module.Migrations
            .OrderBy(m => m.Id, StringComparer.Ordinal)
            .ToList();
        if (migrations.Count == 0) return;

        var appliedSet = await ctx.Set<AppliedMigrationEntity>()
            .Where(m => m.ModuleId == module.ModuleId)
            .Select(m => m.MigrationId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (appliedSet.Count == 0)
        {
            // First time we see this module. EnsureCreated produced the latest schema,
            // so baseline by marking every migration as applied without running it.
            var now = DateTime.UtcNow;
            foreach (var mig in migrations)
            {
                ctx.Set<AppliedMigrationEntity>().Add(new AppliedMigrationEntity
                {
                    ModuleId = module.ModuleId,
                    MigrationId = mig.Id,
                    AppliedAt = now,
                });
            }
            await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
            return;
        }

        var applied = new HashSet<string>(appliedSet, StringComparer.Ordinal);
        foreach (var mig in migrations)
        {
            if (applied.Contains(mig.Id)) continue;

            // Each migration runs inside its own transaction so a partial
            // failure (e.g. a CREATE TABLE succeeded but the follow-up
            // backfill INSERT crashed) rolls back cleanly. Without this
            // wrapper a half-applied migration leaves the schema in a
            // wedge state on the next plugin load — the framework retries
            // the same Up steps, IF NOT EXISTS shields the CREATEs, but
            // any non-idempotent step would compound. The
            // AppliedMigrationEntity row is part of the same transaction
            // so a successful migration atomically records itself.
            await using var tx = await ctx.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                await mig.UpAsync(ctx, ct).ConfigureAwait(false);

                ctx.Set<AppliedMigrationEntity>().Add(new AppliedMigrationEntity
                {
                    ModuleId = module.ModuleId,
                    MigrationId = mig.Id,
                    AppliedAt = DateTime.UtcNow,
                });
                await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
                await tx.CommitAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);
                throw;
            }
        }
    }
}
