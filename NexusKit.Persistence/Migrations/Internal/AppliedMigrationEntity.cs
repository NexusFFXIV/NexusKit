namespace NexusKit.Persistence.Migrations.Internal;

internal sealed class AppliedMigrationEntity
{
    public string ModuleId { get; set; } = null!;
    public string MigrationId { get; set; } = null!;
    public DateTime AppliedAt { get; set; }
}
