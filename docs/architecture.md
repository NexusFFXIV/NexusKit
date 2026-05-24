# Architecture

How the projects in this repo are layered, where to put new code, and what
runs when a plugin loads.

## Project graph

```
PlayerNexusTracker.Plugin                       (Dalamud-tied, IDalamudPlugin)
        │
        ├── NexusKit.Modules.PlayerEnrichment   (cross-cutting bridge)
        │       │
        │       ├── NexusKit.Modules.InternalData    (watcher + history + encounters)
        │       │       │
        │       │       └── NexusKit.GameData        (Lumina sheets, lookups, resolver)
        │       │
        │       ├── NexusKit.Modules.ExternalData    (unified Lodestone + FFXIVCollect view)
        │       │       ├── NexusKit.Modules.FfxivCollect    (Dalamud-free; HttpClient)
        │       │       └── NexusKit.Modules.Lodestone       (Dalamud-free; NetStone)
        │       │
        │       ├── NexusKit.Persistence            (Dalamud-free; EF Core / SQLite + maintenance)
        │       └── NexusKit.Core                   (Dalamud-free; abstractions only)
        │
        ├── NexusKit.Modules.PluginBridge           (Dalamud-free; foreign-plugin IPC adapters)
        └── NexusKit.ChatNotifications              (Dalamud-tied; chat publisher framework)

PlayerNexusTracker.Plugin also references directly:
        ├── NexusKit.Ui          (Dalamud-tied; Windows, widgets, ImGui, AutoSettingsSections)
        ├── NexusKit.Ipc         (Dalamud-tied; ICallGate wrappers + IDalamudPluginProbe)
        ├── NexusKit.Hosting     (Dalamud-free; composition root + lifetime state machine)
        ├── NexusKit.GameData
        ├── NexusKit.Persistence
        ├── NexusKit.Modules.PluginBridge  (Dalamud-free; foreign-plugin adapters)
        └── NexusKit.Core

tests/NexusKit.Modules.ExternalData.Tests        (xUnit; live API integration tests)
```

Arrows point toward the dependency. NexusKit.Core has no project references;
every other project depends on it (directly or transitively).

**Key invariant**: `InternalData` and `ExternalData` deliberately do **not**
reference each other. Their bridge — Lodestone-id resolution and the
refresh-queue worker — lives in `NexusKit.Modules.PlayerEnrichment`, which
is the only place the two are allowed to meet. This keeps each module
reusable on its own and prevents accidental coupling.

## Two big rules

### 1. Dalamud-free vs Dalamud-tied

`NexusKit.Core`, `NexusKit.Persistence`, `NexusKit.Hosting`, and every project
under `NexusModules/` **must not** reference Dalamud assemblies. They define
abstractions (`IPluginContext`, `IPluginLogSink`, `IBrowserLauncher`,
`IIpcRegistry`, …) and consume them.

`NexusKit.Ui`, `NexusKit.Ipc`, and `PlayerNexusTracker.Plugin` are the only
places that reference Dalamud. They wire the abstractions to Dalamud's APIs.

This keeps modules reusable outside Dalamud (unit tests, alternative hosts)
and prevents an accidental coupling drift.

### 2. Kit vs Module vs Plugin

- **Kit** (NexusKit.\*) — plugin-agnostic plumbing. Anything that any future
  plugin would also benefit from belongs here.
- **Module** (NexusKit.Modules.\*) — a self-contained reusable feature with its
  own settings, optional DB tables, optional IPC providers, optional
  translations. Plugin opts in via `services.AddNexusKitXyz()`.
- **Plugin** (PlayerNexusTracker.Plugin) — the only place where this specific
  plugin's domain code lives. Anything plugin-specific stays here.

## Where does new code go?

| New code | Goes in |
|---|---|
| Abstraction (interface, marker, POCO) usable without Dalamud | `NexusKit.Core` |
| EF Core entity / settings / migration helper | `NexusKit.Persistence` |
| Composition-root primitive (no Dalamud) | `NexusKit.Hosting` |
| Window class, command/territory wrapper, ImGui helper | `NexusKit.Ui` |
| Dalamud IPC plumbing | `NexusKit.Ipc` |
| Reusable feature that calls external APIs, scrapes, or manages domain data | new `NexusKit.Modules.Xyz` |
| Adapter for a foreign Dalamud plugin (Lifestream, vnavmesh, …) | `NexusKit.Modules.PluginBridge/Adapters/<Plugin>/` |
| Anything PlayerNexusTracker-specific (Player tracking domain, plugin commands) | `PlayerNexusTracker.Plugin` |

If you're unsure, default to plugin first. Promote into a module / kit when a
second consumer materialises.

## Lifecycle

