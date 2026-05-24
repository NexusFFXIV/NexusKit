# NexusKit.ChatNotifications

A generic chat-notification framework for Dalamud plugins. Modules and the
plugin declare "kinds" of notifications they may emit (history changes,
refresh failures, …); the framework hands each producer a publisher, manages
per-kind user overrides (enable / channel / color), and contributes a
**Notifications** tab to the auto-settings UI.

**Dalamud-tied** — builds with `Dalamud.NET.Sdk` so it can speak directly to
`IChatGui` + `SeString`. The framework is the only piece of NexusKit that
sits between Dalamud's chat API and the rest of the modules; producers stay
on Dalamud-free types (`INotificationProducer`, `IChatNotificationPublisher`)
where possible.

## Public API

| Type | File | Purpose |
|---|---|---|
| `IChatNotificationRegistry` | `IChatNotificationRegistry.cs` | Producers resolve this and call `RegisterKind(...)` in their constructor to declare a kind and receive a publisher. Idempotent — calling twice with the same `Id` returns the same publisher. |
| `IChatNotificationPublisher` | `IChatNotificationPublisher.cs` | Handle a producer holds onto. `Publish(SeString body)` applies the user's current channel / color overrides, prefixes `[PluginName] ` on system-style channels (or fills `XivChatEntry.Name` on sender-bearing ones), and dispatches via `IChatGui.Print`. |
| `INotificationProducer` | `INotificationProducer.cs` | Marker — mirrors the `IIpcProvider` pattern. Resolution is the registration: the plugin enumerates `IEnumerable<INotificationProducer>` at `LoadAsync` and each constructor wires its kind + event subscription. |
| `NotificationKindDefinition` (record) | `NotificationKindDefinition.cs` | `(Id, LabelKey, DescriptionKey, DefaultChannel, DefaultColor, GroupKey?, DefaultEnabled = true)`. `Id` is the stable overrides key (e.g. `"history.player_changed"`); labels are resource keys resolved through `ILocalizer`. Optional `GroupKey` buckets the kind under a CollapsingHeader in the settings UI (siblings sharing a `GroupKey` render together; null = "Other"). `DefaultEnabled = false` registers an opt-in kind that doesn't fire until the user explicitly turns it on. |
| `NotificationChannel` (enum) | `NotificationChannel.cs` | Routable chat channel. First five values (`Echo`, `Notice`, `SystemMessage`, `Debug`, `None`) are the curated palette; remaining values mirror every `XivChatTypeInfo`-decorated `XivChatType` (Say, Shout, Party, FreeCompany, Linkshells, Cross-world variants, …). `None` silently drops. |
| `NotificationColor` (enum) | `NotificationColor.cs` | Curated palette: `Default` (no foreground), `Yellow`, `Green`, `Red`, `Blue`, `Cyan`, `Grey`. Maps to FFXIV's `UIColor` rows via the internal `ChatColorMap`. |
| `ChatNotificationSettings` | `ChatNotificationSettings.cs` | Settings POCO persisted under `"nexuskit.chat_notifications"`. Holds a `Dictionary<string, ChatNotificationKindSetting> Overrides`; missing entries fall back to the kind's registered defaults. |

## Registration

```csharp
// Once per plugin — in ConfigureServices or AddServices:
services.AddNexusKitChatNotifications();
```

`AddNexusKitChatNotifications()` registers the registry singleton, the
`IAutoSettingsSection` that draws the Notifications tab, and the module's
own `Strings.resx` localizer.

Producers register themselves separately as DI singletons and as
`INotificationProducer`:

```csharp
services.AddSingleton<HistoryNotificationProducer>();
services.AddSingleton<INotificationProducer>(sp =>
    sp.GetRequiredService<HistoryNotificationProducer>());
```

The plugin eagerly resolves every producer at `LoadAsync`:

