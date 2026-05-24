# NexusKit.Core

Dalamud-free abstractions consumed by every other NexusKit project, module,
and plugin. **No Dalamud reference; no EF Core; no UI.** Only interfaces,
small POCOs, and a couple of self-contained utilities.

## Public API

| Type | File | Purpose |
|---|---|---|
| `IPluginContext`, `PluginContext` | `Context/` | Plugin name, config directory, version. Filled by the plugin, consumed by every module. |
| `IPluginModule` | `Modules/IPluginModule.cs` | Contract a plugin's composition module fulfils to register services. |
| `IModuleSettings` | `Modules/IModuleSettings.cs` | Marker the framework uses to detect "module-style" settings POCOs (must expose `bool ModuleEnabled`). |
| `IPluginLifetime`, `PluginLifecycleState` | `IPluginLifetime.cs` | Host-owned shutdown token + state machine (`Initializing → Idle ↔ Active → Stopping → Stopped`). Services thread `Stopping` through every async DB / network call so in-flight work cancels cleanly during plugin unload. |
| `ISessionStateProvider` | `ISessionStateProvider.cs` | Pluggable "is the user logged in?" source. The plugin registers an adapter (e.g. Dalamud-backed); the framework maps activation events onto Idle ↔ Active lifecycle states. Optional — without one, the lifetime stays in `Active`. |
| `IPluginLogSink` | `Logging/IPluginLogSink.cs` | Logging contract. Dalamud-tied implementation lives in the plugin. |
| `ILocalizer` | `Localization/ILocalizer.cs` | `TryGet(key, out text)` — the only thing UI code talks to. |
| `ILocalizationSource` | `Localization/ILocalizationSource.cs` | Marker for individual translation backends. Multiple instances form a layered chain. |
| `LayeredLocalizer` | `Localization/LayeredLocalizer.cs` | Aggregates registered sources in reverse-registration order (later wins). |
| `ResourceLocalizer` | `Localization/ResourceLocalizer.cs` | Wraps a `ResourceManager`; honours `CultureInfo.CurrentUICulture`. |
| `LocalizationManager` | `Localization/LocalizationManager.cs` | Authorised culture switcher (host reports, plugin overrides; private internal apply). |
| `LocalizedText` | `Localization/LocalizedText.cs` | Either a literal string or a resource key, resolved against an `ILocalizer`. |
| `RpsThrottle` | `Throttling/RpsThrottle.cs` | Sliding-window rate limiter; `AcquireAsync` blocks until safe to call. |
| `IBrowserLauncher` | `Utilities/IBrowserLauncher.cs` | Open-URL abstraction; Dalamud implementation in NexusKit.Ui. |
| `IIpcRegistry` | `Ipc/IIpcRegistry.cs` | Provider + consumer surface for plugin-to-plugin IPC. |
| `IIpcFunc<…>`, `IIpcAction<…>` | `Ipc/` | Typed proxies returned by `GetFunc` / `GetAction`. |
| `IIpcProvider` | `Ipc/IIpcProvider.cs` | Marker — host resolves all registered providers eagerly so their constructors can publish IPCs. |
| `IPluginBackgroundService` | `IPluginBackgroundService.cs` | Marker — host resolves all registered instances eagerly during `BuildAsync` so a module's long-lived background singletons start without plugin-side wiring. |
| `ActionRenderHint` | `Actions/ActionRenderHint.cs` | Optional render hint produced by a service-level `Preview<X>(…)` method that mirrors a `Try<X>(…)` method. Carries a variant id + optional localized tooltip key + accent color (`Vector4`) so the UI can tint a button and pick a variant-specific tooltip without learning adapter internals. |
| `IDalamudPluginProbe`, `InstalledPluginInfo` | `Ipc/` | Probe a foreign plugin by internal name (`IsInstalled`, `IsLoaded`, `GetInfo`, `ListInstalled`). Dalamud-tied impl in NexusKit.Ipc. |

## Registration

`AddLocalizer<T>()`, `AddLocalizer(ILocalizationSource)`, `AddResourceLocalizer<T>()`,
`AddResourceLocalizer(ResourceManager)` live in
`Localization/LocalizationServiceCollectionExtensions.cs`. They append sources
to the layered chain.

No top-level `AddNexusKitCore()` exists — Core has nothing to register on its
own. Each higher project (`NexusKit.Persistence`, `NexusKit.Ui`, etc.)
registers what it needs.

## Dependencies

- NuGet: `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.7
- No project references

## Example

```csharp
// Inside a module that needs to log + translate strings:
public sealed class MyService
{
    private readonly ILogger<MyService> log;
    private readonly ILocalizer loc;

    public MyService(ILogger<MyService> log, ILocalizer loc)
    {
        this.log = log;
        this.loc = loc;
    }

    public void Greet()
    {
        // Resource-key lookup; falls back to the key if no source resolves it.
        log.LogInformation(loc.Get("mymodule.greet.hello"));
    }
}
```

## When to add code here

Only when the new type:
- Doesn't need Dalamud assemblies, AND
- Doesn't need EF Core, AND
- Will be consumed across multiple downstream projects, AND
- Is an interface or small self-contained utility (no heavy infrastructure).

Heavy infrastructure (EF Core wrappers, ImGui widgets, IPC plumbing) belongs
in the higher projects that already absorb those dependencies.

---

**Maintenance**: when you add/remove a public type here, update the table
above. Cross-project READMEs link to type names; keep them stable or
update the consumers too.
