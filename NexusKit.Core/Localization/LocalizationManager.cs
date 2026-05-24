using System.Globalization;

namespace NexusKit.Core.Localization;

/// <summary>
/// Framework-level helper for switching the process culture. Dalamud-free —
/// any caller that knows the language code can drive it (a plugin bridging
/// Dalamud's LanguageChanged event, a unit test, a CLI host, etc.).
/// <para>
/// Resolution priority: <see cref="Override"/> (explicit user / plugin-author choice)
/// wins over <see cref="HostCulture"/> (reported by the host, e.g. Dalamud's UI language).
/// Pass <c>null</c> to <see cref="SetOverride"/> to clear and follow the host again.
/// </para>
/// <para>
/// Callers cannot bypass this priority — there is no public "apply this culture right
/// now" method. Always go through <see cref="SetOverride"/> (user / plugin author) or
/// <see cref="ReportHostCulture"/> (host bridge).
/// </para>
/// <para>
/// Since every <see cref="ResourceLocalizer"/> reads <see cref="CultureInfo.CurrentUICulture"/>
/// on each lookup, flipping the culture here makes every framework / module / plugin
/// .resx-backed localizer resolve into the new language on its next call — no per-source
/// wiring required.
/// </para>
/// </summary>
public sealed class LocalizationManager
{
    private string? mOverrideCode;
    private string? mHostCode;

    public event Action<CultureInfo>? CultureChanged;

    /// <summary>
    /// Currently effective culture (read from <see cref="CultureInfo.CurrentUICulture"/>).
    /// </summary>
    public CultureInfo CurrentCulture => CultureInfo.CurrentUICulture;

    /// <summary>
    /// Explicit override set via <see cref="SetOverride"/>. Null while following the host.
    /// </summary>
    public string? Override => mOverrideCode;

    /// <summary>
    /// Last culture reported by the host (e.g. Dalamud). May not be the effective one
    /// if an <see cref="Override"/> is active.
    /// </summary>
    public string? HostCulture => mHostCode;

    /// <summary>
    /// Set an explicit language code (e.g. "en", "de"). Takes precedence over the host
    /// culture until cleared. Pass null or empty to drop the override and follow the host.
    /// </summary>
    public void SetOverride(string? langCode)
    {
        mOverrideCode = string.IsNullOrWhiteSpace(langCode) ? null : langCode;
        var effective = mOverrideCode ?? mHostCode;
        if (!string.IsNullOrWhiteSpace(effective))
            ApplyInternal(effective);
    }

    /// <summary>
    /// Called by hosts (e.g. the Dalamud bridge in NexusKit.Ui) to report the system
    /// UI language. Honoured only when no <see cref="Override"/> is active.
    /// </summary>
    public void ReportHostCulture(string langCode)
    {
        if (string.IsNullOrWhiteSpace(langCode)) return;
        mHostCode = langCode;
        if (mOverrideCode is null)
            ApplyInternal(langCode);
    }

    private void ApplyInternal(string langCode)
    {
        CultureInfo culture;
        try
        {
            culture = new CultureInfo(langCode);
        }
        catch (CultureNotFoundException)
        {
            return;
        }

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureChanged?.Invoke(culture);
    }
}
