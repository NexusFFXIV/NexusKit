using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace NexusKit.Sync.Contracts;

/// <summary>
/// The wire format for durations in a contract: an integer count plus a unit suffix, e.g.
/// <c>180d</c>, <c>12h</c>, <c>30m</c>, <c>45s</c>.
/// <para>Two properties matter here, and both feed the contract hash. Parsing accepts any of
/// the four units, but <see cref="Format"/> is <b>canonical</b>: a given <see cref="TimeSpan"/>
/// always produces exactly one string, chosen as the largest unit that divides it evenly.
/// Without that, <c>1d</c> and <c>24h</c> would describe the same contract yet hash
/// differently, and two peers would refuse to talk over a formatting preference.</para>
/// </summary>
public static class DurationText
{
    /// <summary>
    /// Renders a duration canonically. Whole days become <c>Nd</c>, otherwise whole hours
    /// become <c>Nh</c>, otherwise whole minutes <c>Nm</c>, otherwise seconds <c>Ns</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The duration is negative or has sub-second precision, neither of which a retention
    /// policy can express.
    /// </exception>
    public static string Format(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(value), value, "A contract duration cannot be negative.");

        if (value.Ticks % TimeSpan.TicksPerSecond != 0)
            throw new ArgumentOutOfRangeException(nameof(value), value, "A contract duration cannot carry sub-second precision.");

        var totalSeconds = (long)value.TotalSeconds;

        if (totalSeconds != 0 && totalSeconds % 86_400 == 0) return Render(totalSeconds / 86_400, 'd');
        if (totalSeconds != 0 && totalSeconds % 3_600 == 0) return Render(totalSeconds / 3_600, 'h');
        if (totalSeconds != 0 && totalSeconds % 60 == 0) return Render(totalSeconds / 60, 'm');

        return Render(totalSeconds, 's');
    }

    /// <summary>Parses <c>180d</c> / <c>12h</c> / <c>30m</c> / <c>45s</c>. Throws on anything else.</summary>
    /// <exception cref="FormatException">The text is not a valid duration.</exception>
    public static TimeSpan Parse(string text) =>
        TryParse(text, out var value)
            ? value
            : throw new FormatException(
                $"'{text}' is not a contract duration. Expected an integer with a unit suffix, e.g. '180d', '12h', '30m' or '45s'.");

    /// <summary>Parses <c>180d</c> / <c>12h</c> / <c>30m</c> / <c>45s</c> without throwing.</summary>
    public static bool TryParse([NotNullWhen(true)] string? text, out TimeSpan value)
    {
        value = default;
        if (string.IsNullOrEmpty(text) || text.Length < 2) return false;

        var unit = text[^1];
        var digits = text.AsSpan(0, text.Length - 1);

        if (!long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var count)) return false;

        // long.MaxValue seconds would overflow TimeSpan; cap at a century, which is well
        // past any retention anyone means and keeps the arithmetic below safe.
        const long maxSeconds = 100L * 365 * 86_400;

        var seconds = unit switch
        {
            'd' => count <= maxSeconds / 86_400 ? count * 86_400 : -1,
            'h' => count <= maxSeconds / 3_600 ? count * 3_600 : -1,
            'm' => count <= maxSeconds / 60 ? count * 60 : -1,
            's' => count <= maxSeconds ? count : -1,
            _ => -1,
        };

        if (seconds < 0) return false;

        value = TimeSpan.FromSeconds(seconds);
        return true;
    }

    private static string Render(long count, char unit) =>
        string.Create(CultureInfo.InvariantCulture, $"{count}{unit}");
}
