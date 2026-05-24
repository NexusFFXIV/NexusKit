using Dalamud.Game.Text.SeStringHandling;

namespace NexusKit.ChatNotifications;

/// <summary>
/// Handle returned by <see cref="IChatNotificationRegistry.RegisterKind"/>.
/// Producers hold one publisher per registered kind and call
/// <see cref="Publish"/> from their event handlers.
/// <para>The publisher gates on the current settings (Enabled +
/// Channel == None drop silently), applies the configured color via a
/// <c>UIForegroundPayload</c> wrap, prefixes <c>"[PluginName] "</c> from
/// <c>IPluginContext</c>, and dispatches via <c>IChatGui.Print</c>. Cheap to
/// call from a worker thread — the underlying chat API is thread-safe.</para>
/// </summary>
public interface IChatNotificationPublisher
{
    /// <summary>The kind this publisher was created for. Exposed so producers
    /// can reference the same id they registered without keeping a separate
    /// copy.</summary>
    NotificationKindDefinition Kind { get; }

    /// <summary>Send a chat line for this kind. <paramref name="body"/> is the
    /// producer-supplied message content; the framework handles plugin prefix
    /// and color. No-op when the kind is disabled or routed to
    /// <see cref="NotificationChannel.None"/>.</summary>
    void Publish(SeString body);
}
