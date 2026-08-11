# IPC naming and conventions

How NexusKit names IPC channels, what callers should expect, and how to
publish or consume them safely.

## Why a naming convention

Dalamud IPC names are global strings. If two plugins pick the same name,
they collide silently — the second one to register wins. NexusKit's
convention prevents accidental clashes and makes the plugin/module owner
obvious from the name alone.

## Format for our own IPCs

```
[PluginName].[Subsystem].[Member]
```

| Segment | Source |
|---|---|
| `PluginName` | `IPluginContext.PluginName` — set once by the plugin (e.g. `"MyPlugin"`). The framework auto-prefixes; you don't pass it. |
| `Subsystem` | Module name (`"FfxivCollect"`, `"Lodestone"`, …) or a plugin-level component name (`"Settings"`, `"Tracker"`, …). |
| `Member` | The function or event itself (`"GetCharacterJson"`, `"PlayerSighted"`, …). |

You provide `Subsystem` and `Member`; the registry builds the full name:

```csharp
ipc.RegisterFunc<ulong, Task<string?>>(
    subsystem: "FfxivCollect",
    function:  "GetCharacterJson",
    handler:   async id => …);

// Effective registration name (for a plugin named "MyPlugin"):
// "MyPlugin.FfxivCollect.GetCharacterJson"
```

## Format for foreign IPCs

Foreign plugins choose their own names. Most use a 2-segment form:
`<PluginName>.<Function>` (e.g. `Visibility.Disable`, `Penumbra.GetEnabledState`).
We accept whatever they publish, verbatim:

```csharp
var disable = ipc.GetAction<string>("Visibility.Disable");
```

No prefix injection on consume — that would point us at a non-existent IPC.

## JSON convention for cross-plugin data

Dalamud IPC marshals types by name. If we expose a method that returns our
own `Character` POCO, foreign consumers need to reference our assembly to
deserialise — which couples them to us in a way we don't want.

Convention for "outward-facing" IPCs that ship data: **return JSON-serialised
strings.** Consumers deserialise against their own POCOs.

```csharp
// Provider side
registrations.Add(ipc.RegisterFunc<ulong, Task<string?>>(
    "FfxivCollect", "GetCharacterJson",
    async id =>
    {
        var character = await client.GetCharacterAsync(id);
        return character is null ? null : JsonSerializer.Serialize(character);
    }));

// Consumer side (in a foreign plugin)
var func = pi.GetIpcSubscriber<ulong, Task<string?>>(
    "MyPlugin.FfxivCollect.GetCharacterJson");
var json = await func.InvokeFunc(lodestoneId);
var character = JsonSerializer.Deserialize<MyMirrorCharacter>(json);
```

The `Json` suffix on the IPC name makes the contract clear at the call site.

Primitive arguments (`ulong`, `string`, `int`, `bool`) marshal natively
because they're in `mscorlib` — both sides agree on the type.

## Lifecycle

```
Plugin.LoadAsync
  → PluginHostBuilder.BuildAsync
    → BuildServiceProvider
    → DbInitializer
    → for each registered IIpcProvider:
        sp.GetRequiredService<...>() forces construction
        → constructor calls ipc.RegisterFunc / RegisterAction
        → DalamudIpcRegistry tracks the IDisposable for cleanup

Plugin runs.

Plugin.DisposeAsync
  → host.DisposeAsync
    → ServiceProvider disposes singletons
    → IIpcProviders' Dispose runs first (dispose the IDisposables they kept)
    → DalamudIpcRegistry's own Dispose runs (safety net; idempotent via
      ActionDisposable's CAS)
```

The `ActionDisposable` in `Internal/ActionDisposable.cs` is one-shot; double
disposal is a no-op. So having both the provider class and the registry try
to unregister the same IPC is safe.

## How to expose a new IPC from a module

1. **Decide the subsystem name.** For a module, use the module's short
   name (e.g. `"FfxivCollect"`). For plugin-level IPCs, pick a meaningful
   bucket (`"Tracker"`, `"Search"`).
2. **Decide payload shape.** For cross-plugin friendliness, return JSON
   strings (suffix the IPC name with `Json`) or use primitives only.
3. **Write an `IIpcProvider` class.** Take `IIpcRegistry` (plus whatever
   domain services the implementation needs) in the constructor. Register
   IPCs there, track returned `IDisposable`s.
4. **Register as singleton** with `services.AddSingleton<IIpcProvider, MyIpcProvider>()`.
5. **Update the module's README** with the published IPC names + signatures.

`PluginHostBuilder.BuildAsync` resolves `IEnumerable<IIpcProvider>` eagerly
after DB init — your constructor runs and registrations are live before any
user code does.

## How to consume a foreign IPC from a module

1. **Take `IIpcRegistry` in your constructor.**
2. **Call `GetFunc`/`GetAction`** with the foreign plugin's full IPC name.
   You get a typed `IIpcFunc<…>` / `IIpcAction<…>` proxy.
3. **Use `TryInvoke`** if you want graceful degradation when the foreign
   plugin isn't installed (it swallows the exception and returns `false`).
   Use `Invoke` if you want the exception to propagate (e.g. for hard
   dependencies).

## Versioning

Today: no explicit version negotiation. The framework relies on type-shape
stability — if you change the signature of an existing IPC (different
parameter count, different return type), foreign consumers break silently
because Dalamud's IPC matches on full type-name signature.

Recommendation: don't break IPC signatures. Add a new IPC name
(e.g. `GetCharacterJsonV2`) when the contract has to evolve, and keep the
old one for a deprecation window.

---

**Maintenance**: when you change the name format, alter the JSON convention,
or introduce versioning, update this document and the IPC provider tables
in module READMEs.
