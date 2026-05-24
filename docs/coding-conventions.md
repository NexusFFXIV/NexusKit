# Coding Conventions

Rules that hold across every project in this repo. Most are conventions, not
hard checks — but consistent code is much faster to read.

## Formatting

| What | Convention |
|---|---|
| C# source files | 4-space indentation, no tabs |
| `.csproj` / `.sln` / `.yaml` / `.resx` | Tab indentation (default from dotnet templates) |
| Newlines | LF in source; Git's `core.autocrlf` handles Windows checkout (you'll see CRLF warnings — they're expected). |
| Line length | No hard limit; aim for ~100–120 chars where it reads naturally. Wrap long expressions. |
| Trailing whitespace | None. |
| End-of-file newline | One. |

## C# language features

- **`Nullable enable`** in every project. Treat `T?` and `T` as meaningfully
  different. Don't use `!` to silence the compiler unless you've genuinely
  proven the reference can't be null (e.g. directly after a null check, or
  for DI-injected `[PluginService]` statics whose lifetime guarantees they
  are set).
- **`LangVersion=latest`** — use modern features (records, pattern matching,
  collection expressions, primary constructors) where they make code
  clearer. Don't refactor for novelty.
- **`ImplicitUsings=enable`** in framework projects. Dalamud's SDK projects
  don't enable it by default — explicit `using` statements there.
- **File-scoped namespaces** (`namespace Foo;` with a semicolon). One
  namespace per file.

## Naming

| Item | Convention | Example |
|---|---|---|
| Type | PascalCase | `LodestoneClient` |
| Interface | `I` prefix + PascalCase | `ILodestoneClient` |
| Method, property, public field | PascalCase | `GetCharacterAsync` |
| Method / lambda parameter | camelCase, starts lowercase | `lodestoneId`, `httpFactory` |
| Local variable | camelCase, starts lowercase | `cacheKey`, `response` |
| Private instance field | `m` prefix + PascalCase | `mHttpFactory`, `mLogger`, `mTestVar` |
| Constant | PascalCase | `HttpClientName` |
| Async method | suffix `Async` when returning `Task` / `ValueTask` | `LoadAsync` |
| Enum value | PascalCase | `ControlKind.Checkbox` |

The `m` prefix on private instance fields (e.g. `mHttpFactory`) keeps them
visually distinct from parameters and locals without needing `this.` to
disambiguate. Example:

```csharp
public sealed class FfxivCollectClient
{
    private readonly IHttpClientFactory mHttpFactory;
    private readonly ILogger<FfxivCollectClient> mLog;

    public FfxivCollectClient(IHttpClientFactory httpFactory, ILogger<FfxivCollectClient> log)
    {
        mHttpFactory = httpFactory;
        mLog = log;
    }
}
```

Static fields use plain PascalCase (they're already class-scoped and can't
clash with locals/parameters).

## Namespace organisation

Namespaces mirror folder structure. `NexusKit.Persistence/Settings/Schema/`
contains types in `NexusKit.Persistence.Settings.Schema`. Don't break this
to "tidy up" — IDE navigation depends on it.

Project root namespace matches `<RootNamespace>` in the csproj (set
explicitly on every project to avoid surprises with `AssemblyName`).

## Public vs internal

Default to **internal** unless the type is part of a project's public API.

- Concrete implementations behind an interface: usually internal.
  Consumers see the interface from Core / Persistence, the implementation
  lives in Ui / Ipc / a module.
- Settings POCOs: public (consumed across boundaries).
- Models / DTOs: public.
- Designer-generated resource classes (`Language`, `Strings`, `Framework`):
  internal is fine — `AddResourceLocalizer<T>()` uses reflection.

## Async

- Every I/O-bound method is `async`. Cancellation propagates via
  `CancellationToken ct` as the last parameter.
- Don't sync-over-async (`Task.GetAwaiter().GetResult()`) on the UI thread.
  The plugin uses `IAsyncDalamudPlugin`, so `LoadAsync` and `DisposeAsync`
  can be properly async; lean on that.
- Background work fired-and-forgotten (e.g. save-on-change in the auto
  settings UI) uses `_ = SomeAsync(...)` so the compiler doesn't warn —
  acceptable for short, idempotent writes where the user doesn't need
  feedback.
