using Dalamud.Game.Text;

namespace NexusKit.ChatNotifications.Internal;

/// <summary>Resolves <see cref="NotificationChannel"/> to the concrete
/// <see cref="XivChatType"/> the chat plumbing actually understands. Each case
/// corresponds to a XivChatType value carrying <c>[XivChatTypeInfo]</c> — see
/// the type-summary on <see cref="NotificationChannel"/> for why combat/internal
/// XivChatType values are intentionally not exposed.</summary>
internal static class ChatChannelMap
{
    public static XivChatType ToXivChatType(NotificationChannel channel) => channel switch
    {
        NotificationChannel.Echo            => XivChatType.Echo,
        NotificationChannel.Notice          => XivChatType.Notice,
        NotificationChannel.SystemMessage   => XivChatType.SystemMessage,
        NotificationChannel.Debug           => XivChatType.Debug,
        NotificationChannel.Urgent          => XivChatType.Urgent,
        NotificationChannel.Say             => XivChatType.Say,
        NotificationChannel.Shout           => XivChatType.Shout,
        NotificationChannel.TellIncoming    => XivChatType.TellIncoming,
        NotificationChannel.Party           => XivChatType.Party,
        NotificationChannel.Alliance        => XivChatType.Alliance,
        NotificationChannel.Ls1             => XivChatType.Ls1,
        NotificationChannel.Ls2             => XivChatType.Ls2,
        NotificationChannel.Ls3             => XivChatType.Ls3,
        NotificationChannel.Ls4             => XivChatType.Ls4,
        NotificationChannel.Ls5             => XivChatType.Ls5,
        NotificationChannel.Ls6             => XivChatType.Ls6,
        NotificationChannel.Ls7             => XivChatType.Ls7,
        NotificationChannel.Ls8             => XivChatType.Ls8,
        NotificationChannel.FreeCompany     => XivChatType.FreeCompany,
        NotificationChannel.NoviceNetwork   => XivChatType.NoviceNetwork,
        NotificationChannel.CustomEmote     => XivChatType.CustomEmote,
        NotificationChannel.StandardEmote   => XivChatType.StandardEmote,
        NotificationChannel.Yell            => XivChatType.Yell,
        NotificationChannel.CrossParty      => XivChatType.CrossParty,
        NotificationChannel.PvPTeam         => XivChatType.PvPTeam,
        NotificationChannel.CrossLinkShell1 => XivChatType.CrossLinkShell1,
        NotificationChannel.CrossLinkShell2 => XivChatType.CrossLinkShell2,
        NotificationChannel.CrossLinkShell3 => XivChatType.CrossLinkShell3,
        NotificationChannel.CrossLinkShell4 => XivChatType.CrossLinkShell4,
        NotificationChannel.CrossLinkShell5 => XivChatType.CrossLinkShell5,
        NotificationChannel.CrossLinkShell6 => XivChatType.CrossLinkShell6,
        NotificationChannel.CrossLinkShell7 => XivChatType.CrossLinkShell7,
        NotificationChannel.CrossLinkShell8 => XivChatType.CrossLinkShell8,
        // None should never reach the dispatcher — the publisher short-circuits
        // earlier. Fall back to Echo here just to keep the call total.
        _                                   => XivChatType.Echo,
    };

    /// <summary>True if the channel renders a "&lt;Name&gt;" sender slot in the
    /// chat row. System-style channels (Echo, Notice, SystemMessage, Debug,
    /// Urgent) print without a sender, so XivChatEntry.Name is dropped on the
    /// floor for them — the publisher uses this to decide whether to put the
    /// plugin name into the sender slot or fall back to a body prefix.</summary>
    public static bool HasSenderSlot(NotificationChannel channel) => channel switch
    {
        NotificationChannel.Echo or
        NotificationChannel.Notice or
        NotificationChannel.SystemMessage or
        NotificationChannel.Debug or
        NotificationChannel.Urgent or
        NotificationChannel.None => false,
        _ => true,
    };
}
