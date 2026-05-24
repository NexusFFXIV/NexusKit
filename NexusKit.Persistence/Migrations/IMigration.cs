using Microsoft.EntityFrameworkCore;

namespace NexusKit.Persistence.Migrations;

/// <summary>
/// One schema-evolution step contributed by a module. The framework runs
/// pending migrations in <see cref="Id"/>-ascending order at startup and
/// records each in the <c>nexuskit_migrations</c> table.
/// </summary>
public interface IMigration
{
    /// <summary>
    /// Stable, sortable identifier — typically a timestamp prefix like
    /// "20260601_add_ttl_column". Unique within the owning module.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Forward-apply this migration. Use <c>context.Database.ExecuteSqlRawAsync</c>
    /// (or <c>ExecuteSqlInterpolatedAsync</c>) for DDL — SQLite ALTER has well-known
    /// limits, so prefer additive changes or table-copy patterns.
    /// </summary>
    Task UpAsync(DbContext context, CancellationToken ct);
}
