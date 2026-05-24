# DB maintenance

How the framework keeps the plugin's SQLite database tidy without each
module reinventing its own background sweep.

## Pieces

```
DbMaintenanceService         — single background loop. One per plugin.
    └─ IDbMaintenanceContributor   — N units of work, contributed by modules.
                                    Each declares Name + Interval + RunAsync.

IDbStatsService              — read-only snapshot of file sizes + per-table rows.
                                Powers the auto-settings UI; not required for the loop.

MaintenanceState             — persisted last-run timestamps per contributor.
                                Lives in the `settings` table under a known key.
```

`DbMaintenanceService` auto-starts on construction (no host hook required)
and watches `IPluginLifetime.Stopping` for graceful unwind. Modules wire
contributors via `services.AddMaintenanceContributor<T>()`; the framework
walks every contributor on a fixed inner tick and invokes those whose
`Interval` has elapsed since their last successful run.

## Inner-tick cadence

- **Initial delay**: 1 minute after construction. Lets migrations + view
  builds + observation hydration finish before contributors start probing
  tables.
- **Tick interval**: every 15 minutes. Each tick re-evaluates every
  contributor's due-date independently.

Both constants live in `DbMaintenanceService`; bump them only if you have
a concrete reason. Shorter ticks burn idle CPU; longer ticks delay
post-elapsed runs by up to the tick length.

## What ships as a contributor today

| Contributor | Source | Interval | What it does |
|---|---|---|---|
| `VacuumAndOptimizeContributor` | NexusKit.Persistence | 7 days | `ANALYZE; PRAGMA optimize; VACUUM; REINDEX nexuskit_migrations` — keeps the file compact and the query planner well-calibrated. |
| `ExpiredRowMaintenanceContributor` | NexusKit.Persistence | 24 hours | Deletes expired rows from any table that registered an expiry policy. |
| `FfxivCollectCacheMaintenanceContributor` | Modules.FfxivCollect | 12 hours | Drops `nexus_ffxivcollect_cache` rows past their `expires_at`. |
| `LodestoneCacheMaintenanceContributor` | Modules.Lodestone | 12 hours | Same for `nexus_lodestone_cache`. |
| `RefreshQueueMaintenanceContributor` | Modules.PlayerEnrichment | 24 hours | Prunes exhausted refresh-queue rows (`attempt_count ≥ 5` past their backoff window) that have parked indefinitely. |

A plugin can swap the framework's defaults with `Remove<T>` + a different
registration if it wants different cadence per environment.

## Writing a contributor

```csharp
internal sealed class MyCacheEvictionContributor : IDbMaintenanceContributor
{
    public string Name => "my-module-cache-eviction";
    public TimeSpan Interval => TimeSpan.FromHours(6);

    public Task RunAsync(DbContext ctx, CancellationToken ct)
        => ctx.Database.ExecuteSqlRawAsync(
            "DELETE FROM nexus_mymodule_cache WHERE expires_at < @cutoff",
            new SqliteParameter("@cutoff", DateTime.UtcNow), ct);
}
```

Register in the module's `AddNexusKit<Module>()` extension:

```csharp
services.AddMaintenanceContributor<MyCacheEvictionContributor>();
```

### Contract requirements

- **`Name` must be stable.** Used as the key in `MaintenanceState` — change
  it and you reset that contributor's "last ran" memory. Lowercase-kebab
  by convention (e.g. `"refresh-queue-exhausted-prune"`).
- **`Interval` is a minimum.** The service guarantees `RunAsync` is only
  invoked when `now − lastRun ≥ Interval`, but the upper bound is
  effectively `Interval + 15 min` (worst-case tick alignment).
- **`RunAsync` must be idempotent.** A previous run that failed mid-flight,
  or a "Run now" force-invocation, can call you with rows already gone.
  `DELETE … WHERE …` is fine; CREATE-only DDL would need IF NOT EXISTS.
- **Errors are isolated.** One contributor throwing doesn't abort the tick
  — the service catches + logs + skips updating that contributor's last-run
  stamp, so the next tick retries automatically. Don't catch exceptions
  yourself unless you also stamp the success manually.

### Failure handling

A single contributor failure does NOT bubble up: `DbMaintenanceService`
wraps each `RunAsync` in a try/catch, logs at `Warning`, leaves the
last-run timestamp unchanged, and moves on to the next contributor. The
next tick re-tries.

## "Run now" path

`IDbMaintenanceService.RunNowAsync()` bypasses the per-contributor
`Interval` gate and invokes every contributor in one sweep. The
auto-settings DB-maintenance section wires this to a button; users hit it
when they want to force a sweep after wiping a chunk of data.

Errors during a force-run are still per-contributor isolated — one failure
doesn't stop the others.

## Shutdown chores

When the plugin disposes, `DbMaintenanceService.DisposeAsync`:

1. Waits for the running loop to exit (driven by the lifetime token).
2. Opens a fresh DbContext and runs `PRAGMA wal_checkpoint(TRUNCATE)`.
   Trims the WAL sidecar so the next plugin start opens against a
   compact file instead of replaying an accumulated journal.
3. `SqliteConnection.ClearAllPools()` — releases pooled connections so
   the file lock drops cleanly.

Steps 2–3 are best-effort: any exception logs at `Debug` and the host
keeps unwinding.

## UI surface

`DbMaintenanceSettingsSection` (NexusKit.Ui) renders:

- **Header**: on-disk bytes, payload bytes (sum across tables), "other"
  (indexes + page padding + WAL/SHM), freelist bytes, file path.
- **Run now / Refresh** buttons + a single status slot for "running…"
  and last-run error.
- **Last runs**: every registered contributor's `Name` + relative
  timestamp.
- **Tables**: per-table row count, index count, payload bytes, plus a
  totals row.

Wire it in via `services.AddDbMaintenanceSettingsSection(order: 200)`.

---

**Maintenance**: when you add a new built-in contributor, change the tick
cadence, or alter the shutdown-chore behaviour, update the table above
and the inline notes in `DbMaintenanceService`.
