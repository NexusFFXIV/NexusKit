# Settings (NexusKit.Persistence)

How to define typed plugin / module settings, render them in the auto-settings
UI, and persist them in the shared SQLite database.

## Two storage paths

The framework offers two ways to persist settings; they coexist.

### 1. Plain key-value store (`ISettingsStore`)

For internal state that you don't want in the UI: last-opened tab, retry
counters, ephemeral flags. Plain POCO in, plain POCO out.

```csharp
public sealed class MyService
{
    private readonly ISettingsStore store;
    public MyService(ISettingsStore s) { store = s; }

    public async Task SaveLastTabAsync(string tab, CancellationToken ct)
        => await store.SetAsync("myplugin.last_tab", tab, ct);

    public Task<string?> LoadLastTabAsync(CancellationToken ct)
        => store.GetAsync<string>("myplugin.last_tab", ct);
}
```

Values are JSON-serialised under the supplied key. No schema, no UI.

### 2. Typed POCO + fluent schema (`AddSettings<T>`)

For settings the user should see and change. You define a POCO, register a
schema describing how to render each property, and the framework writes the
serialised POCO under a stable key.

```csharp
public sealed class TrackerSettings
{
    public int MaxRecentPlayers { get; set; } = 100;
    public int RefreshTtlDays { get; set; } = 7;
}

services.AddSettings<TrackerSettings>(b => b
    .StoredAs("config")
    .Group("Tracker", order: 10)
    .Property(x => x.MaxRecentPlayers, p => p
        .Label("Max recent players")
        .Description("How many entries the 'Recent players' list shows (most-recent first).")
        .NumericInput()
        .Order(1))
    .Property(x => x.RefreshTtlDays, p => p
        .Label("Refresh after (days)")
        .Description("How long cached Lodestone/FFXIVCollect data stays fresh before the background queue re-fetches it.")
        .NumericInput()
        .Order(2)));
```

Builder methods used here: `.NumericInput()` renders an integer textbox.
For a slider on a numeric property use `.Slider(min, max)` instead; for an
enum-like string use `.Choices("A", "B", "C")`.

The schema becomes an `IRegisteredSettingsSchema`; the `AutoSettingsWindow`
picks it up and renders a tab named "Tracker" in the left sidebar.

## Builder reference

### `SettingsSchemaBuilder<T>` — schema-level

| Method | Effect |
|---|---|
| `StoredAs(string key)` | The DB key (`settings.key` column). Defaults to `typeof(T).FullName`. |
| `Group(string name, int order)` | Group label and sort order. Plugin schemas with the same group share a sidebar tab. |
| `GroupKey(string key, int order)` | Same but resolves via `ILocalizer`. |
| `Title(string)` / `TitleKey(string)` | Optional per-schema subheader, shown when multiple schemas share a group or as the module's tab label. |
| `Property(x => x.Foo, p => …)` | Add a property; lambda configures rendering. |

### `SettingsPropertyBuilder<T, TValue>` — per-property

| Method | Effect |
|---|---|
| `.Label(string)` / `.LabelKey(string)` | Display label. Literal or resource key. |
| `.Description(string)` / `.DescriptionKey(string)` | Tooltip text shown on hover. |
| `.Placeholder(string)` / `.PlaceholderKey(string)` | For `TextBox` controls, ghost text when empty. |
| `.Order(int)` | Sort order within the schema. |
| `.Checkbox()` | Force checkbox control. (Auto-selected for `bool`.) |
| `.TextBox()` | Force textbox. (Auto for `string`.) |
| `.NumericInput()` | Force plain number input. (Auto for `int`/`float`/`double`.) |
| `.Slider(double min, double max)` | Bounded slider; only for numeric types. |
| `.Choices(params TValue[])` | Combo box; works for any sortable type. (Auto for `enum`.) |
| `.Hidden()` | Defines a property but never renders it (keeps it editable via `ISettingsStore`). |

### Auto-resolution of control type

If you don't call `.Checkbox()` / `.Slider()` / etc., the framework picks
based on `PropertyType`:

- `bool` → checkbox
- `string` → textbox
- `int` / `float` / `double` → numeric input
- enum → combo

## Module-style settings (`IModuleSettings`)

A module schema must implement `IModuleSettings` (one `bool ModuleEnabled`
property). That marker is how `AutoSettingsWindow` routes the schema into the
"Modules" section instead of a plugin tab.

The `.RegisterModuleEnabledFlag()` extension adds the `ModuleEnabled` property
to the schema with the framework's standard label/description (translated to
the user's UI language) and marks it `.Hidden()`. Result: the detail tab
doesn't render the toggle a second time — only the "Allgemein" overview
exposes it.

```csharp
public sealed class MyModuleSettings : IModuleSettings
{
    public const string StoreKey = "myplugin.modules.my.settings";

    public bool ModuleEnabled { get; set; } = true;
    public int BatchSize { get; set; } = 100;
}

services.AddSettings<MyModuleSettings>(b => b
    .StoredAs(MyModuleSettings.StoreKey)
    .GroupKey("nexuskit.module.group", order: 300)
    .TitleKey("myplugin.modules.my.title")
    .RegisterModuleEnabledFlag(order: 0)        // hidden in detail tab; rendered in Modules-overview
    .Property(x => x.BatchSize, p => p
        .LabelKey("myplugin.modules.my.batch_size.label")
        .Slider(10, 1000)
        .Order(1)));
```

In the UI:
- The "Modules" sidebar entry lists every IModuleSettings schema's
  `ModuleEnabled` toggle in its "Allgemein" / "General" tab.
- Each enabled module with more than just `ModuleEnabled` gets a detail tab
  named after its `Title` / `TitleKey`.

## Save-on-change

The `AutoSettingsWindow` calls `schema.SaveAsync(store, instance)` after every
control change. Text boxes fire on each keystroke; sliders fire on each drag
event. For tiny POCOs (the typical case) the write is well under 1ms; the
`SettingsStore` keeps an in-memory cache so subsequent reads don't hit the DB.

If you need to batch (long text fields, frequent slider drags), wrap the
property in a debouncing service of your own — the framework doesn't impose one.

## Reading a settings POCO at runtime

```csharp
public sealed class MyService
{
    private readonly ISettingsStore store;
    public MyService(ISettingsStore s) { store = s; }

    public async Task<int> GetBatchSizeAsync(CancellationToken ct)
    {
        var s = await store.GetAsync<MyModuleSettings>(MyModuleSettings.StoreKey, ct)
            ?? new MyModuleSettings();
        return s.BatchSize;
    }
}
```

The settings table is cached, so this is effectively O(1) after the first
read.

---

**Maintenance**: when you add a builder method or change control auto-resolution,
update both reference tables above plus any code that depends on the old behavior.
