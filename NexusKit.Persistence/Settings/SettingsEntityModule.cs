using Microsoft.EntityFrameworkCore;
using NexusKit.Persistence.Schema;

namespace NexusKit.Persistence.Settings;

internal sealed class SettingsEntityModule : IEntityModule
{
    // Empty prefix on purpose: the settings table is the shared cross-module key/value
    // store, not module-owned. Each module persists its settings POCO as a JSON value
    // keyed by its own StoreKey via ISettingsStore.
    public string TablePrefix => string.Empty;

    public void ConfigureEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SettingsEntity>(e =>
        {
            e.ToTable("settings");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasColumnName("key").HasMaxLength(256);
            e.Property(x => x.Value).HasColumnName("value").IsRequired();
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });
    }
}
