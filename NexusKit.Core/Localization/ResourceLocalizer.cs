using System.Resources;

namespace NexusKit.Core.Localization;

/// <summary>
/// <see cref="ILocalizationSource"/> backed by a .NET <see cref="ResourceManager"/>.
/// Honours <see cref="System.Globalization.CultureInfo.CurrentUICulture"/> automatically —
/// plugins flip that on Dalamud's LanguageChanged callback and all resx-backed sources follow.
/// </summary>
public sealed class ResourceLocalizer : ILocalizationSource
{
    private readonly ResourceManager mResources;

    public ResourceLocalizer(ResourceManager resources)
    {
        mResources = resources;
    }

    public bool TryGet(string key, out string text)
    {
        var value = mResources.GetString(key);
        if (value is not null)
        {
            text = value;
            return true;
        }
        text = string.Empty;
        return false;
    }
}
