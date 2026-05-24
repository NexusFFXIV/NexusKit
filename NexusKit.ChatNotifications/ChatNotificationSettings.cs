namespace NexusKit.ChatNotifications;

/// <summary>
/// Root settings POCO persisted under store key <c>"nexuskit.chat_notifications"</c>.
/// Each registered <see cref="NotificationKindDefinition"/> may have an
/// associated <see cref="ChatNotificationKindSetting"/> in <see cref="Overrides"/>
/// — when none is present, the kind's defaults apply. The dictionary is keyed
/// by <see cref="NotificationKindDefinition.Id"/>.
/// <para>The Notifications settings tab reads and writes this object via
/// <c>ISettingsStore.GetAsync&lt;T&gt;</c> / <c>SetAsync&lt;T&gt;</c> — a flat
/// JSON blob, which is exactly what the store already handles.</para>
/// </summary>
public sealed class ChatNotificationSettings
{
    /// <summary>Stable store key for this settings root.</summary>
    public const string StoreKey = "nexuskit.chat_notifications";

    public Dictionary<string, ChatNotificationKindSetting> Overrides { get; set; } = new();
}

/// <summary>Per-kind user overrides. Sensitive defaults live on the
/// <see cref="NotificationKindDefinition"/> the producer registered.</summary>
public sealed class ChatNotificationKindSetting
{
    public bool Enabled { get; set; } = true;
    public NotificationChannel Channel { get; set; }
    public NotificationColor Color { get; set; }
}
