using System.Globalization;

namespace NexusKit.Ui.Utilities;

/// <summary>
/// Human-friendly byte-size rendering for UI surfaces.
/// </summary>
public static class SizeFormat
{
    private const long Kib = 1024L;
    private const long Mib = 1024L * 1024L;

    /// <summary>
    /// Formats a byte count with an adaptive unit suffix (B / KB / MB).
    /// Small payloads (settings table, etc.) don't collapse to "0,00 MB"
    /// because the helper falls through to KB and finally plain bytes when
    /// the value is below the next-larger unit's threshold. Numeric formatting
    /// uses <see cref="CultureInfo.CurrentCulture"/> so the decimal separator
    /// matches the user's locale.
    /// </summary>
    public static string Bytes(long bytes)
    {
        if (bytes >= Mib)
            return string.Format(CultureInfo.CurrentCulture, "{0:F2} MB", bytes / (double)Mib);
        if (bytes >= Kib)
            return string.Format(CultureInfo.CurrentCulture, "{0:F2} KB", bytes / (double)Kib);
        return string.Format(CultureInfo.CurrentCulture, "{0:N0} B", bytes);
    }
}
