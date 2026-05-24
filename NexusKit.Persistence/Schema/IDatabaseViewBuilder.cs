using Microsoft.EntityFrameworkCore;

namespace NexusKit.Persistence.Schema;

/// <summary>
/// Declares a SQLite view that exists for query convenience (typically joining
/// across multiple module tables). View builders run on EVERY plugin start
/// after <c>EnsureCreated</c> and the migration loop — they're idempotent
/// (<c>DROP VIEW IF EXISTS</c> + <c>CREATE VIEW</c>) so re-running them is
/// cheap, and they don't go through the migration baseline mechanism (which
/// would skip view creation on fresh installs that EnsureCreated already
/// satisfied).
/// </summary>
public interface IDatabaseViewBuilder
{
    /// <summary>
    /// Create or recreate the view. Implementations should be idempotent so
    /// schema changes propagate by simply editing the SQL and shipping a new
    /// build — no migration-id juggling required.
    /// </summary>
    Task BuildAsync(DbContext ctx, CancellationToken ct);
}
