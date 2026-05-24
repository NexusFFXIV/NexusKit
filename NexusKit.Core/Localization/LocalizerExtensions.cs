namespace NexusKit.Core.Localization;

public static class LocalizerExtensions
{
    public static string Get(this ILocalizer localizer, string key)
        => localizer.TryGet(key, out var text) ? text : key;

    /// <summary>
    /// Short, localized relative-time string for a past UTC timestamp:
    /// <c>"just now"</c>, <c>"5m"</c>, <c>"3h"</c>, <c>"2d"</c>, <c>"4w"</c>,
    /// <c>"6mo"</c>, <c>"1y"</c> — with each unit pulled from the localizer
    /// (German plugin install renders <c>"3Std"</c> / <c>"gerade eben"</c>).
    /// Used by the detail-panel header and the per-sub-resource breakdown tooltip;
    /// callers wrap with their own "ago" / "vor" template string when needed
    /// (the detail header's <c>ui.main.detail.refresh.updated_ago</c> already
    /// places <c>{0}</c> between the locale-correct prefix and any suffix).
    /// </summary>
    public static string FormatRelativeTime(this ILocalizer localizer, DateTime utc)
    {
        var diff = DateTime.UtcNow - utc;
        if (diff.TotalSeconds < 60) return localizer.Get("nexuskit.time.just_now");
        if (diff.TotalMinutes < 60) return string.Format(localizer.Get("nexuskit.time.minutes_short"), (int)diff.TotalMinutes);
        if (diff.TotalHours   < 24) return string.Format(localizer.Get("nexuskit.time.hours_short"),   (int)diff.TotalHours);
        if (diff.TotalDays    < 7)  return string.Format(localizer.Get("nexuskit.time.days_short"),    (int)diff.TotalDays);
        if (diff.TotalDays    < 30) return string.Format(localizer.Get("nexuskit.time.weeks_short"),   (int)(diff.TotalDays / 7));
        if (diff.TotalDays    < 365) return string.Format(localizer.Get("nexuskit.time.months_short"), (int)(diff.TotalDays / 30));
        return string.Format(localizer.Get("nexuskit.time.years_short"), (int)(diff.TotalDays / 365));
    }

    /// <summary>
    /// Like <see cref="FormatRelativeTime"/> but wraps the result in the locale's
    /// "X ago" pattern: <c>"3h ago"</c> / <c>"vor 3 Std"</c>. The just-now branch
    /// skips the pattern and returns the standalone <c>nexuskit.time.just_now</c>
    /// string so callers don't print "just now ago" / "vor gerade eben". Use this
    /// when the surrounding UI doesn't already supply an "Updated"/"Aktualisiert"
    /// prefix template (e.g. timeline rows in the History tab).
    /// </summary>
    public static string FormatRelativeTimeAgo(this ILocalizer localizer, DateTime utc)
    {
        var diff = DateTime.UtcNow - utc;
        if (diff.TotalSeconds < 60) return localizer.Get("nexuskit.time.just_now");
        return string.Format(localizer.Get("nexuskit.time.ago_pattern"),
                             localizer.FormatRelativeTime(utc));
    }

    /// <summary>
    /// Compact localized duration string for a <see cref="TimeSpan"/>:
    /// <c>"45s"</c>, <c>"12m"</c>, <c>"3h"</c>, <c>"2d"</c>, … Uses the same
    /// <c>nexuskit.time.*</c> unit keys as <see cref="FormatRelativeTime"/>
    /// (and inherits its German shorts: <c>"3 Std."</c> / <c>"45 Sek."</c>).
    /// Negative or zero-length spans collapse to <c>"0 Sek."</c>. Used for
    /// elapsed-time columns (encounters: <c>last_seen − first_seen</c>) where
    /// we want a unit-suffixed reading instead of <c>HH:MM:SS</c>.
    /// </summary>
    public static string FormatTimeSpan(this ILocalizer localizer, TimeSpan span)
    {
        if (span.Ticks <= 0)
            return string.Format(localizer.Get("nexuskit.time.seconds_short"), 0);
        if (span.TotalMinutes < 1)
            return string.Format(localizer.Get("nexuskit.time.seconds_short"), (int)span.TotalSeconds);
        if (span.TotalHours < 1)
            return string.Format(localizer.Get("nexuskit.time.minutes_short"), (int)span.TotalMinutes);
        if (span.TotalDays < 1)
            return string.Format(localizer.Get("nexuskit.time.hours_short"), (int)span.TotalHours);
        if (span.TotalDays < 30)
            return string.Format(localizer.Get("nexuskit.time.days_short"), (int)span.TotalDays);
        if (span.TotalDays < 365)
            return string.Format(localizer.Get("nexuskit.time.months_short"), (int)(span.TotalDays / 30));
        return string.Format(localizer.Get("nexuskit.time.years_short"), (int)(span.TotalDays / 365));
    }
}
