namespace NexusKit.Core.Localization;

public sealed class LayeredLocalizer : ILocalizer
{
    private readonly IReadOnlyList<ILocalizationSource> mSources;

    public LayeredLocalizer(IEnumerable<ILocalizationSource> sources)
    {
        // Reverse so later-registered sources win (plugin overrides framework).
        mSources = sources.Reverse().ToList();
    }

    public bool TryGet(string key, out string text)
    {
        foreach (var src in mSources)
        {
            if (src.TryGet(key, out text))
                return true;
        }
        text = string.Empty;
        return false;
    }
}