```csharp
// In Plugin.LoadAsync, after BuildAsync:
foreach (var _ in host.Services.GetServices<INotificationProducer>())
{
    // resolution IS the registration side-effect
}
```

## Dependencies

- Uses `Dalamud.NET.Sdk/15.0.0` (Dalamud chat types: `IChatGui`, `SeString`,
  `XivChatType`)
- ProjectRefs: `NexusKit.Core`, `NexusKit.Persistence`, `NexusKit.Ui`
  (the last one only for the `IAutoSettingsSection` hook)
- Requires `IChatGui` and `IPluginContext` already registered in DI — the
  plugin supplies both

## Producer example — minimal

```csharp
internal sealed class RefreshFailureNotificationProducer : INotificationProducer, IDisposable
{
    public const string KindId = "refresh_queue.exhausted";

    private readonly IChatNotificationPublisher mPublisher;
    private readonly IPlayerRefreshQueueService mQueue;

    public RefreshFailureNotificationProducer(
        IChatNotificationRegistry registry,
        IPlayerRefreshQueueService queue,
        ILocalizer loc)
    {
        mPublisher = registry.RegisterKind(new NotificationKindDefinition(
            Id: KindId,
            LabelKey: "ui.notifications.refresh_failure.label",
            DescriptionKey: "ui.notifications.refresh_failure.description",
            DefaultChannel: NotificationChannel.Echo,
            DefaultColor: NotificationColor.Red));

        mQueue = queue;
        mQueue.Exhausted += OnExhausted;
    }

    private void OnExhausted(ulong contentId, RefreshCategory cat)
    {
        var text = string.Format(/* locale-aware */ "Refresh exhausted for {0}/{1}", contentId, cat);
        mPublisher.Publish(new SeString(new TextPayload(text)));
    }

    public void Dispose() => mQueue.Exhausted -= OnExhausted;
}
```

## Per-kind user overrides

The Notifications settings tab lists every registered kind (in registration
order) with three controls per row:

- **Enabled** — checkbox. While off, `Publish(...)` no-ops.
- **Channel** — combo bound to `NotificationChannel`. Includes the full
  Dalamud channel palette plus the curated system-style values.
- **Color** — combo bound to `NotificationColor`. `Default` skips the
  `UIForeground` wrap and lets the chat type's native color through.

Overrides are persisted as JSON in the `settings` table under
`ChatNotificationSettings.StoreKey`. The registry caches the deserialised
POCO and every publisher reads it on each `Publish` — so saves propagate
instantly without needing a plugin reload.

## How dispatch works

`ChatNotificationPublisher.Publish` (internal):

1. Resolves the effective `(Enabled, Channel, Color)` — from `Overrides` if
   present, otherwise from `NotificationKindDefinition.Default*`.
2. Bails when disabled or `Channel == None`.
3. Looks up the underlying `XivChatType` via `ChatChannelMap`.
4. For sender-bearing channels (Say, Party, FC, Linkshells, …) sets
   `XivChatEntry.Name = "<PluginName>"`; the body is rendered as-is with the
   color wrap.
5. For system-style channels (Echo / Notice / SystemMessage / Debug /
   Urgent) prepends `[PluginName] ` to the body — those rows don't render
   a sender slot, so the prefix is how the user identifies the source.
6. Calls `IChatGui.Print` — thread-safe; producers can publish from
   threadpool or framework threads alike.

Any exception during dispatch is caught + logged at `Warning`. A
notification problem never bubbles up to the event handler that drove it.

## Translations

Module-shipped keys live in `Resources/Strings.resx` (EN) and
`Strings.de.resx`. They cover the Notifications tab labels (nav title,
empty-state copy), channel names, and color names. Producer-specific
keys (kind label / description) belong in the **plugin's** `Language.resx`
so the user sees the same plugin-side branding the rest of the UI uses.

---

**Maintenance**: when you add a new `NotificationChannel` value or alter
the `Publish` dispatch behaviour, update this README + the inline doc
comments on the affected types.
