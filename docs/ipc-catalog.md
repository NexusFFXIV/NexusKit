# IPC Catalog

Every IPC this plugin publishes, in one place. Foreign plugins consume by
the full name; the format is
`[PluginName].[Subsystem].[Member]` — see
[NexusKit.Ipc/docs/naming.md](../NexusKit/NexusKit.Ipc/docs/naming.md).

The examples below assume the plugin is registered as `PlayerNexusTracker`.
Replace the prefix when this framework is consumed by a different plugin.

## FFXIVCollect module

Source: [NexusKit.Modules.FfxivCollect](../NexusModules/External/NexusKit.Modules.FfxivCollect/README.md).
Backing client: `IFfxivCollectClient`. JSON payloads.

| Full name | Signature | Returns |
|---|---|---|
| `PlayerNexusTracker.FfxivCollect.GetCharacterJson` | `Func<ulong, Task<string?>>` | JSON of `Character` |
| `PlayerNexusTracker.FfxivCollect.GetMountsJson` | `Func<ulong, Task<string?>>` | JSON of `ListResponse<Mount>` |
| `PlayerNexusTracker.FfxivCollect.GetMinionsJson` | `Func<ulong, Task<string?>>` | JSON of `ListResponse<Minion>` |
| `PlayerNexusTracker.FfxivCollect.GetAchievementsJson` | `Func<ulong, Task<string?>>` | JSON of `ListResponse<Achievement>` |

All four respect the module's `ModuleEnabled` toggle: while disabled, every IPC
returns `null` (no HTTP, no cache read).

Model schemas: see
[NexusKit.Modules.FfxivCollect/docs/api-reference.md](../NexusModules/External/NexusKit.Modules.FfxivCollect/docs/api-reference.md).

## Lodestone module

Source: [NexusKit.Modules.Lodestone](../NexusModules/External/NexusKit.Modules.Lodestone/README.md).
Backing client: `ILodestoneClient` (NetStone under the hood). JSON payloads.

| Full name | Signature | Returns |
|---|---|---|
| `PlayerNexusTracker.Lodestone.GetCharacterJson` | `Func<ulong, Task<string?>>` | JSON of `CharacterSummary` |
| `PlayerNexusTracker.Lodestone.SearchCharacterJson` | `Func<string, string, Task<string?>>` | JSON of `CharacterSearchResult` |

Both respect the module's `ModuleEnabled` toggle.

Model schemas: see
[NexusKit.Modules.Lodestone/docs/api-reference.md](../NexusModules/External/NexusKit.Modules.Lodestone/docs/api-reference.md).

## Consuming from a foreign plugin

```csharp
// Function (typed)
var get = pluginInterface.GetIpcSubscriber<ulong, Task<string?>>(
    "PlayerNexusTracker.FfxivCollect.GetCharacterJson");
var json = await get.InvokeFunc(lodestoneId);

// Search with two args
var search = pluginInterface.GetIpcSubscriber<string, string, Task<string?>>(
    "PlayerNexusTracker.Lodestone.SearchCharacterJson");
var json = await search.InvokeFunc("Sora Aratani", "Phoenix");
```

Foreign consumers should JSON-deserialise against their own DTOs — they
don't need a reference to our types.

## What we don't publish

Anything plugin-specific (player tracking commands, encounter feeds, etc.)
is *not* in the catalog. The framework + modules expose generic FFXIV data
access (FFXIVCollect, Lodestone); the plugin's own domain data is the
plugin's choice to expose or keep private.

## Conventions

- **`Json` suffix** on every IPC that returns serialised payload. Makes the
  contract visible at the call site.
- **Primitive arguments only** (`ulong`, `string`, `int`, `bool`). They
  marshal across the IPC boundary natively without shared type references.
- **Module subsystem name** mirrors the module's project suffix (`FfxivCollect`
  for `NexusKit.Modules.FfxivCollect`, `Lodestone` for `NexusKit.Modules.Lodestone`).
- **No versioning today** — see naming.md's "Versioning" section for the
  forward-compatible recommendation if you need to evolve an IPC signature.

---

**Maintenance**: when you add, rename, remove, or change the signature of
any IPC in any module, update the corresponding table here in the same
commit. The catalog is the single contract-of-truth for cross-plugin
consumers.
