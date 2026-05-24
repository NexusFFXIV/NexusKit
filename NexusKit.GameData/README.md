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

Add it alongside the other kit pieces in `Plugin.LoadAsync`. The plugin must already
have registered `IDataManager` via its `[PluginService]` injection.

## Caching

Both providers cache aggressively:
- `ISheetsProvider` keeps one `ExcelSheet<T>` per (type, language) — `IDataManager`'s
  underlying Lumina is memory-mapped, so this is just keeping references.
- `IGameDataResolver` builds a `Dictionary<string, uint>` per (kind, language) on first
  lookup, then serves from it lock-free. Built via `OrdinalIgnoreCase` so casing
  variations on the input don't miss.

Sheets are never invalidated — they're game-data, immutable for the session. A language
switch creates a new cache slot, the old one stays available.
