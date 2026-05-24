namespace NexusKit.Persistence.Maintenance;

/// <summary>
/// Background DB maintenance loop. Started once by the plugin host after
/// <see cref="Migrations.DbInitializer.InitializeAsync"/>; runs every
/// registered <see cref="IDbMaintenanceContributor"/> on its declared
/// <see cref="IDbMaintenanceContributor.Interval"/> until the host shuts
/// down.
///
/// <para>The service hooks <see cref="NexusKit.Core.IPluginLifetime.Stopping"/>
/// for shutdown coordination — the background loop unwinds automatically
/// when the lifetime token cancels. SQLite shutdown chores (WAL
/// checkpoint + connection-pool clear) run from the service's
/// <see cref="IDisposable.Dispose"/> / <see cref="IAsyncDisposable.DisposeAsync"/>
/// path, invoked by the DI container at provider tear-down. Callers do
/// not need to invoke any shutdown method manually.</para>
/// </summary>
public interface IDbMaintenanceService
{
    /// <summary>Run every contributor immediately, ignoring their
    /// last-run gates. Used by the Settings UI's "Run now" button. Returns
    /// once every contributor has either completed or thrown (errors are
    /// caught + logged per-contributor so one failure doesn't abort the
    /// rest). Last-run timestamps are updated on success.</summary>
    Task RunNowAsync(CancellationToken ct = default);

    /// <summary>Schedule snapshot for the Settings UI — one entry per
    /// registered contributor (including those that have never run yet, so
    /// freshly-added contributors show up immediately without waiting for
    /// their first execution). Carries the declared <c>Interval</c>, the
    /// persisted last-run timestamp from <see cref="MaintenanceState"/>
    /// (<see cref="DateTime.MinValue"/> when absent), and the derived
    /// next-run target.</summary>
    Task<IReadOnlyList<MaintenanceScheduleEntry>> GetScheduleSnapshotAsync(CancellationToken ct = default);
}
