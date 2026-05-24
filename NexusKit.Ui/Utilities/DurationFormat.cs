namespace NexusKit.Ui.Utilities;

/// <summary>
/// Human-friendly <see cref="TimeSpan"/> rendering for UI surfaces.
/// </summary>
public static class DurationFormat
{
    /// <summary>
    /// Renders a duration as the two highest non-zero units across d/h/min/s.
    /// "38h 18min" beats "38.3 h" — the fractional form reads like a number,
    /// not a real time span. Days+hours covers the long-tail ETAs, hours+minutes
    /// the typical case, minutes+seconds the short ones. Negative / zero spans
    /// collapse to <c>0s</c>.
    /// </summary>
    public static string TwoUnit(TimeSpan ts)
    {
        if (ts.Ticks <= 0) return "0s";

        var d = (int)ts.TotalDays;
        var h = ts.Hours;
        var m = ts.Minutes;
        var s = ts.Seconds;

        if (d > 0) return h > 0 ? $"{d}d {h}h" : $"{d}d";
        if (h > 0) return m > 0 ? $"{h}h {m}min" : $"{h}h";
        if (m > 0) return s > 0 ? $"{m}min {s}s" : $"{m}min";
        return $"{s}s";
    }
}
