# Plugin Host Lifecycle (NexusKit.Hosting)

Detailed walkthrough of what happens between `Dalamud calls LoadAsync` and
"plugin is ready for user input", and how the symmetric shutdown unwinds.

## Startup sequence

```
[Dalamud loads the assembly]
        ↓
Dalamud constructs Plugin via reflection
   • Sets all [PluginService] static properties before the ctor runs
   • Then invokes the parameterless ctor (empty for IAsyncDalamudPlugin)
        ↓
Dalamud calls Plugin.LoadAsync(cancellationToken)
        ↓
Plugin builds an IPluginContext + IPluginLogSink
        ↓
new PluginHostBuilder()
    .WithContext(ctx)            // stored
    .WithLogSink(sink)           // stored
    .WithModule(module)          // appended to modules list
    .ConfigureServices(action)   // appended to serviceConfigurations list
    .BuildAsync(ct)
        ↓
PluginHostBuilder.BuildAsync(ct):
  ┌─────────────────────────────────────────────────────────────────┐
  │ 1. Validate: context and sink must be set                        │
  │ 2. var services = new ServiceCollection();                       │
  │ 3. services.AddSingleton(context)                                │
  │ 4. services.AddSingleton(sink)                                   │
  │ 5. services.AddLogging(b => b.AddProvider(                       │
  │       new PluginLoggerProvider(sink)))                           │
  │ 6. For each ConfigureServices action — in registration order:    │
  │       action(services)                                           │
  │ 7. For each module — in registration order:                      │
  │       module.Register(services, context)                         │
  │ 8. var provider = services.BuildServiceProvider(                 │
  │       validateScopes: false)                                     │
  │ 9. If IDbContextFactory<PluginDbContext> is registered:          │
  │      a. Resolve IEnumerable<IMigrationModule>                    │
  │      b. await DbInitializer.InitializeAsync(factory, mods, ct)  │
  │         • ctx.Database.EnsureCreatedAsync                        │
  │         • For each IMigrationModule: baseline-or-apply           │
  │ 10. Resolve IEnumerable<IIpcProvider>                            │
  │       • Each constructor runs (side-effect: publish IPCs)        │
  │ 11. Return new PluginHost(provider)                              │
  └─────────────────────────────────────────────────────────────────┘
        ↓
Plugin gets PluginHost back
        ↓
host.Services.GetRequiredService<PluginUiHost>()
   (eagerly resolved by the plugin so Dalamud UiBuilder events get wired)
        ↓
Plugin logs "loaded"
        ↓
Plugin.LoadAsync returns → Dalamud marks the plugin loaded
```

### Ordering rules to know

- **`ConfigureServices` runs before `module.Register`.** This lets the
  plugin opt into framework facilities (`AddNexusKitPersistence`,
  `AddNexusKitUi`, …) before the module adds its own consumers of those
  facilities (`AddNexusKitFfxivCollect` registers a singleton that depends
  on `IDbContextFactory<PluginDbContext>` from `AddNexusKitPersistence`).
- **Multiple `ConfigureServices` actions** run in registration order — a
  late one can override an earlier one (`Replace`, `RemoveAll`).
- **`module.Register` is for plugin-domain wiring.** The plugin's own
  `IPluginModule` typically just calls `services.AddServices()` (a
  plugin-local extension) where the domain composition happens.

### Why `validateScopes: false`

The framework registers almost exclusively singletons. Scope validation
would only surface false alarms (DI complaining that a transient was
captured by a singleton when in practice no scopes exist). When/if we
introduce scoped lifetimes, this flips to `true`.

### What if a `ConfigureServices` action throws?

The exception propagates out of `BuildAsync`, which propagates out of
`Plugin.LoadAsync`. Dalamud logs the failure to `/xllog` and the plugin is
marked unloaded. `IDisposable` services registered up to that point are
**not** disposed — the provider was never built, so there is nothing to
dispose.

Once the provider *has* been built (`EnsureCreatedAsync`, a migration, a view
builder, or one of the eager resolves throwing), `BuildAsync` cleans up: the
whole post-`BuildServiceProvider` block is wrapped in a try/catch that logs at
Error, runs `new PluginHost(provider).DisposeAsync()` — so the cleanup uses the
same `RequestStop`-then-teardown ordering as a normal shutdown — and rethrows
the original exception. Without this the provider leaked undisposed *with live
background loops*, because `IDbMaintenanceService` and every
`IPluginBackgroundService` start their workers during construction.

