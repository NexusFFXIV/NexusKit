using Microsoft.Extensions.DependencyInjection;
using NexusKit.Core.Context;

namespace NexusKit.Core.Modules;

public interface IPluginModule
{
    void Register(IServiceCollection services, IPluginContext context);
}
