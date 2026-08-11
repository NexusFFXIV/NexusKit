# Glossary

Vocabulary used across this repo. Terms are listed alphabetically.

### AutoSettingsSection (`IAutoSettingsSection`)

An extension hook on `AutoSettingsWindow`: implementations contribute one
extra sidebar entry and own their body's rendering. Used for content that
doesn't fit the declarative `AddSettings<T>` model — dynamic lists,
multi-table layouts, force-run buttons. The chat-notification framework,
the DB-maintenance settings tab, the refresh-queue diagnostics tab, and the
plugin's player-filter editor all ship as `IAutoSettingsSection`. Registered
as `services.AddSingleton<IAutoSettingsSection, T>()`.

### AutoSettingsWindow

Framework-provided `SettingsWindow` that renders every registered settings
schema in a sidebar+tabs layout, plus every `IAutoSettingsSection` extension.
Opt in via `services.AddAutoSettingsWindow()`. Replace with a custom one via
`services.AddSettingsWindow<T>()`. See
[NexusKit.Ui/docs/auto-settings.md](../NexusKit.Ui/docs/auto-settings.md).

### Chat-notification framework

`NexusKit.ChatNotifications` — generic chat publisher with per-kind user
overrides (enable / channel / color). Producers declare
`NotificationKindDefinition`s; the framework hands each a `IChatNotificationPublisher`
keyed by id; the user controls every kind in the auto-settings
"Notifications" tab. See
[NexusKit.ChatNotifications/README.md](../NexusKit.ChatNotifications/README.md).

### DbContext

EF Core's connection-and-tracking object. The framework defines a single
`PluginDbContext` that aggregates every registered `IEntityModule` in its
`OnModelCreating`. Consumers don't subclass it — they contribute entities
through `IEntityModule` instead.

### DbMaintenanceContributor (`IDbMaintenanceContributor`)

A unit of periodic database housekeeping (cache eviction, exhausted-row
prune, weekly VACUUM/ANALYZE). The background `DbMaintenanceService` walks
every registered contributor on a 15-min tick and invokes those whose
`Interval` has elapsed since their last successful run. Last-run timestamps
persist via `MaintenanceState`. Register via `services.AddMaintenanceContributor<T>()`.
See [NexusKit.Persistence/docs/maintenance.md](../NexusKit.Persistence/docs/maintenance.md).

### DbMaintenanceService (`IDbMaintenanceService`)

Background loop that drives every registered `IDbMaintenanceContributor`.
Auto-starts on construction, watches `IPluginLifetime.Stopping` for graceful
unwind, then performs WAL checkpoint + connection-pool clear during plugin
disposal. Exposes `RunNowAsync()` for the auto-settings "Run now" button.

### DbStatsService (`IDbStatsService`)

Read-only snapshot of the SQLite file: on-disk bytes, freelist bytes,
per-table row count + index count + payload bytes. Powers the
`DbMaintenanceSettingsSection` UI without each consumer reimplementing the
SQLite-internal table walk.

### Encounter Tracker (`IInternalDataEncounterTracker`)

