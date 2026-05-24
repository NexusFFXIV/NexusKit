namespace NexusKit.Persistence.Maintenance;

/// <summary>Per-table size snapshot mirroring the data the DbInspect CLI
/// surfaces via <c>--inspect-sizes</c>. <see cref="TextOrBlobPayloadBytes"/>
/// approximates the table's contribution to the on-disk file: integer
/// columns and page overhead aren't counted, but in practice text/blob
/// payload dominates (HTML caches, JSON, byte arrays).</summary>
public sealed record DbTableStats(
    string Name,
    long RowCount,
    long TextOrBlobPayloadBytes,
    int IndexCount);

/// <summary>Complete stats snapshot. <see cref="OnDiskBytes"/> is the
/// physical file size on disk (includes integer columns, indexes, free
/// pages, WAL). <see cref="Tables"/> entries are ordered by payload
/// descending for the typical "what's eating my disk" display.
///
/// <para><see cref="FreelistBytes"/> is the amount currently sitting in
/// SQLite's free-page list — pages that have been deallocated but not
/// reclaimed back to the OS. <c>VACUUM</c> shrinks the file by
/// returning these pages. A near-zero value after a maintenance run
/// means the database is as compact as SQLite can make it; a large
/// value means VACUUM didn't get to run (or failed).</para></summary>
public sealed record DbStatsSnapshot(
    string DbFilePath,
    long OnDiskBytes,
    long FreelistBytes,
    IReadOnlyList<DbTableStats> Tables);

/// <summary>Gathers per-table size information from the live plugin DB.
/// Used by the Settings-UI's DB-maintenance section to show users how
/// space is distributed across tables and to surface VACUUM /
/// cache-eviction opportunities. Read-only — no schema mutation, no
/// connection-pool churn beyond a single read-only connection per call.</summary>
public interface IDbStatsService
{
    Task<DbStatsSnapshot> GatherAsync(CancellationToken ct = default);

    /// <summary>Fires after each successful <see cref="GatherAsync"/>.
    /// Adjacent diagnostics surfaces (e.g. plugin-side refresh-queue
    /// stats) subscribe to invalidate their own cached snapshots when
    /// the user hits "Refresh stats" / "Run maintenance now" on the
    /// shared DB-maintenance section. Handlers run on the task that
    /// owned the gather call — UI consumers should treat it as a
    /// background signal and re-trigger their own load on the next
    /// render.</summary>
    event Action? StatsRefreshed;
}
