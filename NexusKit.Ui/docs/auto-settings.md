# AutoSettingsWindow (NexusKit.Ui)

In-depth rendering rules, layout, and customisation paths for the framework's
auto-rendered settings UI.

## When you get this UI

The plugin opts in:

```csharp
services.AddAutoSettingsWindow();
```

That registers `AutoSettingsWindow` as the plugin's `SettingsWindow`. The
`PluginUiHost` then adds it to the `WindowSystem` and routes Dalamud's
"Open Config" button (`UiBuilder.OpenConfigUi`) to it.

If you prefer a fully custom settings UI, skip `AddAutoSettingsWindow()`
and register your own `SettingsWindow` subclass instead:

```csharp
services.AddSettingsWindow<MyCustomSettingsWindow>();
```

The framework picks whichever was registered.

## Layout

```
┌────────────────┬──────────────────────────────────────────────────┐
│ Sidebar        │ Content                                          │
│ ──────────     │                                                  │
│ ▸ Tracker      │ (the active section)                             │
│   Modules      │                                                  │
│ ──────────     │                                                  │
│ (170px wide)   │ (fills remaining width)                          │
└────────────────┴──────────────────────────────────────────────────┘
```

- **Sidebar** (170px wide) lists one entry per non-module schema group plus
  a single "Modules" entry when any `IModuleSettings` schema is registered.
- **Content area** renders the active section. Selection persists via
  stable identity (`plugin:<group-identity>`, `modules`), so language
  changes don't reset the active tab.

## Schema classification

Schemas come from `services.AddSettings<T>(b => …)`. The window sorts each
schema as either:

| Schema kind | Detection | Routing |
|---|---|---|
| **Plugin schema** | `T` does **not** implement `IModuleSettings` | One sidebar entry per `Group(...)`; sharing a group puts multiple schemas in the same tab. |
| **Module schema** | `T` implements `IModuleSettings` | All collected under the single "Modules" sidebar entry. |

## Plugin-group sections

For each unique `Group` among non-module schemas, the sidebar shows one
entry labelled with the group's resolved name. Selecting it renders every
schema in that group sequentially. If a group contains multiple schemas
with a `Title`/`TitleKey`, the title appears as a separator.

```
[Tracker] (sidebar entry — Group = "Tracker", schemas: TrackerSettings only)
   ☑ Enable tracker
   Max recent players: |====------| 250
   Greeting: [_______________]
   Display mode: [Compact ▼]
```

## The "Modules" section

When the sidebar's "Modules" entry is active, the right pane renders a
nested **tab bar**:

```
[Allgemein] [FFXIVCollect] [Lodestone]
─────────────────────────────────────────
(Allgemein tab content)
☑ FFXIVCollect
☑ Lodestone
```

- **First tab: "Allgemein" / "General"** — one `ModuleEnabled` checkbox per
  module schema. The tab is always present.
- **Detail tabs: one per enabled module that has properties beyond the
  `ModuleEnabled` flag.** A module whose only property is `ModuleEnabled`
  doesn't get its own tab.
- **Disabling a module** removes its detail tab on the next frame; the UI
  silently rolls back to "Allgemein" if you were on the now-hidden tab.

### Module detail tab content

Renders every schema property **except** `ModuleEnabled` — the
`RegisterModuleEnabledFlag()` helper marks that property `.Hidden()`, so the
detail tab never duplicates the toggle. The "Allgemein" overview is the single
edit surface for the on/off state; both writes the same row in the settings
table either way.

## Save-on-change

Every `SettingsControlRenderer.Render` call returns `true` if the user
changed the value this frame. The window's loop fires
`schema.SaveAsync(store, instance)` fire-and-forget after the change.

ImGui control-by-control behavior:
| Control | Change frequency |
|---|---|
| Checkbox | One save per click |
| Slider | One save per drag frame (potentially many per second) |
| TextBox | One save per keystroke |
| Combo | One save per selection |
| NumericInput | One save per spinner click |

For typical settings POCOs the write is well under 1ms (small JSON, one
SQLite UPDATE, in-memory cache invalidation). If you have a high-frequency
field that's hammering SQLite, wrap your edit in a debouncing service.

## Control auto-resolution

