namespace NexusKit.ChatNotifications;

/// <summary>
/// Marker interface for "this DI singleton registers one or more chat-notification
/// kinds and subscribes to events that publish them". The host plugin enumerates
/// every <see cref="INotificationProducer"/> at <c>LoadAsync</c> and resolves
/// each one — that resolution is the registration side-effect.
/// <para>This mirrors the <c>IIpcProvider</c> pattern in <c>NexusKit.Ipc</c>:
/// implementations don't need any methods, just a constructor that takes
/// <see cref="IChatNotificationRegistry"/> + the relevant event source and
/// wires everything up.</para>
/// </summary>
public interface INotificationProducer
{
}
