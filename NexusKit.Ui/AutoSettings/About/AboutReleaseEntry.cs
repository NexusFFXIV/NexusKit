namespace NexusKit.Ui.AutoSettings.About;

/// <summary>A localized release group shown by the reusable About page.</summary>
public sealed class AboutReleaseEntry
{
    public required string VersionKey { get; init; }
    public IReadOnlyList<string> NoteKeys { get; init; } = Array.Empty<string>();
}
