namespace NexusKit.Core.Context;

public sealed record PluginContext(
    string PluginName,
    string ConfigDirectory,
    Version PluginVersion) : IPluginContext;
