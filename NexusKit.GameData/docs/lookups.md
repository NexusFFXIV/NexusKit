# Lookup caching

`IGameDataLookups` is the framework's read side over Lumina sheets:
id → localised name for worlds, data centers, class jobs, mounts,
minions, items, race / tribe, grand company ranks, and friends. The
methods are sync and microsecond-cheap by design — UI code calls them
per visible row per frame.

## The hot path

A `PlayerListPanel.DrawRow` call resolves the class-job abbreviation
for every visible character. With 100 rows × 60 fps that's 6 000
calls/second. A naive implementation does, per call:

1. `mSheets.GetSheet<ClassJob>(lang)` — dictionary lookup but still a
   virtual call + hash.
2. `sheet.GetRowOrDefault(rowId)` — Lumina row fetch.
3. `Sanitize(row.Abbreviation)` — two regex `Replace` passes (macro
   strip + whitespace collapse).

The regex parses recompile on every call when written as
`Regex.Replace(text, "pattern", …)`. That alone is the bulk of the cost.

## What the cache does

`GameDataLookups` holds a single `ConcurrentDictionary<NameKey, string?>`
where:

```csharp
private readonly record struct NameKey(byte Family, byte Lang, byte Variant, uint RowId);
```

- `Family` discriminates by lookup method via a private `NameFamily` enum
  (`World`, `DataCenter`, `ClassJobName`, `ClassJobAbbr`, `Item`,
  `Mount`, `Minion`, `Title`, `Ornament`, `Race`, `Tribe`,
  `GrandCompany`, `GrandCompanyRank`, …).
- `Lang` is the requested `GameDataClientLanguage` plus 1, with `0`
  reserved as a sentinel for "use the sheets provider's current
  language". The +1 shift lets `default(byte) = 0` mean "follow
  language" without colliding with a real enum value.
- `Variant` carries the mid-key dimension when one method has more than
  one cell to consider — e.g. race / tribe / title use it for the
  feminine flag, GC rank packs `(gcId, feminine)` into a 0..5 lane.
- `RowId` is the Lumina row id.

Every public `Get*` method routes through `GetCached(family, lang,
rowId, variant, build)` which checks the dictionary first and only
runs the sheet + sanitize on a miss. Null results are cached too, so
a missing row doesn't trigger a fresh lookup on every render.

## Compiled regexes

`Sanitize` strips Lumina's German (and some other) macro placeholders
(`[p]`, `[t]`, …) and collapses repeated whitespace:

```csharp
private static readonly Regex MacroRegex = new(@"\[[a-zA-Z]+\]", RegexOptions.Compiled);
private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

private static string? Sanitize(ReadOnlySeString s)
{
    var t = s.ExtractText();
    if (string.IsNullOrEmpty(t)) return null;
    var stripped = MacroRegex.Replace(t, string.Empty);
    stripped = WhitespaceRegex.Replace(stripped, " ").Trim();
    return stripped.Length == 0 ? null : stripped;
}
```

`RegexOptions.Compiled` plus the static field means the engine is built
once at type init instead of per call. The cache's job is to avoid most
`Sanitize` calls; this fallback is the *cold path* cost ceiling.

## Language changes

The `Lang` byte sentinel for "current language" means entries written
when culture was DE survive a switch to EN — they still produce DE
strings until cleared. The provider exposes:

```csharp
public void Clear() => mNameCache.Clear();
```

Callers (a language-switch handler in the plugin) invoke it when the
user picks a new culture and subsequent reads pick up the new sheet
translation.

## Concurrency

`ConcurrentDictionary` handles concurrent writes lock-free, which
matters because the UI's render thread and the watcher's threadpool
tasks both call the same lookups. There's no `lock` on the hot path.

## Resolver vs Lookups

`IGameDataResolver` (the reverse direction — name → id) has its own
caching model: per-`(kind, language)` `Dictionary<string, uint>` built
lazily on first use, then served from. Different shape, same goal.
See `GameDataResolver.cs` and the "Store IDs, not strings" section of
the project README for usage.

---

**Maintenance**: when you add a public `Get*` method, extend the
`NameFamily` enum, or change `Sanitize`, update this doc.
