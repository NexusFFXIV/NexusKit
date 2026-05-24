# Entity Modules (NexusKit.Persistence)

How modules contribute tables to the shared plugin database.

## The contract

```csharp
public interface IEntityModule
{
    string TablePrefix { get; }
    void ConfigureEntities(ModelBuilder modelBuilder);
}
```

Implementations call `ModelBuilder.Entity<T>()` to map their tables. The
framework's `PluginDbContext` collects every registered `IEntityModule` via
constructor injection and runs each one's `ConfigureEntities` inside
`OnModelCreating`, then prepends `nexus_<TablePrefix>_` to every table the
module configured. Two modules can therefore both ship a logical `player`
table without colliding — they land as `nexus_a_player` and `nexus_b_player`.

An empty `TablePrefix` opts out of the rewrite; it's reserved for shared
framework tables (e.g. the cross-module `settings` key/value store) and
should not be used by application modules.

## Why one shared `DbContext`

Multiple modules contributing to a single DbContext (instead of one DbContext
per module) means:
- One SQLite file per plugin — simple backup story, no cross-file joins
  to worry about.
- Migrations are sequenced per module via `IMigrationModule`, not per-DbContext.
- `EnsureCreatedAsync` automatically picks up a new module's tables when
  the user upgrades.

## Writing an entity module

```csharp
// 1. Your entity (POCO)
public sealed class MountCacheEntity
{
    public string Key { get; set; } = null!;
    public string Response { get; set; } = null!;
    public DateTime FetchedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

// 2. The module — declares a TablePrefix; the table will land as
//    "nexus_mymodule_mount_cache".
internal sealed class MountCacheEntityModule : IEntityModule
{
    public string TablePrefix => "mymodule";

    public void ConfigureEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MountCacheEntity>(e =>
        {
            e.ToTable("mount_cache");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasColumnName("key").HasMaxLength(512);
            e.Property(x => x.Response).HasColumnName("response").IsRequired();
            e.Property(x => x.FetchedAt).HasColumnName("fetched_at");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        });
    }
}

// 3. Registration (in the module's AddNexusKitXxx extension)
services.AddSingleton<IEntityModule, MountCacheEntityModule>();
// Or shorter:
services.AddEntityModule<MountCacheEntityModule>();
```

## Table-naming convention

Tables land as `nexus_<TablePrefix>_<rawName>`. The framework enforces this in
`PluginDbContext.OnModelCreating` — modules only set the raw name via
`e.ToTable("…")` and declare their `TablePrefix`. An empty or whitespace
prefix throws at startup, so collisions can't slip through.

| Source | TablePrefix | Raw name | Resolved table |
|---|---|---|---|
| `NexusKit.Modules.FfxivCollect` | `ffxivcollect` | `cache` | `nexus_ffxivcollect_cache` |
| `NexusKit.Modules.Lodestone` | `lodestone` | `cache` | `nexus_lodestone_cache` |
| `NexusKit.Modules.ExternalData` | `external` | `player` | `nexus_external_player` |
| Framework migration tracking | *(empty — escape hatch)* | `migrations` | `migrations` |
| Framework settings | *(empty — escape hatch)* | `settings` | `settings` |

The plugin's own tables (encounters, players, whatever) still follow whatever
naming you prefer — they're not module-owned.

## Reading + writing entities

Inject `IDbContextFactory<PluginDbContext>`. Each call gets a fresh context,
auto-disposed by `using`:

```csharp
internal sealed class MountCache
{
    private readonly IDbContextFactory<PluginDbContext> factory;
    public MountCache(IDbContextFactory<PluginDbContext> f) { factory = f; }

    public async Task<MountCacheEntity?> ReadAsync(string key, CancellationToken ct)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        return await ctx.Set<MountCacheEntity>().FindAsync([key], cancellationToken: ct);
    }

    public async Task WriteAsync(string key, string response, DateTime expires, CancellationToken ct)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var existing = await ctx.Set<MountCacheEntity>().FindAsync([key], cancellationToken: ct);
        if (existing is null)
        {
            ctx.Set<MountCacheEntity>().Add(new MountCacheEntity
            {
                Key = key, Response = response,
                FetchedAt = DateTime.UtcNow, ExpiresAt = expires,
            });
        }
        else
        {
            existing.Response = response;
            existing.FetchedAt = DateTime.UtcNow;
            existing.ExpiresAt = expires;
        }
        await ctx.SaveChangesAsync(ct);
    }
}
```

`PluginDbContext` is registered as a transient via
`AddDbContextFactory<PluginDbContext>` — the factory pools the underlying
`DbContextOptions` while each `CreateDbContextAsync` call yields a fresh
context. Don't share a context across awaits.

## Evolution

Adding a new `IEntityModule` later is fine — `EnsureCreatedAsync` creates the
module's tables on first plugin startup after the new module is registered.

Changing an *existing* entity module's schema requires an `IMigrationModule`
— see [migrations.md](migrations.md).

## SQLite-specific configuration

- `HasMaxLength(N)` is informational on SQLite (no enforcement) but useful
  for downstream tooling and for staying portable should you ever switch
  providers.
- Composite keys: `e.HasKey(x => new { x.A, x.B });`
- Indexes: `e.HasIndex(x => x.SomeCol);` — fine for read-heavy lookups.

---

**Maintenance**: when you change the table-naming convention, the
factory-registration pattern, or the way `OnModelCreating` collects modules,
update this doc.
