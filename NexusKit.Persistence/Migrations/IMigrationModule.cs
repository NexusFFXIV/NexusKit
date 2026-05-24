namespace NexusKit.Persistence.Migrations;

/// <summary>
/// Bundles a module's schema-evolution history. Each module (and the plugin itself)
/// can register one implementation via <c>AddMigrationModule&lt;T&gt;()</c>.
/// </summary>
public interface IMigrationModule
{
    /// <summary>
    /// Stable identifier scoped to the contributing module — e.g.
    /// "nexuskit.modules.ffxivcollect". Used as a foreign key in the
    /// applied-migrations tracking table.
    /// </summary>
    string ModuleId { get; }

    /// <summary>
    /// Full migration history for this module. The framework decides per startup
    /// which ones to actually run (baseline on first contact, apply pending after).
    /// </summary>
    IReadOnlyList<IMigration> Migrations { get; }
}