`Plugin.DisposeAsync` on the consumer side must still null-guard its host
field: Dalamud calls `DisposeAsync` even when `LoadAsync` threw, and may call
it more than once.

### Do NOT feed `BuildAsync`'s token to `PluginLifetime`

`PluginLifetime`'s CTS is deliberately **not** linked to the `ct` passed to
`BuildAsync`. Under Dalamud that token is a **60-second load timeout**, not an
unload signal: `LocalPlugin.LoadAsync` fabricates its own
`CancellationTokenSource` with `CancelAfter(TimeSpan.FromSeconds(60))` whenever
the caller supplies no token, and no Dalamud call site supplies one. It fires
whether the load succeeded or not, and nothing disposes it.

Linking to it meant the lifetime went `Stopping → Stopped` 60 seconds after
every load while the plugin carried on running, permanently disabling every
`IsStopping`-gated service — observation persistence, encounter tracking,
history, the refresh-queue worker, DB maintenance — with zero log output. It
also *consumed* the last-chance write window a minute after load, so at the
real unload `SetState(Stopping)` returned early and subscribers never got their
final synchronous write at all.

`ct` is therefore used for exactly one thing inside `BuildAsync`:
`DbInitializer.InitializeAsync`, so a genuinely aborted load stops migrating.
A host that has a *real* shutdown token opts in explicitly via
`WithShutdownSignal(...)`, which is `Register`ed rather than linked so the
external path gets the same two-phase `Stopping` (token still live) → `Stopped`
(token cancelled) semantics as `RequestStop`.

Both shutdown transitions log at **Warning** with a reason
(`PluginHost dispose` vs `external shutdown signal`). That line is the only
way to tell "the plugin unloaded" from "the plugin is still running but its
lifetime token was cancelled", so it is deliberately above the Release-build
log-level floor.

## `PluginLoggerProvider`

Bridges `Microsoft.Extensions.Logging.ILogger<T>` to our Dalamud-free
`IPluginLogSink`. Each `ILogger<T>` instance is a lightweight wrapper that:

- Receives a `LogLevel` + state + exception + formatter from the caller.
- Builds a single string: `[CategoryName] formattedMessage`.
- Dispatches to `sink.Information / Warning / Error / Debug / Verbose / Fatal`
  based on `LogLevel`.

The category name is the type's full name (the standard `ILogger<T>`
convention). The Dalamud-tied `DalamudPluginLogSink` in the plugin then
forwards each call to `Dalamud.Plugin.Services.IPluginLog` — so framework
+ module code logging via `ILogger<MyService>` lands in `/xllog` exactly as
if it called `IPluginLog` directly.

## Shutdown sequence

```
[Dalamud unloads the plugin]
        ↓
Plugin.DisposeAsync()
        ↓
host.DisposeAsync()
        ↓
ServiceProvider.DisposeAsync()
   Disposes every IDisposable/IAsyncDisposable singleton in
   reverse-registration order. Notable in our setup:
   • PluginUiHost     — unsubscribes UiBuilder events, removes windows,
                         unsubscribes LanguageChanged
   • LocalizationManager — no resources; clean drop
   • IIpcProviders   — dispose their tracked IDisposables (unregister IPCs)
   • DalamudIpcRegistry — safety-net unregister of anything still tracked
   • CommandRegistry  — removes any commands still registered
   • SettingsStore    — no resources; cache GC'd
   • DbContextFactory — shuts down EF Core pools
        ↓
PluginHost.DisposeAsync completes
        ↓
Plugin.DisposeAsync returns → Dalamud finishes unloading
```

If a `Dispose` throws, the ServiceProvider catches and continues — one bad
service can't strand the rest.

## Why does Hosting not reference Dalamud?

It would be tempting: a single project that hosts both the composition
root and the Dalamud-tied bridges. The cost is that you couldn't run any of
the framework in unit tests, console apps, or alternative shells without
mocking Dalamud assemblies.

By keeping Hosting Dalamud-free:

- `NexusKit.Ui` and `NexusKit.Ipc` are the only places Dalamud appears.
- The plugin supplies Dalamud handles via `ConfigureServices`
  (`services.AddSingleton(PluginInterface)` etc.) — explicit, grep-able.
- A test harness can build the same host with a fake `IPluginContext` and a
  no-op `IPluginLogSink` and run domain modules end-to-end against an
  in-memory or temp-file SQLite.

---

**Maintenance**: when you change the build order, add a new automatic
phase (e.g. background-service startup), or alter the dispose contract,
update this document.
