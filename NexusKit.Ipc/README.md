# NexusKit.Ipc

Dalamud-backed implementation of `IIpcRegistry` from
[NexusKit.Core](../NexusKit.Core/README.md). Wraps
`IDalamudPluginInterface.GetIpc{Provider,Subscriber}<…>` behind a typed,
disposable API.

**This project references Dalamud.** Built with `Dalamud.NET.Sdk` and
`<Use_DalamudPackager>false</Use_DalamudPackager>` — library, not a plugin.

## Public API

| Type | File | Purpose |
|---|---|---|
| `AddNexusKitIpc()` extension | `IpcServiceCollectionExtensions.cs` | Register `DalamudIpcRegistry` as `IIpcRegistry` and `DalamudPluginProbe` as `IDalamudPluginProbe`. |
| `DalamudIpcRegistry` (internal) | `DalamudIpcRegistry.cs` | The implementation. Tracks own registrations for cleanup on disposal. |
| `DalamudPluginProbe` (internal) | `DalamudPluginProbe.cs` | `IDalamudPluginProbe` implementation over `IDalamudPluginInterface.InstalledPlugins`. |
| `DalamudIpcFunc<…>`, `DalamudIpcAction<…>` (internal) | `DalamudIpcFunc.cs`, `DalamudIpcAction.cs` | Typed proxies returned by `GetFunc` / `GetAction`. |

There are no public types beyond the registration extension; everything the
plugin code touches is the `IIpcRegistry` / `IDalamudPluginProbe`
abstractions from Core.

## Registration

```csharp
services.AddSingleton(PluginInterface);   // required: IDalamudPluginInterface
services.AddNexusKitIpc();
```

`DalamudIpcRegistry` is registered as both a concrete singleton (so it gets
the same instance everywhere) and as `IIpcRegistry`. The ServiceProvider
disposes it on shutdown, which unregisters every tracked IPC.

## Dependencies

- Uses `Dalamud.NET.Sdk/15.0.0`
- ProjectRef: `NexusKit.Core`

## Provider example

```csharp
internal sealed class MyIpcProvider : IIpcProvider, IDisposable
{
    private readonly List<IDisposable> registrations = new();

    public MyIpcProvider(IIpcRegistry ipc, IMyClient client)
    {
        // Resulting full IPC name: "MyPlugin.MyModule.GetThingJson"
        registrations.Add(ipc.RegisterFunc<ulong, Task<string?>>(
            "MyModule", "GetThingJson",
            async id => JsonSerializer.Serialize(await client.GetAsync(id))));
    }

    public void Dispose()
    {
        foreach (var r in registrations) r.Dispose();
    }
}

// Register so PluginHostBuilder picks it up eagerly:
services.AddSingleton<IIpcProvider, MyIpcProvider>();
```

`IIpcProvider` is a marker. `PluginHostBuilder.BuildAsync` resolves every
registered `IIpcProvider` after the database is initialised, which constructs
them and triggers their IPC registrations.

## Consumer example

```csharp
public sealed class VisibilityClient
{
    private readonly IIpcAction<string> disable;

    public VisibilityClient(IIpcRegistry ipc)
    {
        // Foreign IPC — use the full name as advertised by the publisher.
        disable = ipc.GetAction<string>("Visibility.Disable");
    }

    public bool HidePlayer(string name) => disable.TryInvoke(name);
}
```

`TryInvoke` swallows exceptions (plugin not installed, IPC version mismatch,
etc.) and returns `false`; `Invoke` propagates them.

## Probing for installed foreign plugins

`IDalamudPluginProbe` (in `NexusKit.Core.Ipc`) is the Dalamud-free
abstraction over `IDalamudPluginInterface.InstalledPlugins`. Use it to
check whether a foreign plugin is installed/loaded before binding to
its IPCs:

```csharp
public sealed class VisibilityClient
{
    private readonly IDalamudPluginProbe probe;
    private readonly IIpcAction<string> disable;

    public VisibilityClient(IDalamudPluginProbe probe, IIpcRegistry ipc)
    {
        this.probe = probe;
        disable = ipc.GetAction<string>("Visibility.Disable");
    }

    public bool HidePlayer(string name)
    {
        if (!probe.IsLoaded("Visibility")) return false;
        return disable.TryInvoke(name);
    }
}
```

`IsLoaded` matches on a plugin's internal name (case-sensitive ordinal).
`GetInfo(internalName)` returns the full `InstalledPluginInfo` record
including `Version` — useful for the Settings UI to show which version
of the foreign plugin is currently active.

**Limitation:** Dalamud has no API to enumerate the IPCs a foreign
plugin has registered. The probe only reports metadata; whether a
specific IPC name is bindable is verified by `TryInvoke` returning
`true`. The `NexusKit.Modules.PluginBridge` module wraps this pattern
into per-plugin adapters with normalized APIs — see its README for the
recommended consumer shape.

## Where to read next

- [docs/naming.md](docs/naming.md) — IPC name format, JSON convention for
  cross-plugin data, versioning, lifecycle.
- [`NexusKit.Modules.PluginBridge`](https://github.com/NexusFFXIV/NexusKit.Modules/blob/main/External/NexusKit.Modules.PluginBridge/README.md)
  — the adapter pattern for consuming foreign plugins behind a Settings
  UI status surface.

---

**Maintenance**: when you add new arity overloads to `IIpcRegistry`,
change the naming convention, alter eager-resolution behaviour, or
extend `IDalamudPluginProbe`, update this README and `docs/naming.md`.
