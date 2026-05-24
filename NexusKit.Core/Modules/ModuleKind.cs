namespace NexusKit.Core.Modules;

/// <summary>
/// Optional classification a module may declare via <see cref="IModuleSettings.Kind"/>.
/// Used by aggregator modules to discover and group sibling modules of a given role at runtime.
/// </summary>
public enum ModuleKind
{
    /// <summary>
    /// Module exposes data fetched from a source outside the game (web API, scraper, etc.),
    /// as opposed to data read from the running FFXIV client.
    /// </summary>
    ExternalDataSource,
}