If a property's `Kind` is `ControlKind.Auto` (the default — set by the
builder unless you called `.Checkbox()` / `.Slider()` / etc.), the renderer
picks based on `PropertyType`:

| Property type | Auto control |
|---|---|
| `bool` | `Checkbox` |
| `string` | `TextBox` |
| `int`, `float`, `double` | `NumericInput` |
| `enum` | `Combo` (auto-resolves but `Choices` must be set manually for now) |
| other | falls through to `TextBox` |

Override with explicit builder calls (`.Slider(min, max)`, `.Combo<TEnum>()`
once that helper exists, `.TextBox()`).

## Description and placeholder rendering

`Description` becomes a tooltip on hover. `Placeholder` is used by
`TextBox` controls for ghost text (`ImGui.InputTextWithHint`).

Both go through the `ILocalizer` — pass them as `.Description(string)` for
literals or `.DescriptionKey(string)` for resource keys.

## Identity for stable navigation

Sidebar selection survives language changes because we key on the schema's
raw `Group.Literal ?? Group.Key`. If a schema migrates from `.Group("X")`
to `.GroupKey("…")`, the user's "currently selected tab" might forget once;
afterwards it sticks again.

## Window sizing

Default opens at 720×540, `ImGuiCond.FirstUseEver` — the user's resize
persists across sessions via Dalamud's `WindowSystem`.

## How values get loaded

Lazy. The first time `Draw` runs, `LoadAll()` synchronously resolves every
schema's stored value via `ISettingsStore.GetAsync<T>(key)` and caches the
materialised POCO in a `Dictionary<Type, object>`. Subsequent edits mutate
that cached instance; save-on-change persists it back through the store.

Reopening the window doesn't reload — we keep the cached POCO for the
lifetime of the `AutoSettingsWindow` (which equals the lifetime of the
plugin, since it's a DI singleton). The settings store cache is the
authoritative copy across reloads.

## Replacing or extending

- **Custom settings window**: `services.AddSettingsWindow<MyWindow>()` (skip
  `AddAutoSettingsWindow`). Implement your own renderer over
  `ISettingsSchemaProvider` / `ISettingsStore`.
- **Custom controls**: today the `SettingsControlRenderer` is `internal`
  and not extensible. If you need a custom control type, the cleanest
  extension is to add it to the framework (PR / fork): new `ControlKind`,
  new builder method, new render branch.
- **Whole new sections via `IAutoSettingsSection`** — see below.

## Extension hook: `IAutoSettingsSection`

When the declarative `AddSettings<T>` schema model doesn't fit (dynamic
row lists, runtime-registered kinds, multi-table editors, force-run
buttons), implement `IAutoSettingsSection` instead:

```csharp
public interface IAutoSettingsSection
{
    string NavTitleKey { get; }     // sidebar label (resolved via ILocalizer)
    int Order { get; }              // sort key relative to other nav items; defaults
                                    // around 0–200 for built-ins
    void Render(ISettingsStore store);   // own the entire content body
}
```

Register as `services.AddSingleton<IAutoSettingsSection, MySection>()`.
The window enumerates every registered section from DI on `Draw` and
inserts each as a sidebar entry. Implementations own persistence via
the provided store; the framework does not save anything for them.

Built-in sections shipped with the framework / plugin:

| Section | Project | Purpose |
|---|---|---|
| `ChatNotificationsSettingsSection` | NexusKit.ChatNotifications | One row per registered notification kind — enable / channel / color |
| `DbMaintenanceSettingsSection` | NexusKit.Ui (via `AddDbMaintenanceSettingsSection`) | On-disk + per-table stats, last-run per contributor, "Run now" button |
| `RefreshQueueSettingsSection` | NexusKit.Modules.PlayerEnrichment | Refresh-queue diagnostics: pending count, per-category breakdown, drain ETA |
| `WidgetSettingsSection` | MyPlugin.Plugin | User-defined player-list filters editor |

Plugin-domain sections (e.g. a custom encounter list, a tag editor) follow
the same shape: one new singleton registered against `IAutoSettingsSection`,
no edits to `AutoSettingsWindow`.

---

**Maintenance**: when you change the routing rules, add a control kind, or
restructure the sidebar/tabs, update this doc.
