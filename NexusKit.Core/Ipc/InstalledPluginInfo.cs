namespace NexusKit.Core.Ipc;

/// <summary>
/// Metadata snapshot for an installed Dalamud plugin, taken at probe time.
/// </summary>
public sealed record InstalledPluginInfo(
    string InternalName,
    string Name,
    Version? Version,
    bool IsLoaded);
