namespace NexusKit.Ui.AutoSettings.About;

/// <summary>Plugin-provided content for the reusable About/Changelog settings
/// section. Labels and note keys are resolved through the host localizer, so a
/// consuming plugin keeps ownership of its translations.</summary>
public sealed class AboutPageOptions
{
    public required string NavTitleKey { get; init; }
    public required string TitleKey { get; init; }
    public required string VersionKey { get; init; }
    public required string DescriptionKey { get; init; }
    public required string StatusReadKey { get; init; }
    public required string StatusUnreadKey { get; init; }
    public required string MarkReadKey { get; init; }
    public required string RepositoryKey { get; init; }
    public required string ReleaseNotesKey { get; init; }
    public required string RepositoryUrl { get; init; }
    public IReadOnlyList<string> NoteKeys { get; init; } = Array.Empty<string>();
    public int Order { get; init; } = 300;
}
