# NexusKit.Ui

Dalamud-tied UI primitives: Window abstractions, the auto-rendered settings
window, the host that wires Dalamud's `UiBuilder` events, plus small utility
services (commands, territory tracking, browser launcher).

**This project references Dalamud.** Anything in here can use
`Dalamud.Plugin.Services.*`, `Dalamud.Interface.*`, `Dalamud.Bindings.ImGui`, etc.

Built with `Dalamud.NET.Sdk` and `<Use_DalamudPackager>false</Use_DalamudPackager>`
— it's a library, not a plugin.

## Public API

| Type | File | Purpose |
|---|---|---|
| `MainWindow`, `SettingsWindow`, `NexusWindow` | `Abstractions/` | Abstract base classes; plugins extend `MainWindow`/`SettingsWindow` for role-marked windows or `NexusWindow` for an extra window registered via `AddWindow<T>()`. |
| `AutoSettingsWindow` | `AutoSettings/AutoSettingsWindow.cs` | Sidebar + tabs layout that renders every registered settings schema AND every `IAutoSettingsSection`. Plugins register it via `AddAutoSettingsWindow()`. |
| `IAutoSettingsSection` | `AutoSettings/IAutoSettingsSection.cs` | Extension hook on `AutoSettingsWindow` — implementations contribute one extra sidebar entry and own their body's rendering. Used for non-declarative UIs (dynamic lists, force-run buttons, multi-table editors). |
| `DbMaintenanceSettingsSection` | `AutoSettings/Maintenance/` | Framework-provided `IAutoSettingsSection`: shows on-disk + per-table stats, last-run per maintenance contributor, and a "Run now" button. Opt-in via `AddDbMaintenanceSettingsSection(order)`. |
| `PluginUiHost` | `PluginUiHost.cs` | Wires `UiBuilder.Draw / OpenMainUi / OpenConfigUi` to the registered windows and bridges Dalamud's UI language into `LocalizationManager`. Disposable. |
| `IWindowManager`, `WindowManager` | `WindowManager.cs` | Open/close any registered `NexusWindow` by type or runtime instance — used by main-window code that needs to flip auxiliary windows without holding direct references. |
| `IImageCache` | `Imaging/` | Texture cache for player avatars / Lodestone images — converts URLs into Dalamud `ISharedImmediateTexture` references with memory-bound eviction. |
| `ICommandRegistry`, `CommandHandler` delegate | `Commands/` | Slash-command registration with per-handle and global dispose-time cleanup. |
| `DalamudBrowserLauncher` (internal) | `Utilities/` | `IBrowserLauncher` implementation via `Dalamud.Utility.Util.OpenLink`. |
| `SizeFormat`, `DurationFormat` | `Utilities/` | Static formatters: `SizeFormat.Bytes(long)` for adaptive B/KB/MB rendering, `DurationFormat.TwoUnit(TimeSpan)` for the two-highest-non-zero unit form used by the refresh-queue ETA + maintenance "last run". |

### Widgets (`Widgets/`)

Domain-agnostic ImGui building blocks. All static, all sync. Use any
that fits — they're independent.

| Widget | Purpose |
|---|---|
| `NexusCard` | Padded child region (`using` scope). |
| `NexusSection` | Disabled-colour title + separator. |
| `NexusGroupBox` | Yellow title + column-bounded separator + body. Helpers: `DrawColumns(...)`, `DrawGrid(perRow, ...)` for paired / wrapped layouts. Optional `titleSuffix` for muted inline metadata. |
| `NexusSplitLayout` | Horizontal master/detail with fixed left width. |
| `NexusStatCard` | "Label + big value + sub-label" tile with optional `LabelSuffix` / `ValueSuffix` slots. |
| `NexusKeyValueRow` | Grey label : value pair with optional custom value drawer. Use `DrawWithControl` when the value column holds a framed control (button, combo) so the label centres against it instead of sitting high in a taller row. |
| `NexusTable` | `BeginTable` wrapper that takes a column spec + clipper-driven row callback. |
| `NexusListClipper` | Virtualized `ForEach<T>` over a list (wraps `ImGuiListClipper`). |
| `NexusLoadingSpinner` | Indeterminate spinner. |
| `NexusHint` | Inline icon + hover tooltip (defaults to `DalamudYellow`, SameLine). |
| `NexusIconButton` | Icon-only button with tooltip; returns bool or takes `Action onClick`. Tooltips are shown with `ImGuiHoveredFlags.AllowWhenDisabled`, so they survive an enclosing `BeginDisabled` scope. |
| `NexusRoundedAvatar` | Rounded child + centred icon glyph; corner radius scales with size. |
| `NexusIconToolbar` | Right-aligned row of icon-button slots that auto-computes its own width from the slots that are actually present. Slots carry their own width so a spinner narrower than a standard button doesn't leave a gap. |

