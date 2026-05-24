namespace NexusKit.ChatNotifications;

/// <summary>
/// User-routable chat channels for plugin notifications. The first five values
/// (Echo, Notice, SystemMessage, Debug, None) are the original curated palette
/// and stay where they are for binary compatibility with persisted
/// <c>ChatNotificationSettings</c>. The remaining values mirror every
/// <c>Dalamud.Game.Text.XivChatType</c> entry decorated with
/// <c>XivChatTypeInfoAttribute</c> — the channels Dalamud advertises as
/// user-facing routing targets. Combat/internal XivChatType values without the
/// attribute (Damage, Miss, ErrorMessage, …) stay out — routing plugin output
/// there silently breaks combat-log filters.
/// <para><c>ChatChannelMap</c> resolves these to the underlying XivChatType.</para>
/// </summary>
public enum NotificationChannel : byte
{
    /// <summary>Local "echo" channel — visible only to the player, default for
    /// most plugin output.</summary>
    Echo = 0,

    /// <summary>Game's "Notice" channel — slightly more prominent than echo,
    /// good for events the user should not miss.</summary>
    Notice = 1,

    /// <summary>System-message channel — formatted like the game's own system
    /// messages (e.g. zone change, party invite). Use sparingly.</summary>
    SystemMessage = 2,

    /// <summary>Debug channel — hidden by default; lets the user opt in to
    /// noisy or development-only notifications by filtering on this channel.</summary>
    Debug = 3,

    /// <summary>No chat output at all — the publisher silently drops messages
    /// for this kind. Equivalent to "Enabled = false" but lets the user keep
    /// the kind registered for future toggling.</summary>
    None = 4,

    // Below: every XivChatType value decorated with [XivChatTypeInfo] that
    // wasn't already in the curated palette above. Source-order matches the
    // Dalamud enum (Urgent < Say < Shout < …) so additions over time append at
    // the end without reshuffling existing slots.

    /// <summary>Urgent notice — louder than <see cref="Notice"/>.</summary>
    Urgent = 5,

    /// <summary>Say — local chat.</summary>
    Say = 6,

    /// <summary>Shout — zone-wide chat.</summary>
    Shout = 7,

    /// <summary>Incoming tell.</summary>
    TellIncoming = 8,

    /// <summary>Party chat.</summary>
    Party = 9,

    /// <summary>Alliance chat.</summary>
    Alliance = 10,

    /// <summary>Linkshell 1.</summary>
    Ls1 = 11,

    /// <summary>Linkshell 2.</summary>
    Ls2 = 12,

    /// <summary>Linkshell 3.</summary>
    Ls3 = 13,

    /// <summary>Linkshell 4.</summary>
    Ls4 = 14,

    /// <summary>Linkshell 5.</summary>
    Ls5 = 15,

    /// <summary>Linkshell 6.</summary>
    Ls6 = 16,

    /// <summary>Linkshell 7.</summary>
    Ls7 = 17,

    /// <summary>Linkshell 8.</summary>
    Ls8 = 18,

    /// <summary>Free Company chat.</summary>
    FreeCompany = 19,

    /// <summary>Novice Network chat.</summary>
    NoviceNetwork = 20,

    /// <summary>Custom emotes.</summary>
    CustomEmote = 21,

    /// <summary>Standard emotes.</summary>
    StandardEmote = 22,

    /// <summary>Yell — wider than <see cref="Shout"/>.</summary>
    Yell = 23,

    /// <summary>Cross-world party chat.</summary>
    CrossParty = 24,

    /// <summary>PvP team chat.</summary>
    PvPTeam = 25,

    /// <summary>Cross-world linkshell 1.</summary>
    CrossLinkShell1 = 26,

    /// <summary>Cross-world linkshell 2.</summary>
    CrossLinkShell2 = 27,

    /// <summary>Cross-world linkshell 3.</summary>
    CrossLinkShell3 = 28,

    /// <summary>Cross-world linkshell 4.</summary>
    CrossLinkShell4 = 29,

    /// <summary>Cross-world linkshell 5.</summary>
    CrossLinkShell5 = 30,

    /// <summary>Cross-world linkshell 6.</summary>
    CrossLinkShell6 = 31,

    /// <summary>Cross-world linkshell 7.</summary>
    CrossLinkShell7 = 32,

    /// <summary>Cross-world linkshell 8.</summary>
    CrossLinkShell8 = 33,
}
