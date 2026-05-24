# Writing a notification producer

Step-by-step recipe for adding a new chat-notification kind to a plugin.
Each producer is a DI singleton that:

1. Declares its `NotificationKindDefinition` at construction time.
2. Subscribes to its event source(s).
3. Formats and publishes lines via the framework-supplied
   `IChatNotificationPublisher`.

## When to create a new producer

One producer = one source of related notifications. Examples in the
PlayerNexusTracker plugin:

| Producer | Kind id | Event source |
|---|---|---|
| `HistoryNotificationProducer` | `history.player_changed` | `IInternalDataHistoryService.HistoryAdded` |
| `EnrichmentResolvedNotificationProducer` | `enrichment.resolved` | `IInternalDataPlayerWatcher.Observed` (filtered for newly-resolved lodestone ids) |
| `RefreshFailureNotificationProducer` | `refresh_queue.exhausted` | `IPlayerRefreshQueueService.Exhausted` |

Pick one event source per producer. Multiple producers can share the same
publisher contract — they're independent singletons.

## Template

```csharp
internal sealed class MyNotificationProducer : INotificationProducer, IDisposable
{
    public const string KindId = "<source>.<event>";

    private readonly IMyEventSource mSource;
    private readonly IChatNotificationPublisher mPublisher;
    private readonly ILocalizer mLoc;
    private bool mDisposed;

    public MyNotificationProducer(
        IMyEventSource source,
        IChatNotificationRegistry registry,
        ILocalizer loc)
    {
        mSource = source;
        mLoc = loc;

        mPublisher = registry.RegisterKind(new NotificationKindDefinition(
            Id: KindId,
            LabelKey: "ui.notifications.my.label",
            DescriptionKey: "ui.notifications.my.description",
            DefaultChannel: NotificationChannel.Echo,
            DefaultColor: NotificationColor.Yellow,
            // Optional: localization key for a CollapsingHeader the settings UI
            // groups this kind under. Sibling kinds with the same GroupKey
            // render together. Omit (or pass null) to land in "Other".
            GroupKey: "ui.notifications.group.my_module",
            // Optional: false for opt-in kinds. Useful when a generic catch-all
            // is enabled by default and you don't want this finer-grained kind
            // to fire alongside it until the user opts in.
            DefaultEnabled: true));

        mSource.Something += OnSomething;
    }

    public void Dispose()
    {
        if (mDisposed) return;
        mDisposed = true;
        mSource.Something -= OnSomething;
    }

    private void OnSomething(SomeEventArgs e)
    {
        var line = string.Format(mLoc.Get("ui.notifications.my.format"),
            e.Subject, e.Detail);
        mPublisher.Publish(new SeString(new TextPayload(line)));
    }
}
```

Register in the plugin's composition module:

```csharp
services.AddSingleton<MyNotificationProducer>();
services.AddSingleton<INotificationProducer>(sp =>
    sp.GetRequiredService<MyNotificationProducer>());
```

The double-registration is intentional: the concrete singleton is what
other services would inject if they ever need it, and the
`INotificationProducer` alias is what the plugin's eager-resolve loop
enumerates.

## Picking an `Id`

`Id` is the settings-store key for per-kind user overrides. Convention:

```
<source>.<event>
```

Examples: `history.player_changed`, `refresh_queue.exhausted`,
`encounter.party_member_joined`. Once shipped, never rename — existing
user overrides become orphaned silently. If you must rebrand a kind,
register the new id, deprecate the old one in the next migration, and
write a one-shot settings migration to copy the override row across.

## Default channel + color

Pick conservatively:

- **`NotificationChannel.Echo`** for most plugin output. Local, can't be
  missed by the player, doesn't pollute other channels.
- **`NotificationChannel.Notice`** when the user must visibly notice
  (e.g. completion of a long-running task).
- **`NotificationChannel.SystemMessage`** sparingly — formats like the
  game's own system messages and inherits their styling.
- **`NotificationChannel.Debug`** for development-only output. Hidden by
  default; users opt in via the channel-filter UI.

Color defaults follow the same logic — `Default` (no override) for
neutral output, `Yellow` for callouts, `Red` for failures, `Green` for
completions. Never `Red` for things that are not errors.

## Grouping kinds in the settings UI

Pass a `GroupKey` to bucket related kinds under a single
`ImGui.CollapsingHeader` in the Notifications tab. Sibling kinds sharing a
`GroupKey` render in one table; the group's heading is the localized value
of the key (define it in the plugin's `Language.resx`, same place as the
per-kind labels).

Group order follows producer registration order — the first kind registered
for each group fixes the group's slot in the UI. Use this to put a "default"
catch-all kind at the top of its group and finer-grained opt-in kinds
underneath.

When you ship a generic catch-all (default-enabled) alongside per-kind
variants (default-disabled via `DefaultEnabled: false`), document the
mutual-exclusion in the catch-all's description — enabling both produces
duplicate lines per burst, which is rarely what the user wants.

## Localization keys

Producer-side keys go in the **plugin's** `Language.resx`, not the
ChatNotifications module's `Strings.resx`. Rationale: the producer is
plugin-specific code; its labels should travel with the plugin's other
strings so a translator handles them in one place.

Keys to provide for each kind:

| Key | Use |
|---|---|
| `<plugin>.notifications.<kind>.label` | Row label in the Notifications tab |
| `<plugin>.notifications.<kind>.description` | Hover tooltip in the Notifications tab |
| `<plugin>.notifications.<kind>.format` | `string.Format` template used by the producer |

The kind's `LabelKey` and `DescriptionKey` point at the first two; the
producer code reaches for the third when composing the body.

## Threading

`Publish` is safe from any thread. Producers typically subscribe to
either:

- **Framework-thread events** (e.g. `IClientState.Login`) — synchronous,
  cheap. Format and publish inline.
- **Threadpool events** (e.g. history-service's `HistoryAdded`) — also
  fine. The Dalamud chat dispatch is thread-safe.

For event handlers that need an `await` (e.g. fetching a free-company
label before formatting the line), use the `_ = HandleAsync(...)`
fire-and-forget pattern with a `try`-`catch` inside — exceptions inside
an async-void-style handler would otherwise tear down the worker.

## Disposing

Producers register against an event source; `Dispose` MUST unsubscribe.
The DI container disposes singletons on plugin unload in
reverse-registration order, so the producer's `Dispose` runs before the
event source's — your unsubscribe is guaranteed to land on a live source.

## Suppression patterns

When the upstream event fires more often than the user wants in chat,
filter inside the producer (don't push the filtering into the framework
— that would make the kind less useful for other consumers).

Example: `EnrichmentResolvedNotificationProducer` subscribes to every
`Observed` event but only fires when the record's `LodestoneId` flipped
from null to a value, so the user sees one notification per resolution
instead of one per scan tick.

---

**Maintenance**: when the framework changes the `Publish` contract,
adds a new `NotificationChannel` value, or shifts the registration
pattern, update this guide alongside the README.
