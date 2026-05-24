# Localization (NexusKit.Core)

Deep dive into how strings travel through the framework: from `.resx` rows
in a module's assembly, through the layered chain, to the visible UI string
in the user's language.

## Surface

Three public types do the work:

- **`ILocalizer.TryGet(key, out text)`** — what every consumer calls.
- **`ILocalizationSource`** — marker for an individual translation backend.
  Implementations are pooled by DI (`AddSingleton<ILocalizationSource, T>`).
- **`LocalizationManager`** — flips `CultureInfo.CurrentUICulture` so
  ResourceLocalizers re-resolve into the right language.

The convenience extension `localizer.Get(key)` calls `TryGet` and falls back
to returning the key on miss, so all call sites stay one-liners.

## The layered chain

Multiple `ILocalizationSource` instances live in DI simultaneously. The
`LayeredLocalizer` (registered as `ILocalizer` singleton) collects them
during construction and iterates **in reverse-registration order** when
resolving:

```
Registered (earliest first):
  1. FrameworkLocalizer (Framework.resx)         ← AddNexusKitUi
  2. FfxivCollect's Strings.resx                  ← AddNexusKitFfxivCollect
  3. Lodestone's Strings.resx                     ← AddNexusKitLodestone
  4. Plugin's Language.resx                       ← Plugin.AddServices

Resolution order (reverse):
  Plugin → Lodestone → FfxivCollect → Framework → (key as fallback)
```

The first source whose `TryGet` returns `true` wins. Plugin sources can
override framework defaults; module-shipped translations beat the
framework's; the framework's English defaults beat returning the key.

## Layering examples

**Default behaviour (plugin has no overrides):**

```
loc.Get("nexuskit.module.enabled.label")
  → Plugin's Language.resx: miss
  → Lodestone's Strings.resx: miss
  → FfxivCollect's Strings.resx: miss
  → FrameworkLocalizer: "Aktiviert" (de) / "Enabled" (en)
```

**Plugin overrides a framework string:**

If the plugin adds `nexuskit.module.enabled.label = "An"` to its
`Language.resx`, the chain returns `"An"` before the framework gets a chance.

## ResourceLocalizer + .resx

The recommended source is a `.resx` file. The framework provides
`ResourceLocalizer` which wraps any `System.Resources.ResourceManager`:

```csharp
public sealed class ResourceLocalizer : ILocalizationSource
{
    public bool TryGet(string key, out string text)
    {
        var value = resources.GetString(key);   // honours CurrentUICulture
        if (value is not null) { text = value; return true; }
        text = string.Empty; return false;
    }
}
```

`ResourceManager.GetString(key)` honours `CultureInfo.CurrentUICulture` by
default — i.e. it picks `Strings.de.resx` over `Strings.resx` when the UI
language is German.

### Registering a `.resx` source

The designer-generated class for any `.resx` (e.g. `Strings.Designer.cs` in
a module, or `Language.Designer.cs` in the plugin) exposes a static
`ResourceManager` property. Plug it in:

```csharp
services.AddResourceLocalizer<Strings>();           // generic — reflects ResourceManager
// or
services.AddResourceLocalizer(Strings.ResourceManager);  // pass directly
```

Both forms are in `Localization/LocalizationServiceCollectionExtensions.cs`.

## Key naming convention

We've been using:

```
nexuskit.<area>.<key>                 # framework-level
nexuskit.modules.<module>.<key>       # module-level (FfxivCollect, Lodestone, …)
<plugin-id>.<area>.<key>              # plugin-level (free; choose your own prefix)
```

Dots are fine in `.resx` names; the resource manager treats them as part of
the key. Keep keys lowercase + ascii so they round-trip cleanly through
designer-generated property names.

## LocalizationManager (culture switching)

A separate singleton, not part of the chain. Its job is to mutate
`CultureInfo.CurrentUICulture` (and the default-thread variants for
background work).

Two authorised entry points; the actual apply is private:

| Caller | Method | Effect |
|---|---|---|
| Host bridge (Dalamud) | `ReportHostCulture(langCode)` | Apply unless an Override is active. |
| Plugin / user UI | `SetOverride(langCode \| null)` | Pin a language regardless of host. Null clears. |

So a user can say "I want this plugin in English even though my Dalamud is
on German" via the settings UI: `localizationManager.SetOverride("en")`.

`PluginUiHost` (in NexusKit.Ui) wires `IDalamudPluginInterface.LanguageChanged`
to `ReportHostCulture`. The bridge keeps NexusKit.Core Dalamud-free.

## Adding a new language to an existing `.resx`

1. Copy `Strings.resx` next to `Strings.<culture>.resx`. The `<culture>`
   piece is a .NET culture name: `de`, `de-DE`, `ja`, `fr-CA`, …
2. Fill `<value>` elements; keep `<data name>` keys identical to the source.
3. Build. The compiler embeds the satellite resource; `ResourceManager`
   picks it up at runtime.

No code change required. The same `AddResourceLocalizer<Strings>` registration
serves every language.

## Adding a brand-new localization source

For non-`.resx` backends (e.g. JSON-on-disk, cloud-fetched translations),
implement `ILocalizationSource` directly:

```csharp
internal sealed class JsonLocalizer : ILocalizationSource
{
    private readonly IReadOnlyDictionary<string, string> entries;

    public JsonLocalizer(string path) =>
        entries = LoadJson(path);

    public bool TryGet(string key, out string text)
    {
        if (entries.TryGetValue(key, out var v)) { text = v; return true; }
        text = string.Empty; return false;
    }
}

services.AddLocalizer<JsonLocalizer>();
```

`AddLocalizer<T>()` adds the source to the chain. It still goes through
`LayeredLocalizer` — your source competes with the others in
reverse-registration order.

---

**Maintenance**: when you alter the chain semantics, change naming
conventions, or modify `LocalizationManager`'s priority rules, update this
file in the same commit.
