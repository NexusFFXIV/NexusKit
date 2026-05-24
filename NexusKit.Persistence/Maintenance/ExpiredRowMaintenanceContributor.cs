using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace NexusKit.Persistence.Maintenance;

/// <summary>
/// Convenience base class for contributors whose only job is "delete rows
/// whose <c>expires_at</c> column has passed". Cache-table cleanups
/// (Lodestone, FFXIVCollect) are the typical clients — their tables share
/// the same <c>(key, response, fetched_at, expires_at)</c> shape, so the
/// DELETE statement is identical modulo the table name. The expires-column
/// name is overridable for the rare case where a table uses a different
/// suffix.
/// </summary>
public abstract class ExpiredRowMaintenanceContributor : IDbMaintenanceContributor
{
    public abstract string Name { get; }
    public abstract TimeSpan Interval { get; }

    /// <summary>Fully-qualified table name (including any
    /// <c>nexus_&lt;prefix&gt;_</c> module prefix).</summary>
    protected abstract string TableName { get; }

    /// <summary>Column carrying the row's expiry timestamp. Defaults to
    /// <c>expires_at</c>, matching the convention used by every cache
    /// table at the time this base class was introduced.</summary>
    protected virtual string ExpiresAtColumn => "expires_at";

    public Task RunAsync(DbContext ctx, CancellationToken ct)
    {
        // Parameterise the cutoff — the table / column names come from
        // trusted subclass constants. SqliteParameter binds the DateTime as
        // ISO-8601 text, matching how EF Core stores datetimes for these
        // tables (so the < comparison hits the same string ordering EF uses
        // on read).
        var sql = $"DELETE FROM {TableName} WHERE {ExpiresAtColumn} < @now";
        return ctx.Database.ExecuteSqlRawAsync(sql,
            new[] { new SqliteParameter("@now", DateTime.UtcNow) }, ct);
    }
}
