using Dalamud.Plugin;
using NexusKit.Core.Ipc;

namespace NexusKit.Ipc;

internal sealed class DalamudPluginProbe : IDalamudPluginProbe
{
    private readonly IDalamudPluginInterface mPluginInterface;

    public DalamudPluginProbe(IDalamudPluginInterface pluginInterface)
    {
        mPluginInterface = pluginInterface;
    }

    public bool IsInstalled(string internalName)
    {
        foreach (var p in mPluginInterface.InstalledPlugins)
        {
            if (string.Equals(p.InternalName, internalName, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    public bool IsLoaded(string internalName)
    {
        foreach (var p in mPluginInterface.InstalledPlugins)
        {
            if (string.Equals(p.InternalName, internalName, StringComparison.Ordinal))
                return p.IsLoaded;
        }
        return false;
    }

    public InstalledPluginInfo? GetInfo(string internalName)
    {
        foreach (var p in mPluginInterface.InstalledPlugins)
        {
            if (string.Equals(p.InternalName, internalName, StringComparison.Ordinal))
                return new InstalledPluginInfo(p.InternalName, p.Name, p.Version, p.IsLoaded);
        }
        return null;
    }

    public IReadOnlyList<InstalledPluginInfo> ListInstalled()
    {
        var list = new List<InstalledPluginInfo>();
        foreach (var p in mPluginInterface.InstalledPlugins)
        {
            list.Add(new InstalledPluginInfo(p.InternalName, p.Name, p.Version, p.IsLoaded));
        }
        return list;
    }
}
