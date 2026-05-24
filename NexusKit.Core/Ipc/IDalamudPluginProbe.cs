namespace NexusKit.Core.Ipc;

/// <summary>
/// Dalamud-free abstraction over <c>IDalamudPluginInterface.InstalledPlugins</c>.
/// Lets modules check whether a foreign plugin is installed/loaded without
/// referencing Dalamud assemblies directly.
/// </summary>
public interface IDalamudPluginProbe
{
    /// <summary>True when a plugin with the given internal name is present on disk (regardless of load state).</summary>
    bool IsInstalled(string internalName);

    /// <summary>True when the plugin is installed AND currently loaded (enabled).</summary>
    bool IsLoaded(string internalName);

    /// <summary>Metadata for a specific plugin, or <c>null</c> when not installed.</summary>
    InstalledPluginInfo? GetInfo(string internalName);

    /// <summary>Snapshot of all installed plugins. Cheap to call (in-memory list).</summary>
    IReadOnlyList<InstalledPluginInfo> ListInstalled();
}
