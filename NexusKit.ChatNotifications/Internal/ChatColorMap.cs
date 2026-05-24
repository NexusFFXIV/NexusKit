namespace NexusKit.ChatNotifications.Internal;

/// <summary>
/// Resolves the curated <see cref="NotificationColor"/> palette to the
/// <c>UIColor</c> row ids the game uses to render <c>UIForegroundPayload</c>.
/// <para>The ids are taken from Lumina's <c>UIColor</c> sheet and match the
/// shades commonly used in plugin chat output. Tweak if a color renders off —
/// the lookup is one place, callers don't care about the raw ids.</para>
/// </summary>
internal static class ChatColorMap
{
    /// <summary>Returns the <c>UIColor</c> row id, or <c>0</c> when the caller
    /// shouldn't emit a foreground payload at all (<see cref="NotificationColor.Default"/>).</summary>
    public static ushort ToUiColor(NotificationColor color) => color switch
    {
        NotificationColor.Default => 0,
        NotificationColor.Yellow  => 25,
        NotificationColor.Green   => 67,
        NotificationColor.Red     => 17,
        NotificationColor.Blue    => 37,
        NotificationColor.Cyan    => 35,
        NotificationColor.Grey    => 3,
        _                         => 0,
    };

    /// <summary>RGBA quadruple used by the settings UI to render a small
    /// color swatch next to each palette entry. Approximation of the chat
    /// color — does not need to match perfectly, just be recognisable.</summary>
    public static (float R, float G, float B, float A) ToPreviewRgba(NotificationColor color) => color switch
    {
        NotificationColor.Yellow => (1.00f, 0.85f, 0.30f, 1f),
        NotificationColor.Green  => (0.40f, 0.85f, 0.40f, 1f),
        NotificationColor.Red    => (1.00f, 0.40f, 0.40f, 1f),
        NotificationColor.Blue   => (0.50f, 0.70f, 1.00f, 1f),
        NotificationColor.Cyan   => (0.40f, 0.90f, 0.95f, 1f),
        NotificationColor.Grey   => (0.70f, 0.70f, 0.70f, 1f),
        _                        => (1.00f, 1.00f, 1.00f, 1f),
    };
}