```
[Dalamud starts plugin]
        ↓
Plugin.LoadAsync(CancellationToken)
        ↓
Build PluginContext (name, config dir, version)
Build DalamudPluginLogSink (Plugin → IPluginLogSink)
        ↓
new PluginHostBuilder()
   .WithContext(ctx)
   .WithLogSink(sink)
   .WithModule(PlayerNexusTrackerModule)         ← plugin's IPluginModule
   .ConfigureServices(s =>
       s.AddSingleton(PluginInterface, CommandManager, ClientState,
                      DataManager, Framework, ObjectTable,
                      Condition, TextureProvider, ChatGui)
        .AddNexusKitPersistence()                ← DB factory + maintenance loop
        .AddNexusKitSettings()                   ← settings store + entity
        .AddNexusKitIpc()                        ← IPC registry
        .AddNexusKitUi()                         ← windows, widgets, language
        .AddNexusKitGameData()                   ← Lumina sheets + lookups
        .AddMainWindow<…>()
        .AddAutoSettingsWindow()
        .AddWindow<DebugWindow>())               ← extra plugin windows
   .BuildAsync(ct)

// Inside PlayerNexusTrackerModule.Register:
//   services.AddSingleton<ISessionStateProvider, DalamudSessionStateProvider>()
//   services.AddNexusKitPlayerEnrichment()      ← brings Internal+External
//                                                 + refresh queue + bridges
//                                                 + maintenance contributor
//   services.AddNexusKitChatNotifications()     ← chat-publisher framework
//   services.AddSingleton<INotificationProducer>(...)
//   services.AddDbMaintenanceSettingsSection()
//   services.AddSingleton<PlayerFilterRegistry>()
//   …
        ↓
ServiceCollection.BuildServiceProvider()
        ↓
PluginLifetime → Initializing
        ↓
DbInitializer.InitializeAsync(factory, migrations, ct)
   • EnsureCreatedAsync for any IEntityModule
   • For each IMigrationModule:
     - if nothing tracked yet → baseline (no migration runs)
     - else → apply pending in Id-ascending order
   • Run every registered IDatabaseViewBuilder (idempotent DROP+CREATE)
        ↓
DbMaintenanceService starts its 15-min tick loop (1-min initial delay)
        ↓
Resolve ISessionStateProvider (when registered)
   → snapshot IsActive; wire Activated/Deactivated to PluginLifetime
   → State becomes Idle (logged-out) or Active (logged-in)
   → State Idle by default if no provider was registered? No — host
     defaults to Active so non-session hosts (tests, CLIs) run normally.
        ↓
Eagerly resolve all registered IIpcProvider
   → each provider's constructor publishes its IPCs
        ↓
[PluginHost returned to Plugin.LoadAsync]
        ↓
host.Services.GetRequiredService<PluginUiHost>()
   • Adds Main/Settings/extra windows to the WindowSystem
   • Subscribes UiBuilder.Draw / OpenMainUi / OpenConfigUi
   • Reports Dalamud UiLanguage to LocalizationManager
   • Subscribes IDalamudPluginInterface.LanguageChanged
        ↓
Plugin force-resolves background services that subscribe in their ctor:
   • IInternalDataPlayerWatcher   — IFramework.Update subscription
   • IInternalDataHistoryService  — diffs ObservationProcessed
   • IInternalDataEncounterTracker — territory + roster persistence
   • IPlayerRefreshQueueService   — worker thread + Observed subscription
   • LiveTagChangeRefreshTrigger  — ObservationProcessed → priority enqueue
   • IEnumerable<INotificationProducer> — each ctor registers + subscribes
        ↓
[Plugin loaded; user sees it in /xlplugins]

… (game runs; PluginLifetime flips Idle ↔ Active on login/logout) …

[Dalamud unloads plugin]
        ↓
Plugin.DisposeAsync()
        ↓
PluginHost.DisposeAsync()
   • PluginLifetime → Stopping (synchronous final-write window)
     - encounter tracker stamps any open encounter's ended_at
     - other services flush pending state via subscribed StateChanged
   • PluginLifetime → Stopped (cancels Stopping token)
   • ServiceProvider disposes singletons in reverse-registration order
   • PluginUiHost unsubscribes Dalamud events, removes windows
   • DbMaintenanceService waits for loop to exit, runs WAL checkpoint,
     clears the SQLite connection pool
   • DalamudIpcRegistry unregisters all own IPCs
   • IIpcProviders dispose their tracked registrations
   • Producers + bridges unsubscribe from their event sources
   • LocalizationManager unsubscribes LanguageChanged
   • DbContextFactory shut down
```

## Composition pattern

Every NexusKit unit (kit project or module) ships an
`AddNexusKitXxx(IServiceCollection)` extension that registers its singletons,
entity modules, schemas, localizers, and IPC providers. The plugin's
`Plugin.LoadAsync` strings these together in `ConfigureServices`. No magic;
the wiring is explicit and grep-able.

