# NexusKit.GameData

Framework-level access to FFXIV's static game data (Lumina Excel sheets).
Lives in NexusKit so any module — `Modules.ExternalData`, future `Modules.InternalData`,
plugin UI code — can consume it without each one re-wrapping `IDataManager`.

**Dalamud-tied** (needs `IDataManager`), like `NexusKit.Ui` and unlike `NexusKit.Core`.

## Public surface

| Interface | Purpose |
|---|---|
| `ISheetsProvider` | Raw passthrough to `IDataManager.GetExcelSheet<T>()` with per-(type, language) caching. Use when no helper covers your sheet. |
| `IGameDataLookups` | Opinionated common-case helpers: world / data-center / territory / class-job / mount / minion / title / ornament / race / tribe / grand-company names. Sync, microsecond-cheap. |
| `IGameDataResolver` | `uint? ResolveIdByName(name, kind, language)` — turn a localized string from a third-party API into the canonical Lumina RowId. Lazy per-(kind, language) name → id dictionary. |
| `ILocalPlayerContext` (from `NexusKit.Core`) | Where the local player is right now. Implemented here because it reads `IObjectTable`. |
| `IPlayerMapMarker` (from `NexusKit.Core`) | Live position of a nearby player, plus "flag it on the map". Implemented here because it reads `IObjectTable` / `IGameGui`. |

## Live game state: `ILocalPlayerContext` and `IPlayerMapMarker`

Both interfaces are declared in `NexusKit.Core` and implemented here, so view code can
ask about live game state without taking a Dalamud dependency of its own.

`IPlayerMapMarker` covers "where is this player, and put a flag there":

```csharp
if (marker.TryGetPosition(contentId) is { } pos)
    ImGui.TextUnformatted($"{pos.MapX:0.0}, {pos.MapY:0.0}");   // display

marker.MarkPosition(contentId);   // flag + open map, false if it couldn't
```

Three things to know before using it:

- **Only nearby players resolve.** Positions come from the object table, so anyone out of
  range, in another zone, or offline yields `null` / `false`. That is not an error state —
  it is the normal case for a database-backed player list, and it is deliberately
  indistinguishable from "unknown player" so callers need only one branch.
- **Nothing is logged on failure.** Every failure mode is an expected UI state, and these
  methods sit on the render path where logging would repeat per frame. Callers render the
  disabled/unavailable state instead.
- **Framework thread only**, like everything else that touches the object table. Any ImGui
  draw callback qualifies.

`MapPosition` carries both coordinate systems: `MapX`/`MapY` for display (the game's own
`(12.3, 8.7)` form) and `World` for anything needing real distances. The map's vertical
axis is world `Z`; world `Y` is elevation and has no place on a 2D map.

## "Store IDs, not strings" pattern

External modules (Lodestone, FFXIVCollect, …) hand us localized names. Persisting those
strings ties the database to one language. Instead:

1. Scrape the name in any supported language.
2. Resolve once via `IGameDataResolver.ResolveIdByName(name, kind, language)` → `uint`.
3. Store the RowId.
4. When you want to display the entry, look up the name via `IGameDataLookups` in the
   currently active plugin culture — Lumina serves it in any of EN / JA / DE / FR.

That's how `nexus_external_player_owned` (and friends) stay multi-language without
re-fetching from external APIs after a language switch.

## Language

`GameDataClientLanguage` is Dalamud-free (`English`, `Japanese`, `German`, `French`) so
interface consumers don't pull `Dalamud.Game.ClientLanguage` into modules that prefer to
stay decoupled. The provider maps internally.

`ISheetsProvider.CurrentLanguage` resolves to:
- `LocalizationManager.CurrentCulture` (NexusKit.Core) if it matches one of the four,
- otherwise `IDataManager.Language` (Dalamud's UI language).

Pass an explicit `GameDataClientLanguage?` to any helper to override per call.

## Registration

```csharp
services.AddNexusKitGameData();
```

Add it alongside the other kit pieces in `Plugin.LoadAsync`. The plugin must already have
registered these via its `[PluginService]` injection: `IDataManager` (sheets),
`IObjectTable` and `IClientState` (local-player context, map markers), and `IGameGui`
(map markers).

## Caching

Both providers cache aggressively:
- `ISheetsProvider` keeps one `ExcelSheet<T>` per (type, language) — `IDataManager`'s
  underlying Lumina is memory-mapped, so this is just keeping references.
- `IGameDataResolver` builds a `Dictionary<string, uint>` per (kind, language) on first
  lookup, then serves from it lock-free. Built via `OrdinalIgnoreCase` so casing
  variations on the input don't miss.

Sheets are never invalidated — they're game-data, immutable for the session. A language
switch creates a new cache slot, the old one stays available.
