# NexusKit.Hosting

The composition root. Builds the `IServiceProvider` for a plugin, initialises
the database, eagerly resolves all `IIpcProvider`s and
`IPluginBackgroundService`s, and routes
`Microsoft.Extensions.Logging.ILogger<T>` to the framework's `IPluginLogSink`.

**No Dalamud reference.** The plugin provides:
- An `IPluginContext` (plugin name + paths)
- An `IPluginLogSink` (a Dalamud-backed implementation lives in the plugin)

Hosting stitches everything else together.

## Public API

| Type | File | Purpose |
|---|---|---|
| `PluginHostBuilder` | `PluginHostBuilder.cs` | Fluent builder: `WithContext / WithLogSink / WithModule / ConfigureServices / WithShutdownSignal / BuildAsync`. |
| `PluginHost` | `PluginHost.cs` | Result of `BuildAsync`. Holds the `ServiceProvider` and disposes it. `IAsyncDisposable` + `IDisposable`. Drives `PluginLifetime` through `Stopping → Stopped` before tearing down singletons. |
| `PluginLifetime` | `PluginLifetime.cs` | `IPluginLifetime` implementation — **public**, and its constructor is public API surface. Owns the `CancellationTokenSource` for `Stopping` and the synchronous state-machine. |
| `LifetimeBridge` (internal) | `LifetimeBridge.cs` | Eager-resolved by `BuildAsync`. Reads `ISessionStateProvider.IsActive` for the initial Idle/Active state and subscribes Activated/Deactivated. No-op when no provider is registered. |
| `PluginLoggerProvider` (internal) | `Logging/PluginLoggerProvider.cs` | `ILoggerProvider` that pipes `ILogger<T>` → `IPluginLogSink`. |

## Registration

Hosting itself isn't registered with DI — the plugin instantiates
`PluginHostBuilder` directly. The builder constructs `IServiceCollection`,
calls every `ConfigureServices` action and every module's `Register`, then
builds the provider.

## Build flow (`BuildAsync`)

```
1. Register IPluginContext + IPluginLogSink as singletons
2. Register PluginLifetime as IPluginLifetime (Initializing state)
3. AddLogging(b => b.AddProvider(new PluginLoggerProvider(sink)))
4. For each ConfigureServices(Action) — run in registration order
5. For each module — call IPluginModule.Register(services, context)
6. services.BuildServiceProvider(validateScopes: false)
7. If IDbContextFactory<PluginDbContext> registered → DbInitializer.InitializeAsync
   • EnsureCreated + per-IMigrationModule baseline/apply
   • Run every registered IDatabaseViewBuilder (idempotent DROP/CREATE)
8. LifetimeBridge resolves ISessionStateProvider (if registered) and wires
   Activated/Deactivated → PluginLifetime state transitions. Snapshot
   IsActive sets initial state to Idle or Active; absent provider stays Active.
9. Eagerly resolve every registered IIpcProvider (constructors publish IPCs)
10. Eagerly resolve every registered IPluginBackgroundService (constructors
    start background loops / subscriptions — modules opt their long-lived
    workers in here instead of pushing them into the plugin's LoadAsync)
11. Return PluginHost wrapping the provider
```

The `validateScopes: false` flag matches the framework's reality — most
registrations are singletons; scope validation would only produce noise.

## Dependencies

- NuGet: `Microsoft.Extensions.DependencyInjection` 10.0.7,
  `Microsoft.Extensions.Logging` 10.0.7
- ProjectRefs: `NexusKit.Core`, `NexusKit.Persistence`

## Example

```csharp
// In Plugin.LoadAsync
var ctx = new PluginContext(
    PluginName: nameof(MyPlugin),
    ConfigDirectory: PluginInterface.GetPluginConfigDirectory(),
    PluginVersion: typeof(MyPlugin).Assembly.GetName().Version!);

host = await new PluginHostBuilder()
    .WithContext(ctx)
    .WithLogSink(new DalamudPluginLogSink(Log))
    .WithModule(new MyPluginModule())          // your IPluginModule
    .ConfigureServices(s =>
    {
        s.AddSingleton(PluginInterface);
        s.AddNexusKitPersistence();
        s.AddNexusKitSettings();
    })
    .BuildAsync(cancellationToken);

// Now resolve anything: host.Services.GetRequiredService<…>()
```

## Why no Dalamud here?

It would be convenient to subscribe to `LanguageChanged` or pull
`ICommandManager` from inside Hosting. Doing so would mean Hosting can't run
in a unit test or alternative shell. The cost — having the plugin add a few
lines to register Dalamud handles — is small and explicit.

`NexusKit.Ui` and `NexusKit.Ipc` *are* the Dalamud-tied layers; they live
on top of Hosting and consume the same `IServiceCollection`.

---

**Maintenance**: when you change the build flow (new auto-invocation, new
required step), update the "Build flow" section above so future contributors
don't reverse-engineer it.