## Localisation flow

```
Plugin's UI code asks for a string
        ↓
LayeredLocalizer.TryGet(key, out text)
   sources iterated in reverse-registration order (plugin wins over framework):
   1. Plugin-provided ILocalizationSource (e.g. Language.resx)
   2. Module-provided ILocalizationSources (Strings.resx per module)
   3. Framework's Framework.resx
        ↓ if all return false
Returns the key itself as fallback (visible defect; you spot missing
translations immediately)
```

`LocalizationManager` (Core) flips `CultureInfo.CurrentUICulture` whenever the
host reports a new culture or the user/plugin author sets an override —
ResourceLocalizers reload automatically on the next lookup.

## Database

One SQLite file per plugin, at
`%APPDATA%/XIVLauncher/pluginConfigs/<PluginName>/<PluginName>.db`.
Tables are contributed by `IEntityModule` implementations across the framework
and any module the plugin opted into. Schema evolution is module-scoped via
`IMigrationModule` — see
[NexusKit.Persistence/docs/migrations.md](../NexusKit/NexusKit.Persistence/docs/migrations.md).

Routine housekeeping is centralised in `DbMaintenanceService`. Modules
contribute units of periodic work as `IDbMaintenanceContributor` (cache
eviction, refresh-queue prune, weekly VACUUM/ANALYZE/OPTIMIZE), and the
framework runs each on its declared `Interval`. See
[NexusKit.Persistence/docs/maintenance.md](../NexusKit/NexusKit.Persistence/docs/maintenance.md).

## Lifetime and session state

`IPluginLifetime` (in `NexusKit.Core`) tracks the plugin's phase
(`Initializing → Idle ↔ Active → Stopping → Stopped`) and exposes a
`Stopping` cancellation token that fires on plugin unload **before** the
DI container starts disposing services. Services thread this token through
every async DB / network call so in-flight work cancels cleanly instead of
writing into a half-disposed container. Plugins opt into login/logout
awareness by registering an `ISessionStateProvider` (the Dalamud plugin
ships `DalamudSessionStateProvider`); the framework maps activation events
onto the Idle ↔ Active states. Without a provider, lifetime starts in
`Active` — fine for non-session hosts (tests, CLIs).

## IPC

Outbound (we publish): `[PluginName].[Subsystem].[Member]`. Auto-prefixed by
`DalamudIpcRegistry.BuildName`. Modules register via `IIpcProvider` and the
host resolves all of them eagerly.

Inbound (we consume foreign IPCs): use full name as-is, e.g.
`Visibility.Disable`. See
[NexusKit.Ipc/docs/naming.md](../NexusKit/NexusKit.Ipc/docs/naming.md).

## Cross-plugin integration

Integrating foreign Dalamud plugins (Lifestream, vnavmesh, Visibility, …)
happens through `NexusKit.Modules.PluginBridge`. The pattern:

1. **Probe.** `IDalamudPluginProbe` (in `NexusKit.Core.Ipc`) is the
   Dalamud-free abstraction over `IDalamudPluginInterface.InstalledPlugins`.
   Implemented by `DalamudPluginProbe` in `NexusKit.Ipc` and registered via
   `AddNexusKitIpc()`.
2. **Adapt.** Each foreign plugin gets one adapter under
   `NexusModules/External/NexusKit.Modules.PluginBridge/Adapters/<Plugin>/`.
   The adapter owns the foreign plugin's `InternalName`, its canonical
   IPC name constants, and a normalized failsoft API
   (`I<Plugin>Adapter.Try…`). It implements both `IExternalPluginAdapter`
   (status surface) and the normalized interface (consumer surface).
3. **Consume.** UI code injects `I<Plugin>Adapter`, checks
   `IsAvailable`, and calls `Try…` methods that return `bool` instead
   of throwing. The Settings tab "Plugin-Bridges" iterates
   `IPluginBridgeRegistry.All()` to render one status row per adapter
   — same instance, no second source of truth.

**Limitation.** Dalamud cannot enumerate the IPCs a foreign plugin has
registered. Adapters carry a curated list of expected IPC names and
treat "plugin loaded" as sufficient evidence that the IPCs are
bindable. A runtime invocation failure is caught by
`IIpcFunc<…>.TryInvoke` / `IIpcAction<…>.TryInvoke` and surfaced as a
`false` return.

See [`NexusKit.Modules.PluginBridge/docs/plugin-bridge.md`](../NexusModules/External/NexusKit.Modules.PluginBridge/docs/plugin-bridge.md)
for the design rationale and adapter-author howto.

---

**Maintenance**: when you add/remove a project, change the layering rules, or
shift the lifecycle, update this file in the same commit.
