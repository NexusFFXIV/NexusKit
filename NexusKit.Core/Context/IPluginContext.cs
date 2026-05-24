namespace NexusKit.Core.Context;

public interface IPluginContext
{
    string PluginName { get; }
    string ConfigDirectory { get; }
    Version PluginVersion { get; }
}
