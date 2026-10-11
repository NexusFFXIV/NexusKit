namespace NexusKit.ChatNotifications;

/// <summary>Controls whether a notification is delivered immediately or held
/// for a digest. Digest delivery is a policy decision only; a later host layer
/// owns buffering and flushing.</summary>
public enum NotificationDigestMode : byte
{
    Immediate = 0,
    DuringQuietHours = 1,
    SessionEnd = 2,
}

/// <summary>Persisted global notification policy. Existing settings JSON remains
/// compatible because all new values have safe defaults.</summary>
public sealed class NotificationPolicySettings
{
    public bool QuietHoursEnabled { get; set; }
    public TimeOnly QuietHoursStart { get; set; } = new(22, 0);
    public TimeOnly QuietHoursEnd { get; set; } = new(7, 0);
    public NotificationDigestMode DigestMode { get; set; }
}

public enum NotificationDelivery : byte
{
    Immediate = 0,
    Digest = 1,
}

public readonly record struct NotificationPolicyDecision(
    NotificationDelivery Delivery,
    bool IsQuietHours);

/// <summary>Pure local-time policy evaluator. Overnight windows (for example
/// 22:00–07:00) are supported; equal start/end means no quiet interval.</summary>
public static class NotificationPolicy
{
    public static NotificationPolicyDecision Decide(
        NotificationPolicySettings settings,
        TimeOnly localTime)
    {
        var quiet = settings.QuietHoursEnabled
            && IsWithinQuietHours(localTime, settings.QuietHoursStart, settings.QuietHoursEnd);

        var delivery = settings.DigestMode switch
        {
            NotificationDigestMode.SessionEnd => NotificationDelivery.Digest,
            NotificationDigestMode.DuringQuietHours when quiet => NotificationDelivery.Digest,
            _ => NotificationDelivery.Immediate,
        };

        return new NotificationPolicyDecision(delivery, quiet);
    }

    private static bool IsWithinQuietHours(TimeOnly value, TimeOnly start, TimeOnly end)
    {
        if (start == end) return false;
        return start < end
            ? value >= start && value < end
            : value >= start || value < end;
    }
}

/// <summary>Small bounded digest buffer. Hosts decide how to render the
/// returned items; this type only guarantees FIFO order and a hard cap.</summary>
public sealed class NotificationDigestBuffer
{
    private const int MaxItems = 100;
    private readonly Queue<NotificationDigestItem> mItems = new();

    public int Count => mItems.Count;

    public void Add(NotificationDigestItem item)
    {
        if (mItems.Count == MaxItems) mItems.Dequeue();
        mItems.Enqueue(item);
    }

    public IReadOnlyList<NotificationDigestItem> Flush()
    {
        var result = mItems.ToArray();
        mItems.Clear();
        return result;
    }
}

public sealed record NotificationDigestItem(
    string Kind,
    ulong ContentId,
    DateTime CreatedAtUtc,
    string Message);
