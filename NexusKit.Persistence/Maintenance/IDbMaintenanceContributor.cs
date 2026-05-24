using Microsoft.EntityFrameworkCore;

namespace NexusKit.Persistence.Maintenance;

/// <summary>
/// A unit of periodic database housekeeping contributed by a module. The
/// <see cref="IDbMaintenanceService"/> walks every registered contributor on
/// a fixed inner-tick cadence and invokes those whose <see cref="Interval"/>
/// has elapsed since their last successful run. Last-run timestamps are
/// persisted via <see cref="MaintenanceState"/>, so a plugin restart doesn't
/// re-trigger work that just ran.
///
/// <para>Implementations should be idempotent and side-effect-scoped: a
/// single contributor's failure must not block others. The service catches
/// exceptions and skips updating the last-run stamp on failure, so the next
/// tick retries automatically.</para>
/// </summary>
public interface IDbMaintenanceContributor
{
    /// <summary>Stable identifier — used as the key in
    /// <see cref="MaintenanceState.LastRunUtcByName"/> and as the display
    /// label in maintenance UIs. Lowercase-kebab convention, e.g.
    /// <c>"refresh-queue-exhausted-prune"</c>.</summary>
    string Name { get; }

    /// <summary>Minimum time between successive runs. The service guarantees
    /// that <c>RunAsync</c> is only invoked when the gap since
    /// <see cref="MaintenanceState.LastRunUtcByName"/> &gt;= this value (or
    /// when the contributor has never run).</summary>
    TimeSpan Interval { get; }

    /// <summary>Execute the contributor's work. The supplied
    /// <paramref name="ctx"/> is fresh per invocation — implementations may
    /// hold it for the duration of the call but should not capture it
    /// beyond the method's scope. <paramref name="ct"/> is honoured during
    /// plugin shutdown.</summary>
    Task RunAsync(DbContext ctx, CancellationToken ct);
}
