namespace NexusKit.Core;

/// <summary>
/// Locale-independent FFXIV housing plot size. Lodestone localizes the label
/// (DE "[Groß]", EN "[Large]") — parsers map both to <see cref="Large"/>.
/// </summary>
public enum HouseSize : byte
{
    Small = 0,    // EN: Small Cottage / DE: Klein
    Medium = 1,   // EN: Mid-Size House / DE: Mittel
    Large = 2,    // EN: Large Mansion / DE: Groß
}
