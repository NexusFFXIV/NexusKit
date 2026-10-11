using Dalamud.Plugin.Services;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Microsoft.Extensions.Logging;
using NexusKit.Core;
using NexusKit.Core.Context;
using NexusKit.Persistence.Settings;

namespace NexusKit.ChatNotifications.Internal;

/// <summary>
/// Default <see cref="IChatNotificationRegistry"/> impl. Owns the per-kind
/// publisher list AND the cached <see cref="ChatNotificationSettings"/> root
/// — the settings UI mutates the cached object in place, then calls
/// <see cref="PersistAsync"/> to write back; publishers read from the cached
/// object on every <c>Publish</c> with no DB hit.
/// </summary>
internal sealed class ChatNotificationRegistry : IChatNotificationRegistry, IDisposable
{
    private readonly IPluginContext mContext;
    private readonly ISettingsStore mStore;
    private readonly IChatGui mChat;
    private readonly ILogger<ChatNotificationRegistry> mLog;
    private readonly NotificationHistoryStore mHistory;
    private readonly NotificationDigestBuffer mDigest;
    private readonly ISessionStateProvider? mSession;
    private readonly List<NotificationKindDefinition> mRegistered = new();
    private readonly Dictionary<string, ChatNotificationPublisher> mPublishers = new();
    private ChatNotificationSettings mSettings = new();

    public ChatNotificationRegistry(
        IPluginContext context,
        ISettingsStore store,
        IChatGui chat,
        ILogger<ChatNotificationRegistry> log,
        NotificationHistoryStore history,
        NotificationDigestBuffer digest,
        ISessionStateProvider? session = null)
    {
        mContext = context;
        mStore = store;
        mChat = chat;
        mLog = log;
        mHistory = history;
        mDigest = digest;
        mSession = session;
        if (mSession is not null) mSession.Deactivated += FlushDigestToChat;
        _ = LoadAsync();
    }

    public IReadOnlyList<NotificationKindDefinition> Registered => mRegistered;

    /// <summary>The cached settings root. Mutated in place by the settings UI;
    /// publishers read from this directly. Never returns null even before the
    /// async load lands — initial value is an empty overrides map so producers
    /// fall back to their definition defaults.</summary>
    public ChatNotificationSettings Settings => mSettings;

    public int DigestCount => mDigest.Count;
    public IReadOnlyList<NotificationDigestItem> FlushDigest() => mDigest.Flush();

    /// <summary>Delivers queued digest items when the active session ends.
    /// A single summary keeps the chat readable even when many events were
    /// queued during the session.</summary>
    public void FlushDigestToChat()
    {
        var items = mDigest.Flush();
        if (items.Count == 0) return;
        try
        {
            var message = new SeStringBuilder()
                .AddText($"[NexusKit] {items.Count} notification(s) while away: ")
                .AddText(string.Join(" | ", items.Select(x => x.Message)))
                .Build();
            mChat.Print(new XivChatEntry { Type = XivChatType.Echo, Message = message });
        }
        catch (Exception ex)
        {
            mLog.LogWarning(ex, "ChatNotifications: digest flush failed");
        }
    }

    public IChatNotificationPublisher RegisterKind(NotificationKindDefinition kind)
    {
        if (mPublishers.TryGetValue(kind.Id, out var existing)) return existing;
        var publisher = new ChatNotificationPublisher(kind, this, mContext, mChat, mLog, mHistory, mDigest);
        mRegistered.Add(kind);
        mPublishers[kind.Id] = publisher;
        return publisher;
    }

    /// <summary>Persists the in-memory settings root. Called by the settings
    /// UI after the user toggles / re-channels / re-colors a kind.</summary>
    public async Task PersistAsync(CancellationToken ct = default)
    {
        try
        {
            await mStore.SetAsync(ChatNotificationSettings.StoreKey, mSettings, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            mLog.LogWarning(ex, "ChatNotifications: settings save failed");
        }
    }

    private async Task LoadAsync()
    {
        try
        {
            var loaded = await mStore.GetAsync<ChatNotificationSettings>(ChatNotificationSettings.StoreKey)
                .ConfigureAwait(false);
            if (loaded is not null) mSettings = loaded;
        }
        catch (Exception ex)
        {
            mLog.LogWarning(ex, "ChatNotifications: settings load failed; using defaults");
        }
    }

    public void Dispose()
    {
        if (mSession is not null) mSession.Deactivated -= FlushDigestToChat;
    }
}
