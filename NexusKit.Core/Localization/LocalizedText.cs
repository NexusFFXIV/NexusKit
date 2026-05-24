namespace NexusKit.Core.Localization;

public readonly struct LocalizedText
{
    public string? Literal { get; }
    public string? Key { get; }

    private LocalizedText(string? literal, string? key)
    {
        Literal = literal;
        Key = key;
    }

    public bool IsEmpty => Literal is null && Key is null;

    public static LocalizedText FromLiteral(string text) => new(text, null);
    public static LocalizedText FromKey(string key) => new(null, key);
    public static LocalizedText Empty => default;

    public string Resolve(ILocalizer localizer, string fallback = "")
    {
        if (Key is not null) return localizer.Get(Key);
        return Literal ?? fallback;
    }

    public string? ResolveOrNull(ILocalizer localizer)
    {
        if (Key is not null) return localizer.Get(Key);
        return Literal;
    }
}
