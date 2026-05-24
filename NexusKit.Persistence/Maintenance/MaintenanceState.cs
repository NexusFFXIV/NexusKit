namespace NexusKit.Persistence.Maintenance;

/// <summary>
/// Persisted last-run-per-contributor map. Lives under a single settings
/// key (POCO shape, like <c>PlayerFilterCollection</c>) so a plugin restart
/// doesn't lose track of when each contributor last ran — without this the
/// 7-day weekly contributors would re-fire on every startup.
/// </summary>
public sealed class MaintenanceState
{
    public const string StoreKey = "nexuskit.persistence.maintenance.state";

    /// <summary>Last successful run timestamp per
    /// <see cref="IDbMaintenanceContributor.Name"/>. Missing keys are
    /// treated as <c>DateTime.MinValue</c> by the service, so a
    /// newly-registered contributor runs on the next tick after startup.</summary>
    public Dictionary<string, DateTime> LastRunUtcByName { get; set; } = new();
}
