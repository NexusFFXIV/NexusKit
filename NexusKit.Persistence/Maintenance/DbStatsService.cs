using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexusKit.Core.Context;

namespace NexusKit.Persistence.Maintenance;

internal sealed class DbStatsService : IDbStatsService
{
    public event Action? StatsRefreshed;

    private readonly IDbContextFactory<PluginDbContext> mFactory;
    private readonly IPluginContext mPluginContext;
    private readonly ILogger<DbStatsService> mLog;

    public DbStatsService(
        IDbContextFactory<PluginDbContext> factory,
        IPluginContext pluginContext,
        ILogger<DbStatsService> log)
    {
        mFactory = factory;
        mPluginContext = pluginContext;
        mLog = log;
    }

    public async Task<DbStatsSnapshot> GatherAsync(CancellationToken ct = default)
    {
        var dbPath = Path.Combine(mPluginContext.ConfigDirectory, $"{mPluginContext.PluginName}.db");
        // SQLite in WAL mode keeps recent writes in a sidecar .db-wal file
        // and only folds them back into the main .db on checkpoint. The
        // user-facing "on-disk size" should reflect the actual footprint
        // on the filesystem — sum the main file + WAL + SHM. Each
        // FileInfo is constructed fresh per call so Windows's per-handle
        // metadata cache can't return a stale Length value from a
        // previous snapshot.
        var onDiskBytes = LengthOrZero(dbPath)
            + LengthOrZero(dbPath + "-wal")
            + LengthOrZero(dbPath + "-shm");

        static long LengthOrZero(string path)
        {
            if (!File.Exists(path)) return 0L;
            var fi = new FileInfo(path);
            fi.Refresh();
            return fi.Length;
        }

        await using var ctx = await mFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var connection = ctx.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct).ConfigureAwait(false);

        // SQLite tracks deallocated pages in a free-list — VACUUM is what
        // returns those pages to the OS. Surfacing the freelist size lets
        // the user tell "VACUUM didn't run / got skipped" (high value)
        // apart from "DB is already as compact as it gets, the remaining
        // delta is indexes + integer columns + page padding" (near zero).
        var pageSize = 0L;
        var freePages = 0L;
        await using (var psCmd = connection.CreateCommand())
        {
            psCmd.CommandText = "PRAGMA page_size";
            pageSize = Convert.ToInt64(await psCmd.ExecuteScalarAsync(ct).ConfigureAwait(false) ?? 0L);
        }
        await using (var flCmd = connection.CreateCommand())
        {
            flCmd.CommandText = "PRAGMA freelist_count";
            freePages = Convert.ToInt64(await flCmd.ExecuteScalarAsync(ct).ConfigureAwait(false) ?? 0L);
        }
        var freelistBytes = pageSize * freePages;

        // Pre-aggregate user-defined index counts in one round-trip
        // (autoindex entries excluded so the count matches "indexes the
        // schema actually declared", not "indexes SQLite synthesised for
        // PK / UNIQUE constraints"). Same approach DbInspect uses.
        var indexCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await using (var idxCmd = connection.CreateCommand())
        {
            idxCmd.CommandText = @"
                SELECT tbl_name, COUNT(*) FROM sqlite_master
                WHERE type='index' AND name NOT LIKE 'sqlite_%'
                GROUP BY tbl_name";
            await using var r = await idxCmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await r.ReadAsync(ct).ConfigureAwait(false))
                indexCounts[r.GetString(0)] = r.GetInt32(1);
        }

        // Walk every user table — auto-discovery via sqlite_master so new
        // tables (added by future migrations / modules) appear without
        // touching this service.
        var tableNames = new List<string>();
        await using (var tablesCmd = connection.CreateCommand())
        {
            tablesCmd.CommandText = @"
                SELECT name FROM sqlite_master
                WHERE type='table' AND name NOT LIKE 'sqlite_%'
                ORDER BY name";
            await using var r = await tablesCmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await r.ReadAsync(ct).ConfigureAwait(false))
                tableNames.Add(r.GetString(0));
        }

        var rows = new List<DbTableStats>(tableNames.Count);
        foreach (var table in tableNames)
        {
            ct.ThrowIfCancellationRequested();
            long rowCount = 0;
            long payload = 0;
            try
            {
                await using (var cnt = connection.CreateCommand())
                {
                    cnt.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";
                    rowCount = Convert.ToInt64(await cnt.ExecuteScalarAsync(ct).ConfigureAwait(false) ?? 0L);
                }

                // Sum length() on every TEXT / BLOB / typeless column —
                // matches the DbInspect approximation. INTEGER columns are
                // counted by SQLite as variable-length but on the order of
                // 1-8 bytes per row; ignoring them keeps the numbers
                // comparable to PRAGMA-style payload reporting and avoids
                // misrepresenting "size driven by row count" as "size
                // driven by payload".
                var textOrBlobCols = new List<string>();
                await using (var cols = connection.CreateCommand())
                {
                    cols.CommandText = $"PRAGMA table_info(\"{table}\")";
                    await using var r = await cols.ExecuteReaderAsync(ct).ConfigureAwait(false);
                    while (await r.ReadAsync(ct).ConfigureAwait(false))
                    {
                        var colName = r.GetString(1);
                        var type = r.IsDBNull(2) ? string.Empty : r.GetString(2);
                        if (type.Equals("TEXT", StringComparison.OrdinalIgnoreCase)
                            || type.Equals("BLOB", StringComparison.OrdinalIgnoreCase)
                            || string.IsNullOrEmpty(type))
                            textOrBlobCols.Add(colName);
                    }
                }

                if (textOrBlobCols.Count > 0 && rowCount > 0)
                {
                    var lenExpr = string.Join(" + ",
                        textOrBlobCols.Select(col => $"COALESCE(length(\"{col}\"), 0)"));
                    await using var sumCmd = connection.CreateCommand();
                    sumCmd.CommandText = $"SELECT SUM({lenExpr}) FROM \"{table}\"";
                    payload = Convert.ToInt64(await sumCmd.ExecuteScalarAsync(ct).ConfigureAwait(false) ?? 0L);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Per-table failure shouldn't abort the whole snapshot —
                // log and continue with whatever partial numbers we have.
                mLog.LogDebug(ex, "DbStats: failed to size table {Table}", table);
            }

            rows.Add(new DbTableStats(
                Name: table,
                RowCount: rowCount,
                TextOrBlobPayloadBytes: payload,
                IndexCount: indexCounts.TryGetValue(table, out var idx) ? idx : 0));
        }

        // Sort by payload desc — the typical "what's eating my disk"
        // ordering, matches the DbInspect output.
        rows.Sort((a, b) => b.TextOrBlobPayloadBytes.CompareTo(a.TextOrBlobPayloadBytes));

        var snapshot = new DbStatsSnapshot(dbPath, onDiskBytes, freelistBytes, rows);
        // Notify adjacent diagnostics (e.g. plugin-side refresh-queue stats)
        // so they can invalidate their own cached snapshots on the next
        // render. Swallow handler exceptions — one rogue subscriber must
        // not abort the gather result.
        try { StatsRefreshed?.Invoke(); }
        catch (Exception ex) { mLog.LogDebug(ex, "DbStats: StatsRefreshed handler threw."); }
        return snapshot;
    }
}
