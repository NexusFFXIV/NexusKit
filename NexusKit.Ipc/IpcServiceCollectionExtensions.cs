using Microsoft.Extensions.DependencyInjection;
using NexusKit.Core.Ipc;

namespace NexusKit.Ipc;

public static class IpcServiceCollectionExtensions
{
    /// <summary>
    /// Register the Dalamud-backed IPC registry. Requires
    /// <c>IDalamudPluginInterface</c> and <c>IPluginContext</c> in DI (the plugin
    /// registers the former; the framework registers the latter via PluginHostBuilder).
    /// </summary>
    public static IServiceCollection AddNexusKitIpc(this IServiceCollection services)
    {
        services.AddSingleton<DalamudIpcRegistry>();
        services.AddSingleton<IIpcRegistry>(sp => sp.GetRequiredService<DalamudIpcRegistry>());
        services.AddSingleton<IDalamudPluginProbe, DalamudPluginProbe>();
        return services;
    }
}
