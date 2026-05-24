using Microsoft.EntityFrameworkCore;
using NexusKit.Persistence.Schema;

namespace NexusKit.Persistence;

public class PluginDbContext : DbContext
{
    private readonly IEnumerable<IEntityModule> mEntityModules;

    public PluginDbContext(DbContextOptions<PluginDbContext> options, IEnumerable<IEntityModule> entityModules)
        : base(options)
    {
        mEntityModules = entityModules;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        foreach (var module in mEntityModules)
        {
            var before = modelBuilder.Model.GetEntityTypes().Select(t => t.ClrType).ToHashSet();
            module.ConfigureEntities(modelBuilder);
            ApplyTablePrefix(modelBuilder, module, before);
        }
    }

    private static void ApplyTablePrefix(ModelBuilder modelBuilder, IEntityModule module, HashSet<Type> alreadyConfigured)
    {
        // Empty prefix is the documented escape hatch for shared framework tables
        // (e.g. the cross-module settings store) — leave those names untouched.
        if (string.IsNullOrEmpty(module.TablePrefix)) return;

        var fullPrefix = $"nexus_{module.TablePrefix}_";

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (alreadyConfigured.Contains(entityType.ClrType)) continue;

            var current = entityType.GetTableName();
            if (current is null) continue;
            if (current.StartsWith(fullPrefix, StringComparison.Ordinal)) continue;

            entityType.SetTableName(fullPrefix + current);
        }
    }
}
