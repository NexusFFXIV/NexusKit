using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexusKit.Core;
using NexusKit.Persistence.Settings;

namespace NexusKit.Persistence.Maintenance;

internal sealed class DbMaintenanceService : IDbMaintenanceService, IDisposable, IAsyncDisposable
{
    /// <summary>How often the background loop wakes up to re-evaluate
    /// contributor due-dates. 15 minutes is a comfortable compromise —
    /// fine-grained enough that a freshly-elapsed daily contributor
    /// doesn't sit idle for long, coarse enough that plugin idle CPU is
    /// negligible.</summary>
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(15);

    /// <summary>Brief delay before the first tick so plugin startup
    /// (migrations, view builds, observation hydrate) settles before
    /// maintenance starts probing.</summary>
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(1);

    private readonly IDbContextFactory<PluginDbContext> mFactory;
    private readonly ISettingsStore mSettings;
    private readonly IEnumerable<IDbMaintenanceContributor> mContributors;
    private readonly IPluginLifetime mLifetime;
    private readonly ILogger<DbMaintenanceService> mLog;

    private Task? mLoopTask;
    private bool mDisposed;

    public DbMaintenanceService(
        IDbContextFactory<PluginDbContext> factory,
        ISettingsStore settings,
        IEnumerable<IDbMaintenanceContributor> contributors,
        IPluginLifetime lifetime,
        ILogger<DbMaintenanceService> log)
    {
        mFactory = factory;
        mSettings = settings;
        mContributors = contributors;
        mLifetime = lifetime;
        mLog = log;

        // Auto-start the loop on construction. The framework-wide lifetime
        // token cancels at plugin shutdown which winds the loop down, so
        // we don't need a separate "start" hook from the host. The 1-minute
        // initial delay inside LoopAsync gives migrations + view builders
        // time to finish before contributors start probing tables.
        mLoopTask = Task.Run(() => LoopAsync(mLifetime.Stopping));
    }

    public async Task RunNowAsync(CancellationToken ct = default)
    {
        // Force-run path: bypass the per-contributor interval gate and
        // execute every registered contributor. Errors are isolated so a
        // single failure doesn't abort the run.
        var state = await LoadStateAsync(ct).ConfigureAwait(false);
        var now = DateTime.UtcNow;
        var anyChange = false;
        foreach (var contributor in mContributors)
        {
            ct.ThrowIfCancellationRequested();
            if (await RunOneAsync(contributor, state, now, ct).ConfigureAwait(false))
                anyChange = true;
        }
        if (anyChange) await SaveStateAsync(state, ct).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (mDisposed) return;
        mDisposed = true;

        // Background loop is winding down on its own — the framework's
        // lifetime token was already cancelled by PluginHost before the DI
        // container started disposing services. Wait for the loop to
        // finish so the SQLite shutdown chores below don't race a
        // still-active DbContext from the tick.
        if (mLoopTask is not null)
        {
            try { await mLoopTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { /* expected */ }
            catch (Exception ex) { mLog.LogWarning(ex, "Maintenance loop terminated with an unhandled error."); }
        }

        await RunShutdownChoresAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (mDisposed) return;
        mDisposed = true;

        // Sync disposal path (Dalamud's IDisposable.Dispose). Block for
        // the loop to exit, then run the chores synchronously — the
        // SQLite provider has sync equivalents for everything we need,
        // and Wait is fine here because the lifetime token already fired
        // so the loop is on its way out.
        try { mLoopTask?.Wait(TimeSpan.FromSeconds(5)); }
        catch (AggregateException) { /* OperationCanceledException inside */ }
        catch (Exception ex) { mLog.LogWarning(ex, "Maintenance loop terminated with an unhandled error."); }

        RunShutdownChoresSync();
    }

    /// <summary>WAL checkpoint + connection pool clear. Best-effort: if
    /// either step throws, log at Debug and move on — the host's shutdown
    /// must not be blocked by a DB hiccup.</summary>
    private async Task RunShutdownChoresAsync()
    {
        try
        {
            await using var ctx = await mFactory.CreateDbContextAsync().ConfigureAwait(false);
            await ctx.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE)").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            mLog.LogDebug(ex, "WAL checkpoint at shutdown skipped (DB may be inaccessible).");
        }

        try { SqliteConnection.ClearAllPools(); }
        catch (Exception ex)
        {
            mLog.LogDebug(ex, "ClearAllPools at shutdown failed (already disposed?).");
        }
    }

