# Schema migrations

How to evolve a module's database schema across releases without breaking
existing user installations.

## Why we have this

`DbInitializer.InitializeAsync` calls `EnsureCreatedAsync` first — that
creates any *new* tables introduced by an `IEntityModule`, which already
handles the "user installs a module they didn't have before" case.

What `EnsureCreated` can **never** do is modify an existing table. As soon
as a shipped module needs an extra column, a renamed column, a new index,
or any other in-place schema change, we need real migrations.

## Mental model

Each module declares an **ordered list of migrations**. Each migration is a
forward-only schema change identified by a sortable string ID.

On every startup, the framework looks at the `nexuskit_migrations` table to
see which migrations have been recorded for a given module:

- **No rows for this module** → first contact with this DB. `EnsureCreated`
  has already produced the latest schema, so we **baseline**: mark every
  migration as applied **without running it**.
- **Some rows exist** → user has been on a prior version. We apply only the
  migrations whose IDs are missing, in ascending ID order.

This means a brand-new install never runs historical SQL — it gets the
current schema for free via `EnsureCreated`. Only upgrade paths actually
execute migrations.

## Writing a migration

```csharp
internal sealed class AddCacheTtlMigration : IMigration
{
    public string Id => "20260601_add_cache_ttl";

    public Task UpAsync(DbContext ctx, CancellationToken ct)
        => ctx.Database.ExecuteSqlRawAsync(
            "ALTER TABLE nexus_ffxivcollect_cache " +
            "ADD COLUMN ttl_seconds INTEGER NOT NULL DEFAULT 86400;",
            ct);
}
```

Bundle migrations in an `IMigrationModule`:

```csharp
internal sealed class FfxivCollectMigrations : IMigrationModule
{
    public string ModuleId => "nexuskit.modules.ffxivcollect";

    public IReadOnlyList<IMigration> Migrations { get; } = new IMigration[]
    {
        new AddCacheTtlMigration(),
        // append more as the schema evolves
    };
}
```

Register in the module's `AddNexusKitXyz()`:

```csharp
services.AddMigrationModule<FfxivCollectMigrations>();
```

That's it. `PluginHostBuilder.BuildAsync` resolves every registered
`IMigrationModule`, hands them to `DbInitializer`, and the runner applies
what's needed.

## ID convention

`Id` is the sort key. We use `yyyymmdd_short_description` so:
- Lexical sort = chronological order
- Identifiers stay human-readable in the database
- Two migrations created on the same day differ by description

The framework only requires unique strings within a single `ModuleId`;
formatting is by convention.

## SQLite limitations to plan around

SQLite's `ALTER TABLE` is restricted:

| Operation | Works? |
|---|---|
| `ADD COLUMN` | Yes |
| `DROP COLUMN` | Only in SQLite ≥ 3.35 (Dalamud's bundled SQLite is recent enough; still verify). |
| `RENAME COLUMN` | Yes (3.25+) |
| `RENAME TABLE` | Yes |
| Modify a column type or constraint | **No** — requires the create-new-table + copy + drop-old pattern. |
| Add a unique index on existing data | Yes if data already satisfies it, otherwise fails. |

For unsupported changes use the standard table-copy pattern:

```sql
CREATE TABLE foo_new (…);
INSERT INTO foo_new SELECT … FROM foo;
DROP TABLE foo;
ALTER TABLE foo_new RENAME TO foo;
```

Wrap each migration's work in transactions if you need atomicity beyond
what `ExecuteSqlRawAsync` already provides.

**When the data is disposable** — caches, queues, or any table whose
rows are short-lived state that gets repopulated by normal plugin
operation — skip the copy and just `DROP TABLE IF EXISTS … ; CREATE
TABLE …`. Example: `RebuildRefreshQueueOnContentId` in
`NexusKit.Modules.InternalData/Persistence/InternalDataMigrations.cs`
re-keys the refresh queue from `lodestone_id` to `content_id`; the
queue is transient (items are processed and deleted within minutes),
so the simpler drop + recreate beats writing an `INSERT … SELECT`
that nobody benefits from.

## What happens if a migration throws

`DbInitializer.ApplyModuleAsync` runs each pending migration in sequence,
recording success per migration. If migration N throws:

- Migrations 1…N-1 are already in `nexuskit_migrations` (applied).
- Migration N is **not** recorded as applied.
- The exception propagates out of `InitializeAsync`, which propagates out of
  `PluginHostBuilder.BuildAsync`, which makes `Plugin.LoadAsync` fail.
- The plugin doesn't load; the user sees the failure in `/xllog`.

Recovery: fix the migration, ship a new build, the user retries.

## Adding migrations to an existing module

Today's modules (FfxivCollect, Lodestone) ship **no** migrations. Their
`EnsureCreated`-derived schema is the de facto v0. The first migration
added to a module describes the change from "v0 of the entity model" to
"v1 of the entity model" — keeping in mind that brand-new installs already
have v1's schema via the updated `IEntityModule.ConfigureEntities`.

So:
1. Update the entity model (e.g. add a property to the C# class).
2. Update `IEntityModule.ConfigureEntities` to map the new shape.
3. Add a migration that describes the SQL needed to go from old schema to new.
4. Register the migration module via `AddMigrationModule<T>()` if you don't
   already.

Fresh installs: `EnsureCreated` produces the new shape, migration is
baselined.
Upgrades: `EnsureCreated` is a no-op (table already exists), migration
runs the `ALTER`.

## Down-migrations

There are none. Forward-only. If you need to undo a column add, write a new
migration that drops it.

---

**Maintenance**: when you change the apply-vs-baseline logic, alter the ID
convention, or add reverse-migration support, update this file in the same
commit.
