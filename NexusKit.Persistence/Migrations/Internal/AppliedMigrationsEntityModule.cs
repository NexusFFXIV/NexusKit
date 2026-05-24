using Microsoft.EntityFrameworkCore;
using NexusKit.Persistence.Schema;

namespace NexusKit.Persistence.Migrations.Internal;

internal sealed class AppliedMigrationsEntityModule : IEntityModule
{
    // Empty prefix on purpose: migration history is shared framework infrastructure,
    // not module-owned. Every IMigrationModule writes its rows here under its own ModuleId.
    public string TablePrefix => string.Empty;

    public void ConfigureEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppliedMigrationEntity>(e =>
        {
            e.ToTable("migrations");
            e.HasKey(x => new { x.ModuleId, x.MigrationId });
            e.Property(x => x.ModuleId).HasColumnName("module_id").HasMaxLength(256);
            e.Property(x => x.MigrationId).HasColumnName("migration_id").HasMaxLength(256);
            e.Property(x => x.AppliedAt).HasColumnName("applied_at");
        });
    }
}
