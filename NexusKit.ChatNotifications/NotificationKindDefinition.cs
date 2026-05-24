namespace NexusKit.ChatNotifications;

/// <summary>
/// Describes one kind of chat notification a producer is going to publish.
/// Registered up-front so the Notifications settings tab can list every kind
/// (with its label + description from the producer's resx) and so the user
/// can toggle / re-channel / re-color it before the producer ever fires.
/// </summary>
/// <param name="Id">Stable identifier, used as the settings-store key for
/// per-kind overrides. Convention: <c>"&lt;source&gt;.&lt;event&gt;"</c>,
/// e.g. <c>"history.player_changed"</c>. Must be unique across all producers
/// in the host plugin.</param>
/// <param name="LabelKey">Localization key for the human-readable name shown
/// in the settings UI ("Player history change", "Refresh attempts exhausted").</param>
/// <param name="DescriptionKey">Localization key for the longer tooltip
/// description shown on hover ("Fires when a tracked player has been renamed,
/// transferred, or joined / left a Free Company.").</param>
/// <param name="DefaultChannel">Initial chat channel used until the user
/// overrides it in settings.</param>
/// <param name="DefaultColor">Initial text color used until the user
/// overrides it in settings.</param>
/// <param name="GroupKey">Optional localization key for a group heading
/// the settings UI uses to bucket related kinds under a single
/// CollapsingHeader. Producers that omit it land in the default
/// (un-grouped) bucket. Presentation-only — the kind's <paramref name="Id"/>
/// is still the source of truth for persistence.</param>
/// <param name="DefaultEnabled">Whether the kind is on by default before the
/// user has touched its settings. Most kinds want true (the framework's
/// historical behaviour). Set to false for opt-in kinds — e.g. a granular
/// per-category kind that exists alongside a generic catch-all that's
/// already enabled, so the user doesn't get duplicate pings out of the
/// box.</param>
/// <param name="SuppressedBy">Optional list of kind ids that — when
/// <em>enabled by the user</em> — visually suppress this kind in the
/// settings UI. The section greys out the row (checkbox + combos) and a
/// hover tooltip names the suppressor so the user understands why the
/// row is inert. UI-only: the publisher does not double-check this at
/// publish time (the existing Enabled gate already prevents disabled
/// kinds from firing). Typical use: a fine-grained per-event kind names
/// a coarser catch-all kind here so the user can't accidentally enable
/// both at once.</param>
public sealed record NotificationKindDefinition(
    string Id,
    string LabelKey,
    string DescriptionKey,
    NotificationChannel DefaultChannel,
    NotificationColor DefaultColor,
    string? GroupKey = null,
    bool DefaultEnabled = true,
    IReadOnlyList<string>? SuppressedBy = null);
