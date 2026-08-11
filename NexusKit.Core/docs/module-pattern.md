# Plugin Module Pattern (NexusKit.Core)

How plugins, framework projects, and reusable modules compose into a single
service graph. The pattern is plain Microsoft.Extensions.DependencyInjection
plus a few framework conventions on top.

## Three abstractions

```
IPluginContext        — who am I? (PluginName, ConfigDirectory, PluginVersion)
IPluginModule         — register my services into the plugin's DI graph
IModuleSettings       — I'm a module with a ModuleEnabled toggle
```

All three are interfaces in `NexusKit.Core` with no Dalamud dependency.

## `IPluginContext`

```csharp
public interface IPluginContext
{
    string PluginName { get; }
    string ConfigDirectory { get; }
    Version PluginVersion { get; }
}
```

Filled once by the plugin's `LoadAsync` and registered as a singleton:

```csharp
var ctx = new PluginContext(
    PluginName: nameof(MyPlugin),
    ConfigDirectory: PluginInterface.GetPluginConfigDirectory(),
    PluginVersion: typeof(Plugin).Assembly.GetName().Version!);

builder.WithContext(ctx);   // PluginHostBuilder registers as IPluginContext singleton
```

Modules consume it for:
- Computing the SQLite path (`Persistence`)
- Prefixing IPC names (`Ipc`)
- Constructing the `WindowSystem` (`Ui`)

## `IPluginModule`

```csharp
public interface IPluginModule
{
    void Register(IServiceCollection services, IPluginContext context);
}
```

A plugin's "domain code wiring", in one place. Typical implementation:

```csharp
public sealed class MyPluginModule : IPluginModule
{
    public void Register(IServiceCollection services, IPluginContext context)
        => services.AddServices();    // calls into the plugin's own extension method
}
```

Registered with the builder:

```csharp
builder.WithModule(new MyPluginModule());
```

`PluginHostBuilder.BuildAsync` calls `module.Register(services, context)`
after every `ConfigureServices` action, so modules see all framework
registrations first.

## `IModuleSettings`

```csharp
public interface IModuleSettings
{
    bool ModuleEnabled { get; set; }
}
```

Marker for module-level settings POCOs. Anywhere a schema's
`SettingsType : IModuleSettings`, the auto-settings UI:
- Routes the schema into the "Modules" section (not a plugin tab).
- Renders the `ModuleEnabled` checkbox in the "Allgemein" / "General" overview.
- Adds a detail tab only if the module has more than just `ModuleEnabled`.

Module side:

```csharp
public sealed class FfxivCollectSettings : IModuleSettings
{
    public const string StoreKey = "nexuskit.modules.ffxivcollect.settings";

    public bool ModuleEnabled { get; set; } = true;
    public string BaseUrl { get; set; } = "https://ffxivcollect.com/api";
    public int CacheTtlHours { get; set; } = 24;
}
```

The `.RegisterModuleEnabledFlag()` schema builder shortcut (in NexusKit.Persistence)
adds the `ModuleEnabled` property to the schema with the framework's standard
label and marks it `.Hidden()` — so the detail tab never renders it, but the
"Modules" overview reads/writes it directly. Translatable via the layered
localizer.

## `AddNexusKitXxx` extension convention

Every NexusKit unit ships exactly one DI extension that registers its
contribution:

| Project | Extension | What it does |
|---|---|---|
| `NexusKit.Persistence` | `AddNexusKitPersistence()` | DbContextFactory + tracking entity module |
| `NexusKit.Persistence` | `AddNexusKitSettings()` | Settings entity + `ISettingsStore` |
| `NexusKit.Ipc` | `AddNexusKitIpc()` | DalamudIpcRegistry as `IIpcRegistry` |
| `NexusKit.Ui` | `AddNexusKitUi()` | WindowSystem, PluginUiHost, LocalizationManager, utilities, FrameworkLocalizer |
| `NexusKit.GameData` | `AddNexusKitGameData()` | `ISheetsProvider`, `IGameDataLookups`, `IGameDataResolver` over Lumina sheets |
| `NexusKit.Modules.FfxivCollect` | `AddNexusKitFfxivCollect()` | Module schema, cache, client, IPC provider, localizer |
| `NexusKit.Modules.Lodestone` | `AddNexusKitLodestone()` | Module schema, cache, client, IPC provider, localizer |
| `NexusKit.Modules.ExternalData` | `AddNexusKitExternalData()` | Unified Lodestone+FFXIVCollect player/FC/catalog services + entity module |
| `NexusKit.Modules.InternalData` | `AddNexusKitInternalData()` | Object-table watcher + history service + entity module |
| `NexusKit.Modules.PlayerEnrichment` | `AddNexusKitPlayerEnrichment()` | Bridge layer: refresh-queue worker + LodestoneId resolution. Pulls InternalData + ExternalData transitively. |

A plugin stitches them in `ConfigureServices`:

```csharp
.ConfigureServices(s =>
{
    // Dalamud handles the plugin owns
    s.AddSingleton(PluginInterface);
    s.AddSingleton(CommandManager);
    s.AddSingleton(ClientState);

    // Framework wiring
    s.AddNexusKitPersistence();
    s.AddNexusKitSettings();
    s.AddNexusKitIpc();
    s.AddNexusKitUi();

    // Plugin's own windows
    s.AddMainWindow<MyMainWindow>();
    s.AddAutoSettingsWindow();   // or AddSettingsWindow<MyCustomSettings>()
})
```

Modules then opt in inside the plugin's own `IPluginModule.Register`:

```csharp
public void Register(IServiceCollection services, IPluginContext context)
{
    services.AddNexusKitFfxivCollect();
    services.AddNexusKitLodestone();
    // Plugin's own services follow…
}
```

The mental model: `ConfigureServices` is for "framework wiring", `Register`
is for "domain wiring". They share the same `IServiceCollection`; the only
difference is the timing inside `PluginHostBuilder.BuildAsync`.

## Building your own module

1. New project under `NexusModules/NexusKit.Modules.<Name>/`.
2. ProjectRefs: `NexusKit.Core`, `NexusKit.Persistence` (only if the module
   stores data).
3. If your module needs settings: a POCO implementing `IModuleSettings`.
4. If your module needs database tables: an `IEntityModule`.
5. If your module needs migrations: an `IMigrationModule`.
6. If your module exposes IPCs: an `IIpcProvider` (register as singleton).
7. If your module has UI strings: a `Resources/Strings.resx` + designer.
8. One public extension method `AddNexusKit<Name>(IServiceCollection)`
   registering everything above.
9. Update the module's `README.md` + `docs/` so the next person finds the
   contract without reading source code.

That's the full contract. No base class, no marker interface, no magic
discovery.

---

**Maintenance**: when the `AddNexusKit*` extension naming changes, when a
new `IPluginModule` capability is added, or when the build flow shifts the
ConfigureServices/Register ordering, update this doc.