Tracks territory-bounded sessions of the local player plus the non-local
characters visible during each. Bridges Dalamud's `TerritoryChanged` and the
observation pipeline into the `encounter` + `player_encounter` tables under
`nexus_internal_*`. Replaces the retired `observed_player.last_seen` /
`seen_count` columns with aggregates per encounter. See
[NexusKit.Modules.InternalData/docs/encounters.md](https://github.com/NexusFFXIV/NexusKit.Modules/blob/main/NexusKit.Modules.InternalData/docs/encounters.md).

### Entity Module

A class implementing `IEntityModule` that registers one or more tables with
the shared `PluginDbContext`. Registered via
`services.AddSingleton<IEntityModule, MyEntityModule>()` or the
`services.AddEntityModule<MyEntityModule>()` shortcut. See
[NexusKit.Persistence/docs/entity-modules.md](../NexusKit.Persistence/docs/entity-modules.md).

### Framework

Synonym for "the NexusKit set of projects" — everything under `NexusKit/`.
The framework is plugin-agnostic; the plugin (and any future plugin) consumes
it.

### Host (PluginHost)

The composition root. Built by `PluginHostBuilder.BuildAsync` from a context,
log sink, `IPluginModule`s, and `ConfigureServices` actions. Owns the
`IServiceProvider` for the entire plugin session.

### INexusDbContextFactory

Lifetime-aware DbContext factory in `NexusKit.Persistence`. Services consume
this instead of the raw `IDbContextFactory<PluginDbContext>` so they don't
have to thread the plugin's `IPluginLifetime.Stopping` token through every
async call by hand — the factory links the caller's `ct` with the lifetime
token automatically. `LifetimeToken` is exposed so `SaveChangesAsync` /
`BeginTransactionAsync` / async LINQ calls can be cancelled cleanly on
plugin unload.

### IPC (Inter-Plugin Communication)

Dalamud's mechanism for plugins to call each other's methods or react to each
other's events. NexusKit wraps it behind `IIpcRegistry` so plugins can
publish without writing Dalamud-specific code, and consume foreign IPCs with
typed proxies. See [ipc-catalog.md](ipc-catalog.md) and
[NexusKit.Ipc/docs/naming.md](../NexusKit.Ipc/docs/naming.md).

### IPC Consumer

Code that calls *foreign* IPCs (those published by another plugin). Uses
`ipc.GetFunc<…>()` / `ipc.GetAction<…>()` and addresses the foreign plugin
by its full IPC name (e.g. `"Visibility.Disable"`).

### IPC Provider

A class that publishes our *own* IPCs. Implements `IIpcProvider` (marker
interface) and registers IPCs in its constructor via `IIpcRegistry`.
`PluginHostBuilder` eagerly resolves every registered provider at startup.

### Kit

A NexusKit project that ships framework-level plumbing — `NexusKit.Core`,
`NexusKit.Persistence`, `NexusKit.Hosting`, `NexusKit.Ui`, `NexusKit.Ipc`.
Plugin-agnostic; never depends on a specific module.

### Layered Localizer

The `ILocalizer` implementation that aggregates every registered
`ILocalizationSource`. Iterates them in *reverse-registration order* so
plugin sources win over module sources, which win over framework defaults.
See [NexusKit.Core/docs/localization.md](../NexusKit.Core/docs/localization.md).

### Localization Source

A class implementing `ILocalizationSource`. One per `.resx`-backed
translation bundle (framework, module, plugin), plus optionally hand-rolled
sources (JSON, DB-backed, etc.).

### Localized Text

A small struct (`LocalizedText`) holding either a literal string or a
resource key. The fluent settings schema uses it so each label can be
plain English (`.Label("…")`) or a translatable key (`.LabelKey("…")`).

### Main Window

Abstract base class `MainWindow : Dalamud.Interface.Windowing.Window`. The
plugin extends it and registers via `services.AddMainWindow<T>()`. Triggered
by Dalamud's "Open" button on the plugin entry in `/xlplugins`.

### Migration

One forward-only schema-evolution step, implementing `IMigration`. Bundled
into an `IMigrationModule`. Applied by `DbInitializer` only on upgrade
installs — fresh installs baseline migrations without running them. See
[NexusKit.Persistence/docs/migrations.md](../NexusKit.Persistence/docs/migrations.md).

### Notification Producer (`INotificationProducer`)

Marker — "this DI singleton registers one or more chat-notification kinds
and subscribes to event sources that drive them". Mirrors the `IIpcProvider`
pattern: resolution IS the registration side-effect. The plugin enumerates
`IEnumerable<INotificationProducer>` at `LoadAsync` to force-construct each.
See [NexusKit.ChatNotifications/docs/producer-guide.md](../NexusKit.ChatNotifications/docs/producer-guide.md).

### Module

A self-contained, reusable feature shipped under `NexusModules/`. Has its
own settings (an `IModuleSettings` POCO), optional DB entities, optional
IPC providers, and its own translations. Plugins opt in via
`services.AddNexusKit<Module>()`. Today:

- `NexusKit.Modules.FfxivCollect` — HTTP client for ffxivcollect.com.
- `NexusKit.Modules.Lodestone` — NetStone-backed scraper.
- `NexusKit.Modules.ExternalData` — unified player / FC / catalog view
  over the two above. References both; clients see one API.
- `NexusKit.Modules.InternalData` — live ObjectTable observation +
  per-field change history. Knows nothing about Lodestone.
- `NexusKit.Modules.PlayerEnrichment` — cross-cutting *bridge* between
  Internal and External. Owns the persistent refresh queue and the
  Lodestone-id resolution category. The only place the two data
  modules are allowed to meet.

### Module Settings

Settings POCO implementing `IModuleSettings`. Carries at least a `bool
ModuleEnabled` flag. The framework routes IModuleSettings-implementing schemas
into the "Modules" section of the auto-settings UI.

### Plugin

The Dalamud-side consumer of NexusKit. Today there's one:
`MyPlugin.Plugin`. Owns its domain code (player tracking,
encounters, etc.) and registers any number of modules. Anything *specific
to this plugin* belongs in the plugin project, not the framework.

### Plugin Lifetime (`IPluginLifetime`)

Host-owned cancellation source plus state machine
(`Initializing → Idle ↔ Active → Stopping → Stopped`).
`Stopping` is a `CancellationToken` that fires at the start of plugin
unload, **before** the DI container starts disposing — services thread it
through every async DB / network call so in-flight work unwinds cleanly
instead of writing into a half-disposed container. `StateChanged` fires on
every transition. The `Stopping` state offers a synchronous last-chance
window for services to flush state (e.g. stamping a still-open encounter's
`ended_at`) before the token cancels and the container tears down.

### Plugin Module (IPluginModule)

A class implementing `IPluginModule.Register(services, context)`. The
plugin's own composition entry point — typically just calls
`services.AddServices()` (a plugin-local extension). Registered with the
builder via `WithModule(module)`. Distinct from a "Module" (`NexusKit.Modules.*`)
despite the name overlap.

### Refresh TTL Provider (`IRefreshTtlProvider`)

Plugin-injectable freshness window used by the refresh queue's
staleness check. Default implementation returns 7 days; plugins register
their own (e.g. backed by user-editable settings) before
`AddNexusKitPlayerEnrichment()` so the module's `TryAddSingleton` fallback
defers to it.

### Session State Provider (`ISessionStateProvider`)

Pluggable "is there an active session right now?" source. The framework
consumes this to drive the `IPluginLifetime` state machine without a direct
Dalamud reference. The MyPlugin plugin ships
`DalamudSessionStateProvider`, which adapts `IClientState.Login` /
`IClientState.Logout`. Hosts without a session concept (tests, CLIs) skip
the registration; lifetime then stays in `Active` permanently.

### POCO

Plain Old C# Object — a class with public properties, no business logic.
We use them everywhere: settings (`TrackerSettings`), DTOs
(`Models/Character`), DB entities (`SettingsEntity`).

### Schema (Settings Schema)

A description of how a settings POCO renders in the UI — labels,
descriptions, control types, ordering. Built via fluent
`SettingsSchemaBuilder<T>` and registered with `services.AddSettings<T>(b => …)`.
The schema is metadata; the data is stored separately via `ISettingsStore`.

### Settings Store (ISettingsStore)

Simple key-value POCO storage backed by the `settings` DB table. JSON-serialises
on write, deserialises on read, caches in memory. Use for plain K/V state;
use a settings schema (above) when you want the value to surface in the UI.

### View Builder (`IDatabaseViewBuilder`)

Idempotent `DROP VIEW IF EXISTS … ; CREATE VIEW …` runner. Plugins/modules
register builders via `services.AddViewBuilder<T>()`; the framework runs
each builder once per plugin start, after migrations. Views are
schema-derived (depend on existing tables) so they bypass migration
bookkeeping and re-create cleanly when the underlying schema shifts. The
plugin's `nexus_filter_player` view powers the SQL-driven half of the
player-filter pipeline.

### Settings Window

Abstract base class `SettingsWindow : Dalamud.Interface.Windowing.Window`.
Triggered by Dalamud's gear icon on the plugin entry. Use `AddAutoSettingsWindow()`
for the framework version or `AddSettingsWindow<T>()` to register your own.

---

**Maintenance**: when you introduce a new framework concept (new core
abstraction, new module type, new lifecycle phase), add it here so the
vocabulary stays in one place.
