namespace NexusKit.Persistence.Maintenance;

/// <summary>
/// Per-contributor schedule snapshot consumed by the Settings UI. Mirrors
/// what the maintenance loop sees on each tick: declared <see cref="Interval"/>,
/// the persisted <see cref="LastRunUtc"/> (or <see cref="DateTime.MinValue"/>
/// when the contributor has never run yet), and the derived
/// <see cref="NextRunUtc"/> = <c>LastRunUtc + Interval</c> that the loop
/// gates against on every wakeup.
/// </summary>
public sealed record MaintenanceScheduleEntry(
    string Name,
    TimeSpan Interval,
    DateTime LastRunUtc,
    DateTime NextRunUtc);
