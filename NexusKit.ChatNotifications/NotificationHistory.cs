using NexusKit.Persistence.Settings;

namespace NexusKit.ChatNotifications;

public sealed record NotificationHistoryEntry(
    string Kind,
    DateTime CreatedAtUtc,
    string Message,
    bool IsRead = false,
    ulong ContentId = 0,
    string? EventId = null);

/// <summary>Bounded notification history. Settings persistence keeps the
/// diagnostic view useful after a plugin restart without introducing a new
/// database table.</summary>
public sealed class NotificationHistoryStore
{
    private const string StoreKey = "nexuskit.chatnotifications.history.v1";
    private const int MaxEntries = 200;
    private static readonly TimeSpan CoalesceWindow = TimeSpan.FromSeconds(10);
    private readonly object mGate = new();
    private readonly ISettingsStore? mStore;
    private readonly List<NotificationHistoryEntry> mEntries = new();

    public NotificationHistoryStore(ISettingsStore? store = null)
    {
        mStore = store;
        if (mStore is not null) _ = LoadAsync();
    }

    public bool Add(string kind, string message, DateTime? nowUtc = null)
        => Add(kind, message, 0, null, nowUtc);

    /// <summary>Adds one notification unless the same event was already
    /// recorded for the same kind/content within the coalescing window.</summary>
    public bool Add(string kind, string message, ulong contentId,
        string? eventId = null, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        lock (mGate)
        {
            if (mEntries.Any(x => x.Kind == kind && x.ContentId == contentId
                && (eventId is not null
                    ? x.EventId == eventId
                    : x.EventId is null && x.Message == message)
                && (now - x.CreatedAtUtc).Duration() <= CoalesceWindow))
                return false;
            mEntries.Add(new NotificationHistoryEntry(kind, now, message,
                ContentId: contentId, EventId: eventId));
            if (mEntries.Count > MaxEntries) mEntries.RemoveAt(0);
            PersistLocked();
            return true;
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
        Persist();
    }

    private void PersistLocked() => _ = SaveAsync(mEntries.ToArray());
    private void Persist()
    {
        NotificationHistoryEntry[] snapshot;
        lock (mGate) snapshot = mEntries.ToArray();
        _ = SaveAsync(snapshot);
    }

    private async Task LoadAsync()
    {
        try
        {
            var loaded = await mStore!.GetAsync<NotificationHistoryEntry[]>(StoreKey).ConfigureAwait(false);
            if (loaded is not { Length: > 0 }) return;
            lock (mGate)
            {
                if (mEntries.Count == 0)
                    mEntries.AddRange(loaded.TakeLast(MaxEntries));
            }
        }
        catch
        {
            // Diagnostics must never prevent notification delivery.
        }
    }

    private async Task SaveAsync(IReadOnlyList<NotificationHistoryEntry> entries)
    {
        if (mStore is null) return;
        try { await mStore.SetAsync(StoreKey, entries.ToArray()).ConfigureAwait(false); }
        catch { /* Best-effort diagnostics persistence. */ }
    }
}
