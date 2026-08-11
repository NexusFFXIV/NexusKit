using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Logging;
using NexusKit.Core.Context;

namespace NexusKit.ChatNotifications.Internal;

/// <summary>
/// Per-kind publisher. Reads its effective settings off the registry's cached
/// <see cref="ChatNotificationSettings"/> at every publish — falls back to the
/// kind's defaults when no override is present.
/// </summary>
internal sealed class ChatNotificationPublisher : IChatNotificationPublisher
{
    private readonly ChatNotificationRegistry mOwner;
    private readonly IPluginContext mContext;
    private readonly IChatGui mChat;
    private readonly ILogger mLog;

    public NotificationKindDefinition Kind { get; }

    public ChatNotificationPublisher(
        NotificationKindDefinition kind,
        ChatNotificationRegistry owner,
        IPluginContext context,
        IChatGui chat,
        ILogger log)
    {
        Kind = kind;
        mOwner = owner;
        mContext = context;
        mChat = chat;
        mLog = log;
    }

    public void Publish(SeString body)
    {
        try
        {
            var (enabled, channel, color) = Resolve();
            if (!enabled || channel == NotificationChannel.None) return;

            var hasSender = ChatChannelMap.HasSenderSlot(channel);
            var message = Compose(body, color, includeBodyPrefix: !hasSender);
            var entry = new XivChatEntry
            {
                Type = ChatChannelMap.ToXivChatType(channel),
                Message = message,
            };
            // Sender-bearing channels reserve a "<Name>" slot — fill it with
            // the plugin name so the row reads "<MyPlugin> body"
            // instead of FFXIV's default empty "<>". System-style channels
            // (Echo/Notice/SystemMessage/Debug/Urgent) drop the Name silently;
            // for those Compose() already prepended the "[PluginName] " body
            // marker so the line still identifies as ours.
            if (hasSender)
                entry.Name = new SeString(new TextPayload(mContext.PluginName));
            mChat.Print(entry);
        }
        catch (Exception ex)
        {
            // Chat dispatch failure must not bubble up to the producer's event
            // handler — a notification problem isn't worth crashing the worker.
            mLog.LogWarning(ex, "ChatNotifications: publish failed for {Kind}", Kind.Id);
        }
    }

    private (bool Enabled, NotificationChannel Channel, NotificationColor Color) Resolve()
    {
        if (mOwner.Settings.Overrides.TryGetValue(Kind.Id, out var o))
            return (o.Enabled, o.Channel, o.Color);
        // No override — use the kind's registered defaults including
        // DefaultEnabled (opt-in kinds register with false here so they don't
        // fire until the user explicitly turns them on).
        return (Kind.DefaultEnabled, Kind.DefaultChannel, Kind.DefaultColor);
    }

    private SeString Compose(SeString body, NotificationColor color, bool includeBodyPrefix)
    {
        var uiColor = ChatColorMap.ToUiColor(color);
        var builder = new SeStringBuilder();

        // System-style channels render no sender slot, so the row otherwise
        // shows up as anonymous coloured text — prepend a textual marker so
        // the user can still tell who emitted the line. Sender-bearing
        // channels skip this prefix because XivChatEntry.Name already puts
        // "<PluginName>" into the row.
        if (includeBodyPrefix)
            builder.AddText($"[{mContext.PluginName}] ");

        if (uiColor == 0)
        {
            builder.Append(body);
        }
        else
        {
            builder.AddUiForeground(uiColor);
            builder.Append(body);
            builder.AddUiForegroundOff();
        }
        return builder.Build();
    }
}