### Disabled buttons should say why

`Slot.Button(..., enabled: false)` greys the button out and swallows the click. Pass
`disabledTooltip` as well, or the hover text will keep describing an action the button
won't perform:

```csharp
NexusIconToolbar.Slot.Button(FontAwesomeIcon.MapMarkerAlt,
    loc.Get("…mark_position"),
    () => marker.MarkPosition(id),
    enabled: inRange,
    disabledTooltip: loc.Get("…mark_position.disabled"));
```

## Registration

```csharp
services.AddNexusKitUi();                          // WindowSystem, PluginUiHost,
                                                   // LocalizationManager, utilities,
                                                   // FrameworkLocalizer + LayeredLocalizer,
                                                   // pulls AddNexusKitCore() transitively
services.AddMainWindow<MyMainWindow>();            // (optional) plugin's main window
services.AddSettingsWindow<MyCustomSettings>();    // (optional) custom settings UI
services.AddAutoSettingsWindow();                  // (optional) framework auto UI — pick this OR AddSettingsWindow<T>
services.AddWindow<MyDebugWindow>();               // (optional) any extra NexusWindow
services.AddDbMaintenanceSettingsSection();        // (optional) DB-maintenance tab in auto-settings
```

The plugin must also have registered Dalamud handles in DI before resolving
these services:

```csharp
services.AddSingleton(PluginInterface);     // IDalamudPluginInterface
services.AddSingleton(CommandManager);      // ICommandManager — only needed if you use ICommandRegistry
```

## Dependencies

- Uses `Dalamud.NET.Sdk/15.0.0` so Dalamud types are on the reference set
- ProjectRefs: `NexusKit.Core`, `NexusKit.Persistence`
- Bundled translations: `Resources/Framework.resx` (+ `.de.resx`); see
  Localization in [NexusKit.Core/README.md](../NexusKit.Core/README.md)

## Example: a plugin's main window

```csharp
public sealed class MyMainWindow : MainWindow
{
    public MyMainWindow() : base("MyPlugin###MyPlugin_Main")
    {
        Size = new Vector2(600, 400);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        ImGui.Text("hello");
    }
}

// Registration
services.AddMainWindow<MyMainWindow>();
```

## Example: register a slash command

```csharp
public sealed class GreetCommand : IDisposable
{
    private readonly IDisposable handle;

    public GreetCommand(ICommandRegistry commands)
    {
        handle = commands.Register("/greet",
            (cmd, args) => /* ... */,
            help: "Say hello.");
    }

    public void Dispose() => handle.Dispose();
}
```

## The AutoSettingsWindow

When you call `services.AddAutoSettingsWindow()`, the framework's
`AutoSettingsWindow` is registered as your `SettingsWindow`. It renders
every schema registered via `services.AddSettings<T>(b => …)`:

- A left sidebar lists non-module schema groups, plus a "Modules" entry when
  any `IModuleSettings`-implementing schema is registered.
- The right pane shows the active section.
- The "Modules" section is itself a tab bar: an "Allgemein"/"General" tab lists
  every module with just its `ModuleEnabled` toggle; each enabled module that
  has more than just `ModuleEnabled` gets a dedicated tab with its detail
  settings.
- Save-on-change: any control mutation triggers `schema.SaveAsync(store, …)`.

If you want a different layout, register your own
`SettingsWindow` subclass with `services.AddSettingsWindow<T>()` instead.

---

**Maintenance**: when you add a window abstraction, a utility service, or
restructure the auto-settings layout, update the public-API table and any
example that depends on it.
