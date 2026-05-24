using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace NexusKit.Persistence.Maintenance;

/// <summary>
/// Weekly heavy-housekeeping pass: <c>REINDEX</c> → <c>ANALYZE</c> →
/// <c>PRAGMA optimize</c> → <c>VACUUM</c>. The canonical SQLite
/// "rebuild and shrink" recipe for long-running embedded databases.
///
/// <para><b>VACUUM is the expensive step.</b> It rewrites the entire
/// database file under an exclusive lock; on a multi-hundred-megabyte DB it
/// can pause concurrent writes for several seconds. We accept that cost
/// once per week — the savings (reclaimed free pages after deletes, tidy
/// page ordering) keep the on-disk size honest. <c>REINDEX</c> and
/// <c>ANALYZE</c> are cheap; <c>PRAGMA optimize</c> uses ANALYZE's
/// updated stats to drop or rebuild stale indexes.</para>
///
/// <para>The runtime plugin keeps other connections live (watcher's
/// observation upserts, refresh-queue worker), and SQLite's exclusive
/// lock for VACUUM contends with their pending writes. The
/// <c>busy_timeout</c> PRAGMA below makes our connection wait up to 30 s
/// for those to clear instead of returning <c>SQLITE_BUSY</c> immediately
/// — which was the silent-failure mode where "Run maintenance now"
/// appeared to complete in the UI but the file never actually shrank.</para>
/// </summary>
internal sealed class VacuumAndOptimizeContributor : IDbMaintenanceContributor
{
    /// <summary>How long the SQLite connection waits for competing writers
    /// to release their locks before failing. 30 s comfortably outlasts
    /// the watcher's per-observation transaction (sub-100 ms) and the
    /// refresh-queue worker's per-row save (sub-second), while still
    /// surfacing genuine deadlocks within a reasonable window.</summary>
    private const int BusyTimeoutMs = 30_000;

    public string Name => "vacuum-and-optimize";

    /// <summary>7-day cadence. Weekly is frequent enough to keep the file
    /// from drifting after a big deletion run, infrequent enough that the
    /// multi-second VACUUM pause is amortised.</summary>
    public TimeSpan Interval => TimeSpan.FromDays(7);

    public async Task RunAsync(DbContext ctx, CancellationToken ct)
    {
        // Reach past EF Core to the raw SQLite connection so we can
        // (a) set busy_timeout on this exact connection (PRAGMAs are
        // connection-local in SQLite) and (b) be certain no implicit EF
        // transaction wrapping interferes with VACUUM — VACUUM refuses to
        // run inside a transaction and SQLite returns a misleading
        // "cannot VACUUM from within a transaction" error if it does.
        var connection = (SqliteConnection)ctx.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct).ConfigureAwait(false);

        await ExecuteAsync(connection, $"PRAGMA busy_timeout = {BusyTimeoutMs}", ct).ConfigureAwait(false);

        // Order: rebuild structure first, refresh stats, let SQLite decide
        // which indexes to keep, then defragment the file. VACUUM benefits
        // from the fresh ANALYZE stats which inform the page-rewrite
        // heuristics.
        await ExecuteAsync(connection, "REINDEX", ct).ConfigureAwait(false);
        await ExecuteAsync(connection, "ANALYZE", ct).ConfigureAwait(false);
        await ExecuteAsync(connection, "PRAGMA optimize", ct).ConfigureAwait(false);
        await ExecuteAsync(connection, "VACUUM", ct).ConfigureAwait(false);

        // VACUUM in WAL mode writes the rewritten database into the WAL
        // first; SQLite then auto-checkpoints to fold it back into the
        // main .db file. If another connection (the watcher, the
        // refresh-queue worker) holds a read transaction at that moment,
        // the auto-checkpoint stalls in PASSIVE mode and the WAL stays
        // inflated — the user sees an apparent "on-disk size doubled"
        // because the WAL now mirrors the entire database content.
        // Force a TRUNCATE checkpoint with the same busy_timeout grace
        // period so the WAL actually drains and the .db-wal file
        // shrinks to zero.
        await ExecuteAsync(connection, "PRAGMA wal_checkpoint(TRUNCATE)", ct).ConfigureAwait(false);
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        // Match the busy_timeout we set above — without an explicit
        // command timeout, the driver applies a 30 s wall-clock cap which
        // would race the PRAGMA on very busy databases.
        cmd.CommandTimeout = BusyTimeoutMs / 1000 + 30;
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
