# NexusKit.Persistence

SQLite + EF Core plumbing. Provides one shared `DbContext` per plugin into
which modules contribute their entity configurations, plus a fluent settings
schema and modular schema-evolution pipeline.

**No Dalamud reference.** Consumers see EF Core / SQLite types; the storage
location is derived from `IPluginContext.ConfigDirectory`.

## Public API

| Type | File | Purpose |
|---|---|---|
| `PluginDbContext` | `PluginDbContext.cs` | Generic DbContext; collects every registered `IEntityModule` in `OnModelCreating`. |
| `INexusDbContextFactory` | `INexusDbContextFactory.cs` | **Lifetime-aware** factory consumers depend on instead of the raw `IDbContextFactory<PluginDbContext>`. Auto-links any caller `ct` with `IPluginLifetime.Stopping`; exposes `LifetimeToken` for `SaveChangesAsync` / `BeginTransactionAsync`. |
| `IEntityModule` | `Schema/IEntityModule.cs` | A module contributes its tables via `ConfigureEntities(ModelBuilder)` and declares a `TablePrefix` — the framework prepends `nexus_<TablePrefix>_` to every table the module configures. |
| `IDatabaseViewBuilder` | `Schema/IDatabaseViewBuilder.cs` | Idempotent `DROP VIEW IF EXISTS … ; CREATE VIEW …` runner. Builders run after migrations, on every plugin start; views re-create cleanly when the underlying schema shifts. |
| `ISettingsStore` | `Settings/ISettingsStore.cs` | Plain key-value POCO storage (`GetAsync<T>` / `SetAsync<T>` / `DeleteAsync`). |
| `SettingsEntity` | `Settings/SettingsEntity.cs` | The `settings` table row (Key / Value / UpdatedAt). |
| `SettingsSchemaBuilder<T>` | `Settings/Schema/` | Fluent API: `Group`, `Title`, `Property(x => x.Foo, p => p.Label(…).Slider(…))`, … |
| `SettingsPropertyBuilder<T, TValue>` | `Settings/Schema/` | Per-property builder; `.Label / .LabelKey / .Description / .DescriptionKey / .Placeholder / .PlaceholderKey / .Order / .Checkbox / .TextBox / .NumericInput / .Slider(min,max) / .Choices(...) / .Hidden`. |
| `SettingsSchemaBuilderExtensions` | `Settings/Schema/` | `.RegisterModuleEnabledFlag(order)` shortcut for `IModuleSettings` (registers the hidden `ModuleEnabled` toggle with framework labels). |
| `IRegisteredSettingsSchema`, `ISettingsSchemaProvider` | `Settings/Schema/` | Discovered by AutoSettingsWindow. |
| `IMigration`, `IMigrationModule` | `Migrations/` | Forward-only schema evolution step + module bundling. |
| `DbInitializer` | `Migrations/DbInitializer.cs` | Runs `EnsureCreatedAsync` + per-module baseline-or-apply at startup. |
| `IDbMaintenanceService`, `DbMaintenanceService` | `Maintenance/` | Background loop driving every registered `IDbMaintenanceContributor` on a 15-min tick; owns WAL-checkpoint + connection-pool clear during shutdown. |
| `IDbMaintenanceContributor` | `Maintenance/IDbMaintenanceContributor.cs` | One unit of periodic housekeeping (`Name`, `Interval`, `RunAsync`). Last-run timestamps persist via `MaintenanceState`. |
| `IDbStatsService`, `DbStatsService` | `Maintenance/` | Read-only snapshot: on-disk + freelist bytes, per-table row count + index count + payload bytes. Powers the auto-settings DB-maintenance section. |

## Registration

```csharp
services.AddNexusKitPersistence();           // DbContextFactory + INexusDbContextFactory
                                             // + DbMaintenanceService + VacuumAndOptimizeContributor
                                             // + DbStatsService + tracking entity
services.AddNexusKitSettings();              // Settings entity + ISettingsStore
services.AddEntityModule<MyEntityModule>();  // contribute custom tables
services.AddViewBuilder<MyViewBuilder>();    // contribute SQL views
services.AddSettings<MySettings>(b => …);    // declare a settings schema
services.AddMigrationModule<MyMigrations>(); // contribute schema migrations
services.AddMaintenanceContributor<MyMaint>(); // contribute periodic housekeeping
```

All extensions in `PersistenceServiceCollectionExtensions.cs`.

## Dependencies

- NuGet: `Microsoft.EntityFrameworkCore` 10.0.0, `Microsoft.EntityFrameworkCore.Sqlite` 10.0.0
- ProjectRef: `NexusKit.Core`

## Database location

`%APPDATA%/XIVLauncher/pluginConfigs/<PluginName>/<PluginName>.db` — derived
from `IPluginContext.PluginName` + `ConfigDirectory`. One file per plugin;
all module tables live in it side-by-side.

## Example: contribute an entity + read/write it

```csharp
// Entity
public sealed class MountCacheEntity
{
    public string Key { get; set; } = null!;
    public string Response { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
}

// Entity module — table is auto-prefixed to "nexus_mymodule_mount_cache"
internal sealed class MountCacheEntityModule : IEntityModule
{
    public string TablePrefix => "mymodule";

    public void ConfigureEntities(ModelBuilder mb) => mb.Entity<MountCacheEntity>(e =>
    {
        e.ToTable("mount_cache");
        e.HasKey(x => x.Key);
        e.Property(x => x.Response).IsRequired();
    });
}

// Registration
services.AddSingleton<IEntityModule, MountCacheEntityModule>();

// Usage (any service injects IDbContextFactory<PluginDbContext>)
public sealed class MyClient
{
    private readonly IDbContextFactory<PluginDbContext> factory;
    public MyClient(IDbContextFactory<PluginDbContext> f) { factory = f; }

    public async Task<string?> ReadAsync(string key, CancellationToken ct)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var row = await ctx.Set<MountCacheEntity>().FindAsync([key], cancellationToken: ct);
        return row?.Response;
    }
}
```

## Where to read next

- [docs/settings.md](docs/settings.md) — full fluent-schema reference + module
  vs plugin schema conventions.
- [docs/migrations.md](docs/migrations.md) — when to write a migration, the
  baseline-vs-apply rule, SQLite caveats.
- [docs/entity-modules.md](docs/entity-modules.md) — `IEntityModule` and the
  `nexus_<prefix>_*` table-naming contract.
- [docs/maintenance.md](docs/maintenance.md) — the periodic-contributor loop,
  shutdown chores, and how to register cache eviction / VACUUM units.

---

**Maintenance**: when you add a builder method, change `IMigrationModule`
semantics, or move a public type, update this README and the linked docs.
