namespace NexusKit.ChatNotifications;

/// <summary>
/// Curated palette for chat-notification text. Maps to <c>UIColor</c> row ids
/// via <c>ChatColorMap</c> and renders through a <c>UIForegroundPayload</c>
/// wrapping the message body. <c>Default</c> means "no foreground payload" —
/// the chat type's own color shows through.
/// </summary>
public enum NotificationColor : byte
{
    /// <summary>No color override — uses whatever color the chat channel
    /// renders by default.</summary>
    Default = 0,

    Yellow = 1,
    Green = 2,
    Red = 3,
    Blue = 4,
    Cyan = 5,
    Grey = 6,
}