    private void RunShutdownChoresSync()
    {
        try
        {
            using var ctx = mFactory.CreateDbContext();
            ctx.Database.ExecuteSqlRaw("PRAGMA wal_checkpoint(TRUNCATE)");
        }
        catch (Exception ex)
        {
            mLog.LogDebug(ex, "WAL checkpoint at shutdown skipped (DB may be inaccessible).");
        }

        try { SqliteConnection.ClearAllPools(); }
        catch (Exception ex)
        {
            mLog.LogDebug(ex, "ClearAllPools at shutdown failed (already disposed?).");
        }
    }

    public async Task<IReadOnlyList<MaintenanceScheduleEntry>> GetScheduleSnapshotAsync(CancellationToken ct = default)
    {
        // Enumerate registered contributors as the source of truth (NOT the
        // state dict) so a freshly-added contributor with no run yet still
        // shows up in the Settings UI on the very next refresh — its
        // LastRunUtc reads as DateTime.MinValue and the UI surfaces it as
        // "due / never run".
        var state = await LoadStateAsync(ct).ConfigureAwait(false);
        var entries = new List<MaintenanceScheduleEntry>();
        foreach (var c in mContributors)
        {
            var lastRun = state.LastRunUtcByName.TryGetValue(c.Name, out var ts)
                ? ts : DateTime.MinValue;
            // Clamp the next-run computation when lastRun is MinValue: adding
            // Interval to MinValue would still be in the past (DateTime range
            // dwarfs any sane interval), so the result naturally renders as
            // "due now" — no special case needed.
            var nextRun = lastRun == DateTime.MinValue ? DateTime.MinValue : lastRun + c.Interval;
            entries.Add(new MaintenanceScheduleEntry(c.Name, c.Interval, lastRun, nextRun));
        }
        return entries;
    }

    // ─── internals ──────────────────────────────────────────────────────

    private async Task LoopAsync(CancellationToken ct)
    {
        try { await Task.Delay(InitialDelay, ct).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }

        while (!ct.IsCancellationRequested)
        {
            try { await TickAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                // Per-tick error fence: a single contributor's RunOneAsync
                // catches its own failures, but anything else (state load
                // / save) surfaces here. Log and continue — the next tick
                // retries automatically.
                mLog.LogWarning(ex, "DB maintenance tick failed.");
            }

            try { await Task.Delay(TickInterval, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var state = await LoadStateAsync(ct).ConfigureAwait(false);
        var now = DateTime.UtcNow;
        var anyChange = false;

        foreach (var contributor in mContributors)
        {
            ct.ThrowIfCancellationRequested();
            var lastRun = state.LastRunUtcByName.TryGetValue(contributor.Name, out var ts)
                ? ts : DateTime.MinValue;
            if (now - lastRun < contributor.Interval) continue;

            if (await RunOneAsync(contributor, state, now, ct).ConfigureAwait(false))
                anyChange = true;
        }

        if (anyChange) await SaveStateAsync(state, ct).ConfigureAwait(false);
    }

    /// <summary>Run a single contributor and stamp its last-run time on
    /// success. Errors are caught + logged; the timestamp is NOT updated
    /// when the contributor throws, so the next tick retries.</summary>
    /// <returns>True when the state map was mutated and should be saved.</returns>
    private async Task<bool> RunOneAsync(
        IDbMaintenanceContributor contributor,
        MaintenanceState state,
        DateTime now,
        CancellationToken ct)
    {
        try
        {
            await using var ctx = await mFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await contributor.RunAsync(ctx, ct).ConfigureAwait(false);
            state.LastRunUtcByName[contributor.Name] = now;
            mLog.LogDebug("DB maintenance contributor '{Name}' completed.", contributor.Name);
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            mLog.LogWarning(ex, "DB maintenance contributor '{Name}' failed.", contributor.Name);
            return false;
        }
    }

    private async Task<MaintenanceState> LoadStateAsync(CancellationToken ct)
    {
        var loaded = await mSettings.GetAsync<MaintenanceState>(MaintenanceState.StoreKey, ct).ConfigureAwait(false);
        return loaded ?? new MaintenanceState();
    }

    private Task SaveStateAsync(MaintenanceState state, CancellationToken ct)
        => mSettings.SetAsync(MaintenanceState.StoreKey, state, ct);
}
