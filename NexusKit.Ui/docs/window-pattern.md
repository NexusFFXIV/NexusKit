# Window Pattern (NexusKit.Ui)

How the framework's `MainWindow` / `SettingsWindow` abstractions, the
`PluginUiHost`, and Dalamud's `WindowSystem` fit together.

## Abstractions

```csharp
public abstract class MainWindow : Window         // Dalamud's Window
public abstract class SettingsWindow : Window
```

Both inherit Dalamud's `Window` and add no functionality — they are pure
*role markers*. The framework uses the concrete class to decide what to do:

| Role | Trigger | Action |
|---|---|---|
| `MainWindow` | User clicks the plugin's "Open" button in `/xlplugins` | `PluginUiHost` sets the window's `IsOpen = true` |
| `SettingsWindow` | User clicks the plugin's gear icon | Same, but for the settings window |

Plugins extend one of these, register via DI, and the wiring just works.

## Constructor signature

```csharp
protected MainWindow(string name, ImGuiWindowFlags flags = ImGuiWindowFlags.None);
protected SettingsWindow(string name, ImGuiWindowFlags flags = ImGuiWindowFlags.None);
```

`name` is the title (`"My Plugin"`) plus an optional `###id` part Dalamud uses
for state persistence across reloads. The convention from our own code:

```csharp
public MyPluginMainWindow() : base("MyPlugin###MyPlugin_Main")
{
    Size = new Vector2(600, 400);
    SizeCondition = ImGuiCond.FirstUseEver;
}
```

The `###` plus a stable suffix keeps Dalamud's saved window state intact
even if you change the visible title.

## Drawing

Override `Draw()`. Dalamud calls it every frame while the window is open.
You have full ImGui at your disposal:

```csharp
public override void Draw()
{
    ImGui.Text("Hello");
    if (ImGui.Button("Do thing")) Doit();
}
```

For lifecycle hooks, Dalamud's `Window` exposes:
- `OnOpen()` — runs when `IsOpen` flips from false to true
- `OnClose()` — runs when the user closes the window
- `PreDraw()` / `PostDraw()` — before/after ImGui rendering for style stacks
- `DrawConditions()` — return `false` to skip rendering this frame even
  when `IsOpen` is true

## Registration

```csharp
services.AddMainWindow<MyMainWindow>();
services.AddSettingsWindow<MyCustomSettings>();   // or AddAutoSettingsWindow()
```

Both methods register the concrete type as a singleton **and** as the base
class (`MainWindow` / `SettingsWindow`). The `PluginUiHost` resolves the
base class, so DI returns the registered concrete instance.

Multiple `MainWindow` or `SettingsWindow` registrations: only the
last-registered wins (`AddSingleton` semantics). The framework picks one of
each, not many.

## `PluginUiHost` — the integration glue

Single class wiring three Dalamud concerns:

1. **`WindowSystem.Draw`** — added to `UiBuilder.Draw` so Dalamud paints
   our windows every frame.
2. **`UiBuilder.OpenMainUi` / `OpenConfigUi`** — when the user clicks the
   plugin's "Open" or gear button, flip the corresponding window's
   `IsOpen`.
3. **`IDalamudPluginInterface.LanguageChanged`** — push the new culture
   into `LocalizationManager.ReportHostCulture` so the layered localizer
   re-resolves into the right language.

The host is resolved eagerly by the plugin in `LoadAsync`:

```csharp
host.Services.GetRequiredService<PluginUiHost>();
```

Constructor side-effects subscribe to the Dalamud events; the
`IDisposable.Dispose` (run by the ServiceProvider on shutdown) unsubscribes
them.

If the plugin doesn't register a `MainWindow` (or `SettingsWindow`), the
respective event handler is a no-op — Dalamud-side button is still there
but does nothing.

## Where does `WindowSystem` come from

`AddNexusKitUi()` registers a `WindowSystem` singleton whose constructor
takes the plugin name from `IPluginContext`:

```csharp
services.AddSingleton<WindowSystem>(sp =>
{
    var ctx = sp.GetRequiredService<IPluginContext>();
    return new WindowSystem(ctx.PluginName);
});
```

Dalamud uses the system's namespace string to namespace ImGui state
(window positions, scroll, etc.). Per-plugin uniqueness is automatic.

`PluginUiHost`'s constructor adds the registered `MainWindow` /
`SettingsWindow` (when present) to the system; on dispose it calls
`WindowSystem.RemoveAllWindows()`.

## Opening / closing programmatically

```csharp
mainWindow.IsOpen = true;     // open
mainWindow.IsOpen = false;    // close
mainWindow.IsOpen = !mainWindow.IsOpen;   // toggle
```

`PluginUiHost` exposes `OpenMain()` / `OpenConfig()` methods you can call
from slash commands or any other service:

```csharp
public sealed class OpenMainCommand
{
    public OpenMainCommand(ICommandRegistry cmds, PluginUiHost ui)
        => cmds.Register("/mymain", (_, _) => ui.OpenMain(), help: "Open the main window.");
}
```

## Window subclasses that need DI

Both `MainWindow` and `SettingsWindow` are abstract classes registered as
DI singletons via `AddMainWindow<T>` / `AddSettingsWindow<T>`. They can take
constructor parameters like any other service:

```csharp
public sealed class MyMainWindow : MainWindow
{
    private readonly IFfxivCollectClient ffxiv;
    private readonly ILocalizer loc;

    public MyMainWindow(IFfxivCollectClient ffxiv, ILocalizer loc)
        : base("My Plugin###MyPlugin_Main")
    {
        this.ffxiv = ffxiv;
        this.loc = loc;
    }

    public override void Draw() { /* … */ }
}
```

DI resolves the parameters; you focus on rendering.

---

**Maintenance**: when you change `PluginUiHost`'s event subscriptions, add
a third window role (e.g. `OverlayWindow`), or shift the registration
convention, update this doc.
