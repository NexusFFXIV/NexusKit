namespace NexusKit.ChatNotifications;

/// <summary>
/// Central registry of chat-notification kinds for the host plugin. Producers
/// resolve this singleton from DI and call <see cref="RegisterKind"/> in their
/// constructor to declare what they can emit; the returned
/// <see cref="IChatNotificationPublisher"/> is the handle they later call to
/// actually send a line.
/// <para>The Notifications settings tab reads <see cref="Registered"/> to
/// render one row per kind. Registration order is preserved.</para>
/// </summary>
public interface IChatNotificationRegistry
{
    /// <summary>All kinds currently registered, in registration order. Cheap to
    /// enumerate on the draw thread.</summary>
    IReadOnlyList<NotificationKindDefinition> Registered { get; }

    /// <summary>Declare a notification kind. Returns the publisher the producer
    /// holds onto for later <c>Publish(...)</c> calls. Calling twice with the
    /// same <see cref="NotificationKindDefinition.Id"/> returns the
    /// already-registered publisher (idempotent) — duplicate registration is
    /// not an error so producers can be eagerly resolved more than once
    /// without bookkeeping.</summary>
    IChatNotificationPublisher RegisterKind(NotificationKindDefinition kind);
}