- **`ConfigureAwait(false)` on every background-I/O `await`** — database
  reads/writes (`DbContext`, `IDbContextFactory`), HTTP requests
  (`HttpClient`, NetStone, FFXIVCollect), file I/O, and any other
  thread-pool-bound work. The default behaviour captures the current
  synchronization context, which on the framework thread is Dalamud's;
  capturing it for a thread-pool continuation we never want back on the
  framework thread is wasted bookkeeping and a deadlock risk if a caller
  ever sync-waits.

  ```csharp
  await using var ctx = await mDb.CreateDbContextAsync(ct).ConfigureAwait(false);
  var resp = await mHttp.GetAsync(url, ct).ConfigureAwait(false);
  ```

  **Exceptions** — keep the default behaviour (no `ConfigureAwait`) when:
  - the code lives in `IFramework.Update` handlers, Dalamud event
    callbacks (`OpenMainUi`, `LanguageChanged`, …), or any other path
    the framework explicitly drives on a specific thread that the
    continuation legitimately needs to run on;
  - `Plugin.LoadAsync` itself — Dalamud awaits it with `await`, and the
    next statement after each `await` is plain framework wiring with
    no thread-affinity requirement, but the symmetry with framework
    callbacks keeps the file consistent;
  - UI `Window.Draw` / settings rendering — there's no `await` in the
    hot path, but if you ever add one for an async file picker etc.
    you'll want the continuation back on the UI thread.

  If you're not sure: it's a background path → add `.ConfigureAwait(false)`.

## Dependency injection

- Singletons by default — most of our objects are stateless or hold caches
  meant to live the whole plugin session.
- Transient only when there's a reason (per-call HTTP clients, per-call
  DbContexts via `IDbContextFactory`).
- No service-locator anti-pattern: don't grab `IServiceProvider` to resolve
  things ad-hoc inside a method. Constructor-inject what you need.
- The exception: `PluginUiHost`'s constructor uses `IServiceProvider.GetService<MainWindow>()`
  because both `MainWindow` and `SettingsWindow` are optional registrations.

## Logging

Use `Microsoft.Extensions.Logging.ILogger<T>` injected via DI. The framework
pipes it through `IPluginLogSink` into Dalamud's `/xllog`.

```csharp
internal sealed class FfxivCollectClient
{
    private readonly ILogger<FfxivCollectClient> log;
    // …

    public async Task<Character?> GetCharacterAsync(ulong id, CancellationToken ct)
    {
        try
        {
            // …
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "FFXIVCollect fetch failed for {Id}", id);
            return null;
        }
    }
}
```

- Structured logging templates (`"{Name}"` placeholders), not string interpolation.
- `LogWarning` for recoverable failures; `LogError` for "we don't know how
  to handle this"; `LogInformation` for lifecycle events.
- Never `LogInformation` on a hot path (per-tick rendering, per-call
  cache hits) — that fills `/xllog` and tanks performance.

## Exception handling

- Catch `Exception` at boundaries where a failure shouldn't take down the
  plugin (HTTP fetches, IPC invocations, async fire-and-forget writes).
  Always log + return a sensible fallback.
- Let `OperationCanceledException` propagate. Code that takes a
  `CancellationToken` is expected to honour it.
- Never swallow exceptions silently. If you `catch { }`, leave a comment
  explaining why (shutdown races are the typical valid case).

## Localisation

Every user-visible string goes through `ILocalizer`. Two options:

```csharp
.Label("Max recent players")                    // literal — translation maintained outside .resx
.LabelKey("settings.tracker.max_recent.label")  // resource key — translatable
```

Default to keys for plugin/module strings. Literals are fine for prototype
code and rare debug-only text.

Module-shipped translations live in the module's `Resources/Strings.resx`;
plugin translations in the plugin's `Resources/Language.resx`. See
[glossary.md](glossary.md) for the layering rule.

## File organisation

- One public type per file. Helpers / private types can co-locate.
- File name matches the type name.
- Folder structure groups related concerns (`Settings/`, `Settings/Schema/`,
  `Migrations/`, `Ipc/`, `Localization/`, etc.).
- Test files (none yet) would live in a sibling `*.Tests` project.

## Documentation

- XML doc comments on every public type and method that has non-obvious
  behaviour. Skip the trivial getters / one-line passthroughs.
- README and `docs/` files **must** track public-API changes in the same
  commit — see the maintenance notes at the foot of every doc file.

---

**Maintenance**: when you change indentation, naming, or any of the above
conventions, update this file in the same commit.
