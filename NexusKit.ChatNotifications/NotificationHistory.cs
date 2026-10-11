namespace NexusKit.ChatNotifications;

public sealed record NotificationHistoryEntry(
    string Kind,
    DateTime CreatedAtUtc,
    string Message,
    bool IsRead = false);

/// <summary>Bounded in-memory history for diagnostics and a future persisted
/// view. Duplicate kind/message pairs inside the coalescing window are ignored.</summary>
public sealed class NotificationHistoryStore
{
    private const int MaxEntries = 200;
    private static readonly TimeSpan CoalesceWindow = TimeSpan.FromSeconds(10);
    private readonly object mGate = new();
    private readonly List<NotificationHistoryEntry> mEntries = new();

    public void Add(string kind, string message, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        lock (mGate)
        {
            if (mEntries.Any(x => x.Kind == kind && x.Message == message
                && now - x.CreatedAtUtc <= CoalesceWindow)) return;
            mEntries.Add(new NotificationHistoryEntry(kind, now, message));
            if (mEntries.Count > MaxEntries) mEntries.RemoveAt(0);
        }
    }

    public IReadOnlyList<NotificationHistoryEntry> GetLatest(int limit = 50)
    {
        lock (mGate) return mEntries.AsEnumerable().Reverse().Take(Math.Clamp(limit, 1, MaxEntries)).ToArray();
    }

    public void MarkAllRead()
    {
        lock (mGate)
            for (var i = 0; i < mEntries.Count; i++)
                mEntries[i] = mEntries[i] with { IsRead = true };
    }
}
